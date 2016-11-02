using System;
using System.Diagnostics;
using System.Threading;

namespace blqw.Logger
{
    /// <summary>
    /// 用于将日志内容写入系统事件
    /// </summary>
    public sealed class SystemLogWriter : IWriter
    {
        private readonly string _applicationName;
        private EventLog _logger;
        /// <summary>
        /// 初始化实例
        /// </summary>
        /// <param name="applicationName"> 系统事件记录的应用程序名 </param>
        public SystemLogWriter(string applicationName)
        {
            _applicationName = applicationName;
        }

        /// <summary>
        /// 初始化写入器
        /// </summary>
        /// <param name="listener"> </param>
        public void Initialize(TraceListener listener)
        {
            var eventSource = listener.Name ?? "None";
            if (!EventLog.SourceExists(eventSource))
            {
                EventLog.CreateEventSource(eventSource, _applicationName);
            }
            Name = $"{_applicationName}.{eventSource}";
            _logger = new EventLog(_applicationName, ".", eventSource);
        }
        /// <summary>
        /// 执行与释放或重置非托管资源关联的应用程序定义的任务。
        /// </summary>
        public void Dispose() => Interlocked.Exchange(ref _logger, null)?.Dispose();

        /// <summary>
        /// 批处理最大数量
        /// </summary>
        int IWriter.BatchMaxCount => 0;

        /// <summary>
        /// 批处理最大等待时间
        /// </summary>
        TimeSpan IWriter.BatchMaxWait => TimeSpan.Zero;

        /// <summary>
        /// 写入器名称
        /// </summary>
        public string Name { get; private set; }


        /// <summary>
        /// 日志跟踪器
        /// </summary>
        public TraceSource Logger { get; set; }


        /// <summary>
        /// 追加日志
        /// </summary>
        /// <param name="item"> </param>
        /// <exception cref="ObjectDisposedException"> 对象已释放 </exception>
        public void Append(LogItem item)
        {
            if (_logger == null)
            {
                if (Name == null)
                {
                    throw new NotSupportedException("对象尚未初始化");
                }
                throw new ObjectDisposedException("对象已释放");
            }
            if (item.IsFirst != item.IsLast)
            {
                return;
            }
            _logger.WriteEntry(item.ToString(), GetEntryType(item.Level), item.TraceEventID, (short) item.Level);
        }


        /// <summary>
        /// 刷新缓存
        /// </summary>
        public void Flush()
        {
        }

        /// <summary>
        /// 获取跟踪侦听器支持的自定义特性。
        /// </summary>
        /// <returns> 为跟踪侦听器支持的自定义特性命名的字符串数组；或者如果没有自定义特性，则为 null。 </returns>
        public string[] GetSupportedAttributes() => null;

        //无需刷新

        private EventLogEntryType GetEntryType(TraceEventType type)
        {
            switch (type)
            {
                case TraceEventType.Critical:
                case TraceEventType.Error:
                    return EventLogEntryType.Error;
                case TraceEventType.Warning:
                    return EventLogEntryType.Warning;
                default:
                    return EventLogEntryType.Information;
            }
        }
    }
}