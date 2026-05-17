using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Serilog;
using ShaderCad.App.ViewModels;
using ShaderCad.Core.Components.Primitives;
using ShaderCad.Core.Models;
using ShaderCad.Kernel.Engines;
using ShaderCad.Renderer.Controls;

namespace ShaderCad.App;

public partial class MainWindow : Window
{
    private readonly CadNode    _rootNode;
    private readonly MeshEngine _meshEngine;

    // オーバーレイのマウス追跡
    private bool  _leftDrag, _midDrag;
    private Point _lastMouse;

    public MainWindow()
    {
        InitializeComponent();

        _rootNode = new CadNode { Name = "Root" };
        var sphere = _rootNode.AddComponent<SphereComponent>();
        sphere.Radius = 2.0;

        _meshEngine = new MeshEngine();

        // Inspector の ViewModel
        var inspectorVm = new InspectorViewModel();
        inspectorVm.TargetComponent = sphere;
        foreach (var prop in inspectorVm.Properties)
            prop.PropertyChanged += (_, _) => RebuildAndRender();

        var inspectorView = this.FindControl<Views.InspectorView>("InspectorViewControl");
        if (inspectorView != null)
            inspectorView.DataContext = inspectorVm;

        // ★ 透明オーバーレイにマウスイベントをフック
        var overlay = this.FindControl<Border>("ViewportOverlay");
        if (overlay != null)
        {
            overlay.PointerPressed  += Overlay_Pressed;
            overlay.PointerReleased += Overlay_Released;
            overlay.PointerMoved    += Overlay_Moved;
            overlay.PointerWheelChanged += Overlay_Wheel;
            Log.Information("[MainWindow] Overlay イベント登録完了");
        }
        else
        {
            Log.Warning("[MainWindow] ViewportOverlay が見つかりません");
        }

        RebuildAndRender();
    }

    // ── オーバーレイのマウスイベント ───────────────
    private void Overlay_Pressed(object? s, PointerPressedEventArgs e)
    {
        var p = e.GetCurrentPoint(this).Properties;
        Log.Debug("[MainWindow] Overlay Pressed L={L} M={M}",
            p.IsLeftButtonPressed, p.IsMiddleButtonPressed);
        _lastMouse = e.GetPosition(this);
        _leftDrag  = p.IsLeftButtonPressed;
        _midDrag   = p.IsMiddleButtonPressed;
        e.Pointer.Capture(s as Border);
    }

    private void Overlay_Released(object? s, PointerReleasedEventArgs e)
    {
        Log.Debug("[MainWindow] Overlay Released");
        _leftDrag = _midDrag = false;
        e.Pointer.Capture(null);
    }

    private void Overlay_Moved(object? s, PointerEventArgs e)
    {
        var pos = e.GetPosition(this);
        var dx  = pos.X - _lastMouse.X;
        var dy  = pos.Y - _lastMouse.Y;
        _lastMouse = pos;

        var viewport = this.FindControl<SilkViewportControl>("ViewportControl");
        if (viewport == null) return;

        if (_leftDrag)
        {
            Log.Debug("[MainWindow] Orbit dx={X:F1} dy={Y:F1}", dx, dy);
            viewport.Orbit(dx, dy);
        }
        else if (_midDrag)
        {
            viewport.Pan(dx, dy);
        }
    }

    private void Overlay_Wheel(object? s, PointerWheelEventArgs e)
    {
        Log.Debug("[MainWindow] Wheel dy={D}", e.Delta.Y);
        var viewport = this.FindControl<SilkViewportControl>("ViewportControl");
        viewport?.Zoom(e.Delta.Y);
    }

    // ── CAD パイプライン ────────────────────────────
    private void RebuildAndRender()
    {
        _meshEngine.Rebuild(_rootNode);
        var viewport = this.FindControl<SilkViewportControl>("ViewportControl");
        viewport?.UploadMeshes(_meshEngine.GeneratedMeshes);
    }
}
