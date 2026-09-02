using Engine.Render.Contracts;
using Engine.Windowing.Contracts;
using Silk.NET.OpenGL;

namespace Engine.Render;

internal sealed class GlScreenCapture(GL gl, IEngineWindow window) : IScreenCapture
{
    public void CaptureToFile(string path)
    {
        var size = window.Native.FramebufferSize;
        var width = size.X;
        var height = size.Y;
        var stride = width * 4;

        var bottomUp = new byte[stride * height];
        gl.ReadPixels(0, 0, (uint)width, (uint)height, PixelFormat.Rgba, PixelType.UnsignedByte, bottomUp.AsSpan());

        // OpenGL's row 0 is the bottom of the image; PngWriter wants row 0
        // to be the top.
        var topDown = new byte[bottomUp.Length];
        for (var y = 0; y < height; y++)
            Array.Copy(bottomUp, (height - 1 - y) * stride, topDown, y * stride, stride);

        PngWriter.Write(path, width, height, topDown);
    }
}
