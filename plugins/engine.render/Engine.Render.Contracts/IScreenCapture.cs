namespace Engine.Render.Contracts;

/// <summary>
/// Reads the current frame back from the GPU and saves it to a PNG file.
/// Exists so the render output can be checked without a real, visible
/// display or screenshot tool — an agent (or a human working headlessly)
/// can render a frame and look at the file instead. See M1 in
/// docs/kernel-contract.md §8.
/// </summary>
public interface IScreenCapture
{
    void CaptureToFile(string path);
}
