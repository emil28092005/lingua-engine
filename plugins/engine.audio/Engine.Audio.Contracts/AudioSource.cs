using Engine.Kernel.World;

namespace Engine.Audio.Contracts;

/// <summary>
/// A sound clip attached to a GameObject. ClipPath is loaded lazily, the
/// first time engine.audio sees this component — not eagerly on
/// AddComponent, since Component construction has no plugin context to
/// load anything through (same reason texture loading in engine.render
/// happens in a system, not a component constructor).
/// </summary>
public sealed class AudioSource : Component
{
    public string ClipPath = "";
    public float Volume = 1f;
    public bool Loop = false;
    public bool PlayOnAwake = false;
}
