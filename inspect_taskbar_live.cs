using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;

public class TaskbarInspector
{
    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    public struct RECT { public int Left, Top, Right, Bottom; }

    public static void Main()
    {
        IntPtr tray = FindWindow("Shell_TrayWnd", null);
        Console.WriteLine($"Shell_TrayWnd: 0x{tray.ToInt64():X}");
        if (tray == IntPtr.Zero) return;

        GetWindowRect(tray, out var tr);
        Console.WriteLine($"Taskbar Rect: ({tr.Left}, {tr.Top}, {tr.Right}, {tr.Bottom}) Width={tr.Right-tr.Left}, Height={tr.Bottom-tr.Top}");

        EnumChildWindows(tray, (h, l) =>
        {
            var sb = new StringBuilder(256);
            GetClassName(h, sb, 256);
            GetWindowRect(h, out var r);
            Console.WriteLine($"Child: 0x{h.ToInt64():X} Class={sb} Rect=({r.Left}, {r.Top}, {r.Right}, {r.Bottom}) W={r.Right-r.Left} H={r.Bottom-r.Top}");
            return true;
        }, IntPtr.Zero);

        // Now test UI Automation
        Console.WriteLine("--- UI Automation ---");
        try
        {
            var elem = AutomationElement.FromHandle(tray);
            if (elem != null)
            {
                var children = elem.FindAll(TreeScope.Children, Condition.TrueCondition);
                Console.WriteLine($"Top-level UIA children count: {children.Count}");
                foreach (AutomationElement c in children)
                {
                    try
                    {
                        var r = c.Current.BoundingRectangle;
                        Console.WriteLine($"  UIA Child: Id='{c.Current.AutomationId}' Name='{c.Current.Name}' Class='{c.Current.ClassName}' Rect=({r.Left},{r.Top},{r.Width},{r.Height})");
                        
                        // Check subchildren
                        var sub = c.FindAll(TreeScope.Children, Condition.TrueCondition);
                        foreach (AutomationElement sc in sub)
                        {
                            var sr = sc.Current.BoundingRectangle;
                            Console.WriteLine($"    UIA Sub: Id='{sc.Current.AutomationId}' Name='{sc.Current.Name}' Class='{sc.Current.ClassName}' Rect=({sr.Left},{sr.Top},{sr.Width},{sr.Height})");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  Error reading child: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"UIA Error: {ex}");
        }
    }
}
