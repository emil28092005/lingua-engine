# Kernel Contract v0

A draft, not a final decision. The microkernel, the plugin contract, and the
hot-reload model for Lingua Engine — a modular engine on C#/.NET, with a
constraint that shapes half the decisions below: most of the code, kernel and
plugins alike, will be written by an LLM agent rather than a human.

- **Stack** — .NET 9, C# 13
- **Platforms** — Linux, Windows
- **Kernel** — BCL only, no dependencies
- **Object model** — `GameObject` / `Component`
- **Physics** — [Box3D](https://github.com/erincatto/box3d) (C, MIT), bound via P/Invoke — not our own
- **Primary author** — an agent
- **Goal** — Play-in-editor with no domain reload

Built for a small team's own use, at indie scale — not AAA. That scope
licenses several calls made below: accepting GC pauses instead of chasing a
zero-allocation hot path, picking `GameObject`/`Component` over a faster
struct-of-arrays ECS, buying rendering and windowing off the shelf instead of
writing them. None of those are free choices at a bigger scale; at this one,
dev velocity outweighs the performance left on the table.

---

## 1. Principle: the kernel is a shared language, not "the engine minus plugins"

The tempting version of "everything is a plugin" makes even the object model
a plugin. That's a trap: if every other plugin depends on the
GameObject/Component plugin, that plugin *is* the kernel already — just with
an extra layer of indirection and none of the stability guarantees a kernel
should provide.

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
| 01 | **World** | `GameObject` hierarchy (parent/children, name, tags) plus typed `Component` instances, with a type index so `Query<T>()` costs O(matches), not O(all). Plain classes, zero `unsafe` — see §7. |
| 02 | **Scheduler** | Frame stages, topological system ordering, parallel execution of systems with disjoint declared access, and debug-mode enforcement of that access — see §7. Structural changes (adding/removing a `GameObject` or `Component`) are queued and applied at the stage boundary, so a running system never sees a collection mutate under it. |
| 03 | **Plugin Host** | Manifest parsing, dependency resolution, ALC loading, unloading, reload. |
| 04 | **Service Registry** | Publishing and discovering interfaces between plugins. Control path, not the hot path. |
| 05 | **Event Bus** | `Publish`/`Subscribe`, ownership-tracked and leak-safe the same way as the Scheduler's systems — see §7. `PluginHost` publishes `PluginLoaded`/`PluginUnloaded`; `engine.assets` publishes `TextureReloaded` when a watched file changes on disk (M2). Still nothing publishes `GameObject` created/destroyed facts — `GameWorld` doesn't touch the bus at all, deliberately: a publish on every structural change would tax the hot path for listeners that usually don't exist. |
| 06 | **Time & Log** | Frame clock (`DeltaTime`, `ElapsedTime`, `FrameCount`, `FixedDeltaTime`) and logging, both on `IPluginContext`. The fixed-step accumulator (`Time.ConsumeFixedSteps`) shipped with M4, driving `Stage.FixedUpdate` — `engine.physics` is its first real consumer. |

`GameObject.Transform` is the one field embedded directly rather than
modeled as a `Component` subclass — it's a plain struct holding local
position, rotation, and scale, because nearly every system in the engine
touches it every frame, and routing that through the same virtual-dispatch
path as every other component would tax the one thing everything depends
on. `GameObject.WorldMatrix` composes it with the parent chain on every
read rather than caching a value — a cache here would need invalidating on
every reparent and every ancestor's change, which is more bookkeeping than
a handful of matrix multiplies costs at indie scale. Everything else —
`MeshRenderer`, `Rigidbody`, `AudioSource`, game-specific components — is a
plain class, heap-allocated, no special treatment.

### Plugins — everything else, no exceptions

`windowing` · `render` · `physics` · `audio` · `input` · `assets` ·
`scene-format` · `animation` · `ui` · `scripting` · `editor-shell` ·
`inspector` · `gizmos` · `profiler` · `introspect` · `build-pipeline` ·
the game itself.

The editor is also just a set of plugins over the same kernel. This is the
architecture's real test: if the editor can't be assembled as plugins, the
extensibility claim is decorative. A game build is the same kernel minus the
editor plugins.

### Per-project configuration

A plugin's manifest declares what it needs; a **project's** manifest
declares which plugins it loads, at which versions, and where to find its
own. This is the piece that actually makes modularity a per-project
property rather than a claim about the engine in the abstract — a new
project doesn't fork the engine to swap an implementation, it points its
manifest at a different plugin satisfying the same contracts, or adds
project-local plugins that never leave its own tree.

```json
// MyGame/project.json
{
  "engineVersion": "^0.3",
  "plugins": [
    { "id": "engine.windowing" },
    { "id": "engine.render",  "version": "^0.3" },
    { "id": "engine.physics", "version": "^0.2" },
    { "id": "mygame.enemies" }
  ],
  "pluginPaths": ["./plugins"]
}
```

`engine.render` here could just as well point at a project-local fork with
the same `contracts` and a bumped `id` — the Plugin Host resolves a
project's manifest through the exact same dependency graph it already
builds for plugin-to-plugin `dependsOn`, so nothing new has to be built to
support it.

### Two channels, two costs

Plugins talk to the kernel — and to each other — through two paths with
deliberately different prices:

| Channel | For | Cost | Frequency |
|---|---|---|---|
| **World** (GameObjects & Components) | Anything per-entity: transforms, meshes, colliders, health. Render reads what physics wrote without knowing physics exists. | Direct field access on a cached component reference; `Query<T>()` is a type-index lookup, not a scan | 10⁴–10⁶ / frame |
| **Services** (interfaces) | Commands and resources: load an asset, open a window, compile a shader, open an editor panel. | Virtual call, negligible | a handful / scene |
| **Events** (bus) | Facts with no fixed consumer at design time: asset reloaded, plugin unloaded, entity destroyed. | Allocation + fan-out to subscribers | tens / frame |

> **The line that must never be crossed.** Never write
> `IPhysicsService.GetPosition(GameObject go)`. A single call is cheap, but that
> shape of API invites calling it in a loop over entities — and now the
> plugin boundary sits in the hot path. Position is on `GameObject.Transform`,
> not a service method. Services hand out *capabilities*; `World` hands out
> *data*.
>
> The same rule applies one level down, inside `World` itself: don't call
> `otherGameObject.GetComponent<T>()` for a different entity from inside a
> per-entity loop — that's a type-indexed lookup multiplied by iteration
> count, the exact perf trap Unity code is famous for. Resolve the
> components you need once, before the loop starts, and index into that.

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
    // determines whether the ALC unloads at all — see §4.
    void Shutdown(IPluginContext ctx);
}

public interface IPluginContext
{
    IWorld           World    { get; }  // data
    IServiceRegistry Services { get; }  // Provide<T> / Require<T>
    ISchedule        Schedule { get; }  // systems and ordering
    IEventBus        Events   { get; }  // Publish / Subscribe
    ILogger          Log      { get; }
    ITime            Time     { get; }  // DeltaTime, ElapsedTime, FrameCount
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
// plugins/engine.render/Contracts/MeshRenderer.cs

// Plain data, no methods. Lives in the Contracts assembly — see §4
// for why that split is what makes reload safe.
public sealed class MeshRenderer : Component
{
    public MeshHandle Handle;
}
```

```csharp
// plugins/engine.render/RenderPlugin.cs

public sealed class RenderPlugin : IPlugin
{
    public void Configure(IPluginContext ctx)
    {
        // control plane: hand out an interface, take one in
        var window = ctx.Services.Require<IEngineWindow>();
        ctx.Services.Provide<IRenderer>(new VulkanRenderer(window));

        // data plane: the system queries GameObjects by component type.
        // Reads/Writes are declared explicitly — the scheduler uses
        // them to run systems with disjoint access in parallel, and
        // in debug builds enforces that a system only touches what
        // it declared — see §7.
        ctx.Schedule.Add(Stage.Render, SubmitDrawCalls)
           .After("engine.transform:propagate")
           .Reads<MeshRenderer>();
    }

    public void Shutdown(IPluginContext ctx)
    {
        // undo everything: systems, services, subscriptions, GPU resources
        ctx.Services.Revoke<IRenderer>();
        ctx.Schedule.RemoveAllFrom("engine.render");
    }

    static void SubmitDrawCalls(in Frame f, IWorld world)
    {
        // type-indexed lookup, not a scan — see the World row in §2
        foreach (var go in world.Query<MeshRenderer>())
            f.Draw(go.GetComponent<MeshRenderer>().Handle, go.WorldMatrix);
    }
}
```

## 4. Hot reload: why every plugin is two assemblies

A collectible `AssemblyLoadContext` only unloads once *nothing* references
its contents. One forgotten event subscription, one live `Task`, one cached
`Type` — and the unload silently fails to happen, leaking a little more
memory on every reload.

The most treacherous reference isn't a subscription — it's the **component
classes themselves**. If a plugin declares `class MeshRenderer : Component`
and a `GameObject` holds one in its component list, the kernel holds a
reference to a type from the context you're trying to unload. That plugin
will never unload.

This is why every plugin splits into two assemblies:

- **Contracts** (`*.Contracts.dll`) — component classes, service
  interfaces. Loaded into the **Default ALC**, which lives for the process
  lifetime and never unloads. `World` owning references into it is fine,
  because it isn't supposed to unload.
- **Implementation** (`*.dll`) — systems, service implementations. Loaded
  into a **collectible ALC**, recreated on every reload. No `static` state —
  only code that operates on objects it doesn't own.

References only point from implementation to contracts, never the reverse,
which is what lets `Unload()` actually succeed. Because component *instances*
live in `World`, owned by the kernel, an implementation-only reload never
touches game data at all — it isn't snapshotted and restored, it's simply
never in the collectible ALC to begin with.

### Reload sequence

1. A file watcher sees a freshly built `Engine.Render.dll`. The build happens
   externally, via plain `dotnet build` — the editor doesn't need its own
   compiler.
2. The scheduler finishes the current frame and pauses. Reload never happens
   mid-stage.
3. `Shutdown()` runs: systems, services, subscriptions, and native resources
   are torn down. Anything `Configure` registered has to be undone here, or
   step 4 fails. `World`'s component instances aren't touched — the
   implementation assembly never held them.
4. `alc.Unload()` + `GC.Collect()`, then a `WeakReference` check. If the
   context doesn't collect, that's a loud error naming the pinning reference
   — not a silent leak.
5. A new ALC, the new assembly loads, `Configure()` runs. The plugin doesn't
   know it was reloaded.
6. The scheduler rebuilds its ordering graph and resumes. Typical budget:
   200–400 ms, almost all of it spent waiting on the build.

In practice this covers ~95% of iteration, because most changes are to
system logic, not component shape.

> **Changing a component's own fields is a different, rarer case — and it
> doesn't hot-reload at all.** A component's fields live in the Contracts
> assembly, and the Default ALC hosting it never unloads by design. There is
> no in-process path to swap it. This isn't a gap to fill later; it's a
> deliberate seam. Field changes are rare enough that paying for an editor
> restart there — reloading the scene from its serialized file rather than
> migrating live objects — is a better trade than writing and maintaining
> live-migration code for the 95% case that doesn't need it.

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
Play. Built and shipped in M3 — this is what actually runs, not the design
sketch that preceded it:

```csharp
// IWorld (Engine.Kernel)
string Snapshot();          // a scene-format dump of the whole graph
void Restore(string snapshot);

// Engine.Editor.Contracts / IPlayModeController — engine.editor's real
// implementation wraps exactly these two calls, nothing else:
void EnterPlay() => _snapshot = world.Snapshot();
void ExitPlay()  { world.Restore(_snapshot!); _snapshot = null; }
```

`Snapshot`/`Restore` reuse `SceneFormat` — the same JSON serialization a
scene file on disk already uses — rather than a bespoke clone mechanism.
A scene file and a Play-mode snapshot are the same problem (capture every
`GameObject`'s state faithfully enough to reconstruct it) at two different
moments; reusing already-proven serialization beat maintaining a second way
to walk the same graph. `WorldSnapshotTests` times a 300-`GameObject`
snapshot+restore at under 100ms — M3's literal "done when" — and the real
editor path confirms it: `PlayModeController.EnterPlay` logs its own
wall-clock cost, and a real run against `samples/WindowDemo`'s scene
measured 13ms.

There's no `SystemGroup.Play`/`SystemGroup.Edit` split in the scheduler —
that would mean every system declaring which group it belongs to, for a
distinction the host loop alone can already make. `Engine.Host` checks
`IPlayModeController.IsPlaying` once per frame and only runs `Stage.Update`
while it's true; `Stage.Render`/`Stage.Present` run every frame regardless,
so Edit mode still shows a live, responsive scene view — just one that
never ticks. A project with no `engine.editor` loaded sees no behavior
change at all: `Stage.Update` runs unconditionally, same as before Play
mode existed.

A side effect of Play being nothing but a snapshot and a stage-gate: system
code can be edited *during* Play without restarting — hot-reloading a
plugin mid-Play still works, because nothing about Play mode touches the
ALC or plugin loading at all. That's the feedback loop the whole engine
exists to enable.

## 6. Where this breaks

| Risk | The problem | Mitigation |
|---|---|---|
| **ALC leaks** — the main killer | Unload silently fails from one forgotten reference. Symptom: memory growth after N reloads; cause takes days to find. | 200-cycle test in CI. Diagnose pinning references in the host itself, not via an external profiler. |
| **Scope** | The kernel is 3–5k lines and a couple of months. The renderer, asset pipeline, and editor are years, and they decide whether the engine ships. | Don't write your own RHI or physics engine. Silk.NET or Veldrid underneath the renderer, Box3D underneath physics; originality goes into the architecture on top. |
| **GC pressure from Components** | Every component is a heap object; churn from creating/destroying GameObjects at runtime (bullets, particles, pickups) means allocation and collection, against a 16.6 ms frame budget. | Pool GameObjects and components for anything spawned/destroyed at high frequency. Server GC. `Query<T>()` iterators must not allocate. |
| **Creeping abstraction** | The temptation to hide `World` behind a "cleaner" interface. Kills performance invisibly and irreversibly. | The rule in §2 is law. Review rejects any service method that takes a `GameObject`. |
| **Plausible-but-wrong code** — agent-specific | The agent produces code that compiles, passes a smoke test, and breaks on someone else's GPU — sync, barriers, resource lifetime. | Minimize new subsystems; Silk.NET/Veldrid is risk management, not time-saving. A conformance harness gates every plugin merge. |
| **Contract drift** | A contract change requires updating every dependent plugin, and a stale implementation keeps compiling while silently diverging from spec. | Versions in the manifest, plus running *every* plugin's harness on every build, not just the changed one. |

## 7. Written by an agent, not a human

This isn't an afterthought — it's an input condition. It's part of why §2
picked the most conventional possible object model instead of a
performance-first one, and it adds a surface no classic editor needs at all.

**What works in our favor:**

- *A plugin's boundary matches a context window's boundary.* Writing
  `engine.physics` only requires the kernel API, physics' own contracts, and
  its own code — nothing else. Modularity chosen for team reasons turns out
  to also be how you fit a task in an agent's head.
- *Blast radius is bounded by the plugin.* Plausible-but-wrong code is
  inevitable; the question is what it can break. The kernel is written once,
  tested, and **frozen** — the agent never touches it again after that. A bug
  in a plugin stays a bug in that plugin.
- *GameObject/Component is the most over-represented pattern in an LLM's
  training data of any game architecture.* That's also a reason it won over
  a hand-rolled ECS: components are plain classes with plain fields, no
  stride arithmetic, no manual layout, nothing that compiles cleanly and
  corrupts memory at runtime. The one performance-motivated exception,
  `Transform` as an inline struct, is confined to the kernel and never
  written by the agent at all.

**What has to change:**

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
| **M0** | **Kernel only.** `World` as a `GameObject`/`Component` hierarchy with type-indexed queries, staged scheduler with access enforcement, plugin host with ALC, service registry, JSON world dump. No window, no graphics. | An agent runs the full loop from §7 unassisted: edits a headless plugin, rebuilds, reads the changed dump — and the 200-cycle leak test is green. |
| **M1** | **Window, input, a triangle.** Three separate plugins over Silk.NET. First real-load test of the data channel. | The triangle's color changes by editing system code, with no app restart. |
| **M2** | **Assets and scenes.** Hot-reloading asset plugin, scene format, `World` serialization. | Swapping a texture on disk changes the picture with nothing stopped; a scene loads and saves. |
| **M3** | **Editor as plugins — done.** Shell (`engine.editor`, ImGui over the live scene, see §5 and the new `Stage.Present`), reflection-based inspector and hierarchy, Play/Stop on snapshots, a real 3D translate gizmo driven by the actual camera. | Entering Play takes under 100 ms — the original complaint about Unity is closed. Proven twice: `WorldSnapshotTests` (kernel, 300 `GameObject`s) and a real editor run against `samples/WindowDemo` (13ms, logged by `PlayModeController`). |
| **M4** | **One small game, end to end — in progress.** `engine.physics` (Box3D) and `engine.audio` (miniaudio) done, both over a narrow, scalars-only P/Invoke shim; `samples/PhysicsDemo` ties physics/audio/input/render together with `engine.editor` deliberately excluded from its `project.json`; `.github/workflows/build.yml` builds and tests on real `ubuntu-latest`/`windows-latest` runners. Not yet: the workflow has actually run on GitHub (blocked on an OAuth `workflow` scope grant), and the demo is a physics sandbox, not yet a scored 20-minute game. | The build runs on both platforms with no editor plugins in the shipped binary. |

---

**The kernel's open questions are resolved.** What M0 shipped without
deciding, in order:

- **`Time` and `Log`: both in the kernel**, both on `IPluginContext`. Every
  plugin needs logging and a frame clock; there's no realistic case for a
  project wanting to swap either out per-project the way it would swap
  physics or rendering. `Log` was already built this way by the time the
  question got asked explicitly — `Time` (`DeltaTime`, `ElapsedTime`,
  `FrameCount`) shipped alongside closing the question, not before it.
  **Explicitly still deferred:** the fixed-step accumulator the original
  kernel scope named alongside the frame clock. Building it now, with no
  physics system to test it against, would be untested speculative
  machinery — exactly what this project has avoided everywhere else. It
  arrives with M4, alongside the `Stage.FixedUpdate` it would drive.
- **Event Bus: real, not event-components in `World`.** A disposable
  event-as-`GameObject` fits a pure ECS's cheap-entity model better than
  ours, where `GameObject` carries persistent identity and hierarchy. A
  conventional `Publish`/`Subscribe` bus is the better fit for this object
  model specifically. `PluginHost` publishing `PluginLoaded` /
  `PluginUnloaded` is the concrete proof it's real infrastructure, not an
  API nobody calls — and `Subscribe`/`RemoveAllFrom` follow the exact
  leak-safety shape `Schedule` already established (ownership tracked by
  the subscribing delegate's declaring assembly), proven the same way:
  `sandbox.echo` subscribes for real, and the 200-cycle leak test in
  `AlcUnloadTests` now exercises that cleanup path, not just `Schedule`'s.
- **Frame stages: fixed, kernel-defined — not plugin-extensible.** A stage
  is part of the shared language every plugin and the host loop rely on;
  letting plugins register arbitrary custom stages would mean the host's
  frame loop can no longer just call a known, closed set of `RunStage`s. No
  stage gets added without something real to run in it — which is exactly
  what happened once: M3 added `Stage.Present` alongside `{Update, Render}`
  when `engine.editor`'s ImGui overlay needed to draw after the scene but
  before the buffers swap, and `engine.render`'s old single Clear+Draw+
  SwapBuffers system had nowhere else to put the swap that wouldn't race
  it. `Stage.FixedUpdate` is still the only stage left on the M4 list.
- **Data-oriented fast path: still open, on purpose, with a trigger
  condition instead of a deadline.** Not "undecided" the way the other
  three were — deliberately not worth deciding before there's a concrete
  system to decide it against. Revisit when a specific system (particles is
  the standing example) needs tens of thousands of `GameObject`s updated
  per frame *and* profiling — not intuition — shows `GameObject`/`Component`
  overhead is the actual bottleneck. Until then, an early decision here
  would be optimizing against a guess.

Assembly names in the examples are placeholders.
