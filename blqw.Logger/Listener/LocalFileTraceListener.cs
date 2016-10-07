using System.Diagnostics;

namespace blqw.Logger
{
    /// <summary>
    /// 本地日志侦听器
    /// </summary>
    internal sealed class LocalFileTraceListener : TraceListenerBase
    {
        /// <summary>
        /// 创建一个队列
        /// </summary>
        /// <returns> </returns>
        protected override WriteQueue CreateQueue() => new WriteQueue(new LocalFileTraceWriter(Name), 1000 * 10000) { Logger = InnerLogger };

        /// <summary>
        /// 以线程为单位记录和输出日志 构造函数
        /// </summary>
        public LocalFileTraceListener() : base(true, null)
        {
        }
    }
}