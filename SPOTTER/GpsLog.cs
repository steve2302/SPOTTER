using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace SPOTTER.Controllers
{
    /// <summary>
    /// Persistent GPS/connection diagnostics logger. Writes timestamped lines to
    /// a plain-text file so field issues can be investigated with Notepad after
    /// the fact, without needing DebugView attached. Every line is also sent to
    /// <see cref="Debug.WriteLine"/>, so attaching DebugView still works for
    /// live debugging.
    ///
    /// Log location: %LOCALAPPDATA%\SPOTTER\gps-debug.log
    /// Rotation:     when the file passes 2 MB, it is renamed to .old
    ///               (overwriting any previous .old), and a fresh file starts.
    ///               Only one rotated copy is kept, so disk usage is bounded.
    /// Thread safety: all writes are serialised through a lock. Safe to call
    ///               from any thread, including the GPS reader and the UI.
    /// Error handling: every public method swallows all exceptions. Logging
    ///               must never fail the app, so a locked file, missing
    ///               AppData folder, read-only drive, etc. all just degrade
    ///               silently to Debug.WriteLine-only output.
    /// </summary>
    public static class GpsLog
    {
        private static readonly object _writeLock = new object();
        private const long MaxBytes = 2 * 1024 * 1024; // 2 MB
        private static readonly string _logPath;
        private static bool _headerWritten;

        /// <summary>
        /// Absolute path of the current log file, or null if the log directory
        /// could not be created (in which case GpsLog degrades to Debug-only).
        /// Useful for a "Show log file" menu entry.
        /// </summary>
        public static string LogFilePath => _logPath;

        static GpsLog()
        {
            try
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SPOTTER");
                Directory.CreateDirectory(dir);
                _logPath = Path.Combine(dir, "gps-debug.log");
            }
            catch
            {
                _logPath = null;
            }
        }

        /// <summary>
        /// Append a timestamped line to the log file and mirror it to Debug.WriteLine.
        /// </summary>
        public static void WriteLine(string message)
        {
            // Mirror to DebugView etc. so existing debugging workflows still work.
            try { Debug.WriteLine(message); } catch { /* never fail */ }

            if (_logPath == null) return;

            try
            {
                lock (_writeLock)
                {
                    RotateIfNeeded();

                    using (var writer = new StreamWriter(_logPath, append: true))
                    {
                        if (!_headerWritten)
                        {
                            writer.WriteLine();
                            writer.WriteLine("================================================================");
                            writer.WriteLine($"=== SPOTTER GPS log session starting {DateTime.Now:yyyy-MM-dd HH:mm:ss zzz} ===");
                            writer.WriteLine($"=== Machine: {Environment.MachineName}  User: {Environment.UserName} ===");
                            writer.WriteLine($"=== OS: {Environment.OSVersion}  CLR: {Environment.Version} ===");
                            writer.WriteLine("================================================================");
                            _headerWritten = true;
                        }

                        writer.WriteLine(
                            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [T{Thread.CurrentThread.ManagedThreadId:D3}] {message}");
                    }
                }
            }
            catch
            {
                // Never fail the app on a logging error.
            }
        }

        /// <summary>
        /// Convenience overload for formatted messages. Suppresses format
        /// exceptions so a bad format string never crashes the caller.
        /// </summary>
        public static void WriteLine(string format, params object[] args)
        {
            string message;
            try { message = string.Format(format, args); }
            catch { message = format; }
            WriteLine(message);
        }

        /// <summary>
        /// If the current log file has grown past MaxBytes, rename it to
        /// &lt;path&gt;.old (deleting any previous .old) and let the caller start
        /// a fresh file. Called from inside the write lock, so there is no
        /// race with concurrent writers.
        /// </summary>
        private static void RotateIfNeeded()
        {
            try
            {
                var info = new FileInfo(_logPath);
                if (!info.Exists || info.Length < MaxBytes) return;

                string oldPath = _logPath + ".old";
                if (File.Exists(oldPath)) File.Delete(oldPath);
                File.Move(_logPath, oldPath);
                _headerWritten = false; // write a fresh header into the new file
            }
            catch
            {
                // If we can't rotate (file locked by another process reading it,
                // AV scanner holding it, etc.) just truncate so the file does
                // not grow without bound. Losing the tail is better than a
                // runaway log file in the field.
                try { File.Delete(_logPath); _headerWritten = false; } catch { }
            }
        }
    }
}
