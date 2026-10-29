using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("M0Probe")]
[assembly: InternalsVisibleTo("NexaSM.Tests")]

namespace NexaSM.Persistence.Sqlite;

// Only the test consumer can install checkpoints. No wire/configuration switch exposes them.
internal enum AdmissionCheckpoint
{
    AfterIntentWrite,
    AfterOperationWrite,
    BeforeCommit,
    AfterCommit
}
