using Engine.Kernel.Plugins;
using Engine.Kernel.Scheduling;
using Engine.Kernel.World;
using Engine.Render.Contracts;
using Engine.Windowing.Contracts;
using Silk.NET.OpenGL;

namespace Engine.Render;

/// <summary>
/// M1's render pipeline: one hardcoded triangle, drawn every Render stage.
/// See M1 in docs/kernel-contract.md §8.
///
/// This is the whole point of M1's "done when": change TriangleColor,
/// rebuild just this plugin, and reload it while the window from
/// engine.windowing stays open — the color changes with no app restart.
/// Verified by hand against a real window and a live GL context, and via
/// IScreenCapture — no external image library; see the note on PngWriter
/// for why.
///
/// engine.windowing alone produces a window that never becomes visible on
/// Wayland — unlike X11, a Wayland surface with no committed buffer simply
/// isn't shown by the compositor, so an "empty" window isn't even a black
/// rectangle, it's nothing at all. This plugin's first Clear+SwapBuffers is
/// what actually makes the window appear.
/// </summary>
public sealed class RenderPlugin : IPlugin
{
    private const string VertexShaderSource = """
        #version 330 core
        layout (location = 0) in vec2 aPosition;

        void main()
        {
            gl_Position = vec4(aPosition, 0.0, 1.0);
        }
        """;

    private const string FragmentShaderSource = """
        #version 330 core
        out vec4 FragColor;
        uniform vec4 uColor;

        void main()
        {
            FragColor = uColor;
        }
        """;

    private static readonly float[] TriangleColor = [0.2f, 0.8f, 0.4f, 1f];

    private static readonly float[] Vertices =
    [
         0.0f,  0.6f,
        -0.6f, -0.6f,
         0.6f, -0.6f,
    ];

    private GL? _gl;
    private IEngineWindow? _window;
    private uint _vao;
    private uint _vbo;
    private uint _program;
    private int _colorLocation;

    public unsafe void Configure(IPluginContext ctx)
    {
        _window = ctx.Services.Require<IEngineWindow>();
        _window.Native.GLContext!.MakeCurrent();
        _gl = _window.Native.CreateOpenGL();

        _program = LinkProgram(_gl, VertexShaderSource, FragmentShaderSource);
        _colorLocation = _gl.GetUniformLocation(_program, "uColor");

        _vao = _gl.GenVertexArray();
        _gl.BindVertexArray(_vao);

        _vbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        _gl.BufferData<float>(BufferTargetARB.ArrayBuffer, Vertices, BufferUsageARB.StaticDraw);

        _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), (void*)0);
        _gl.EnableVertexAttribArray(0);
        _gl.BindVertexArray(0);

        ctx.Services.Provide<IScreenCapture>(new GlScreenCapture(_gl, _window));
        ctx.Schedule.Add(Stage.Render, Draw);
        ctx.Log.Info("GL context created, triangle ready");
    }

    public void Shutdown(IPluginContext ctx)
    {
        ctx.Schedule.RemoveAllFrom("engine.render");
        ctx.Services.Revoke<IScreenCapture>();

        if (_gl is not null)
        {
            _gl.DeleteVertexArray(_vao);
            _gl.DeleteBuffer(_vbo);
            _gl.DeleteProgram(_program);
            _gl.Dispose();
        }

        _gl = null;
        _window = null;
    }

    private void Draw(IWorld world)
    {
        _gl!.ClearColor(0.05f, 0.05f, 0.08f, 1f);
        _gl.Clear(ClearBufferMask.ColorBufferBit);

        _gl.UseProgram(_program);
        _gl.Uniform4(_colorLocation, TriangleColor[0], TriangleColor[1], TriangleColor[2], TriangleColor[3]);
        _gl.BindVertexArray(_vao);
        _gl.DrawArrays(PrimitiveType.Triangles, 0, 3);

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
