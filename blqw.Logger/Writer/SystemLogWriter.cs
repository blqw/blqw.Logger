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
            buffer?.Dispose();
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
        /// 队列最大长度
        /// </summary>
        public int QueueMaxCount => int.MaxValue;

        /// <summary>
        /// 写入器名称
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 日志跟踪器
        /// </summary>
        public TraceSource Logger { get; set; }

        private static readonly byte[] _ModuleBytes = Encoding.UTF8.GetBytes("Module");
        private static readonly byte[] _CategoryBytes = Encoding.UTF8.GetBytes("Category");
        private static readonly byte[] _ContentBytes = Encoding.UTF8.GetBytes("Content");
        private static readonly byte[] _CallstackBytes = Encoding.UTF8.GetBytes("Callstack");
        private static readonly byte _SpaceBytes = Encoding.UTF8.GetBytes(" ")[0];
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
            _buffer.Position = 0;
            Wirte(_ModuleBytes, item.Module);
            Wirte(_CategoryBytes, item.Category);
            Wirte(_ContentBytes, item.Content?.ToString());
            Wirte(_CallstackBytes, item.Callstack);
            var raw = _buffer.Position == 0 ? null : _buffer.ToArray();
            _buffer.Position = 0;
            if (item.Module == null && item.Message == null)
            {
                _logger.WriteEntry("无", GetEntryType(item.Level), 0, (short)item.Level, raw);
            }
            else
            {
                _logger.WriteEntry($"{item.Module}{Environment.NewLine}{item.Message}", GetEntryType(item.Level), 0, (short)item.Level, raw);
            }
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

        private MemoryStream _buffer = new MemoryStream();

        private void Wirte(byte[] name, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }
            _buffer.Write(name, 0, name.Length);
            for (var i = 9 - name.Length; i >= 0; i--)
            {
                _buffer.WriteByte(_SpaceBytes);
            }
            _buffer.WriteByte(_ColonBytes);
            _buffer.WriteByte(_SpaceBytes);
            var bytes = Encoding.UTF8.GetBytes(value);
            _buffer.Write(bytes, 0, bytes.Length);
            _buffer.WriteByte(_Newline[0]);
            if (_Newline.Length == 2) _buffer.WriteByte(_Newline[1]);
        }
        
        /// <summary>
        /// 刷新缓存
        /// </summary>
        public void Flush() => _buffer?.Flush();
    }
}
