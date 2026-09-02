using System.Numerics;
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
/// M3's render pipeline: a real perspective camera, drawing every
/// GameObject with a QuadRenderer at its own WorldMatrix — upgraded from
/// M2's single hardcoded NDC-space quad specifically because gizmos need
/// real 3D geometry and a real camera to mean anything (a handle dragged
/// in screen space has to map onto an actual 3D axis). See M3 in
/// docs/kernel-contract.md §8.
///
/// GameObject.WorldMatrix and this plugin's own matrices are both
/// System.Numerics.Matrix4x4, which is row-vector (v' = v * M, and
/// composition reads left to right — see WorldMatrix's own doc comment).
/// The shaders below use the same convention deliberately
/// (`vec4(aPosition, 1.0) * uModel * uView * uProjection`), not GLSL's
/// more common column-vector order. That's also why SetMatrix uploads
/// with `transpose: true` — System.Numerics stores a matrix's first row
/// as its first four floats, but glUniformMatrix4fv with transpose=false
/// reads that same layout as the first *column* instead; asking GL to
/// transpose is what makes the bytes mean what C# already computed them
/// to mean. Confirmed empirically, not just reasoned through: with
/// transpose=false the quad rendered as a blank screen — no error, no
/// crash, just wrong — and flipping the one flag was the entire fix.
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
    // content-authoring work, M4+ territory) — hardcoded the same way
    // TriangleColor was hardcoded before textures existed at all.
    private const string TexturePath = "assets/texture.png";

    private const string VertexShaderSource = """
        #version 330 core
        layout (location = 0) in vec3 aPosition;
        layout (location = 1) in vec2 aUv;
        out vec2 vUv;

        uniform mat4 uModel;
        uniform mat4 uView;
        uniform mat4 uProjection;

        void main()
        {
            gl_Position = vec4(aPosition, 1.0) * uModel * uView * uProjection;
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

    // Unit quad (1x1 world units before Transform.LocalScale), centered
    // at its own origin, in the XY plane facing +Z.
    private static readonly float[] Vertices =
    [
        // position            uv
        -0.5f,  0.5f, 0f,      0f, 1f,
        -0.5f, -0.5f, 0f,      0f, 0f,
         0.5f, -0.5f, 0f,      1f, 0f,

        -0.5f,  0.5f, 0f,      0f, 1f,
         0.5f, -0.5f, 0f,      1f, 0f,
         0.5f,  0.5f, 0f,      1f, 1f,
    ];

    private GL? _gl;
    private IEngineWindow? _window;
    private CameraService? _camera;
    private uint _vao;
    private uint _vbo;
    private uint _program;
    private uint _texture;
    private int _modelLocation;
    private int _viewLocation;
    private int _projectionLocation;
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

        _camera = new CameraService(_window);
        ctx.Services.Provide<ICameraService>(_camera);

        _program = LinkProgram(_gl, VertexShaderSource, FragmentShaderSource);
        _modelLocation = _gl.GetUniformLocation(_program, "uModel");
        _viewLocation = _gl.GetUniformLocation(_program, "uView");
        _projectionLocation = _gl.GetUniformLocation(_program, "uProjection");

        _vao = _gl.GenVertexArray();
        _gl.BindVertexArray(_vao);

        _vbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        _gl.BufferData<float>(BufferTargetARB.ArrayBuffer, Vertices, BufferUsageARB.StaticDraw);

        const uint stride = 5 * sizeof(float);
        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, (void*)(3 * sizeof(float)));
        _gl.EnableVertexAttribArray(1);

        _gl.BindVertexArray(0);
        _gl.Enable(EnableCap.DepthTest);

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
        ctx.Schedule.Add(Stage.Render, Draw).Reads<QuadRenderer>();
        ctx.Schedule.Add(Stage.Present, Present);
        ctx.Log.Info("GL context created, 3D quad pipeline ready");
    }

    public void Shutdown(IPluginContext ctx)
    {
        ctx.Schedule.RemoveAllFrom("engine.render");
        ctx.Events.RemoveAllFrom("engine.render");
        ctx.Services.Revoke<IScreenCapture>();
        ctx.Services.Revoke<ICameraService>();

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
        _camera = null;
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

    private unsafe void Draw(IWorld world)
    {
        _gl!.ClearColor(0.05f, 0.05f, 0.08f, 1f);
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        _gl.UseProgram(_program);
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, _texture);
        _gl.BindVertexArray(_vao);

        var view = _camera!.View;
        var projection = _camera.Projection;
        SetMatrix(_viewLocation, view);
        SetMatrix(_projectionLocation, projection);

        foreach (var go in world.Query<QuadRenderer>())
        {
            SetMatrix(_modelLocation, go.WorldMatrix);
            _gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
        }
    }

    // Split from Draw() into its own Stage.Present system so engine.editor
    // can draw ImGui on top of the scene from a Stage.Render system of its
    // own (registered after this plugin's, per project.json order) without
    // racing the swap — see Stage.Present's doc comment.
    private void Present(IWorld world) => _window!.Native.GLContext!.SwapBuffers();

    private unsafe void SetMatrix(int location, Matrix4x4 matrix) =>
        _gl!.UniformMatrix4(location, 1, true, (float*)&matrix);

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
