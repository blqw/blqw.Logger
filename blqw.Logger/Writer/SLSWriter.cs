using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Caching;
using System.Text;
using System.Threading.Tasks;

namespace blqw.Logger
{
    internal class SLSWriter : IWriter
    {
        //单个文件容量阈值
        private const long FILE_MAX_SIZE = 5*1024*1024; //兆

        /// <summary>
        /// 需要转义的字符
        /// </summary>
        private static readonly char[] _ReplaceChars = { '\n', '\r', '%', '"', ',', '\0' };

        /// <summary>
        /// 缓冲区
        /// </summary>
        [ThreadStatic]
        private static StringBuilder Buffer;

        /// <summary>
        /// 下一次删除文件的时间
        /// </summary>
        private static DateTime _NextDeleteTime;

        /// <summary>
        /// 字符缓冲1
        /// </summary>
        private readonly StringBuilder _Buffer;

        /// <summary>
        /// 字符缓冲2
        /// </summary>
        private readonly StringBuilder _IndexerBuffer;

        /// <summary>
        /// 缓存
        /// </summary>
        private readonly MemoryCache Cache;

        /// <summary>
        /// 队列
        /// </summary>
        private readonly ConcurrentQueue<List<LogItem>> Queue;
        
        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="dir"> 文件输出路径 </param>
        public SLSWriter(string dir, InternalLogger logger)
        {
            Logger = logger;
            Cache = new MemoryCache("LogCache:" + dir);
            Name = dir;
            _Buffer = new StringBuilder();
            _IndexerBuffer = new StringBuilder();
            Queue = new ConcurrentQueue<List<LogItem>>();
        }

        public InternalLogger Logger { get; }

        /// <summary>
        /// 批处理最大数量
        /// </summary>
        public int BatchMaxCount { get; set; } = 0;

        /// <summary>
        /// 批处理最大等待时间
        /// </summary>
        public TimeSpan BatchMaxWait { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// 队列最大长度
        /// </summary>
        public int QueueMaxCount { get; set; } = 0;

        /// <summary>
        /// 写入器名称
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 追加日志
        /// </summary>
        /// <param name="item"> </param>
        public void Append(LogItem item)
        {
            Logger?.Entry();
            var key = item.LogID.ToString("n");
            var list = Cache[key] as List<LogItem>;

            if (list == null)
            {
                if (item.IsLast)
                {
                    Logger?.Exit();
                    return;
                }
                list = new List<LogItem>();
                Cache.Add(key, list, new CacheItemPolicy
                {
                    AbsoluteExpiration = DateTimeOffset.Now.AddSeconds(90),
                    RemovedCallback = RemovedCallback
                });
            }

            if (item.IsFirst && (list.Count > 0))
            {
                if (list[0].IsFirst == false)
                {
                    list.Insert(0, item);
                }
            }
            else if (item.IsLast)
            {
                if (list[0].IsFirst)
                {
                    list[0] = item;
                }
                else
                {
                    list.Insert(0, item);
                }
                Cache.Remove(key);
            }
            else
            {
                list.Add(item);
            }
            Logger?.Exit();
        }

        /// <summary> 执行与释放或重置非托管资源关联的应用程序定义的任务。 </summary>
        /// <filterpriority> 2 </filterpriority>
        public void Dispose() => Cache?.Dispose();

        /// <summary>
        /// 刷新缓存
        /// </summary>
        public void Flush()
        {
            Logger?.Entry();
            try
            {
                List<LogItem> logs;
                while (Queue.TryDequeue(out logs))
                {
                    if ((logs == null) || (logs.Count == 0))
                    {
                        Logger?.Exit();
                        return;
                    }

                    _Buffer.AppendLine();
                    var log = logs[0];
                    _Buffer.Append(log.Time.ToString("yyyy-MM-dd HH:mm:ss"));
                    _Buffer.Append(',');
                    _Buffer.Append(log.LogID.ToString("n"));
                    _Buffer.Append(',');
                    _Buffer.Append((int) log.Level);
                    _Buffer.Append(',');
                    _Buffer.Append(log.Module);
                    _Buffer.Append(',');
                    for (int i = 1, length = logs.Count; i < length; i++)
                    {
                        log = logs[i];

                        _Buffer.Append(log.Time.ToString("HH:mm:ss.fff"));
                        _Buffer.Append("%2C"); //这是一个逗号
                        _Buffer.Append((int) log.Level);
                        _Buffer.Append("%2C"); //这是一个逗号
                        _Buffer.Append(DoubleDecode(log.Category));
                        _Buffer.Append("%2C"); //这是一个逗号
                        _Buffer.Append(DoubleDecode(log.Message));
                        _Buffer.Append("%2C"); //这是一个逗号
                        _Buffer.Append(DoubleDecode(log.Callstack));
                        _Buffer.Append("%2C"); //这是一个逗号
                        _Buffer.Append(DoubleDecode(GetString(log.Content)));
                        _Buffer.Append("%0D%0A"); //这是一个换行
                        _IndexerBuffer.Append(
                            log.Message?.Replace('\n', ' ')
                                .Replace('\r', ' ')
                                .Replace(',', ' ')
                                .Replace('"', ' ')
                                .Replace('\'', ' '));
                        _IndexerBuffer.Append(" ");
                    }
                    _Buffer.Append(',');
                    _Buffer.Append(_IndexerBuffer);
                    _IndexerBuffer.Clear();
                }
                WriteFile(Name, _Buffer);
            }
            finally
            {
                _Buffer.Clear();
                _IndexerBuffer.Clear();
            }
            Logger?.Exit();
        }

        /// <summary>
        /// 缓存被移除事件
        /// </summary>
        /// <param name="arguments"> </param>
        private void RemovedCallback(CacheEntryRemovedArguments arguments)
        {
            Logger?.Entry();
            var list = arguments?.CacheItem?.Value as List<LogItem>;
            if (list != null)
            {
                Queue.Enqueue(list);
            }
            Logger?.Exit();
        }

        /// <summary>
        /// 写入文件
        /// </summary>
        /// <param name="path"> </param>
        /// <param name="buffer"> </param>
        private void WriteFile(string path, StringBuilder buffer)
        {
            Logger?.Entry();
            if (buffer.Length == 0)
            {
                Logger?.Entry();
                return;
            }
            path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, string.Format(path, DateTime.Now));
            if (_NextDeleteTime < DateTime.Now)
            {
                _NextDeleteTime = DateTime.Today.AddDays(1);
                Task.Run(() => Delete(path, 2));
            }
            var max = GetMaxFileNumber(path);
            var file = GetFile(path, max);
            if (file.Directory?.Exists == false)
            {
                file.Directory.Create();
            }
            File.AppendAllText(file.FullName, buffer.ToString());
            Logger?.Exit();
        }

        /// <summary>
        /// 获取一个可以写入数据的文件
        /// </summary>
        /// <param name="path"> 文件路径 </param>
        /// <param name="fileNumber"> 文件编号 </param>
        /// <returns> </returns>
        private static FileInfo GetFile(string path, int fileNumber)
        {
            while (true)
            {
                var file = new FileInfo(Path.Combine(path, fileNumber + ".log"));
                if (file.Exists == false)
                {
                    return file;
                }
                if (file.Length < FILE_MAX_SIZE) //文件大小没有超过限制
                {
                    return file;
                }
                fileNumber = fileNumber + 1;
            }
        }

        /// <summary>
        /// 获取文件的最大编号
        /// </summary>
        /// <param name="path"> </param>
        /// <returns> </returns>
        private static int GetMaxFileNumber(string path)
        {
            if (!Directory.Exists(path))
            {
                return 0;
            }

            var number = 0;
            var files = Directory.GetFiles(path, "*.log", SearchOption.TopDirectoryOnly);
            foreach (var f in files)
            {
                int i;
                if (int.TryParse(Path.GetFileNameWithoutExtension(f), out i) && (i > number))
                {
                    number = i;
                }
            }
            return number;
        }

        /// <summary>
        /// 二次转义
        /// </summary>
        /// <param name="text"> </param>
        /// <returns> </returns>
        private static string DoubleDecode(string text)
        {
            if (text == null)
            {
                return "";
            }
            if (text.IndexOfAny(_ReplaceChars) >= 0)
            {
                return text
                    .Replace("%", "%2525") //2次转义
                    .Replace(",", "%252C")
                    .Replace("\n", "%250A")
                    .Replace("\r", "%250D")
                    .Replace("\0", "%2500")
                    .Replace("\"", "%2522");
            }
            return text;
        }

        private static string GetString(object content)
        {
            var ex = content as Exception;
            if (ex == null)
            {
                return content?.ToString();
            }

            try
            {
                if (Buffer == null)
                {
                    Buffer = new StringBuilder();
                }
                Buffer.Append("Assembly : ")
                    .AppendLine(ex.GetType().AssemblyQualifiedName);
                if (ex.TargetSite != null)
                {
                    Buffer.Append("Method : ")
                        .Append(ex.TargetSite.ReflectedType)
                        .Append(" + ")
                        .AppendLine(ex.TargetSite.ToString());
                }
                Buffer.AppendLine("Detail : ")
                    .AppendLine(ex.ToString());
                if (ex.Data.Count == 0)
                {
                    return Buffer.ToString();
                }
                Buffer.AppendLine("Data : ");
                foreach (DictionaryEntry item in ex.Data)
                {
                    var value = item.Value;
                    Buffer.Append(item.Key)
                        .Append(" : ");
                    if (value == null)
                    {
                        Buffer.Append("<null>");
                    }
                    else
                    {
                        Buffer.Append(value);
                    }
                    Buffer.AppendLine();
                }
                return Buffer.ToString();
            }
            finally
            {
                Buffer?.Clear();
            }
        }

        public void Delete(string path, int days)
        {
            Logger?.Entry();
            var root = Directory.GetParent(path);
            if (root.Exists == false)
            {
                Logger?.Exit();
                return;
            }
            var time = DateTime.Today.AddDays(-days);
            foreach (var dir in root.GetDirectories())
            {
                if (dir.LastWriteTime <= time)
                {
                    try
                    {
                        dir.Delete(true);
                    }
                    catch (Exception ex)
                    {
                        Logger?.Error(ex, $"删除({dir.FullName})下文件失败");
                    }
                }
            }
            Logger?.Exit();
        }
    }
}