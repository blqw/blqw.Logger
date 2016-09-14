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
    internal class InternalLogger
    {
        public static InternalLogger Instance { get; } = new InternalLogger();

        private readonly TraceSource _Source = InitSource();

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
                source.Listeners.Add(new SLSTraceListener(dirPath, null));
            }
            return source;
        }


        /// <summary>
        /// 分割线
        /// </summary>
        private const string CUTTING_LINE = "---------------------------------------------------------------";

        /// <summary>
        /// 输出异常信息
        /// </summary>
        public void Error(Exception ex, string title = null, [CallerMemberName] string member = null, [CallerLineNumber] int line = 0, [CallerFilePath] string file = null)
        {
            Log(TraceEventType.Error, title, ex.ToString(), member, line, file);
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
        public void Log(TraceEventType type, string title, string message = null, [CallerMemberName] string member = null, [CallerLineNumber] int line = 0, [CallerFilePath] string file = null)
        {
            if (_Source.Switch.ShouldTrace(type) == false)
            {
                return;
            }
            try
            {
                var txt = string.Join(Environment.NewLine,
                    string.Empty,
                    //$"[{type}]: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}",
                    $"[{member}]: {title}",
                    $"{file}: {line}",
                    message,
                    CUTTING_LINE,
                    string.Empty);
                _Source.TraceEvent(type, 1, txt);
                _Source.Flush();
            }
            catch
            {
                // ignored
            }
        }

        public void Entry([CallerMemberName] string member = null, [CallerLineNumber] int line = 0, [CallerFilePath] string file = null)
        {
            Log(TraceEventType.Start, $"进入方法 {member}", null, member, line, file);
        }

        public void Return(string @return, [CallerMemberName] string member = null, [CallerLineNumber] int line = 0, [CallerFilePath] string file = null)
        {
            Log(TraceEventType.Stop, $"离开方法 {member}", $"return {@return}", member, line, file);
        }

        public void Exit([CallerMemberName] string member = null, [CallerLineNumber] int line = 0, [CallerFilePath] string file = null)
        {
            Log(TraceEventType.Stop, $"离开方法 {member}", null, member, line, file);
        }
    }
}