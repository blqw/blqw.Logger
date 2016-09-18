using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Caching;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace blqw.Logger
{
    internal class SLSWriter : IWriter
    {
        //单个文件容量阈值
        private const long DEFAULT_FILE_MAX_SIZE = 5 * 1024 * 1024; //兆

        /// <summary>
        /// 需要转义的字符
        /// </summary>
        private static readonly char[] _ReplaceChars = { '\n', '\r', '%', '"', ',', '\0' };

        /// <summary>
        /// 缓存
        /// </summary>
        private MemoryCache _cache;

        /// <summary>
        /// 队列
        /// </summary>
        private readonly ConcurrentQueue<List<LogItem>> Queue;

        private FileWriter _writer;

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="dir"> 文件输出路径 </param>
        public SLSWriter(string dir, TraceSource logger)
        {
            Logger = logger;
            _cache = new MemoryCache("LogCache:" + dir);
            Name = dir;
            Queue = new ConcurrentQueue<List<LogItem>>();
            _writer = new FileWriter(dir, DEFAULT_FILE_MAX_SIZE);
        }

        public TraceSource Logger { get; set; }

        /// <summary>
        /// 批处理最大数量
        /// </summary>
        public int BatchMaxCount { get; set; } = 1;

        /// <summary>
        /// 批处理最大等待时间
        /// </summary>
        public TimeSpan BatchMaxWait { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// 队列最大长度
        /// </summary>
        public int QueueMaxCount { get; set; } = 100000;

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
            var list = _cache[key] as List<LogItem>;

            if (list == null)
            {
                if (item.IsLast)
                {
                    Logger?.Exit();
                    return;
                }
                list = new List<LogItem>();
                _cache.Add(key, list, new CacheItemPolicy
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
                _cache.Remove(key);
            }
            else
            {
                list.Add(item);
            }
            Logger?.Exit();
        }

        /// <summary> 执行与释放或重置非托管资源关联的应用程序定义的任务。 </summary>
        /// <filterpriority> 2 </filterpriority>
        public void Dispose()
        {
            var cache = Interlocked.Exchange(ref _cache, null);
            cache?.Dispose();
            var writer = Interlocked.Exchange(ref _writer, null);
            writer?.Dispose();
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

        private static readonly byte[] _Code_Comma = Encoding.UTF8.GetBytes("%2C");
        private static readonly byte[] _Code_Newline = Encoding.UTF8.GetBytes("%0D%0A");
        private static readonly HashSet<byte> _Code_SpaceKeys = new HashSet<byte>(Encoding.UTF8.GetBytes("\r\n,\"\'"));
        private static readonly byte _Code_Space = Encoding.UTF8.GetBytes(" ")[0];

        private static readonly byte[] _Code_Assembly = Encoding.UTF8.GetBytes("Assembly : ");
        private static readonly byte[] _Code_Method = Encoding.UTF8.GetBytes("Method : ");
        private static readonly byte[] _Code_Plus = Encoding.UTF8.GetBytes(" + ");
        private static readonly byte[] _Code_Detail = Encoding.UTF8.GetBytes("Detail : ");
        private static readonly byte[] _Code_Data = Encoding.UTF8.GetBytes("Data : ");
        private static readonly byte[] _Code_Null = Encoding.UTF8.GetBytes("<null>");


        private void Write(object content)
        {
            var ex = content as Exception;
            if (ex == null)
            {
                _writer.Append(DoubleDecode(content?.ToString()));
                return;
            }

            _writer.Append(_Code_Assembly).Append(_Code_Newline);
            _writer.Append(DoubleDecode(ex.GetType().AssemblyQualifiedName));
            if (ex.TargetSite != null)
            {
                _writer.Append(_Code_Method).Append(_Code_Newline);
                _writer.Append(DoubleDecode(ex.TargetSite.ReflectedType?.ToString()));
                _writer.Append(_Code_Plus).Append(_Code_Newline);
                _writer.Append(DoubleDecode(ex.TargetSite.ToString()));
            }
            _writer.Append(_Code_Detail).Append(_Code_Newline);
            _writer.Append(DoubleDecode(ex.ToString()));
            if (ex.Data.Count == 0)
            {
                return;
            }
            _writer.Append(_Code_Data).Append(_Code_Newline);
            foreach (DictionaryEntry item in ex.Data)
            {
                var value = item.Value;
                _writer.Append(DoubleDecode(item.Key?.ToString())).AppendWhiteSpace().AppendColon().AppendWhiteSpace();
                if (value == null)
                {
                    _writer.Append(_Code_Null);
                }
                else
                {
                    _writer.Append(DoubleDecode(value.ToString()));
                }
                _writer.Append(_Code_Newline);
            }
        }

        /// <summary>
        /// 刷新缓存
        /// </summary>
        /// <exception cref="IOException"> 发生了 I/O 错误。- 或 -另一个线程可能已导致操作系统的文件句柄位置发生意外更改。 </exception>
        /// <exception cref="ObjectDisposedException"> 流已关闭。 </exception>
        /// <exception cref="NotSupportedException"> 当前流实例不支持写入。 </exception>
        public void Flush()
        {
            Logger?.Entry();
            List<LogItem> logs;
            while (Queue.TryDequeue(out logs))
            {
                _writer.ChangeFileIfFull();
                if ((logs == null) || (logs.Count == 0))
                {
                    Logger?.Exit();
                    return;
                }

                _writer.AppendLine();
                var log = logs[0];
                _writer.Append(log.Time.ToString("yyyy-MM-dd HH:mm:ss")).AppendComma();
                _writer.Append(log.LogID.ToString("n")).AppendComma();
                _writer.Append(log.Level.ToString()).AppendComma();
                _writer.Append(log.Module).AppendComma();
                for (int i = 1, length = logs.Count; i < length; i++)
                {
                    log = logs[i];

                    _writer.Append(log.Time.ToString("HH:mm:ss.fff")).Append(_Code_Comma);
                    _writer.Append(log.Level.ToString()).Append(_Code_Comma);
                    _writer.Append(DoubleDecode(log.Category)).Append(_Code_Comma);
                    _writer.Append(DoubleDecode(log.Message)).Append(_Code_Comma);
                    _writer.Append(DoubleDecode(log.Callstack)).Append(_Code_Comma);
                    Write(log.Content);
                    _writer.Append(_Code_Newline);
                }
                _writer.AppendComma();
                //追加索引
                for (int i = 1, length = logs.Count; i < length; i++)
                {
                    log = logs[i];
                    if (string.IsNullOrWhiteSpace(log.Message))
                    {
                        continue;
                    }
                    var bytes = Encoding.UTF8.GetBytes(log.Message);
                    for (int j = 0, l = bytes.Length; j < l; j++)
                    {
                        if (_Code_SpaceKeys.Contains(bytes[j]))
                        {
                            bytes[j] = _Code_Space;
                        }
                    }
                    _writer.AppendWhiteSpace();
                }
            }
            _writer.Flush();
            Logger?.Exit();
        }

    }
}