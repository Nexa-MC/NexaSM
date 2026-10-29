using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using NexaSM.Contracts;
using NexaSM.Domain;
using NexaSM.Services;
namespace NexaSM.Persistence.Sqlite;

// One database per control domain. The bound identity is checked on every admission.
public sealed class SqliteOperationStore : IOperationStore
{
    private readonly string connectionString;
    private readonly string domain;
    private readonly Action<AdmissionCheckpoint, OperationId>? checkpoint;
    public SqliteOperationStore(string path, string domain) : this(path, domain, null) { }

    internal SqliteOperationStore(string path, string domain, Action<AdmissionCheckpoint, OperationId>? checkpoint)
    {
        this.checkpoint = checkpoint;
        if (string.IsNullOrWhiteSpace(domain)) throw new ArgumentException("Domain required.");
        this.domain = domain;
        connectionString = new SqliteConnectionStringBuilder { DataSource = Path.GetFullPath(path),
            Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false, DefaultTimeout = 10 }.ToString();
        using var db = Open();
        using var tx = db.BeginTransaction();
        Execute(db, tx, """
            CREATE TABLE IF NOT EXISTS metadata(domain TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS instances(id TEXT PRIMARY KEY, desired INTEGER NOT NULL, version INTEGER NOT NULL, generation INTEGER NOT NULL);
            CREATE TABLE IF NOT EXISTS operations(id TEXT PRIMARY KEY, principal TEXT NOT NULL, kind TEXT NOT NULL,
                target TEXT NOT NULL, idem TEXT NOT NULL, digest TEXT NOT NULL, state INTEGER NOT NULL,
                UNIQUE(principal,kind,target,idem));
            CREATE TABLE IF NOT EXISTS outbox(operation_id TEXT PRIMARY KEY REFERENCES operations(id), target TEXT NOT NULL, desired INTEGER NOT NULL);
            """);
        using var identity = Command(db, tx, "SELECT domain FROM metadata LIMIT 1");
        var existing = identity.ExecuteScalar() as string;
        if (existing is null) Execute(db, tx, "INSERT INTO metadata VALUES($d)", ("$d", domain));
        else if (existing != domain) throw new DomainException("ControlDomainMismatch");
        tx.Commit();
    }
    private SqliteConnection Open()
    {
        var db = new SqliteConnection(connectionString);
        try
        {
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "PRAGMA foreign_keys=ON; PRAGMA synchronous=FULL;";
            cmd.ExecuteNonQuery();
            return db;
        }
        catch { db.Dispose(); throw; }
    }
    public void CreateInstance(InstanceId id)
    {
        id.Validate(); using var db = Open();
        Execute(db, null, "INSERT INTO instances VALUES($id,0,0,0)", ("$id", id.Value.ToString("D")));
    }
    public InstanceSpec ReadInstance(InstanceId id)
    {
        using var db = Open();
        using var cmd = Command(db, null, "SELECT desired,version,generation FROM instances WHERE id=$id", ("$id", id.Value.ToString("D")));
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) throw new DomainException("NotFound");
        return new(id, (DesiredState)reader.GetInt32(0), new(reader.GetInt64(1)), new(reader.GetInt64(2)));
    }
    public OperationReceipt Accept(AuthorizedScope scope, DesiredStateCommand command)
    {
        CommandCodec.Validate(command);
        if (scope.ControlDomain != domain || string.IsNullOrWhiteSpace(scope.Principal)) throw new DomainException("InvalidScope");
        // This command has a closed typed payload. CommandId is correlation, not semantic payload.
        var canonical = string.Join("|", command.SchemaVersion.ToString(CultureInfo.InvariantCulture),
            command.ExpectedVersion.Value.ToString(CultureInfo.InvariantCulture), ((int)command.DesiredState).ToString(CultureInfo.InvariantCulture));
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        var target = command.Target.Value.ToString("D");
        using var db = Open(); using var tx = db.BeginTransaction(deferred: false);
        using (var lookup = Command(db, tx, "SELECT id,digest,state FROM operations WHERE principal=$p AND kind='SetDesiredState' AND target=$t AND idem=$k",
            ("$p", scope.Principal), ("$t", target), ("$k", command.IdempotencyKey)))
        using (var reader = lookup.ExecuteReader())
        {
            if (reader.Read())
            {
                if (reader.GetString(1) != digest) throw new DomainException("IdempotencyConflict");
                return new(new(Guid.Parse(reader.GetString(0))), (OperationState)reader.GetInt32(2));
            }
        }
        var id = Guid.NewGuid();
        var changed = Execute(db, tx, "UPDATE instances SET generation=generation+CASE WHEN desired<>$d THEN 1 ELSE 0 END, desired=$d, version=version+1 WHERE id=$t AND version=$v",
            ("$d", (int)command.DesiredState), ("$t", target), ("$v", command.ExpectedVersion.Value));
        if (changed != 1) throw new DomainException("VersionConflictOrNotFound");
        checkpoint?.Invoke(AdmissionCheckpoint.AfterIntentWrite, new(id));
        Execute(db, tx, "INSERT INTO operations VALUES($id,$p,'SetDesiredState',$t,$k,$hash,0)",
            ("$id", id.ToString("D")), ("$p", scope.Principal), ("$t", target), ("$k", command.IdempotencyKey), ("$hash", digest));
        checkpoint?.Invoke(AdmissionCheckpoint.AfterOperationWrite, new(id));
        Execute(db, tx, "INSERT INTO outbox VALUES($id,$t,$d)", ("$id", id.ToString("D")), ("$t", target), ("$d", (int)command.DesiredState));
        checkpoint?.Invoke(AdmissionCheckpoint.BeforeCommit, new(id));
        tx.Commit(); // Receipt means durable admission only; no process has been started.
        checkpoint?.Invoke(AdmissionCheckpoint.AfterCommit, new(id));
        return new(new(id), OperationState.Pending);
    }
    public OperationReceipt? Find(OperationId id)
    {
        using var db = Open(); using var cmd = Command(db, null, "SELECT state FROM operations WHERE id=$id", ("$id", id.Value.ToString("D")));
        var state = cmd.ExecuteScalar(); return state is null ? null : new(id, (OperationState)Convert.ToInt32(state, CultureInfo.InvariantCulture));
    }
    public IReadOnlyList<OutboxItem> ReadPending(int limit)
    {
        if (limit is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(limit));
        using var db = Open(); using var cmd = Command(db, null, "SELECT operation_id,target,desired FROM outbox ORDER BY rowid LIMIT $n", ("$n", limit));
        using var reader = cmd.ExecuteReader(); var items = new List<OutboxItem>();
        while (reader.Read()) items.Add(new(new(Guid.Parse(reader.GetString(0))), new(Guid.Parse(reader.GetString(1))), (DesiredState)reader.GetInt32(2)));
        return items;
    }
    private static SqliteCommand Command(SqliteConnection db, SqliteTransaction? tx, string sql, params (string, object)[] args)
    {
        var command = db.CreateCommand(); command.Transaction = tx; command.CommandText = sql;
        foreach (var (name, value) in args) command.Parameters.AddWithValue(name, value);
        return command;
    }
    private static int Execute(SqliteConnection db, SqliteTransaction? tx, string sql, params (string, object)[] args)
    { using var command = Command(db, tx, sql, args); return command.ExecuteNonQuery(); }
}
