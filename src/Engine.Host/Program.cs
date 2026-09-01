// The runtime host — the headless half of the loop described in
// docs/kernel-contract.md §7:
//
//   engine run --headless --plugins <dir> --project <project.json> --frames <n> [--dump <path>]
//
// Deliberately not implemented yet, both noted explicitly below rather than
// silently accepted or rejected as gibberish:
//   --scene    no scene format exists (that's M2) — a plugin that needs
//              world content seeds it itself; see sandbox.echo's Configure.
//   --assert   no query DSL exists to parse the doc's illustrative
//              `count(Rigidbody where sleeping) == 12` syntax; a dump is
//              plain JSON, so external tooling (jq, a test script) already
//              covers "check something about it" without us inventing a
//              parser for a grammar that was never actually specified.
//   diag why-pinned   needs a way to walk the GC heap for what's still
//              referencing an ALC; nothing has ever failed to unload in
//              testing, so there's nothing to build this against yet.

using Engine.Kernel.Diagnostics;
using Engine.Kernel.Events;
using Engine.Kernel.Plugins;
using Engine.Kernel.Scheduling;
using Engine.Kernel.Services;
using Engine.Kernel.World;

if (args.Length == 0)
{
    PrintUsage();
    return 1;
}

if (args[0] == "diag")
{
    Console.Error.WriteLine("`engine diag` isn't implemented yet — see docs/kernel-contract.md §7.");
    return 1;
}

if (args[0] != "run")
{
    PrintUsage();
    return 1;
}

string? projectPath = null;
string? pluginsPath = null;
string? dumpPath = null;
var frames = 0;
var headless = false;

for (var i = 1; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--headless":
            headless = true;
            break;
        case "--frames" when i + 1 < args.Length:
            frames = int.Parse(args[++i]);
            break;
        case "--project" when i + 1 < args.Length:
            projectPath = args[++i];
            break;
        case "--plugins" when i + 1 < args.Length:
            pluginsPath = args[++i];
            break;
        case "--dump" when i + 1 < args.Length:
            dumpPath = args[++i];
            break;
        case "--scene":
        case "--assert":
            Console.Error.WriteLine($"'{args[i]}' isn't implemented yet — see the notes at the top of Program.cs.");
            return 1;
        default:
            Console.Error.WriteLine($"Unrecognized argument: '{args[i]}'");
            PrintUsage();
            return 1;
    }
}

if (!headless)
{
    Console.Error.WriteLine("Only --headless is implemented — there's no windowing plugin yet.");
    return 1;
}

if (projectPath is null || pluginsPath is null)
{
    Console.Error.WriteLine("--project and --plugins are both required.");
    PrintUsage();
    return 1;
}

var world = new GameWorld();
var schedule = new Schedule();
var host = new PluginHost(world, new ServiceRegistry(), schedule, new NullEventBus());

IReadOnlyList<string> loaded;
try
{
    loaded = host.LoadProject(projectPath, [pluginsPath]);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Failed to load project '{projectPath}': {ex.Message}");
    return 1;
}

Console.WriteLine($"Loaded {loaded.Count} plugin(s): {string.Join(", ", loaded)}");

for (var frame = 0; frame < frames; frame++)
    schedule.RunStage(Stage.Update, world);

Console.WriteLine($"Ran {frames} update frame(s).");

if (dumpPath is not null)
{
    File.WriteAllText(dumpPath, WorldDumper.ToJson(world));
    Console.WriteLine($"Wrote world dump to '{dumpPath}'.");
}

return 0;

static void PrintUsage()
{
    Console.Error.WriteLine(
        "Usage: engine run --headless --plugins <dir> --project <project.json> --frames <n> [--dump <path>]");
}
