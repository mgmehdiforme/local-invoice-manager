// The app keeps its language and calendar in process-wide defaults (one database per process in production),
// so test hosts with different settings must not run at the same time.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
