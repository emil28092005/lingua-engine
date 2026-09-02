namespace Engine.Assets.Contracts;

/// <summary>
/// Raw decoded pixels, nothing graphics-API-specific — uploading this to
/// an actual GPU texture is engine.render's job, not this plugin's, so a
/// future non-OpenGL render plugin could reuse engine.assets unchanged.
/// </summary>
public sealed record TextureData(int Width, int Height, byte[] Rgba);
