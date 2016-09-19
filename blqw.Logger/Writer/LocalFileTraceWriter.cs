using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace blqw.Logger
{
    /// <summary>
    /// 本地日志写入器
    /// </summary>
    public sealed class LocalFileTraceWriter : IWriter
    {
        //单个文件容量阈值
        private const long DEFAULT_FILE_MAX_SIZE = 5*1024*1024; //兆

        /// <summary>
        /// 间隔号
        /// </summary>
        private static readonly byte[] line = Encoding.UTF8.GetBytes(new string('-', 70));

        private static readonly byte[] _LogIDBytes = Encoding.UTF8.GetBytes("LogID");
        private static readonly byte[] _TimeBytes = Encoding.UTF8.GetBytes("Time");
        private static readonly byte[] _LevelBytes = Encoding.UTF8.GetBytes("Level");
        private static readonly byte[] _ModuleBytes = Encoding.UTF8.GetBytes("Module");
        private static readonly byte[] _CategoryBytes = Encoding.UTF8.GetBytes("Category");
        private static readonly byte[] _MessageBytes = Encoding.UTF8.GetBytes("Message");
        private static readonly byte[] _ContentBytes = Encoding.UTF8.GetBytes("Content");
        private static readonly byte[] _CallstackBytes = Encoding.UTF8.GetBytes("Callstack");


        /// <summary>
        /// 文件写入器
        /// </summary>
        private FileWriter _writer;

        /// <summary>
        /// 初始化
        /// </summary>
        public LocalFileTraceWriter(string directory)
        {
            string dirPath;
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            if (Path.GetDirectoryName(baseDir)?.ToLowerInvariant() == "bin")
            {
                dirPath = Path.Combine(baseDir, "..", directory);
            }
            else
            {
                dirPath = Path.Combine(baseDir, directory);
            }
            if (Directory.Exists(dirPath) == false)
            {
                Directory.CreateDirectory(dirPath);
            }
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
        public string Name => nameof(LocalFileTraceWriter);

        public TraceSource Logger
        {
            get { return _writer?.Logger; }
            set
            {
                if (_writer != null)
                {
                    _writer.Logger = value;
                }
            }
        }

        /// <summary>
        /// 追加日志
        /// </summary>
        /// <param directory="item"> </param>
        public void Append(LogItem item)
        {
            if (item.IsFirst != item.IsLast)
            {
                return;
            }
            _writer.ChangeFileIfFull(); //如果文件满了就换一个
            WirteIfNotNull(_LogIDBytes, item.LogID.ToString()); 
            WirteIfNotNull(_TimeBytes, item.Time.ToString("yyyy-MM-dd HH:mm:ss.ffffff"));
            WirteIfNotNull(_LevelBytes, GetString(item.Level));
            WirteIfNotNull(_ModuleBytes, item.Module);
            WirteIfNotNull(_CategoryBytes, item.Category);
            WirteIfNotNull(_MessageBytes, item.Message);
            WirteIfNotNull(_ContentBytes, item.Content?.ToString());
            WirteIfNotNull(_CallstackBytes, item.Callstack);
            _writer.Append(line);
            _writer.AppendLine();
            _writer.AppendLine();
        }

        /// <summary>
        /// 刷新缓存
        /// </summary>
        public void Flush()
        {
            _writer.Flush();
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

        private void WirteIfNotNull(byte[] name, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }
            _writer.Append(name);
            for (var i = 9 - name.Length; i >= 0; i--)
            {
                _writer.AppendWhiteSpace();
            }
            _writer.AppendColon();
            _writer.AppendWhiteSpace();
            _writer.Append(value);
            _writer.AppendLine();
        }
    }
}