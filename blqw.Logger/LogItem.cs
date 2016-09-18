using System;
using System.Diagnostics;

namespace blqw.Logger
{
    /// <summary>
    /// 日志项
    /// </summary>
    public struct LogItem
    {
        public bool IsNull { get; private set; }
        public static readonly LogItem Null = new LogItem() { IsNull = true };
        /// <summary>
        /// 模块名
        /// </summary>
        public string Module { get; internal set; }
        /// <summary>
        /// 日志id
        /// </summary>
        public Guid LogID { get; internal set; }
        /// <summary>
        /// 日志等级
        /// </summary>
        public TraceLevel Level { get; internal set; }
        /// <summary>
        /// 日志标题
        /// </summary>
        public string Category { get; internal set; }
        /// <summary>
        /// 日志时间
        /// </summary>
        public DateTime Time { get; internal set; }
        /// <summary>
        /// 日志消息
        /// </summary>
        public string Message { get; internal set; }
        /// <summary>
        /// 日志堆栈
        /// </summary>
        public string Callstack { get; internal set; }
        /// <summary>
        /// 日志内容
        /// </summary>
        public object Content { get; internal set; }
        /// <summary>
        /// 第一条日志
        /// </summary>
        public bool IsFirst { get; internal set; }
        /// <summary>
        /// 最后一条日志
        /// </summary>
        public bool IsLast { get; internal set; }
    }
}