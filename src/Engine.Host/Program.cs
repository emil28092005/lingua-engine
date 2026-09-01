// The runtime host: resolves a project's plugins and runs the engine loop.
// Its shape is sketched in docs/kernel-contract.md §7 —
//
//   engine run --headless --frames 60 --scene ... --dump ... --assert ...
//   engine diag why-pinned <plugin-id>
//
// Neither the argument parsing nor the loop it drives exists yet; both
// depend on PluginHost and Scheduler, which are M0's actual work. This is
// a placeholder so the solution builds and runs end to end.

Console.WriteLine("Lingua Engine host — not yet implemented. See docs/kernel-contract.md, M0.");
