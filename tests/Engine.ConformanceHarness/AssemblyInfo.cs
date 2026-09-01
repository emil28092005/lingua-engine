using Xunit;

// ALC-unload tests are sensitive to *any* concurrent activity that might
// transiently keep something reachable — xUnit parallelizes across test
// classes by default, and AlcUnloadTests running alongside
// PluginSystemTests in the same process is exactly the kind of
// interference that produces flaky, hard-to-explain unload failures.
// Everything in this assembly runs sequentially instead.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
