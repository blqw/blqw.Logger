using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
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
                source.Listeners.Add(new LocalFileTraceListener());
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
                //source.TraceData(type, 1, "xxx");
                source.TraceData(type, 1, new LogItem
                {
                    Category = GetString(type),
                    Message = title,
                    Module = member,
                    Time = DateTime.Now,
                    Callstack = $"{file}:{line}",
                    Content = message,
                });
            }
            catch (Exception ex)
            {
                // ignored
            }
        }

        private static string GetString(TraceEventType type)
        {
            switch (type)
            {
                case TraceEventType.Critical:
                    return "Critical";
                case TraceEventType.Error:
                    return "Error";
                case TraceEventType.Warning:
                    return "Warning";
                case TraceEventType.Information:
                    return "Information";
                case TraceEventType.Verbose:
                    return "Verbose";
                case TraceEventType.Start:
                    return "Start";
                case TraceEventType.Stop:
                    return "Stop";
                case TraceEventType.Suspend:
                    return "Suspend";
                case TraceEventType.Resume:
                    return "Resume";
                case TraceEventType.Transfer:
                    return "Transfer";
                default:
                    return type.ToString();
            }
        }

        [ThreadStatic]
        private static StringBuilder _Buffer;

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

        public static void FlushAll(this TraceSource source)
        {
            if (source == null || source.Switch.Level == SourceLevels.Off)
            {
                return;
            }
            try
            {
                source.Flush();
            }
            catch (Exception ex)
            {
                // ignored
            }
        }
    }
}