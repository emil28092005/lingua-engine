// The runtime host — drives both halves of the loop described in
// docs/kernel-contract.md §7 and the M1 windowed case in §8:
//
//   engine run --headless --plugins <dir> --project <project.json> --frames <n> [--scene <path>] [--dump <path>]
//   engine run --windowed --plugins <dir> --project <project.json> [--scene <path>] [--dump <path>]
//
// --scene loads after every plugin in --project, not before: a scene file
// names its components by type ("TypeFullName, AssemblyName" — see
// SceneFormat), and that only resolves once the plugin that defines the
// type has loaded its Contracts assembly into the Default ALC. It's
// additive onto whatever's already in World — nothing pre-clears it.
//
// --windowed needs a loaded plugin that provides IEngineWindow (engine.
// windowing) — Engine.Host references that plugin's *Contracts* assembly
// directly (never its implementation, which stays dynamically loaded via
// PluginHost/ALC same as any other plugin) because driving a window-pumped
// loop is the host's job, not the kernel's: Engine.Kernel never hears about
// Silk.NET at all.
//
// Deliberately not implemented yet, noted explicitly below rather than
// silently accepted or rejected as gibberish:
//   --assert   no query DSL exists to parse the doc's illustrative
//              `count(Rigidbody where sleeping) == 12` syntax; a dump is
//              plain JSON, so external tooling (jq, a test script) already
//              covers "check something about it" without us inventing a
//              parser for a grammar that was never actually specified.
//   diag why-pinned   needs a way to walk the GC heap for what's still
//              referencing an ALC; nothing has ever failed to unload in
//              testing, so there's nothing to build this against yet.

using Engine.Editor.Contracts;
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
string? scenePath = null;
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
        case "--scene" when i + 1 < args.Length:
            scenePath = args[++i];
            break;
        case "--screenshot" when i + 1 < args.Length:
            screenshotPath = args[++i];
            break;
        case "--screenshot-after-frames" when i + 1 < args.Length:
            screenshotAfterFrames = int.Parse(args[++i]);
            break;
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
var events = new EventBus();
var time = new Time();
var host = new PluginHost(world, services, schedule, events, time);

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

if (scenePath is not null)
{
    try
    {
        SceneFormat.Load(world, scenePath);
        Console.WriteLine($"Loaded scene '{scenePath}'.");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Failed to load scene '{scenePath}': {ex.Message}");
        return 1;
    }
}

if (windowed)
{
    if (!services.TryRequire<IEngineWindow>(out var window))
    {
        Console.Error.WriteLine(
            "--windowed requires a loaded plugin that provides IEngineWindow (e.g. engine.windowing).");
        return 1;
    }

    var playMode = services.TryRequire<IPlayModeController>(out var pmc) ? pmc : null;

    Console.WriteLine(
        """
        Window open — close it to exit. Commands (type + Enter):
          r <plugin-id>       reload that plugin live
          screenshot <path>   save the current frame to a PNG (needs a plugin providing IScreenCapture)
          play                enter Play mode (needs a plugin providing IPlayModeController)
          stop                exit Play mode, restoring the pre-Play snapshot
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
            // >= 1, not == 2: "play"/"stop" (added alongside
            // IPlayModeController) take no argument, unlike "r <id>" and
            // "screenshot <path>" — an empty argument is harmless for
            // those two since they never got parsed with one anyway.
            if (parts.Length >= 1)
                commandQueue.Enqueue((parts[0], parts.Length > 1 ? parts[1] : ""));
        }
    });

    var frameCount = 0;
    var lastTime = window!.Native.Time;

    while (!window.IsClosing)
    {
        frameCount++;
        window.Native.DoEvents();

        var currentTime = window.Native.Time;
        time.Tick((float)(currentTime - lastTime));
        lastTime = currentTime;

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
                        // Not unconditional: a plugin whose previous
                        // reload already failed inside Load (a bad
                        // Configure — now rolled back, see PluginHost.
                        // Load) isn't loaded any more, and Unload would
                        // just throw "not loaded" on this retry instead of
                        // ever reaching Load again.
                        if (host.IsLoaded(cmd.Argument))
                            host.Unload(cmd.Argument);

                        host.Load(pluginDirectory);
                        Console.WriteLine($"Reloaded '{cmd.Argument}'. World state and the window were untouched.");
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine(
                            $"Failed to reload '{cmd.Argument}': {ex.Message} " +
                            $"'{cmd.Argument}' is now unloaded (not just still running the old code) — " +
                            $"fix the error and run 'r {cmd.Argument}' again.");
                    }

                    break;

                case "screenshot":
                    pendingScreenshots.Add(cmd.Argument);
                    break;

                case "play":
                    if (playMode is null)
                        Console.Error.WriteLine("No loaded plugin provides IPlayModeController (e.g. engine.editor).");
                    else
                        playMode.EnterPlay();
                    break;

                case "stop":
                    if (playMode is null)
                        Console.Error.WriteLine("No loaded plugin provides IPlayModeController (e.g. engine.editor).");
                    else
                        playMode.ExitPlay();
                    break;

                default:
                    Console.Error.WriteLine($"Unknown command: '{cmd.Command}'");
                    break;
            }
        }

        // No IPlayModeController loaded (no engine.editor) means there's no
        // Edit/Play distinction to make — Update always runs, same as
        // before this plugin existed. With one loaded, Update only runs
        // while actually Playing: Edit mode still renders the scene every
        // frame (so the editor UI stays responsive and the view isn't
        // frozen mid-edit), it just never ticks it. FixedUpdate is gated
        // the same way and for the same reason — physics shouldn't step
        // while Edit mode has the world frozen — and only accumulates time
        // while playing, so Play doesn't open with a catch-up burst of
        // steps for however long Edit mode had been sitting idle.
        if (playMode is null || playMode.IsPlaying)
        {
            var fixedSteps = time.ConsumeFixedSteps();
            for (var step = 0; step < fixedSteps; step++)
                schedule.RunStage(Stage.FixedUpdate, world);

            schedule.RunStage(Stage.Update, world);
        }

        schedule.RunStage(Stage.Render, world);
        schedule.RunStage(Stage.Present, world);

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
    // No wall clock to measure — headless runs as fast as the CPU allows,
    // not once per real 1/60s. A fixed nominal delta keeps ITime.DeltaTime
    // meaningful for systems that use it, and keeps headless runs
    // deterministic, which --windowed's real wall-clock delta can't be.
    const float headlessDeltaTime = 1f / 60f;

    for (var frame = 0; frame < frames; frame++)
    {
        time.Tick(headlessDeltaTime);

        var fixedSteps = time.ConsumeFixedSteps();
        for (var step = 0; step < fixedSteps; step++)
            schedule.RunStage(Stage.FixedUpdate, world);

        schedule.RunStage(Stage.Update, world);
    }

    Console.WriteLine($"Ran {frames} update frame(s).");
}

if (dumpPath is not null)
{
    SceneFormat.Save(world, dumpPath);
    Console.WriteLine($"Wrote world dump to '{dumpPath}'.");
}

return 0;

static void PrintUsage()
{
    Console.Error.WriteLine(
        """
        Usage:
          engine run --headless --plugins <dir> --project <project.json> --frames <n> [--scene <path>] [--dump <path>]
          engine run --windowed --plugins <dir> --project <project.json> [--scene <path>] [--dump <path>]
                      [--screenshot <path> [--screenshot-after-frames <n>]]

        --screenshot captures once, after <n> frames (default 1), then exits
        — for scripts and agents; no interactive terminal needed. To keep
        the window open and drive it interactively instead, type commands
        into stdin while it runs: 'r <plugin-id>' or 'screenshot <path>'.
        """);
}
