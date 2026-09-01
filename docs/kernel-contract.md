# Kernel Contract v0

A draft, not a final decision. The microkernel, the plugin contract, and the
hot-reload model for Lingua Engine — a modular engine on C#/.NET, with a
constraint that shapes half the decisions below: most of the code, kernel and
plugins alike, will be written by an LLM agent rather than a human.

- **Stack** — .NET 9, C# 13
- **Platforms** — Linux, Windows
- **Kernel** — BCL only, no dependencies
- **Primary author** — an agent
- **Goal** — Play-in-editor with no domain reload

---

## 1. Principle: the kernel is a shared language, not "the engine minus plugins"

The tempting version of "everything is a plugin" makes even the ECS a plugin.
That's a trap: if every other plugin depends on the ECS plugin, the ECS *is*
the kernel already — just with an extra layer of indirection and none of the
stability guarantees a kernel should provide.

**What becomes a plugin is behavior, not the shared data model.**

The kernel is a *lingua franca*: the minimal set of types and mechanisms
plugins need in order to understand each other at all. Anything two
independent plugins are required to agree on lives in the kernel. Everything
else lives outside it.

This is how every plugin architecture that survived contact with reality is
built — Eclipse, VS Code, OSGi, Bevy: a small, stable, extensible kernel plus
everything else on top. Trying to make the shared language itself swappable
produces either indirection overhead or a kernel so empty it guarantees
nothing.

## 2. Scope: what's in, what's out

### Kernel — roughly 4,000 lines, BCL only

| # | Piece | Role |
|---|---|---|
| 01 | **World** | ECS storage: entities, components, queries. Sparse sets on typed arrays, zero `unsafe` — see §7. |
| 02 | **Scheduler** | Frame stages, topological system ordering, parallelism from access conflicts, and debug-mode enforcement of declared access — see §7. |
| 03 | **Plugin Host** | Manifest parsing, dependency resolution, ALC loading, unloading, reload. |
| 04 | **Service Registry** | Publishing and discovering interfaces between plugins. Control path, not the hot path. |
| 05 | **Event Bus** | Decoupled notifications: entity created, asset reloaded, plugin unloaded. |
| 06 | **Time & Log** | Frame clock, fixed-step accumulator, logging interface. Kept minimal. |

### Plugins — everything else, no exceptions

`windowing` · `render` · `physics` · `audio` · `input` · `assets` ·
`scene-format` · `animation` · `ui` · `scripting` · `editor-shell` ·
`inspector` · `gizmos` · `profiler` · `introspect` · `build-pipeline` ·
the game itself.

The editor is also just a set of plugins over the same kernel. This is the
architecture's real test: if the editor can't be assembled as plugins, the
extensibility claim is decorative. A game build is the same kernel minus the
editor plugins.

### Two channels, two costs

Plugins talk to the kernel — and to each other — through two paths with
deliberately different prices:

| Channel | For | Cost | Frequency |
|---|---|---|---|
| **World** (ECS components) | Anything per-entity: transforms, meshes, colliders, health. Render reads what physics wrote without knowing physics exists. | Direct memory access, zero allocation, zero dispatch | 10⁴–10⁶ / frame |
| **Services** (interfaces) | Commands and resources: load an asset, open a window, compile a shader, open an editor panel. | Virtual call, negligible | a handful / scene |
| **Events** (bus) | Facts with no fixed consumer at design time: asset reloaded, plugin unloaded, entity destroyed. | Allocation + fan-out to subscribers | tens / frame |

> **The line that must never be crossed.** Never write
> `IPhysicsService.GetPosition(entity)`. A single call is cheap, but that
> shape of API invites calling it in a loop over entities — and now the
> plugin boundary sits in the hot path. Position is a component in `World`,
> not a service method. Services hand out *capabilities*; `World` hands out
> *data*.

## 3. The plugin contract

A plugin is two entry points and a manifest next to them. The manifest is a
separate file, not assembly attributes — the host has to build the dependency
graph *before* loading anything, or plugin load order becomes a
chicken-and-egg problem with ALC loading itself.

```csharp
// Engine.Kernel / IPlugin.cs

// A plugin holds no game state. None.
// State lives in World; the plugin is code that operates on it.
public interface IPlugin
{
    // Registration: services, systems, component types.
    void Configure(IPluginContext ctx);

    // Full undo of Configure. Whether this method is honest
    // determines whether the ALC unloads at all — see §5.
    void Shutdown(IPluginContext ctx);
}

public interface IPluginContext
{
    IWorld           World    { get; }  // data
    IServiceRegistry Services { get; }  // Provide<T> / Require<T>
    ISchedule        Schedule { get; }  // systems and ordering
    IEventBus        Events   { get; }
    ILogger          Log      { get; }
}
```

```json
// plugins/engine.render/plugin.json
{
  "id":        "engine.render",
  "version":   "0.3.1",
  "contracts": "Engine.Render.Contracts.dll",  // Default ALC
  "assembly":  "Engine.Render.dll",             // Collectible ALC
  "dependsOn": {
    "engine.windowing": "^0.3",
    "engine.assets":    "^0.2"
  },
  "reloadable": true
}
```

```csharp
// plugins/engine.render/RenderPlugin.cs

public sealed class RenderPlugin : IPlugin
{
    public void Configure(IPluginContext ctx)
    {
        // control plane: hand out an interface, take one in
        var window = ctx.Services.Require<IWindow>();
        ctx.Services.Provide<IRenderer>(new VulkanRenderer(window));

        // data plane: the system reads components directly.
        // Reads/Writes are declared explicitly — the scheduler
        // builds a conflict graph from them and parallelizes the
        // frame, and in debug builds enforces the declaration.
        ctx.Schedule.Add(Stage.Render, SubmitDrawCalls)
           .After("engine.transform:propagate")
           .Reads<Transform, MeshRenderer>();
    }

    public void Shutdown(IPluginContext ctx)
    {
        // undo everything: systems, services, subscriptions, GPU resources
        ctx.Services.Revoke<IRenderer>();
        ctx.Schedule.RemoveAllFrom("engine.render");
    }

    static void SubmitDrawCalls(in Frame f, Query<Transform, MeshRenderer> q)
    {
        foreach (var (xf, mesh) in q)   // ref access, no boxing
            f.Draw(mesh.Handle, xf.Matrix);
    }
}
```

## 4. Hot reload: why every plugin is two assemblies

A collectible `AssemblyLoadContext` only unloads once *nothing* references
its contents. One forgotten event subscription, one live `Task`, one cached
`Type` — and the unload silently fails to happen, leaking a little more
memory on every reload.

The most treacherous reference isn't a subscription — it's the **component
structs themselves**. If a plugin declares `struct Transform` and `World`
stores a `Transform[]`, the kernel holds a reference to a type from the
context you're trying to unload. That plugin will never unload.

This is why every plugin splits into two assemblies:

- **Contracts** (`*.Contracts.dll`) — component structs, service
  interfaces. Loaded into the **Default ALC**, which lives for the process
  lifetime and never unloads. `World` owning references into it is fine,
  because it isn't supposed to unload.
- **Implementation** (`*.dll`) — systems, service implementations. Loaded
  into a **collectible ALC**, recreated on every reload. No `static` state,
  no data — only code.

References only point from implementation to contracts, never the reverse,
which is what lets `Unload()` actually succeed. In practice, ~95% of
iteration is logic changes: instant reload. Changing a component's fields is
rare and requires an editor restart.

### Reload sequence

1. A file watcher sees a freshly built `Engine.Render.dll`. The build happens
   externally, via plain `dotnet build` — the editor doesn't need its own
   compiler.
2. The scheduler finishes the current frame and pauses. Reload never happens
   mid-stage.
3. `Shutdown()` runs: systems, services, subscriptions, and native resources
   are torn down. Anything `Configure` registered has to be undone here, or
   step 5 fails.
4. A snapshot of this plugin's component data is taken — only if contracts
   were also rebuilt. Component arrays are copied along with a field
   descriptor.
5. `alc.Unload()` + `GC.Collect()`, then a `WeakReference` check. If the
   context doesn't collect, that's a loud error naming the pinning reference
   — not a silent leak.
6. A new ALC, the new assembly loads, `Configure()` runs. The plugin doesn't
   know it was reloaded.
7. Data is restored: old and new field layouts are matched by name. Matches
   are copied, new fields get default values, removed fields are dropped.
8. The scheduler rebuilds its ordering graph and resumes. Typical budget:
   200–400 ms, almost all of it spent waiting on the build.

> **Leak testing belongs in CI from day one.** Load and unload a test plugin
> 200 times in a row; after each cycle, verify the ALC's `WeakReference` is
> dead and working-set memory hasn't grown. This is the one thing that keeps
> the architecture from slowly degrading — ALC leaks accumulate invisibly and
> surface months later, by which point the cause is indistinguishable from
> noise.

## 5. Play mode without domain reload

Unity's Play-mode wait isn't about compilation — it's about serializing all
script state, tearing the domain down, and recreating it. That step doesn't
exist here: state never lived in plugin code to begin with. It lives in
`World`, owned by the kernel, untouched by reload and untouched by entering
Play.

```csharp
// Engine.Editor / PlayMode.cs

// Entering Play is a memory copy, not a runtime rebuild.
void EnterPlay()
{
    _snapshot = world.Snapshot();   // array copy, low single-digit ms
    schedule.SetGroup(SystemGroup.Play);
}

void ExitPlay()
{
    world.Restore(_snapshot);       // Play-mode edits roll back
    schedule.SetGroup(SystemGroup.Edit);
}
```

Play becomes a system-group switch, not a world rebuild. A side effect of
the same decision: system code can be edited *during* Play without
restarting — state is preserved. That's the feedback loop the whole engine
exists to enable.

## 6. Where this breaks

| Risk | The problem | Mitigation |
|---|---|---|
| **ALC leaks** — the main killer | Unload silently fails from one forgotten reference. Symptom: memory growth after N reloads; cause takes days to find. | 200-cycle test in CI. Diagnose pinning references in the host itself, not via an external profiler. |
| **Scope** | The kernel is 3–5k lines and a couple of months. The renderer, asset pipeline, and editor are years, and they decide whether the engine ships. | Don't write your own RHI. Silk.NET or Veldrid underneath; originality goes into the architecture on top. |
| **GC in the hot path** | Collector pauses against a 16.6 ms frame budget. The managed-runtime tradeoff is accepted, but it demands discipline. | `unmanaged` structs for components, `Span<T>` in systems, allocation only at load time. Server GC. |
| **Creeping abstraction** | The temptation to hide `World` behind a "cleaner" interface. Kills performance invisibly and irreversibly. | The rule in §2 is law. Review rejects any service method that takes an `Entity`. |
| **Plausible-but-wrong code** — agent-specific | The agent produces code that compiles, passes a smoke test, and breaks on someone else's GPU — sync, barriers, resource lifetime. | Minimize new subsystems; Silk.NET/Veldrid is risk management, not time-saving. A conformance harness gates every plugin merge. |
| **Contract drift** | A contract change requires updating every dependent plugin, and a stale implementation keeps compiling while silently diverging from spec. | Versions in the manifest, plus running *every* plugin's harness on every build, not just the changed one. |

## 7. Written by an agent, not a human

This isn't an afterthought — it's an input condition. It's why §2's storage
is simpler than a "fast" ECS would normally be, and it adds a surface no
classic editor needs at all.

**What works in our favor:**

- *A plugin's boundary matches a context window's boundary.* Writing
  `engine.physics` only requires the kernel API, physics' own contracts, and
  its own code — nothing else. Modularity chosen for team reasons turns out
  to also be how you fit a task in an agent's head.
- *Blast radius is bounded by the plugin.* Plausible-but-wrong code is
  inevitable; the question is what it can break. The kernel is written once,
  tested, and **frozen** — the agent never touches it again after that. A bug
  in a plugin stays a bug in that plugin.

**What has to change:**

- *No `unsafe` in the v1 hot path.* Stride arithmetic, alignment, a pointer
  that outlives a GC-triggering call — exactly the code an LLM writes
  convincingly and wrong, failing as nondeterministic memory corruption.
  Hence sparse sets on `T[]` instead of archetype chunks; chunked layout
  stays an optimization behind the same query API for whenever a profile
  actually calls for it.
- *Explicit over clever.* Naming conventions, code generators, reflection
  magic save a human keystrokes but hide behavior from something that
  reasons over text. Verbose, explicit system and service registration is a
  deliberate cost. Reflection stays where it's safe: the editor inspector.

> **Verification instead of trust.** `Reads<>` / `Writes<>` declarations must
> be enforced in debug builds: a system touching an undeclared component
> fails immediately, with a message naming the violation. For a human this is
> hygiene; for an agent it's structural — otherwise a wrong access
> declaration becomes a race that reproduces once in a hundred runs and is
> otherwise undiagnosable. Same principle for the plugin conformance harness:
> load, reload 200 times, verify `Shutdown` fully undoes `Configure`. The
> agent needs to be able to tell, on its own, that it's actually done.

### Introspection surface

The agent doesn't look at a screen. Whatever the editor shows a human's eyes
has to be available as data, or the feedback loop closes on a human and the
whole point of fast reload is lost.

```
# run a scene headless and check an assertion about world state
engine run --headless --frames 60 \
           --scene tests/physics_stack.scene \
           --dump out/world.json \
           --assert "count(Rigidbody where sleeping) == 12"

# why a plugin won't unload — instead of guessing from a profiler
engine diag why-pinned engine.render
```

**The loop this enables:** agent edits a system → `dotnet build` for one
plugin → ALC reload (world state untouched) → 60 headless frames →
machine-readable dump + assertions → back to the agent, no human in the
loop. Fast reload saves a human time on its own; paired with headless runs
and a state dump, it becomes a loop the agent can close by itself — and the
minutes-to-seconds iteration speedup multiplies by however many iterations
the agent can now run.

**The kernel is written once and frozen.** It's the one place where a
mistake is expensive and spreads everywhere. A small kernel isn't only an
architectural preference — it bounds how much code has to be correct.

## 8. Build order

Each milestone ends in a working demo, not a "finished subsystem." The order
is chosen so the riskiest bet — ALC unloading — gets tested first, while the
cost of changing course is still zero.

| | Milestone | Done when |
|---|---|---|
| **M0** | **Kernel only.** `World` on sparse sets, staged scheduler with access enforcement, plugin host with ALC, service registry, JSON world dump. No window, no graphics. | An agent runs the full loop from §7 unassisted: edits a headless plugin, rebuilds, reads the changed dump — and the 200-cycle leak test is green. |
| **M1** | **Window, input, a triangle.** Three separate plugins over Silk.NET. First real-load test of the data channel. | The triangle's color changes by editing system code, with no app restart. |
| **M2** | **Assets and scenes.** Hot-reloading asset plugin, scene format, `World` serialization. | Swapping a texture on disk changes the picture with nothing stopped; a scene loads and saves. |
| **M3** | **Editor as plugins.** Shell, reflection-based component inspector, hierarchy, gizmos, Play/Stop on snapshots. | Entering Play takes under 100 ms — the original complaint about Unity is closed. |
| **M4** | **One small game, end to end.** Physics, audio, a Linux + Windows build pipeline. A 20-minute game, shipped as an executable. | The build runs on both platforms with no editor plugins in the shipped binary. |

---

Open questions to resolve before M0: whether `Time` and `Log` belong in the
kernel or as plugins; whether the Event Bus is needed at launch or whether
event-components in `World` cover its role; whether the set of frame stages
is fixed or plugin-extensible; and at what profiling point (if ever) to move
from sparse sets to archetype chunks. Assembly names in the examples are
placeholders.
