using Engine.Assets.Contracts;
using Engine.Kernel.Diagnostics;
using Engine.Kernel.Plugins;
using Engine.Kernel.Scheduling;
using Engine.Kernel.World;
using Engine.Render.Contracts;
using Engine.Windowing.Contracts;
using Silk.NET.OpenGL;

namespace Engine.Render;

/// <summary>
/// M2's render pipeline: a textured quad, drawn every Render stage. See M2
/// in docs/kernel-contract.md §8.
///
/// This is the actual "done when": swap TexturePath's file on disk and the
/// quad's texture changes with no app restart — engine.assets watches the
/// file and publishes TextureReloaded; this plugin subscribes and
/// re-uploads to the same GL texture handle. Verified by hand against a
/// real window and a live GL context, via IScreenCapture — no external
/// image library; see the note on PngWriter for why.
///
/// engine.windowing alone produces a window that never becomes visible on
/// Wayland — unlike X11, a Wayland surface with no committed buffer simply
/// isn't shown by the compositor, so an "empty" window isn't even a black
/// rectangle, it's nothing at all. This plugin's first Clear+SwapBuffers is
/// what actually makes the window appear.
///
/// Also found the hard way: with VSync on (the default), a session where
/// the compositor stops handing out frame callbacks — locking the screen
/// reproduced it directly — makes the *second* frame's SwapBuffers block
/// forever (the first has nothing to wait on yet, so it returns fine,
/// which is what makes this easy to miss). See VSync=false in
/// WindowingPlugin and the SwapInterval(0) call below.
/// </summary>
public sealed class RenderPlugin : IPlugin
{
    // There's no material/asset-reference component yet (that's real
    // content-authoring work, M3+ territory) — hardcoded the same way
    // TriangleColor was hardcoded before textures existed at all.
    private const string TexturePath = "assets/texture.png";

    private const string VertexShaderSource = """
        #version 330 core
        layout (location = 0) in vec2 aPosition;
        layout (location = 1) in vec2 aUv;
        out vec2 vUv;

        void main()
        {
            gl_Position = vec4(aPosition, 0.0, 1.0);
            vUv = aUv;
        }
        """;

    private const string FragmentShaderSource = """
        #version 330 core
        in vec2 vUv;
        out vec4 FragColor;
        uniform sampler2D uTexture;

        void main()
        {
            FragColor = texture(uTexture, vUv);
        }
        """;

    private static readonly float[] Vertices =
    [
        // position       uv
        -0.6f,  0.6f,     0f, 1f,
        -0.6f, -0.6f,     0f, 0f,
         0.6f, -0.6f,     1f, 0f,

        -0.6f,  0.6f,     0f, 1f,
         0.6f, -0.6f,     1f, 0f,
         0.6f,  0.6f,     1f, 1f,
    ];

    private GL? _gl;
    private IEngineWindow? _window;
    private uint _vao;
    private uint _vbo;
    private uint _program;
    private uint _texture;
    private Action<TextureReloaded>? _onTextureReloaded;
    private ILogger? _log;

    public unsafe void Configure(IPluginContext ctx)
    {
        _log = ctx.Log;
        _window = ctx.Services.Require<IEngineWindow>();
        _window.Native.GLContext!.MakeCurrent();
        _gl = _window.Native.CreateOpenGL();

        // Belt-and-suspenders alongside VSync=false in WindowingPlugin's
        // WindowOptions — SwapInterval(0) is the lower-level, harder-to-
        // ignore way to say the same thing directly to the GL context. See
        // the note there on why a blocked SwapBuffers is a real, already-hit
        // failure mode here, not a hypothetical one.
        _window.Native.GLContext.SwapInterval(0);

        _program = LinkProgram(_gl, VertexShaderSource, FragmentShaderSource);

        _vao = _gl.GenVertexArray();
        _gl.BindVertexArray(_vao);

        _vbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        _gl.BufferData<float>(BufferTargetARB.ArrayBuffer, Vertices, BufferUsageARB.StaticDraw);

        const uint stride = 4 * sizeof(float);
        _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, (void*)0);
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, (void*)(2 * sizeof(float)));
        _gl.EnableVertexAttribArray(1);

        _gl.BindVertexArray(0);

        var assets = ctx.Services.Require<IAssetService>();
        var initial = assets.LoadTexture(TexturePath);
        _texture = _gl.GenTexture();
        UploadPixels(initial);

        _onTextureReloaded = evt =>
        {
            if (Path.GetFullPath(evt.Path) == Path.GetFullPath(TexturePath))
                UploadPixels(evt.Data);
        };
        ctx.Events.Subscribe(_onTextureReloaded);

        ctx.Services.Provide<IScreenCapture>(new GlScreenCapture(_gl, _window));
        ctx.Schedule.Add(Stage.Render, Draw);
        ctx.Log.Info("GL context created, textured quad ready");
    }

    public void Shutdown(IPluginContext ctx)
    {
        ctx.Schedule.RemoveAllFrom("engine.render");
        ctx.Events.RemoveAllFrom("engine.render");
        ctx.Services.Revoke<IScreenCapture>();

        if (_gl is not null)
        {
            _gl.DeleteTexture(_texture);
            _gl.DeleteVertexArray(_vao);
            _gl.DeleteBuffer(_vbo);
            _gl.DeleteProgram(_program);
            _gl.Dispose();
        }

        _gl = null;
        _window = null;
        _onTextureReloaded = null;
        _log = null;
    }

    private unsafe void UploadPixels(TextureData data)
    {
        _gl!.BindTexture(TextureTarget.Texture2D, _texture);

        fixed (byte* pixels = data.Rgba)
        {
            _gl.TexImage2D(
                TextureTarget.Texture2D, level: 0, internalformat: InternalFormat.Rgba,
                (uint)data.Width, (uint)data.Height, border: 0,
                format: PixelFormat.Rgba, type: PixelType.UnsignedByte, pixels);
        }

        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);

        // Only checked here, not every frame in Draw() — a hot-path
        // GetError() call would tax the one thing that runs constantly
        // for a check that only ever fires from a handful of infrequent
        // upload calls. Worth it here specifically: an upload that fails
        // silently doesn't throw, it just leaves stale or undefined data
        // bound to the texture — exactly what a blank-screen bug looks
        // like from the outside, with nothing in the way of a stack trace
        // to point at it.
        var error = _gl.GetError();
        if (error != GLEnum.NoError)
            _log?.Warn($"GL error after texture upload: {error}");
    }

    private void Draw(IWorld world)
    {
        _gl!.ClearColor(0.05f, 0.05f, 0.08f, 1f);
        _gl.Clear(ClearBufferMask.ColorBufferBit);

        _gl.UseProgram(_program);
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, _texture);
        _gl.BindVertexArray(_vao);
        _gl.DrawArrays(PrimitiveType.Triangles, 0, 6);

        _window!.Native.GLContext!.SwapBuffers();
    }

    private static uint LinkProgram(GL gl, string vertexSource, string fragmentSource)
    {
        var vertex = CompileShader(gl, ShaderType.VertexShader, vertexSource);
        var fragment = CompileShader(gl, ShaderType.FragmentShader, fragmentSource);

        var program = gl.CreateProgram();
        gl.AttachShader(program, vertex);
        gl.AttachShader(program, fragment);
        gl.LinkProgram(program);

        gl.GetProgram(program, GLEnum.LinkStatus, out var linked);
        if (linked == 0)
            throw new InvalidOperationException($"Shader program failed to link: {gl.GetProgramInfoLog(program)}");

        gl.DetachShader(program, vertex);
        gl.DetachShader(program, fragment);
        gl.DeleteShader(vertex);
        gl.DeleteShader(fragment);

        return program;
    }

    private static uint CompileShader(GL gl, ShaderType type, string source)
    {
        var shader = gl.CreateShader(type);
        gl.ShaderSource(shader, source);
        gl.CompileShader(shader);

        gl.GetShader(shader, GLEnum.CompileStatus, out var compiled);
        if (compiled == 0)
            throw new InvalidOperationException($"{type} failed to compile: {gl.GetShaderInfoLog(shader)}");

        return shader;
    }
}
