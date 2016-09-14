using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;

namespace blqw.Logger
{
    /// <summary>
    /// 用于记录异常的静态方法
    /// </summary>
    internal static class TraceSourceExtensions
    {
        public static TraceSource InternalSource { get; } = InitSource();

        private static TraceSource InitSource()
        {
            var source = new TraceSource("blqw.Logger", SourceLevels.Error);

            if (source.Listeners?.Count == 1 && source.Listeners[0] is DefaultTraceListener)
            {
                source.Listeners.Clear();
                string dirPath;
                if (Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory)?.ToLowerInvariant() == "bin")
                {
                    dirPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..\\blqw.Logger-Logs");
                }
                else
                {
                    dirPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "blqw.Logger-Logs");
                }
                if (Directory.Exists(dirPath) == false)
                {
                    Directory.CreateDirectory(dirPath);
                }
                source.Listeners.Add(new SLSTraceListener(dirPath, null) { Name = nameof(TraceSourceExtensions) });
            }
            return source;
        }

        /// <summary>
        /// 输出异常信息
        /// </summary>
        public static void Error(this TraceSource source, Exception ex, string title = null, [CallerMemberName] string member = null, [CallerLineNumber] int line = 0, [CallerFilePath] string file = null)
        {
            Log(source, TraceEventType.Error, title, ex.ToString(), member, line, file);
        }

        /// <summary>
        /// 输出调试信息
        /// </summary>
        /// <param name="type"> </param>
        /// <param name="title"></param>
        /// <param name="message"></param>
        /// <param name="member"></param>
        /// <param name="line"></param>
        /// <param name="file"></param>
        public static void Log(this TraceSource source, TraceEventType type, string title, string message = null, [CallerMemberName] string member = null, [CallerLineNumber] int line = 0, [CallerFilePath] string file = null)
        {
            if (source == null || source.Switch.ShouldTrace(type) == false)
            {
                return;
            }
            try
            {
                var txt = string.Join(Environment.NewLine,
                    $"[{type}]: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}",
                    $"[{member}]: {title}",
                    $"{file}: {line}",
                    message);
                source.TraceEvent(type, 1, txt);
                if (!Trace.AutoFlush) source.Flush();
            }
            catch (Exception ex)
            {
                // ignored
            }
        }

        public static void Entry(this TraceSource source, [CallerMemberName] string member = null, [CallerLineNumber] int line = 0, [CallerFilePath] string file = null)
        {
            Log(source, TraceEventType.Start, $"进入方法 {member}", null, member, line, file);
        }

        public static void Return(this TraceSource source, string @return, [CallerMemberName] string member = null, [CallerLineNumber] int line = 0, [CallerFilePath] string file = null)
        {
            Log(source, TraceEventType.Stop, $"离开方法 {member}", $"return {@return}", member, line, file);
        }

        public static void Exit(this TraceSource source, [CallerMemberName] string member = null, [CallerLineNumber] int line = 0, [CallerFilePath] string file = null)
        {
            Log(source, TraceEventType.Stop, $"离开方法 {member}", null, member, line, file);
        }
    }
}