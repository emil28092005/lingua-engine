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

## Status

Pre-implementation. The current design is written up in
[`docs/kernel-contract.md`](docs/kernel-contract.md): what belongs in the
kernel, the plugin contract, the hot-reload model, and the build order
(M0–M4). Nothing has shipped yet — this document is the thing to argue with
before code gets written.

## License

[MIT](LICENSE)
