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
/// - 左ドラッグ   → Orbit 回転 (Godot/PlayCanvas 球面座標)
/// - スクロール   → ズーム
/// - 中ドラッグ   → パン
/// </summary>
public class SilkViewportControl : OpenGlControlBase
{
    // ── GL リソース（メッシュ） ──────────────────────
    private GL? _gl;
    private uint _meshVao, _meshVbo, _meshProgram;
    private int  _vertexCount;
    private bool _glInitialized;
    private bool _meshNeedsUpdate;
    private float[] _pendingVertexData = Array.Empty<float>();

    // ── GL リソース（グリッド / 軸） ─────────────────
    private uint _lineVao, _lineVbo, _lineProgram;
    private int  _lineVertexCount;

    // ── Orbit Camera ────────────────────────────────
    private float _azimuth   = 30f * MathF.PI / 180f;
    private float _elevation = 25f * MathF.PI / 180f;
    private float _distance  = 12f;
    private Vector3 _target  = Vector3.Zero;

    private const float AzimuthSens   = 0.005f;
    private const float ElevationSens = 0.005f;
    private const float PanSens       = 0.01f;
    private const float ZoomFactor    = 1.1f;
    private const float MinDist       = 0.5f;
    private const float MaxDist       = 500f;

    // ── マウス入力 ───────────────────────────────────
    private bool  _leftDrag, _midDrag;
    private Point _lastMouse;

    // ═══════════════════════════════════════════════
    // シェーダー（メッシュ用）
    // ═══════════════════════════════════════════════
    private const string MeshVert = @"
attribute vec3 aPos;
attribute vec3 aNormal;
varying vec3 vNormal;
varying vec3 vFragPos;
uniform mat4 uMVP;
uniform mat4 uModel;
void main() {
    gl_Position = uMVP * vec4(aPos, 1.0);
    vNormal  = aNormal;
    vFragPos = vec3(uModel * vec4(aPos, 1.0));
}";
    private const string MeshFrag = @"
#ifdef GL_ES
precision mediump float;
#endif
varying vec3 vNormal;
varying vec3 vFragPos;
uniform vec3 uCamPos;
void main() {
    vec3 N = normalize(vNormal);
    vec3 L = normalize(vec3(5.0, 8.0, 5.0) - vFragPos);
    vec3 V = normalize(uCamPos - vFragPos);
    vec3 H = normalize(L + V);
    float light = 0.22 + max(dot(N,L),0.0)*0.62 + pow(max(dot(N,H),0.0),32.0)*0.25;
    gl_FragColor = vec4(0.72*light, 0.76*light, 0.92*light, 1.0);
}";

    // ═══════════════════════════════════════════════
    // シェーダー（ライン用 = グリッド・軸）
    // ═══════════════════════════════════════════════
    private const string LineVert = @"
attribute vec3 aPos;
attribute vec3 aColor;
varying vec3 vColor;
uniform mat4 uMVP;
void main() {
    gl_Position = uMVP * vec4(aPos, 1.0);
    vColor = aColor;
}";
    private const string LineFrag = @"
#ifdef GL_ES
precision mediump float;
#endif
varying vec3 vColor;
void main() {
    gl_FragColor = vec4(vColor, 1.0);
}";

    // ═══════════════════════════════════════════════
    // 外部からカメラを操作する API（オーバーレイから呼ぶ）
    // ═══════════════════════════════════════════════
    public void Orbit(double dx, double dy)
    {
        _azimuth   -= (float)dx * AzimuthSens;
        _elevation += (float)dy * ElevationSens;
        _elevation  = Math.Clamp(_elevation, -89f * MathF.PI / 180f, 89f * MathF.PI / 180f);
        RequestNextFrameRendering();
    }

    public void Pan(double dx, double dy)
    {
        var cam = CamPos();
        var fwd = Vector3.Normalize(_target - cam);
        var rt  = Vector3.Normalize(Vector3.Cross(fwd, Vector3.UnitY));
        var up  = Vector3.Cross(rt, fwd);
        float scale = PanSens * (_distance / 10f);
        _target -= rt * (float)dx * scale;
        _target += up * (float)dy * scale;
        RequestNextFrameRendering();
    }

    public void Zoom(double delta)
    {
        _distance = Math.Clamp(
            _distance * (delta > 0 ? 1f / ZoomFactor : ZoomFactor),
            MinDist, MaxDist);
        RequestNextFrameRendering();
    }

    // ═══════════════════════════════════════════════
    // 外部 API
    // ═══════════════════════════════════════════════
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

    // ═══════════════════════════════════════════════
    // 球面座標 → カメラ位置
    // ═══════════════════════════════════════════════
    private Vector3 CamPos()
    {
        float cE = MathF.Cos(_elevation), sE = MathF.Sin(_elevation);
        float cA = MathF.Cos(_azimuth),   sA = MathF.Sin(_azimuth);
        return _target + new Vector3(
            _distance * cE * sA,
            _distance * sE,
            _distance * cE * cA);
    }

    // ═══════════════════════════════════════════════
    // グリッド頂点データ生成
    // ═══════════════════════════════════════════════
    private static float[] BuildGridLines()
    {
        const int   half  = 10;     // グリッド半径（マス数）
        const float step  = 1.0f;
        // 各ラインは 2頂点 × (pos3 + col3) = 12 float
        var data = new List<float>();

        void Line(float x0, float y0, float z0, float x1, float y1, float z1,
                  float r,  float g,  float b)
        {
            data.Add(x0); data.Add(y0); data.Add(z0); data.Add(r); data.Add(g); data.Add(b);
            data.Add(x1); data.Add(y1); data.Add(z1); data.Add(r); data.Add(g); data.Add(b);
        }

        // XZ グリッド（暗いグレー）
        float gc = 0.30f;
        for (int i = -half; i <= half; i++)
        {
            float fi = i * step;
            // X 方向
            Line(-half * step, 0, fi,  half * step, 0, fi,  gc, gc, gc);
            // Z 方向
            Line(fi, 0, -half * step,  fi, 0,  half * step,  gc, gc, gc);
        }

        // X 軸（赤）  Y 軸（緑）  Z 軸（青）
        float al = half * step;
        Line(0, 0, 0,  al, 0,  0,   0.9f, 0.20f, 0.20f); // +X red
        Line(0, 0, 0,  0,  al, 0,   0.20f, 0.9f, 0.20f); // +Y green
        Line(0, 0, 0,  0,  0,  al,  0.20f, 0.40f, 0.9f); // +Z blue

        return data.ToArray();
    }

    // ═══════════════════════════════════════════════
    // イベント登録
    // ═══════════════════════════════════════════════
    public SilkViewportControl() => Focusable = true;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        AddHandler(PointerPressedEvent,     OnMousePressed,  handledEventsToo: true);
        AddHandler(PointerReleasedEvent,    OnMouseReleased, handledEventsToo: true);
        AddHandler(PointerMovedEvent,       OnMouseMoved,    handledEventsToo: true);
        AddHandler(PointerWheelChangedEvent,OnMouseWheel,    handledEventsToo: true);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        RemoveHandler(PointerPressedEvent,     OnMousePressed);
        RemoveHandler(PointerReleasedEvent,    OnMouseReleased);
        RemoveHandler(PointerMovedEvent,       OnMouseMoved);
        RemoveHandler(PointerWheelChangedEvent,OnMouseWheel);
        base.OnDetachedFromVisualTree(e);
    }

    private void OnMousePressed(object? s, PointerPressedEventArgs e)
    {
        var p = e.GetCurrentPoint(this).Properties;
        Log.Debug("[Viewport] Press L={L} M={M}", p.IsLeftButtonPressed, p.IsMiddleButtonPressed);
        _lastMouse = e.GetPosition(this);
        if (p.IsLeftButtonPressed)   _leftDrag = true;
        if (p.IsMiddleButtonPressed) _midDrag  = true;
        e.Pointer.Capture(this); Focus();
    }

    private void OnMouseReleased(object? s, PointerReleasedEventArgs e)
    {
        Log.Debug("[Viewport] Release");
        _leftDrag = _midDrag = false;
        e.Pointer.Capture(null);
    }

    private void OnMouseMoved(object? s, PointerEventArgs e)
    {
        var pos = e.GetPosition(this);
        var d   = pos - _lastMouse;
        _lastMouse = pos;

        if (_leftDrag)
        {
            _azimuth   -= (float)d.X * AzimuthSens;
            _elevation += (float)d.Y * ElevationSens;
            _elevation  = Math.Clamp(_elevation, -89f * MathF.PI / 180f, 89f * MathF.PI / 180f);
            RequestNextFrameRendering();
        }
        else if (_midDrag)
        {
            var cam = CamPos();
            var fwd = Vector3.Normalize(_target - cam);
            var rt  = Vector3.Normalize(Vector3.Cross(fwd, Vector3.UnitY));
            var up  = Vector3.Cross(rt, fwd);
            _target -= rt * (float)d.X * PanSens * (_distance / 10f);
            _target += up * (float)d.Y * PanSens * (_distance / 10f);
            RequestNextFrameRendering();
        }
    }

    private void OnMouseWheel(object? s, PointerWheelEventArgs e)
    {
        Log.Debug("[Viewport] Wheel dy={D} dist={Dist:F2}", e.Delta.Y, _distance);
        _distance = Math.Clamp(
            _distance * (e.Delta.Y > 0 ? 1f / ZoomFactor : ZoomFactor),
            MinDist, MaxDist);
        RequestNextFrameRendering();
    }

    // ═══════════════════════════════════════════════
    // GL 初期化
    // ═══════════════════════════════════════════════
    protected override unsafe void OnOpenGlInit(GlInterface gl)
    {
        base.OnOpenGlInit(gl);
        _gl = GL.GetApi(gl.GetProcAddress);

        _gl.ClearColor(0.11f, 0.11f, 0.13f, 1f);
        _gl.Enable(EnableCap.DepthTest);

        // --- メッシュ用シェーダー ---
        _meshProgram = BuildProgram(MeshVert, MeshFrag);

        _meshVao = _gl.GenVertexArray();
        _meshVbo = _gl.GenBuffer();
        _gl.BindVertexArray(_meshVao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _meshVbo);
        uint mp = (uint)_gl.GetAttribLocation(_meshProgram, "aPos");
        uint mn = (uint)_gl.GetAttribLocation(_meshProgram, "aNormal");
        uint ms = (uint)(6 * sizeof(float));
        _gl.VertexAttribPointer(mp, 3, VertexAttribPointerType.Float, false, ms, (void*)0);
        _gl.EnableVertexAttribArray(mp);
        _gl.VertexAttribPointer(mn, 3, VertexAttribPointerType.Float, false, ms, (void*)(3 * sizeof(float)));
        _gl.EnableVertexAttribArray(mn);
        _gl.BindVertexArray(0);

        // --- ライン（グリッド・軸）用シェーダー ---
        _lineProgram = BuildProgram(LineVert, LineFrag);
        var gridData = BuildGridLines();

        _lineVao = _gl.GenVertexArray();
        _lineVbo = _gl.GenBuffer();
        _gl.BindVertexArray(_lineVao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _lineVbo);
        fixed (float* ptr = gridData)
            _gl.BufferData(BufferTargetARB.ArrayBuffer,
                (nuint)(gridData.Length * sizeof(float)), ptr, BufferUsageARB.StaticDraw);
        uint lp = (uint)_gl.GetAttribLocation(_lineProgram, "aPos");
        uint lc = (uint)_gl.GetAttribLocation(_lineProgram, "aColor");
        uint ls = (uint)(6 * sizeof(float));
        _gl.VertexAttribPointer(lp, 3, VertexAttribPointerType.Float, false, ls, (void*)0);
        _gl.EnableVertexAttribArray(lp);
        _gl.VertexAttribPointer(lc, 3, VertexAttribPointerType.Float, false, ls, (void*)(3 * sizeof(float)));
        _gl.EnableVertexAttribArray(lc);
        _lineVertexCount = gridData.Length / 6;
        _gl.BindVertexArray(0);

        _glInitialized = true;
        Log.Information("[Viewport] GL Init OK  gridVerts={G}", _lineVertexCount);

        DispatcherTimer.Run(() => { RequestNextFrameRendering(); return true; },
            TimeSpan.FromMilliseconds(16));
    }

    // ═══════════════════════════════════════════════
    // 描画
    // ═══════════════════════════════════════════════
    protected override unsafe void OnOpenGlRender(GlInterface gl, int fb)
    {
        if (_gl == null || !_glInitialized) return;

        var scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
        int w = (int)(Bounds.Width  * scaling);
        int h = (int)(Bounds.Height * scaling);
        if (w <= 0 || h <= 0) return;

        _gl.Viewport(0, 0, (uint)w, (uint)h);
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        // VBO 更新
        if (_meshNeedsUpdate && _pendingVertexData.Length > 0)
        {
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _meshVbo);
            fixed (float* ptr = _pendingVertexData)
                _gl.BufferData(BufferTargetARB.ArrayBuffer,
                    (nuint)(_pendingVertexData.Length * sizeof(float)), ptr,
                    BufferUsageARB.DynamicDraw);
            _vertexCount = _pendingVertexData.Length / 6;
            _meshNeedsUpdate = false;
        }

        // MVP 計算
        var camPos = CamPos();
        var model  = Matrix4x4.Identity;
        var view   = Matrix4x4.CreateLookAt(camPos, _target, Vector3.UnitY);
        var proj   = Matrix4x4.CreatePerspectiveFieldOfView(
            50f * MathF.PI / 180f, (float)w / h, 0.01f, 1000f);
        var mvp = model * view * proj;

        // --- グリッド / 軸を描画 ---
        _gl.UseProgram(_lineProgram);
        int lMvpLoc = _gl.GetUniformLocation(_lineProgram, "uMVP");
        _gl.UniformMatrix4(lMvpLoc, 1, false,
            MemoryMarshal.CreateReadOnlySpan(ref mvp.M11, 16));
        _gl.BindVertexArray(_lineVao);
        _gl.DrawArrays(PrimitiveType.Lines, 0, (uint)_lineVertexCount);
        _gl.BindVertexArray(0);

        // --- メッシュを描画 ---
        if (_vertexCount > 0)
        {
            _gl.UseProgram(_meshProgram);
            int mMvpLoc   = _gl.GetUniformLocation(_meshProgram, "uMVP");
            int mModelLoc = _gl.GetUniformLocation(_meshProgram, "uModel");
            int mCamLoc   = _gl.GetUniformLocation(_meshProgram, "uCamPos");
            _gl.UniformMatrix4(mMvpLoc,   1, false,
                MemoryMarshal.CreateReadOnlySpan(ref mvp.M11,   16));
            _gl.UniformMatrix4(mModelLoc, 1, false,
                MemoryMarshal.CreateReadOnlySpan(ref model.M11, 16));
            _gl.Uniform3(mCamLoc, camPos.X, camPos.Y, camPos.Z);
            _gl.BindVertexArray(_meshVao);
            _gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)_vertexCount);
            _gl.BindVertexArray(0);
        }
    }

    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        _gl?.DeleteBuffer(_meshVbo);  _gl?.DeleteVertexArray(_meshVao);
        _gl?.DeleteBuffer(_lineVbo);  _gl?.DeleteVertexArray(_lineVao);
        _gl?.DeleteProgram(_meshProgram); _gl?.DeleteProgram(_lineProgram);
        _gl?.Dispose();
        base.OnOpenGlDeinit(gl);
    }

    // ═══════════════════════════════════════════════
    // ユーティリティ
    // ═══════════════════════════════════════════════
    private uint BuildProgram(string vertSrc, string fragSrc)
    {
        uint vs = CompileShader(ShaderType.VertexShader,   vertSrc);
        uint fs = CompileShader(ShaderType.FragmentShader, fragSrc);
        uint pg = _gl!.CreateProgram();
        _gl.AttachShader(pg, vs); _gl.AttachShader(pg, fs);
        _gl.LinkProgram(pg);
        _gl.GetProgram(pg, ProgramPropertyARB.LinkStatus, out int ok);
        if (ok == 0) Log.Error("[Viewport] Link error: {E}", _gl.GetProgramInfoLog(pg));
        _gl.DeleteShader(vs); _gl.DeleteShader(fs);
        return pg;
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
