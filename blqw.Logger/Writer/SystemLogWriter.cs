using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace blqw.Logger
{
    public sealed class SystemLogWriter : IWriter
    {
        private EventLog _logger;

        public SystemLogWriter(string logName, string source)
        {
            if (!EventLog.SourceExists(source))
            {
                EventLog.CreateEventSource(source, logName);
            }
            _logger = new EventLog(logName, ".", source);
        }
        /// <summary>执行与释放或重置非托管资源关联的应用程序定义的任务。</summary>
        public void Dispose()
        {
            var logger = Interlocked.Exchange(ref _logger, null);
            logger?.Dispose();
            var buffer = Interlocked.Exchange(ref _buffer, null);
            buffer?.Clear();
        }

        /// <summary>
        /// 批处理最大数量
        /// </summary>
        public int BatchMaxCount { get; }

        /// <summary>
        /// 批处理最大等待时间
        /// </summary>
        public TimeSpan BatchMaxWait { get; }
        
        /// <summary>
        /// 写入器名称
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 日志跟踪器
        /// </summary>
        public TraceSource Logger { get; set; }
        
        /// <summary>
        /// 冒号(:)
        /// </summary>
        private static readonly byte _ColonBytes = Encoding.UTF8.GetBytes(":")[0];

        /// <summary>
        /// 新行(<seealso cref="Environment.NewLine" />)
        /// </summary>
        private static readonly byte[] _Newline = Encoding.UTF8.GetBytes(Environment.NewLine);

        /// <summary>
        /// 追加日志
        /// </summary>
        /// <param name="item"> </param>
        /// <exception cref="ObjectDisposedException">对象已释放</exception>
        public void Append(LogItem item)
        {
            if (_logger == null)
            {
                throw new ObjectDisposedException("对象已释放");
            }
            if (item.IsFirst != item.IsLast)
            {
                return;
            }
            _buffer.Clear();
            Wirte("LogID", item.LogID.ToString("n"));
            Wirte("Time", item.Time.ToString("yyyy-MM-dd HH:mm:ss.ffffff"));
            Wirte("Level", item.Level.ToString());
            Wirte("Module", item.Module);
            Wirte("Category", item.Category);
            Wirte("Message", item.Message);
            Wirte("Content", item.Content?.ToString());
            Wirte("Callstack", item.Callstack);
            var txt = _buffer.ToString();
            _buffer.Clear();
            _logger.WriteEntry(txt, GetEntryType(item.Level), 0, 0);
        }

        private EventLogEntryType GetEntryType(TraceLevel level)
        {
            switch (level)
            {
                case TraceLevel.Error:
                    return EventLogEntryType.Error;
                case TraceLevel.Warning:
                    return EventLogEntryType.Warning;
                default:
                    return EventLogEntryType.Information;
            }
        }

        private StringBuilder _buffer = new StringBuilder();

        private void Wirte(string name, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }
            _buffer.Append(name);
            _buffer.Append(' ', 9 - name.Length);
            _buffer.Append(':');
            _buffer.Append(' ');
            _buffer.Append(value);
            _buffer.AppendLine();
        }

        /// <summary>
        /// 刷新缓存
        /// </summary>
        public void Flush() { }
    }
}
