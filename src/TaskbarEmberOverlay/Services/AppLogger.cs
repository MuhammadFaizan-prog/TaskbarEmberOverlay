using System;
using System.IO;

namespace TaskbarEmberOverlay.Services
{
    public static class AppLogger
    {
        private static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TaskbarEmberOverlay",
            "app.log");

        private static readonly object LockObj = new();

        public static void Log(string message)
        {
            try
            {
                lock (LockObj)
                {
                    string dir = Path.GetDirectoryName(LogPath)!;
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}\n");
                }
            }
            catch { }
        }
    }
}
