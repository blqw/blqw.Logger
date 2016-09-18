using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Caching;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace blqw.Logger
{
    public sealed class LocalFileTraceWriter : IWriter
    {
        //单个文件容量阈值
        private const long DEFAULT_FILE_MAX_SIZE = 5 * 1024 * 1024; //兆

        /// <summary>
        /// 初始化
        /// </summary>
        public LocalFileTraceWriter()
        {
            string dirPath;
            if (Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory)?.ToLowerInvariant() == "bin")
            {
                dirPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"..\\{nameof(blqw)}.Logger-Logs");
            }
            else
            {
                dirPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{nameof(blqw)}.Logger-Logs");
            }
            if (Directory.Exists(dirPath) == false)
            {
                Directory.CreateDirectory(dirPath);
            }
            Name = nameof(LocalFileTraceWriter);
            _writer = new FileWriter(dirPath, DEFAULT_FILE_MAX_SIZE);
        }

        /// <summary>
        /// 执行与释放或重置非托管资源关联的应用程序定义的任务。
        /// </summary>
        public void Dispose()
        {
            var writer = Interlocked.Exchange(ref _writer, null);
            writer?.Dispose();
        }

        /// <summary>
        /// 批处理最大数量
        /// </summary>
        public int BatchMaxCount { get; } = 0;

        /// <summary>
        /// 批处理最大等待时间
        /// </summary>
        public TimeSpan BatchMaxWait { get; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// 队列最大长度
        /// </summary>
        public int QueueMaxCount { get; } = 1000000;

        /// <summary>
        /// 写入器名称
        /// </summary>
        public string Name { get; }

        public TraceSource Logger
        {
            get { return _writer?.Logger; }
            set { if (_writer != null) _writer.Logger = value; }
        }

        private static readonly byte[] line = Encoding.UTF8.GetBytes(new string('-', 70));

        private FileWriter _writer;

        /// <summary>
        /// 追加日志
        /// </summary>
        /// <param name="item"> </param>
        public void Append(LogItem item)
        {
            if (item.IsFirst != item.IsLast)
            {
                return;
            }
            _writer.ChangeFileIfFull();
            Wirte("LogID", item.LogID.ToString());
            Wirte("Time", item.Time.ToString("yyyy-MM-dd HH:mm:ss.ffffff"));
            Wirte("Level", GetString(item.Level));
            Wirte("Module", item.Module);
            Wirte("Category", item.Category);
            Wirte("Message", item.Message);
            Wirte("Content", item.Content?.ToString());
            Wirte("Callstack", item.Callstack);
            _writer.Append(line);
            _writer.AppendLine();
            _writer.AppendLine();

        }

        private string GetString(TraceLevel itemLevel)
        {
            switch (itemLevel)
            {
                case TraceLevel.Off:
                    return null;
                case TraceLevel.Error:
                    return "Error";
                case TraceLevel.Warning:
                    return "Warning";
                case TraceLevel.Info:
                    return "Info";
                case TraceLevel.Verbose:
                    return "Verbose";
                default:
                    return itemLevel.ToString();
            }
        }

        private void Wirte(string name, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }
            _writer.Append(name, Encoding.UTF8);
            for (var i = 10 - name.Length - 1; i >= 0; i--)
            {
                _writer.AppendWhiteSpace();
            }
            _writer.AppendColon();
            _writer.AppendWhiteSpace();
            _writer.Append(value,Encoding.UTF8);
            _writer.AppendLine();
        }

        /// <summary>
        /// 刷新缓存
        /// </summary>
        public void Flush()
        {

        }

    }
}