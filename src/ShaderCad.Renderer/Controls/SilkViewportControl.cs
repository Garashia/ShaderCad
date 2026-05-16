using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Threading;
using g3;
using Serilog;
using Silk.NET.OpenGL;

namespace ShaderCad.Renderer.Controls;

/// <summary>
/// Avalonia の OpenGL コンテキスト内で Silk.NET を使って描画するビューポートコントロール。
/// </summary>
public class SilkViewportControl : OpenGlControlBase
{
    private GL? _gl;
    private uint _vao, _vbo, _shaderProgram;
    private int _vertexCount;
    private bool _glInitialized;
    private bool _meshNeedsUpdate;
    private float[] _pendingVertexData = Array.Empty<float>();

    // GLES2 / OpenGL 2.1 互換シェーダー (#version 120 で attribute/varying を使用)
    private const string Vert = @"
attribute vec3 aPos;
attribute vec3 aNormal;
varying vec3 vNormal;
uniform mat4 uMVP;
void main() {
    gl_Position = uMVP * vec4(aPos, 1.0);
    vNormal = aNormal;
}";

    private const string Frag = @"
#ifdef GL_ES
precision mediump float;
#endif
varying vec3 vNormal;
void main() {
    vec3 L = normalize(vec3(1.0, 2.0, 3.0));
    float d = max(dot(normalize(vNormal), L), 0.0);
    float light = 0.3 + 0.7 * d;
    gl_FragColor = vec4(0.8 * light, 0.85 * light, 1.0 * light, 1.0);
}";

    // ─── 外部から呼ぶ API ───────────────────────────────
    public void UploadMeshes(IReadOnlyList<DMesh3> meshes)
    {
        var data = new List<float>();
        foreach (var mesh in meshes)
        {
            foreach (var tid in mesh.TriangleIndices())
            {
                var tri = mesh.GetTriangle(tid);
                for (int j = 0; j < 3; j++)
                {
                    var v = mesh.GetVertex(tri[j]);
                    var n = mesh.HasVertexNormals ? mesh.GetVertexNormal(tri[j]) : Vector3f.AxisZ;
                    data.Add((float)v.x); data.Add((float)v.y); data.Add((float)v.z);
                    data.Add(n.x);        data.Add(n.y);        data.Add(n.z);
                }
            }
        }
        _pendingVertexData = data.ToArray();
        _meshNeedsUpdate = true;
        Log.Debug("[Viewport] UploadMeshes {V} verts, glInit={G}", data.Count / 6, _glInitialized);
        RequestNextFrameRendering();
    }

    // ─── OpenGL 初期化 ──────────────────────────────────
    protected override unsafe void OnOpenGlInit(GlInterface gl)
    {
        base.OnOpenGlInit(gl);
        _gl = GL.GetApi(gl.GetProcAddress);

        _gl.ClearColor(0.13f, 0.13f, 0.15f, 1f);
        _gl.Enable(EnableCap.DepthTest);

        // シェーダーのコンパイル
        uint vs = CompileShader(ShaderType.VertexShader, Vert);
        uint fs = CompileShader(ShaderType.FragmentShader, Frag);
        _shaderProgram = _gl.CreateProgram();
        _gl.AttachShader(_shaderProgram, vs);
        _gl.AttachShader(_shaderProgram, fs);
        _gl.LinkProgram(_shaderProgram);
        _gl.GetProgram(_shaderProgram, ProgramPropertyARB.LinkStatus, out int linked);
        if (linked == 0)
            Log.Error("[Viewport] Link error: {E}", _gl.GetProgramInfoLog(_shaderProgram));
        else
            Log.Information("[Viewport] Shader OK");
        _gl.DeleteShader(vs);
        _gl.DeleteShader(fs);

        // VAO / VBO
        _vao = _gl.GenVertexArray();
        _vbo = _gl.GenBuffer();

        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

        // attribute の location をシェーダーから取得
        uint posLoc = (uint)_gl.GetAttribLocation(_shaderProgram, "aPos");
        uint nrmLoc = (uint)_gl.GetAttribLocation(_shaderProgram, "aNormal");
        uint stride = (uint)(6 * sizeof(float));
        _gl.VertexAttribPointer(posLoc, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
        _gl.EnableVertexAttribArray(posLoc);
        _gl.VertexAttribPointer(nrmLoc, 3, VertexAttribPointerType.Float, false, stride, (void*)(3 * sizeof(float)));
        _gl.EnableVertexAttribArray(nrmLoc);
        _gl.BindVertexArray(0);

        _glInitialized = true;
        Log.Information("[Viewport] Init done. posLoc={P} nrmLoc={N}", posLoc, nrmLoc);

        // 描画ループ
        DispatcherTimer.Run(() => { RequestNextFrameRendering(); return true; },
            TimeSpan.FromMilliseconds(16));
    }

    // ─── 描画 ───────────────────────────────────────────
    protected override unsafe void OnOpenGlRender(GlInterface gl, int fb)
    {
        if (_gl == null || !_glInitialized) return;

        // ★ 重要: DPIスケーリングを考慮してViewportを設定
        var scaling = Avalonia.Controls.TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
        int w = (int)(Bounds.Width  * scaling);
        int h = (int)(Bounds.Height * scaling);
        if (w <= 0 || h <= 0) return;

        _gl.Viewport(0, 0, (uint)w, (uint)h);
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        // VBO 更新（GL スレッド上で安全に実行）
        if (_meshNeedsUpdate && _pendingVertexData.Length > 0)
        {
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
            fixed (float* ptr = _pendingVertexData)
                _gl.BufferData(BufferTargetARB.ArrayBuffer,
                    (nuint)(_pendingVertexData.Length * sizeof(float)), ptr,
                    BufferUsageARB.DynamicDraw);
            _vertexCount = _pendingVertexData.Length / 6;
            _meshNeedsUpdate = false;
            Log.Debug("[Viewport] VBO uploaded {V} verts", _vertexCount);
        }

        if (_vertexCount <= 0) return;

        _gl.UseProgram(_shaderProgram);

        // ★ 正しいMVP計算 (Avalonia公式サンプルと同じ方式)
        // System.Numerics を OpenGL に渡す場合:
        //   transpose=FALSE + MemoryMarshal で生のバイト列をそのまま渡す
        // 乗算順: model * view * proj (System.Numerics の慣例)
        float aspect = (float)w / h;
        var model = Matrix4x4.Identity;
        var view  = Matrix4x4.CreateLookAt(
            new Vector3(0f, 3f, 12f),   // カメラ位置
            Vector3.Zero,               // 注視点
            Vector3.UnitY);
        var proj  = Matrix4x4.CreatePerspectiveFieldOfView(
            50f * MathF.PI / 180f, aspect, 0.1f, 500f);

        // System.Numerics は行ベクトル用なので: V' = V * M
        // OpenGL (GLSL) は列ベクトル用: V' = M * V
        // → transpose=FALSE で渡すと OpenGL が自動的に転置を行う
        var mvp = model * view * proj;
        int mvpLoc = _gl.GetUniformLocation(_shaderProgram, "uMVP");
        _gl.UniformMatrix4(mvpLoc, 1, false,
            MemoryMarshal.CreateReadOnlySpan(ref mvp.M11, 16));

        _gl.BindVertexArray(_vao);
        _gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)_vertexCount);
        _gl.BindVertexArray(0);
    }

    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        _gl?.DeleteBuffer(_vbo);
        _gl?.DeleteVertexArray(_vao);
        _gl?.DeleteProgram(_shaderProgram);
        _gl?.Dispose();
        base.OnOpenGlDeinit(gl);
    }

    // ─── ユーティリティ ─────────────────────────────────
    private uint CompileShader(ShaderType type, string src)
    {
        uint id = _gl!.CreateShader(type);
        _gl.ShaderSource(id, src);
        _gl.CompileShader(id);
        _gl.GetShader(id, ShaderParameterName.CompileStatus, out int ok);
        if (ok == 0)
        {
            string err = _gl.GetShaderInfoLog(id);
            Log.Error("[Viewport] {T} compile error: {E}", type, err);
            throw new Exception($"{type}: {err}");
        }
        Log.Debug("[Viewport] {T} compiled OK", type);
        return id;
    }
}
