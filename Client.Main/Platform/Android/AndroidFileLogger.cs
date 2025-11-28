#if ANDROID
using Android.App;
using Android.Content;
using Android.Provider;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Client.Main.Platform.Android
{
    /// <summary>
    /// File logger provider for Android that writes logs to Downloads folder.
    /// Uses MediaStore API for Android 10+ compatibility.
    /// </summary>
    public class AndroidFileLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentDictionary<string, AndroidFileLogger> _loggers = new();
        private readonly AndroidFileLogWriter _writer;

        public AndroidFileLoggerProvider()
        {
            _writer = new AndroidFileLogWriter();
        }

        public ILogger CreateLogger(string categoryName)
        {
            return _loggers.GetOrAdd(categoryName, name => new AndroidFileLogger(name, _writer));
        }

        public void Dispose()
        {
            _loggers.Clear();
            _writer.Dispose();
        }
    }

    internal class AndroidFileLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly AndroidFileLogWriter _writer;

        public AndroidFileLogger(string categoryName, AndroidFileLogWriter writer)
        {
            _categoryName = categoryName;
            _writer = writer;
        }

        public IDisposable BeginScope<TState>(TState state) => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var level = GetLogLevelString(logLevel);
            var message = formatter(state, exception);

            var logEntry = $"[{timestamp}] [{level}] {_categoryName}: {message}";

            if (exception != null)
            {
                logEntry += $"\n{exception}";
            }

            _writer.WriteLog(logEntry);
        }

        private static string GetLogLevelString(LogLevel logLevel)
        {
            return logLevel switch
            {
                LogLevel.Trace => "TRACE",
                LogLevel.Debug => "DEBUG",
                LogLevel.Information => "INFO ",
                LogLevel.Warning => "WARN ",
                LogLevel.Error => "ERROR",
                LogLevel.Critical => "CRIT ",
                _ => "NONE "
            };
        }
    }

    internal class AndroidFileLogWriter : IDisposable
    {
        private readonly BlockingCollection<string> _logQueue = new(1000);
        private readonly Thread _writerThread;
        private readonly CancellationTokenSource _cts = new();
        private readonly string _logFilePath;
        private bool _disposed;

        public AndroidFileLogWriter()
        {
            var ctx = Application.Context;
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var fileName = $"MuAndroid_{timestamp}.log";

            // Try legacy path first (Android < 10)
            try
            {
                var downloadsPath = global::Android.OS.Environment
                    .GetExternalStoragePublicDirectory(global::Android.OS.Environment.DirectoryDownloads)
                    .AbsolutePath;

                _logFilePath = Path.Combine(downloadsPath, fileName);
                Directory.CreateDirectory(downloadsPath);

                // Test write
                File.WriteAllText(_logFilePath, $"=== MuAndroid Log Started at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n");
            }
            catch
            {
                // Android 10+ requires MediaStore API
                _logFilePath = CreateFileViaMediaStore(fileName);
            }

            // Start background writer thread
            _writerThread = new Thread(WriterLoop)
            {
                IsBackground = true,
                Name = "AndroidFileLogWriter"
            };
            _writerThread.Start();
        }

        private string CreateFileViaMediaStore(string fileName)
        {
            // MediaStore API is only available on Android 10+ (API 29)
            if (!OperatingSystem.IsAndroidVersionAtLeast(29))
                return null;

            var ctx = Application.Context;

            var values = new ContentValues();
            values.Put(MediaStore.IMediaColumns.DisplayName, fileName);
            values.Put(MediaStore.IMediaColumns.MimeType, "text/plain");
            values.Put(MediaStore.IMediaColumns.RelativePath, global::Android.OS.Environment.DirectoryDownloads);

            var uri = ctx.ContentResolver.Insert(MediaStore.Downloads.ExternalContentUri, values);

            if (uri != null)
            {
                try
                {
                    using var stream = ctx.ContentResolver.OpenOutputStream(uri);
                    using var writer = new StreamWriter(stream);
                    writer.WriteLine($"=== MuAndroid Log Started at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
                    writer.Flush();
                }
                catch { }

                return $"/storage/emulated/0/Download/{fileName}";
            }

            return null;
        }

        public void WriteLog(string logEntry)
        {
            if (!_disposed && !string.IsNullOrEmpty(_logFilePath))
            {
                try
                {
                    _logQueue.Add(logEntry, _cts.Token);
                }
                catch (OperationCanceledException)
                {
                    // Logger is being disposed
                }
            }
        }

        private void WriterLoop()
        {
            var buffer = new StringBuilder(4096);

            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    // Wait for log entry with timeout
                    if (_logQueue.TryTake(out var logEntry, 500, _cts.Token))
                    {
                        buffer.AppendLine(logEntry);

                        // Batch write: collect more entries if available
                        while (_logQueue.TryTake(out logEntry, 0) && buffer.Length < 32768)
                        {
                            buffer.AppendLine(logEntry);
                        }

                        // Write to file
                        FlushToFile(buffer.ToString());
                        buffer.Clear();
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    global::Android.Util.Log.Error("AndroidFileLogWriter", $"Error writing log: {ex}");
                }
            }

            // Final flush
            if (buffer.Length > 0)
            {
                FlushToFile(buffer.ToString());
            }
        }

        private void FlushToFile(string content)
        {
            if (string.IsNullOrEmpty(_logFilePath))
                return;

            try
            {
                File.AppendAllText(_logFilePath, content);
            }
            catch
            {
                // If legacy path fails, try MediaStore
                FlushViaMediaStore(content);
            }
        }

        private void FlushViaMediaStore(string content)
        {
            if (!OperatingSystem.IsAndroidVersionAtLeast(29))
                return;

            try
            {
                var ctx = Application.Context;
                var fileName = Path.GetFileName(_logFilePath);

                // Query for existing file
                var selection = $"{MediaStore.IMediaColumns.DisplayName} = ?";
                var selectionArgs = new[] { fileName };

                using var cursor = ctx.ContentResolver.Query(
                    MediaStore.Downloads.ExternalContentUri,
                    new[] { "_id" },
                    selection,
                    selectionArgs,
                    null);

                if (cursor != null && cursor.MoveToFirst())
                {
                    var idColumn = cursor.GetColumnIndex("_id");
                    var id = cursor.GetLong(idColumn);
                    var uri = ContentUris.WithAppendedId(MediaStore.Downloads.ExternalContentUri, id);

                    using var stream = ctx.ContentResolver.OpenOutputStream(uri, "wa");
                    using var writer = new StreamWriter(stream);
                    writer.Write(content);
                    writer.Flush();
                }
            }
            catch (Exception ex)
            {
                global::Android.Util.Log.Error("AndroidFileLogWriter", $"MediaStore write error: {ex}");
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _cts.Cancel();
            _logQueue.CompleteAdding();

            if (!_writerThread.Join(TimeSpan.FromSeconds(2)))
            {
                global::Android.Util.Log.Warn("AndroidFileLogWriter", "Writer thread did not exit in time");
            }

            _logQueue.Dispose();
            _cts.Dispose();
        }
    }
}
#endif
