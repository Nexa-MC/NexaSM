using System.Text.Json;
using NexaSM.Domain;
using NexaSM.Contracts;
using NexaSM.Services;
using NexaSM.Services.Composition;
using NexaSM.Persistence.Sqlite;

if (args.Length is < 1 or > 2)
{
    Console.Error.WriteLine("usage: M0Probe <scratch-database> [--resume|--hold|--checkpoint=Name|--latency]");
    return 2;
}
var option = args.Length == 2 ? args[1] : "";
AdmissionCheckpoint? selected = null;
if (option.StartsWith("--checkpoint=", StringComparison.Ordinal))
{
    if (!Enum.TryParse<AdmissionCheckpoint>(option[13..], out var point) || !Enum.IsDefined(point)) return 2;
    selected = point;
}
else if (option is not ("" or "--resume" or "--hold" or "--latency")) return 2;
var resume = option == "--resume";
if (File.Exists(args[0]) != resume)
{
    Console.Error.WriteLine("New probe needs a new database; --resume needs an existing probe database.");
    return 2;
}
if (option == "--latency") return await ResponseProbe.Run(args[0]);

void Checkpoint(AdmissionCheckpoint point, OperationId id)
{
    if (selected != point) return;
    // Diagnostic marker, deliberately NOT an OperationReceipt. The parent kills this process.
    Console.WriteLine($"CHECKPOINT {point} {id.Value:D}");
    Console.Out.Flush();
    Thread.Sleep(Timeout.Infinite);
}
var store = new SqliteOperationStore(args[0], "probe", Checkpoint);
var instance = new InstanceId(Guid.Parse("b15d0ce8-f87d-40a2-9609-b19802b84965"));
if (!resume) store.CreateInstance(instance);
var command = new DesiredStateCommand(Guid.NewGuid(), "probe-start", instance, new(0), DesiredState.Running);
var wire = JsonSerializer.SerializeToUtf8Bytes(command, ContractJson.Default.DesiredStateCommand);
var service = new OperationService(store);
await using var management = new LocalManagement(service);
var receipt = await management.SubmitAsync(new("probe", "local-probe"), CommandCodec.Read(wire));
var reopened = new SqliteOperationStore(args[0], "probe");
if (reopened.Find(receipt.OperationId) != receipt || reopened.ReadPending(10).Count != 1 ||
    new OperationService(reopened).Submit(new("probe", "local-probe"), command) != receipt) return 1;
Console.WriteLine(JsonSerializer.Serialize(receipt, ContractJson.Default.OperationReceipt));
if (option == "--hold") { Console.Out.Flush(); await Task.Delay(Timeout.Infinite); }
return 0;
