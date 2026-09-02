namespace Engine.Assets.Contracts;

public interface IAssetService
{
    /// <summary>
    /// Decodes the PNG at <paramref name="path"/> and starts watching it
    /// for changes. Returns the initial data; later changes arrive via
    /// <see cref="TextureReloaded"/>, not a return value — there's nothing
    /// to return to once the caller has moved on.
    /// </summary>
    TextureData LoadTexture(string path);
}
