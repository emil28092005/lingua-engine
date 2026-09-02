# Lingua Engine

A modular, plugin-first game engine built around one idea: the kernel is a
shared language, not a shared implementation. Everything the engine can do —
rendering, physics, audio, even the editor itself — is a plugin that speaks
that language. The kernel only defines the vocabulary plugins use to
understand each other.

Built for Linux and Windows, in C#/.NET, with two goals that shape every
design decision:

- **Fast iteration.** No Unity-style domain reload. Plugins hot-reload their
  compiled code without resetting game state, because state never lives in
  plugin code to begin with — see [`docs/kernel-contract.md`](docs/kernel-contract.md).
- **A small, frozen kernel.** Everything else — including the parts most
  engines treat as core — is a plugin, versioned and replaceable per project.

## How this gets built

Most of the code here — kernel and plugins alike — is written by an LLM
coding agent rather than by hand. That's not incidental: it's a design
input. It's why the object model is `GameObject`/`Component` instead of a
hand-rolled ECS, why registration is verbose and explicit instead of
convention-based, and why the engine has a headless, scriptable
introspection surface no classic editor bothers with — see
[`docs/kernel-contract.md`](docs/kernel-contract.md#7-written-by-an-agent-not-a-human).

## Status

**M0 done.** The kernel — `World` (`GameObject`/`Component`, type-indexed
queries), `Schedule` (stage execution, conflict batching, debug-mode access
enforcement), `PluginHost` (two-ALC load/unload, verified leak-free over
200 cycles), and a headless CLI (`engine run --headless ... --dump`) — all
exist and are tested. The full agent loop from
[`docs/kernel-contract.md#7`](docs/kernel-contract.md#7-written-by-an-agent-not-a-human)
runs end to end.

**M1 done.** `engine.windowing`, `engine.render` (a real shader-drawn
triangle, not just a clear color), and `engine.input` all exist over
Silk.NET. The milestone's actual claim — edit a plugin's code, rebuild just
it, reload it while a real window stays open, see the change with no app
restart — is proven against a live GL context: two PNGs of the *same*
running window, before and after a live reload, orange triangle then green,
same process the whole time. `IScreenCapture` (`engine.render`) reads the
frame back from the GPU and writes it to a file with a hand-rolled PNG
encoder — no `SixLabors.ImageSharp` (its license isn't MIT/Apache) and no
desktop screenshot tool, so this is checkable without a screen at all,
exactly the introspection story `docs/kernel-contract.md#7` argues for.

**The kernel is closed.** All four questions the original design left open
— `Time`/`Log`'s home, whether the Event Bus is real infrastructure or
event-components, whether frame stages are fixed or plugin-extensible, and
the data-oriented-fast-path question — are resolved, each with working code
behind it, not just an answer written into the doc. `Time` and the Event
Bus (`Publish`/`Subscribe`, leak-safe the same way `Schedule` already is)
both shipped; `sandbox.echo` subscribes to `PluginLoaded` for real, so the
200-cycle leak test now proves `EventBus` doesn't leak too, not just
`Schedule`. See the resolutions in
[`docs/kernel-contract.md`](docs/kernel-contract.md) — one of the four
(the fast path) is deliberately still open, but with a concrete trigger
condition instead of a deadline, not left vague.

No physics yet — see the build order (M0–M4) in
[`docs/kernel-contract.md`](docs/kernel-contract.md) for what's next.

Design and implementation are argued over in the same place: the doc is
still the thing to disagree with before code changes to match.

## License

[MIT](LICENSE)
