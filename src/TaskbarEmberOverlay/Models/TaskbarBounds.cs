using System;
using System.Windows;
using TaskbarEmberOverlay.Interop;

namespace TaskbarEmberOverlay.Models
{
    public class TaskbarBounds
    {
        public IntPtr Hwnd { get; set; }
        public bool IsPrimary { get; set; }
        public NativeMethods.RECT PhysicalRect { get; set; }
        public double DpiScale { get; set; } = 1.0;
        public Rect DipRect { get; set; }
        public bool IsVisible { get; set; } = true;
        public bool IsAutoHide { get; set; } = false;

        public static Rect CalculateDipRect(NativeMethods.RECT physical, double dpiScale)
        {
            if (dpiScale <= 0) dpiScale = 1.0;
            double left = physical.Left / dpiScale;
            double top = physical.Top / dpiScale;
            double width = physical.Width / dpiScale;
            double height = physical.Height / dpiScale;
            return new Rect(left, top, Math.Max(1, width), Math.Max(1, height));
        }

        public static double DpiToScale(uint dpi)
        {
            if (dpi == 0) return 1.0;
            return dpi / 96.0;
        }

        public override string ToString()
        {
            return $"Taskbar [HWND=0x{Hwnd.ToInt64():X}] Primary={IsPrimary} Visible={IsVisible} AutoHide={IsAutoHide} DPI={DpiScale:F2} Phys={PhysicalRect} DIP={DipRect}";
        }
    }
}
