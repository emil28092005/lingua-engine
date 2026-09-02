namespace Engine.Assets.Contracts;

/// <summary>
/// M2's "done when": swapping a texture on disk changes the picture with
/// nothing stopped. engine.assets watches the file and publishes this
/// through IEventBus when it changes; engine.render subscribes and
/// re-uploads to the GPU. Nothing else connects the two plugins directly.
/// </summary>
public readonly record struct TextureReloaded(string Path, TextureData Data);
