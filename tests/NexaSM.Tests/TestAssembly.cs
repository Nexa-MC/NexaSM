using Xunit;
// Avoid unrelated CPU/SQLite stress collections obscuring response-deadline tests.
// Explicit concurrency tests still exercise simultaneous admissions.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
