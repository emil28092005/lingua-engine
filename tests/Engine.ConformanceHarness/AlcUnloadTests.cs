namespace Engine.ConformanceHarness;

/// <summary>
/// The test docs/kernel-contract.md §4 calls the one thing that keeps this
/// architecture from slowly degrading: load and unload a plugin 200 times,
/// and after every cycle verify the ALC actually collected. Runs against
/// Sandbox.Echo — see the ReferenceOutputAssembly="false" note in this
/// project's .csproj for why that reference doesn't link its types in.
/// </summary>
public class AlcUnloadTests
{
    // TODO(M0): once PluginHost exists —
    //   for (int i = 0; i < 200; i++) {
    //       var handle = host.Load("sandbox.echo");
    //       var weakAlc = host.Unload(handle);
    //       GC.Collect(); GC.WaitForPendingFinalizers();
    //       Assert.False(weakAlc.IsAlive, $"ALC survived cycle {i}");
    //   }
    // and assert working-set memory hasn't grown beyond noise.
    [Fact(Skip = "PluginHost has no implementation yet — see M0 in docs/kernel-contract.md §8.")]
    public void Plugin_Survives_200_Load_Unload_Cycles()
    {
    }
}
