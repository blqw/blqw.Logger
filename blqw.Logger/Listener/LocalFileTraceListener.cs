using System.Diagnostics;

namespace blqw.Logger
{
    /// <summary>
    /// 本地日志侦听器
    /// </summary>
    internal sealed class LocalFileTraceListener : BaseTraceListener
    {
        /// <summary>
        /// 创建一个写入器
        /// </summary>
        /// <returns> </returns>
        protected override IWriter CreateWriter() => new LocalFileTraceWriter(Name);

        /// <summary>
        /// 向特定于侦听器的输出中写入跟踪信息、数据对象和事件信息。
        /// </summary>
        /// <param name="eventCache">
        /// 包含当前进程 ID、线程 ID 以及堆栈跟踪信息的
        /// <see cref="T:System.Diagnostics.TraceEventCache" />
        /// 对象。
        /// </param>
        /// <param name="source"> 标识输出时使用的名称，通常为生成跟踪事件的应用程序的名称。 </param>
        /// <param name="eventType">
        /// <see cref="T:System.Diagnostics.TraceEventType" />
        /// 值之一，指定引发跟踪的事件类型。
        /// </param>
        /// <param name="id"> 事件的数值标识符。 </param>
        /// <param name="data"> 要发出的跟踪数据。 </param>
        public override void TraceData(TraceEventCache eventCache, string source, TraceEventType eventType, int id,
            object data)
        {
            TraceLevel traceLevel;
            if (ShouldTrace(eventType, data, out traceLevel))
            {
                AppendToQueue(traceLevel, source, eventType.GetString(), data);
            }
        }
    }
}