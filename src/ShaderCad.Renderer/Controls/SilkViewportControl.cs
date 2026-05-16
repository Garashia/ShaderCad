using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Threading;
using g3;
using Serilog;
using Silk.NET.OpenGL;

namespace ShaderCad.Renderer.Controls;

/// <summary>
/// Silk.NET OpenGL ビューポート。
/// カメラ操作は Godot/PlayCanvas と同じ「球面座標 Orbit Camera」を採用:
///   - 左ドラッグ   → 方位角(Azimuth) / 仰角(Elevation) 変更
///   - スクロール   → ズーム（距離）
///   - 中ドラッグ   → パン（注視点移動）
/// </summary>
public class SilkViewportControl : OpenGlControlBase
{
    // ── GL リソース ──────────────────────────────────
    private GL? _gl;
    private uint _vao, _vbo, _shaderProgram;
    private int _vertexCount;
    private bool _glInitialized;
    private bool _meshNeedsUpdate;
    private float[] _pendingVertexData = Array.Empty<float>();

    // ── Orbit Camera 状態（Godot Cursor 相当） ──────
    private float _azimuth   = 30f  * MathF.PI / 180f;   // 水平角 (rad)
    private float _elevation = 25f  * MathF.PI / 180f;   // 仰角 (rad)
    private float _distance  = 12f;                        // 注視点からの距離
    private Vector3 _target  = Vector3.Zero;               // 注視点（パン先）

    private const float AzimuthSensitivity   = 0.005f;
    private const float ElevationSensitivity = 0.005f;
    private const float PanSensitivity       = 0.01f;
    private const float ZoomSensitivity      = 1.1f;       // 倍率
    private const float MinDistance          = 0.5f;
    private const float MaxDistance          = 500f;
    private const float MaxElevation         =  89f * MathF.PI / 180f;
    private const float MinElevation         = -89f * MathF.PI / 180f;

    // ── マウス入力 ───────────────────────────────────
    private bool    _leftDragging;
    private bool    _middleDragging;
    private Point   _lastMousePos;

    // ── シェーダー（GLES2 / GL2.1 互換） ────────────
    private const string Vert = @"
attribute vec3 aPos;
attribute vec3 aNormal;
varying vec3 vNormal;
varying vec3 vFragPos;
uniform mat4 uModel;
uniform mat4 uMVP;
void main() {
    gl_Position = uMVP * vec4(aPos, 1.0);
    vNormal   = aNormal;
    vFragPos  = vec3(uModel * vec4(aPos, 1.0));
}";

    private const string Frag = @"
#ifdef GL_ES
precision mediump float;
#endif
varying vec3 vNormal;
varying vec3 vFragPos;
uniform vec3 uCameraPos;
void main() {
    vec3 N = normalize(vNormal);
    vec3 L = normalize(vec3(5.0, 8.0, 5.0) - vFragPos);
    vec3 V = normalize(uCameraPos - vFragPos);
    vec3 H = normalize(L + V);

    float ambient  = 0.20;
    float diffuse  = max(dot(N, L), 0.0) * 0.65;
    float specular = pow(max(dot(N, H), 0.0), 32.0) * 0.25;

    float light = ambient + diffuse + specular;
    gl_FragColor = vec4(0.72 * light, 0.76 * light, 0.92 * light, 1.0);
}";

    // ── 外部 API ─────────────────────────────────────
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
        Log.Debug("[Viewport] UploadMeshes {V} verts", data.Count / 6);
        RequestNextFrameRendering();
    }

    // ── 球面座標 → カメラ位置 ────────────────────────
    /// <summary>
    /// Godot / PlayCanvas と同じ球面座標変換。
    /// azimuth=0, elevation=0 → カメラは +Z 方向から見る。
    /// </summary>
    private Vector3 ComputeCameraPosition()
    {
        float cosE = MathF.Cos(_elevation);
        float sinE = MathF.Sin(_elevation);
        float cosA = MathF.Cos(_azimuth);
        float sinA = MathF.Sin(_azimuth);

        // PlayCanvas orbit-camera.js と同じ計算式
        return _target + new Vector3(
            _distance * cosE * sinA,
            _distance * sinE,
            _distance * cosE * cosA);
    }

    // ── マウスイベント ───────────────────────────────
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var props = e.GetCurrentPoint(this).Properties;
        _lastMousePos = e.GetPosition(this);

        if (props.IsLeftButtonPressed)  _leftDragging   = true;
        if (props.IsMiddleButtonPressed) _middleDragging = true;

        e.Pointer.Capture(this);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _leftDragging   = false;
        _middleDragging = false;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var pos   = e.GetPosition(this);
        var delta = pos - _lastMousePos;
        _lastMousePos = pos;

        if (_leftDragging)
        {
            // Godot: y_rot += delta.x, x_rot -= delta.y
            _azimuth   -= (float)delta.X * AzimuthSensitivity;
            _elevation += (float)delta.Y * ElevationSensitivity;
            _elevation  = Math.Clamp(_elevation, MinElevation, MaxElevation);
            RequestNextFrameRendering();
        }
        else if (_middleDragging)
        {
            // パン: カメラの右方向・上方向に沿って注視点を移動
            var camPos = ComputeCameraPosition();
            var forward = Vector3.Normalize(_target - camPos);
            var right   = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
            var up      = Vector3.Cross(right, forward);

            _target -= right * (float)delta.X * PanSensitivity * (_distance / 10f);
            _target += up    * (float)delta.Y * PanSensitivity * (_distance / 10f);
            RequestNextFrameRendering();
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        // PlayCanvas: distance = Clamp(distance * factor, min, max)
        float factor = e.Delta.Y > 0 ? 1f / ZoomSensitivity : ZoomSensitivity;
        _distance = Math.Clamp(_distance * factor, MinDistance, MaxDistance);
        RequestNextFrameRendering();
    }

    // ── GL 初期化 ────────────────────────────────────
    protected override unsafe void OnOpenGlInit(GlInterface gl)
    {
        base.OnOpenGlInit(gl);
        _gl = GL.GetApi(gl.GetProcAddress);

        _gl.ClearColor(0.12f, 0.12f, 0.14f, 1f);
        _gl.Enable(EnableCap.DepthTest);

        uint vs = CompileShader(ShaderType.VertexShader,   Vert);
        uint fs = CompileShader(ShaderType.FragmentShader, Frag);
        _shaderProgram = _gl.CreateProgram();
        _gl.AttachShader(_shaderProgram, vs);
        _gl.AttachShader(_shaderProgram, fs);
        _gl.LinkProgram(_shaderProgram);
        _gl.GetProgram(_shaderProgram, ProgramPropertyARB.LinkStatus, out int linked);
        if (linked == 0) Log.Error("[Viewport] Link: {E}", _gl.GetProgramInfoLog(_shaderProgram));
        _gl.DeleteShader(vs);
        _gl.DeleteShader(fs);

        _vao = _gl.GenVertexArray();
        _vbo = _gl.GenBuffer();
        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

        uint posLoc = (uint)_gl.GetAttribLocation(_shaderProgram, "aPos");
        uint nrmLoc = (uint)_gl.GetAttribLocation(_shaderProgram, "aNormal");
        uint stride = (uint)(6 * sizeof(float));
        _gl.VertexAttribPointer(posLoc, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
        _gl.EnableVertexAttribArray(posLoc);
        _gl.VertexAttribPointer(nrmLoc, 3, VertexAttribPointerType.Float, false, stride, (void*)(3 * sizeof(float)));
        _gl.EnableVertexAttribArray(nrmLoc);
        _gl.BindVertexArray(0);

        _glInitialized = true;
        Log.Information("[Viewport] GL Init OK");

        DispatcherTimer.Run(() => { RequestNextFrameRendering(); return true; },
            TimeSpan.FromMilliseconds(16));
    }

    // ── 描画 ─────────────────────────────────────────
    protected override unsafe void OnOpenGlRender(GlInterface gl, int fb)
    {
        if (_gl == null || !_glInitialized) return;

        var scaling = Avalonia.Controls.TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
        int w = (int)(Bounds.Width  * scaling);
        int h = (int)(Bounds.Height * scaling);
        if (w <= 0 || h <= 0) return;

        _gl.Viewport(0, 0, (uint)w, (uint)h);
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        if (_meshNeedsUpdate && _pendingVertexData.Length > 0)
        {
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
            fixed (float* ptr = _pendingVertexData)
                _gl.BufferData(BufferTargetARB.ArrayBuffer,
                    (nuint)(_pendingVertexData.Length * sizeof(float)), ptr,
                    BufferUsageARB.DynamicDraw);
            _vertexCount = _pendingVertexData.Length / 6;
            _meshNeedsUpdate = false;
        }

        if (_vertexCount <= 0) return;

        _gl.UseProgram(_shaderProgram);

        // カメラ位置を球面座標から計算
        var camPos = ComputeCameraPosition();
        var model  = Matrix4x4.Identity;
        var view   = Matrix4x4.CreateLookAt(camPos, _target, Vector3.UnitY);
        var proj   = Matrix4x4.CreatePerspectiveFieldOfView(
            50f * MathF.PI / 180f, (float)w / h, 0.01f, 1000f);

        var mvp = model * view * proj;

        int mvpLoc   = _gl.GetUniformLocation(_shaderProgram, "uMVP");
        int modelLoc = _gl.GetUniformLocation(_shaderProgram, "uModel");
        int camLoc   = _gl.GetUniformLocation(_shaderProgram, "uCameraPos");

        _gl.UniformMatrix4(mvpLoc,   1, false, MemoryMarshal.CreateReadOnlySpan(ref mvp.M11,   16));
        _gl.UniformMatrix4(modelLoc, 1, false, MemoryMarshal.CreateReadOnlySpan(ref model.M11, 16));
        _gl.Uniform3(camLoc, camPos.X, camPos.Y, camPos.Z);

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

    private uint CompileShader(ShaderType type, string src)
    {
        uint id = _gl!.CreateShader(type);
        _gl.ShaderSource(id, src);
        _gl.CompileShader(id);
        _gl.GetShader(id, ShaderParameterName.CompileStatus, out int ok);
        if (ok == 0)
        {
            var err = _gl.GetShaderInfoLog(id);
            Log.Error("[Viewport] {T}: {E}", type, err);
            throw new Exception($"{type}: {err}");
        }
        return id;
    }
}
