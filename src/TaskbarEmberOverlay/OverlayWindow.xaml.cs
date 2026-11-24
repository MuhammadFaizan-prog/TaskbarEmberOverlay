using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using TaskbarEmberOverlay.Interop;
using TaskbarEmberOverlay.Models;
using TaskbarEmberOverlay.Services;

namespace TaskbarEmberOverlay
{
    public partial class OverlayWindow : Window
    {
        private readonly EmberEngine _engine = new();
        private AppSettings _settings;
        private IntPtr _hwnd = IntPtr.Zero;
        private IntPtr _targetTaskbarHwnd = IntPtr.Zero;
        private bool _isRenderingHooked;
        private Stopwatch _stopwatch = new();
        private double _lastRenderTime;
        private double _accumulatedDt;
        private bool _isPaused;

        private double _layoutScanTimer;

        public TaskbarBounds? CurrentBounds { get; private set; }

        public OverlayWindow(AppSettings settings)
        {
            InitializeComponent();
            _settings = settings;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var helper = new WindowInteropHelper(this);
            _hwnd = helper.Handle;
            AppLogger.Log($"OverlayWindow.OnSourceInitialized: HWND={_hwnd}");

            // Apply non-activating, layered, transparent, toolwindow styles
            int exStyle = NativeMethods.GetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE).ToInt32();
            exStyle |= NativeMethods.WS_EX_TOPMOST |
                       NativeMethods.WS_EX_TRANSPARENT |
                       NativeMethods.WS_EX_LAYERED |
                       NativeMethods.WS_EX_TOOLWINDOW |
                       NativeMethods.WS_EX_NOACTIVATE;
            NativeMethods.SetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(exStyle));

            // Hook window messages for click-through pass
            var source = HwndSource.FromHwnd(_hwnd);
            source?.AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_NCHITTEST)
            {
                // HTTRANSPARENT ensures every mouse event falls right through to taskbar below
                handled = true;
                return new IntPtr(NativeMethods.HTTRANSPARENT);
            }

            return IntPtr.Zero;
        }

        public void UpdateSettings(AppSettings settings)
        {
            _settings = settings;
            if (CurrentBounds != null)
            {
                UpdateTaskbarBounds(CurrentBounds);
            }
            InvalidateVisual();
        }

        public void SetPaused(bool paused)
        {
            _isPaused = paused;
            if (_isPaused)
            {
                StopRenderingLoop();
                InvalidateVisual();
            }
            else if (IsVisible && CurrentBounds?.IsVisible == true)
            {
                StartRenderingLoop();
            }
        }

        public void UpdateTaskbarBounds(TaskbarBounds bounds)
        {
            CurrentBounds = bounds;
            _targetTaskbarHwnd = bounds.Hwnd;

            AppLogger.Log($"OverlayWindow.UpdateTaskbarBounds: Hwnd={bounds.Hwnd}, visible={bounds.IsVisible}, rect={bounds.DipRect}");

            if (!bounds.IsVisible)
            {
                StopRenderingLoop();
                Visibility = Visibility.Hidden;
                return;
            }

            double newLeft = bounds.DipRect.Left;
            double newTop = bounds.DipRect.Top;
            double newWidth = Math.Max(10, bounds.DipRect.Width);
            double newHeight = Math.Max(10, bounds.DipRect.Height);

            Left = newLeft;
            Top = newTop;
            Width = newWidth;
            Height = newHeight;

            double thickness = _settings.EdgeThickness;
            var freeSpaces = TaskbarLayoutDetector.DetectFreeSpaces(_targetTaskbarHwnd, bounds.DipRect, bounds.DpiScale);
            ApplyClip(newWidth, newHeight, thickness, freeSpaces);

            _engine.InitializeDimensions(newWidth, newHeight, thickness, freeSpaces);

            if (Visibility != Visibility.Visible)
            {
                Visibility = Visibility.Visible;
            }

            // Keep Z-Order tied to taskbar (directly above taskbar, never globally topmost)
            if (_hwnd != IntPtr.Zero)
            {
                NativeMethods.SetWindowPos(
                    _hwnd,
                    NativeMethods.HWND_TOPMOST,
                    0, 0, 0, 0,
                    NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
            }

            if (!_isPaused)
            {
                StartRenderingLoop();
            }
        }

        private void StartRenderingLoop()
        {
            if (_isRenderingHooked) return;

            bool isStatic = IsReducedMotionActive();
            if (isStatic)
            {
                // In static mode, no animation frame loop is needed
                InvalidateVisual();
                return;
            }

            _stopwatch.Restart();
            _lastRenderTime = 0;
            _accumulatedDt = 0;
            CompositionTarget.Rendering += OnCompositionRendering;
            _isRenderingHooked = true;
        }

        private void StopRenderingLoop()
        {
            if (!_isRenderingHooked) return;

            CompositionTarget.Rendering -= OnCompositionRendering;
            _isRenderingHooked = false;
            _stopwatch.Stop();
        }

        private void OnCompositionRendering(object? sender, EventArgs e)
        {
            if (!IsVisible || _isPaused)
            {
                StopRenderingLoop();
                return;
            }

            double currentTime = _stopwatch.Elapsed.TotalSeconds;
            double dt = currentTime - _lastRenderTime;
            _lastRenderTime = currentTime;

            if (dt <= 0 || dt > 0.2) dt = 0.033; // Clamp dt spikes

            double targetFrameTime = 1.0 / Math.Clamp(_settings.TargetFps, 10, 60);
            _accumulatedDt += dt;

            // Periodically check layout every 1.5 seconds to track taskbar icon changes dynamically
            _layoutScanTimer += dt;
            if (_layoutScanTimer >= 1.5 && CurrentBounds != null)
            {
                _layoutScanTimer = 0;
                RefreshFreeSpaces(CurrentBounds);

                // Keep overlay topmost above taskbar so clicking other windows doesn't occlude it
                if (_hwnd != IntPtr.Zero)
                {
                    NativeMethods.SetWindowPos(
                        _hwnd,
                        NativeMethods.HWND_TOPMOST,
                        0, 0, 0, 0,
                        NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
                }
            }

            if (_accumulatedDt >= targetFrameTime)
            {
                _engine.Update(_accumulatedDt, currentTime, _settings);
                _accumulatedDt = 0;
                InvalidateVisual();
            }
        }

        private void RefreshFreeSpaces(TaskbarBounds bounds)
        {
            try
            {
                var freeSpaces = TaskbarLayoutDetector.DetectFreeSpaces(_targetTaskbarHwnd, bounds.DipRect, bounds.DpiScale);
                ApplyClip(bounds.DipRect.Width, bounds.DipRect.Height, _settings.EdgeThickness, freeSpaces);
                _engine.UpdateFreeSpaces(freeSpaces);
            }
            catch { }
        }

        private void ApplyClip(double width, double height, double thickness, System.Collections.Generic.List<Rect> freeSpaces)
        {
            var outerRect = new Rect(0, 0, width, height);
            var innerRect = new Rect(
                thickness,
                thickness,
                Math.Max(0, width - 2 * thickness),
                Math.Max(0, height - 2 * thickness));

            var outerGeom = new RectangleGeometry(outerRect);
            var innerGeom = new RectangleGeometry(innerRect);
            var perimeterClip = new CombinedGeometry(GeometryCombineMode.Exclude, outerGeom, innerGeom);

            Geometry finalClip = perimeterClip;
            if (freeSpaces != null && freeSpaces.Count > 0)
            {
                var fsGroup = new GeometryGroup();
                foreach (var fs in freeSpaces)
                {
                    // Full-height free space strip so the feathered drawing in Render dissolves naturally
                    fsGroup.Children.Add(new RectangleGeometry(new Rect(fs.Left - 1, 0, fs.Width + 2, height)));
                }
                finalClip = new CombinedGeometry(GeometryCombineMode.Union, perimeterClip, fsGroup);
            }
            finalClip.Freeze();
            Clip = finalClip;
        }

        private bool IsReducedMotionActive()
        {
            if (_settings.ReducedMotion.HasValue)
            {
                return _settings.ReducedMotion.Value;
            }

            try
            {
                if (SystemParameters.HighContrast) return true;
                if (!SystemParameters.ClientAreaAnimation) return true;
            }
            catch
            {
                // Fallback
            }

            return false;
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            if (_isPaused) return;

            bool isStatic = IsReducedMotionActive();
            double timeSeconds = _stopwatch.Elapsed.TotalSeconds;
            _engine.Render(dc, timeSeconds, _settings, isStatic);
        }

        protected override void OnClosed(EventArgs e)
        {
            StopRenderingLoop();
            base.OnClosed(e);
        }
    }
}
