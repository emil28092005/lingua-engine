using Engine.Kernel.World;

namespace Engine.Render.Contracts;

/// <summary>
/// Marks a GameObject as a flat, unit-sized (1x1 world unit before
/// scaling) quad, drawn at its own WorldMatrix — the only renderable
/// component that exists yet. No size fields here: Transform.LocalScale
/// already means "how big," and a second, competing way to say the same
/// thing would just raise the question of which one wins. A real
/// mesh/material system (arbitrary geometry, per-instance textures) is
/// real content-pipeline work, out of scope here — this exists so M3's
/// gizmos have something real to select and move, not to be a renderer.
/// Every QuadRenderer shares engine.render's one hardcoded texture for
/// the same reason TexturePath itself is hardcoded — there's no
/// material/asset-reference system yet to point it at one per instance.
/// </summary>
public sealed class QuadRenderer : Component;
