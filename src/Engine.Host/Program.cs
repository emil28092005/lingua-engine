// The runtime host — drives both halves of the loop described in
// docs/kernel-contract.md §7 and the M1 windowed case in §8:
//
//   engine run --headless --plugins <dir> --project <project.json> --frames <n> [--dump <path>]
//   engine run --windowed --plugins <dir> --project <project.json> [--dump <path>]
//
// --windowed needs a loaded plugin that provides IEngineWindow (engine.
// windowing) — Engine.Host references that plugin's *Contracts* assembly
// directly (never its implementation, which stays dynamically loaded via
// PluginHost/ALC same as any other plugin) because driving a window-pumped
// loop is the host's job, not the kernel's: Engine.Kernel never hears about
// Silk.NET at all.
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
using Engine.Render.Contracts;
using Engine.Windowing.Contracts;

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
string? screenshotPath = null;
var frames = 0;
var screenshotAfterFrames = 1;
var headless = false;
var windowed = false;

for (var i = 1; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--headless":
            headless = true;
            break;
        case "--windowed":
            windowed = true;
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
        case "--screenshot" when i + 1 < args.Length:
            screenshotPath = args[++i];
            break;
        case "--screenshot-after-frames" when i + 1 < args.Length:
            screenshotAfterFrames = int.Parse(args[++i]);
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

if (headless == windowed)
{
    Console.Error.WriteLine("Pass exactly one of --headless or --windowed.");
    PrintUsage();
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
var services = new ServiceRegistry();
var host = new PluginHost(world, services, schedule, new NullEventBus());

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

if (windowed)
{
    if (!services.TryRequire<IEngineWindow>(out var window))
    {
        Console.Error.WriteLine(
            "--windowed requires a loaded plugin that provides IEngineWindow (e.g. engine.windowing).");
        return 1;
    }

    Console.WriteLine(
        """
        Window open — close it to exit. Commands (type + Enter):
          r <plugin-id>       reload that plugin live
          screenshot <path>   save the current frame to a PNG (needs a plugin providing IScreenCapture)
        """);

    // Line-based, not Console.ReadKey: KeyAvailable needs a real terminal
    // in raw mode and throws or misbehaves on piped/redirected stdin. A
    // background reader plus a thread-safe queue works either way and
    // costs nothing on the render loop's own thread.
    var commandQueue = new System.Collections.Concurrent.ConcurrentQueue<(string Command, string Argument)>();
    _ = Task.Run(() =>
    {
        string? line;
        while ((line = Console.ReadLine()) is not null)
        {
            var parts = line.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts is [var command, var argument])
                commandQueue.Enqueue((command, argument));
        }
    });

    var frameCount = 0;

    while (!window!.IsClosing)
    {
        frameCount++;
        window.Native.DoEvents();

        if (window.IsClosing)
            break;

        // "screenshot" waits until after this frame's Render stage below —
        // captured now, it would grab whatever the *previous* frame left
        // in the framebuffer, not what this iteration is about to draw.
        var pendingScreenshots = new List<string>();

        // --screenshot is the non-interactive path: capture once, then
        // exit, so a script can fire-and-forget instead of managing a
        // stdin pipe into a long-running process.
        if (screenshotPath is not null && frameCount == screenshotAfterFrames)
            pendingScreenshots.Add(screenshotPath);

        while (commandQueue.TryDequeue(out var cmd))
        {
            switch (cmd.Command)
            {
                case "r":
                    var pluginDirectory = Path.Combine(pluginsPath, cmd.Argument);
                    Console.WriteLine($"Reloading '{cmd.Argument}'...");
                    try
                    {
                        host.Unload(cmd.Argument);
                        host.Load(pluginDirectory);
                        Console.WriteLine($"Reloaded '{cmd.Argument}'. World state and the window were untouched.");
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Failed to reload '{cmd.Argument}': {ex.Message}");
                    }

                    break;

                case "screenshot":
                    pendingScreenshots.Add(cmd.Argument);
                    break;

                default:
                    Console.Error.WriteLine($"Unknown command: '{cmd.Command}'");
                    break;
            }
        }

        schedule.RunStage(Stage.Update, world);
        schedule.RunStage(Stage.Render, world);

        foreach (var path in pendingScreenshots)
        {
            if (!services.TryRequire<IScreenCapture>(out var capture))
            {
                Console.Error.WriteLine("No loaded plugin provides IScreenCapture (e.g. engine.render).");
                continue;
            }

            try
            {
                capture!.CaptureToFile(path);
                Console.WriteLine($"Wrote screenshot to '{path}'.");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to capture screenshot: {ex.Message}");
            }
        }

        if (screenshotPath is not null && frameCount == screenshotAfterFrames)
            break;
    }

    Console.WriteLine("Window closed.");
}
else
{
    for (var frame = 0; frame < frames; frame++)
        schedule.RunStage(Stage.Update, world);

    Console.WriteLine($"Ran {frames} update frame(s).");
}

if (dumpPath is not null)
{
    File.WriteAllText(dumpPath, WorldDumper.ToJson(world));
    Console.WriteLine($"Wrote world dump to '{dumpPath}'.");
}

return 0;

static void PrintUsage()
{
    Console.Error.WriteLine(
        """
        Usage:
          engine run --headless --plugins <dir> --project <project.json> --frames <n> [--dump <path>]
          engine run --windowed --plugins <dir> --project <project.json> [--dump <path>]
                      [--screenshot <path> [--screenshot-after-frames <n>]]

        --screenshot captures once, after <n> frames (default 1), then exits
        — for scripts and agents; no interactive terminal needed. To keep
        the window open and drive it interactively instead, type commands
        into stdin while it runs: 'r <plugin-id>' or 'screenshot <path>'.
        """);
}
