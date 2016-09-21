using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace blqw.Logger
{
    /// <summary>
    /// 用于记录异常的静态方法
    /// </summary>
    internal static class TraceSourceExtensions
    {
        /// <summary>
        /// 用于本地日志输出的日志跟踪器单例
        /// </summary>
        public static TraceSource InternalSource { get; } = InitSource();

        /// <summary>
        /// 初始化日志跟踪器
        /// </summary>
        /// <returns></returns>
        private static TraceSource InitSource()
        {
            var source = new TraceSource("blqw.Logger", SourceLevels.Error);

            if ((source.Listeners?.Count == 1) && source.Listeners[0] is DefaultTraceListener)
            {
                source.Listeners.Clear();
                source.Listeners.Add(new LocalFileTraceListener { Name = $"{nameof(blqw)}.InnerLogger-Logs" });
                //source.Listeners.Add(new SystemLogTraceListener() { Name = "Internal" });
            }
            return source;
        }

        /// <summary>
        /// 输出异常信息
        /// </summary>
        public static void Error(this TraceSource source, Exception ex, string title = null,
            [CallerMemberName] string member = null, [CallerLineNumber] int line = 0,
            [CallerFilePath] string file = null)
        {
            Log(source, TraceEventType.Error, title, ex.ToString(), member, line, file);
        }

        /// <summary>
        /// 输出调试信息
        /// </summary>
        /// <param name="type"> </param>
        /// <param name="title"> </param>
        /// <param name="message"> </param>
        /// <param name="member"> </param>
        /// <param name="line"> </param>
        /// <param name="file"> </param>
        public static void Log(this TraceSource source, TraceEventType type, string title, string message = null,
            [CallerMemberName] string member = null, [CallerLineNumber] int line = 0,
            [CallerFilePath] string file = null)
        {
            if ((source == null) || (source.Switch.ShouldTrace(type) == false))
            {
                return;
            }
            try
            {
                source.TraceData(type, 1, new LogItem
                {
                    Title = title,
                    MessageOrContent = message,
                    LoggerName = source.Name,
                    Time = DateTime.Now,
                    Callstack = $"{member}{Environment.NewLine}{file}:{line}",
                    LogID = Trace.CorrelationManager.ActivityId
                });
            }
            catch
            {
                // ignored
            }
        }

        /// <summary>
        /// 获取枚举的字符串形式
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static string GetString(this TraceEventType type)
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
                    return type.ToString(); //直接ToString 会有反射的性能损耗
            }
        }

        /// <summary>
        /// 进入方法
        /// </summary>
        public static void Entry(this TraceSource source, [CallerMemberName] string member = null,
            [CallerLineNumber] int line = 0, [CallerFilePath] string file = null)
        {
            Log(source, TraceEventType.Start, $"进入方法 {member}", null, member, line, file);
        }

        /// <summary>
        /// 离开方法并有一个返回值
        /// </summary>
        public static void Return(this TraceSource source, string @return, [CallerMemberName] string member = null,
            [CallerLineNumber] int line = 0, [CallerFilePath] string file = null)
        {
            Log(source, TraceEventType.Stop, $"离开方法 {member}", $"return {@return}", member, line, file);
        }

        /// <summary>
        /// 离开方法
        /// </summary>
        public static void Exit(this TraceSource source, [CallerMemberName] string member = null,
            [CallerLineNumber] int line = 0, [CallerFilePath] string file = null)
        {
            Log(source, TraceEventType.Stop, $"离开方法 {member}", null, member, line, file);
        }

        /// <summary>
        /// 刷新日志
        /// </summary>
        /// <param name="source"></param>
        public static void FlushAll(this TraceSource source)
        {
            if ((source == null) || (source.Switch.Level == SourceLevels.Off))
            {
                return;
            }
            try
            {
                source.Flush();
            }
            catch// (Exception ex)
            {
                // ignored
            }
        }
    }
}