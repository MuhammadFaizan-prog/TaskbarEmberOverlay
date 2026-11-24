using System;
using System.Windows;
using System.Windows.Media;
using TaskbarEmberOverlay.Services;
using Xunit;

namespace TaskbarEmberOverlay.Tests
{
    public class GeometryTests
    {
        private readonly Xunit.Abstractions.ITestOutputHelper _output;

        public GeometryTests(Xunit.Abstractions.ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void Test_SideAnimationFramesLoaded()
        {
            var engine = new EmberEngine();
            _output.WriteLine($"Loaded side animation frames: {EmberEngine.SideAnimationFrameCount}, LastError={EmberEngine.LastError}");
            Assert.True(EmberEngine.SideAnimationFrameCount > 0, $"Side animation frames should be loaded! Error: {EmberEngine.LastError}");
        }

        [Fact]
        public void Test_DetectFreeSpaces_Direct()
        {
            IntPtr tray = TaskbarEmberOverlay.Interop.NativeMethods.FindWindow("Shell_TrayWnd", null);
            _output.WriteLine($"Shell_TrayWnd: 0x{tray.ToInt64():X}");
            if (tray != IntPtr.Zero)
            {
                TaskbarEmberOverlay.Interop.NativeMethods.GetWindowRect(tray, out var rect);
                _output.WriteLine($"Tray rect: {rect}");
                var dipRect = new Rect(0, 0, rect.Width, rect.Height);
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var spaces = TaskbarLayoutDetector.DetectFreeSpaces(tray, dipRect, 1.0, true);
                sw.Stop();
                _output.WriteLine($"DetectFreeSpaces took: {sw.ElapsedMilliseconds} ms, count: {spaces.Count}");
                foreach (var s in spaces)
                {
                    _output.WriteLine($"  Space: {s}");
                }
            }
        }

        [Fact]
        public void CombinedGeometry_ExcludesInteriorRectMathematically()
        {
            double width = 1920;
            double height = 48;
            double thickness = 4.0;

            var outerRect = new Rect(0, 0, width, height);
            var innerRect = new Rect(thickness, thickness, width - 2 * thickness, height - 2 * thickness);

            var outerGeom = new RectangleGeometry(outerRect);
            var innerGeom = new RectangleGeometry(innerRect);
            var combined = new CombinedGeometry(GeometryCombineMode.Exclude, outerGeom, innerGeom);

            // Points inside the interior (e.g. Start button, center icons, clock)
            var startIconPoint = new Point(width / 2, height / 2);
            var taskbarCenterPoint = new Point(100, 24);
            var clockCenterPoint = new Point(1850, 24);

            Assert.False(combined.FillContains(startIconPoint), "Center taskbar point must NOT be contained in perimeter clip");
            Assert.False(combined.FillContains(taskbarCenterPoint), "Taskbar icon area point must NOT be contained in perimeter clip");
            Assert.False(combined.FillContains(clockCenterPoint), "Clock area point must NOT be contained in perimeter clip");

            // Points on the outer perimeter band (e.g. y = 1.5, x = 1.5)
            var topEdgePoint = new Point(500, 2.0);
            var bottomEdgePoint = new Point(500, 46.5);
            var leftEdgePoint = new Point(2.0, 24);
            var rightEdgePoint = new Point(1918.0, 24);

            Assert.True(combined.FillContains(topEdgePoint), "Top perimeter edge point must be contained in perimeter clip");
            Assert.True(combined.FillContains(bottomEdgePoint), "Bottom perimeter edge point must be contained in perimeter clip");
            Assert.True(combined.FillContains(leftEdgePoint), "Left perimeter edge point must be contained in perimeter clip");
            Assert.True(combined.FillContains(rightEdgePoint), "Right perimeter edge point must be contained in perimeter clip");
        }

        [Fact]
        public void CombinedGeometry_WithFreeSpaces_IncludesFreeSpaceAndExcludesIconHitAreas()
        {
            double width = 1920;
            double height = 48;
            double thickness = 6.0;

            var outerRect = new Rect(0, 0, width, height);
            var innerRect = new Rect(thickness, thickness, width - 2 * thickness, height - 2 * thickness);

            var outerGeom = new RectangleGeometry(outerRect);
            var innerGeom = new RectangleGeometry(innerRect);
            var perimeterClip = new CombinedGeometry(GeometryCombineMode.Exclude, outerGeom, innerGeom);

            // Left free space between Weather and Start button
            var leftFreeSpace = new Rect(180, 2, 450, height - 4);
            // Right free space between AppList and Tray
            var rightFreeSpace = new Rect(1250, 2, 350, height - 4);

            var fsGroup = new GeometryGroup();
            fsGroup.Children.Add(new RectangleGeometry(leftFreeSpace, 4.0, 4.0));
            fsGroup.Children.Add(new RectangleGeometry(rightFreeSpace, 4.0, 4.0));

            var totalClip = new CombinedGeometry(GeometryCombineMode.Union, perimeterClip, fsGroup);

            // Icon hit areas must be strictly FALSE (100% transparent and clickable)
            var startButtonPoint = new Point(720, 24);
            var pinnedAppPoint = new Point(960, 24);
            var trayClockPoint = new Point(1800, 24);
            var weatherIconPoint = new Point(50, 24);

            Assert.False(totalClip.FillContains(startButtonPoint), "Start button interior must be 100% excluded/transparent");
            Assert.False(totalClip.FillContains(pinnedAppPoint), "Pinned app icon interior must be 100% excluded/transparent");
            Assert.False(totalClip.FillContains(trayClockPoint), "Tray clock interior must be 100% excluded/transparent");
            Assert.False(totalClip.FillContains(weatherIconPoint), "Weather icon interior must be 100% excluded/transparent");

            // Free space regions must be TRUE (displays side animation)
            var leftFreeSpaceCenter = new Point(350, 24);
            var rightFreeSpaceCenter = new Point(1400, 24);

            Assert.True(totalClip.FillContains(leftFreeSpaceCenter), "Left free space must be included for side animation");
            Assert.True(totalClip.FillContains(rightFreeSpaceCenter), "Right free space must be included for side animation");

            // Perimeter points along edges must be TRUE (displays ember crackle frame)
            var topEdgePoint = new Point(960, 2.0);
            var bottomEdgePoint = new Point(960, 46.0);

            Assert.True(totalClip.FillContains(topEdgePoint), "Top edge above icons must display perimeter crackle");
            Assert.True(totalClip.FillContains(bottomEdgePoint), "Bottom edge below icons must display perimeter crackle");
        }
    }
}
