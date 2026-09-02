using Engine.Kernel.World;

namespace Engine.Render.Contracts;

/// <summary>
/// A unit cube (1x1x1 before Transform.LocalScale), same no-size-fields
/// reasoning as QuadRenderer — Transform.LocalScale already means "how
/// big." Added for M4's physics demo: a BoxCollider falling and settling
/// only reads as a real physics object on screen if it actually looks like
/// a box, not a flat card.
/// </summary>
public sealed class CubeRenderer : Component;
