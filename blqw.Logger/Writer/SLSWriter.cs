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
    /// <summary>
    /// 用于以SLS的格式写入日志到文件
    /// </summary>
    internal class SLSWriter : IWriter, IFlushAsync
    {
        //单个文件容量阈值
        private const long DEFAULT_FILE_MAX_SIZE = 5*1024*1024; //兆

        /// <summary>
        /// 需要转义的字符
        /// </summary>
        private static readonly char[] _ReplaceChars = { '\n', '\r', '%', '"', ',', '\0' };

        private static readonly byte[] _CommaBytes = Encoding.UTF8.GetBytes("%2C");
        private static readonly byte[] _NewlineBytes = Encoding.UTF8.GetBytes("%0D%0A");
        private static readonly HashSet<byte> _SpaceKeysBytes = new HashSet<byte>(Encoding.UTF8.GetBytes("\r\n,\"\'"));
        private static readonly byte _SpaceBytes = Encoding.UTF8.GetBytes(" ")[0];

        private static readonly byte[] _AssemblyBytes = Encoding.UTF8.GetBytes("Assembly : ");
        private static readonly byte[] _MethodBytes = Encoding.UTF8.GetBytes("Method : ");
        private static readonly byte[] _PlusBytes = Encoding.UTF8.GetBytes(" + ");
        private static readonly byte[] _DetailBytes = Encoding.UTF8.GetBytes("Detail : ");
        private static readonly byte[] _DataBytes = Encoding.UTF8.GetBytes("Data : ");
        private static readonly byte[] _NullBytes = Encoding.UTF8.GetBytes("<null>");

        /// <summary>
        /// 队列
        /// </summary>
        private readonly ConcurrentQueue<List<LogItem>> Queue;

        /// <summary>
        /// 缓存
        /// </summary>
        private MemoryCache _cache;

        /// <summary>
        /// 异步刷新内容到文件的任务
        /// </summary>
        private Task _flushTask;

        private int _queueMaxCount = 100000;

        private FileWriter _writer;

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="dir"> 文件输出路径 </param>
        public SLSWriter(string dir, TraceSource logger)
        {
            Logger = logger;
            _cache = new MemoryCache(Guid.NewGuid().ToString());
            Name = dir;
            Queue = new ConcurrentQueue<List<LogItem>>();
            _writer = new FileWriter(dir, DEFAULT_FILE_MAX_SIZE);
        }

        /// <summary>
        /// 异步刷新
        /// </summary>
        /// <param name="token"> </param>
        /// <returns> </returns>
        public async Task FlushAsync(CancellationToken token)
        {
            if (_flushTask != null)
            {
                await _flushTask;
            }
            _flushTask = Task.Factory.StartNew(Flush, token);
        }

        public TraceSource Logger { get; set; }

        /// <summary>
        /// 批处理最大数量
        /// </summary>
        public int BatchMaxCount { get; set; } = 0;

        /// <summary>
        /// 批处理最大等待时间
        /// </summary>
        public TimeSpan BatchMaxWait { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// 队列最大长度
        /// </summary>
        public int QueueMaxCount
        {
            get { return _queueMaxCount; }
            set
            {
                if (_queueMaxCount <= 0)
                {
                    _queueMaxCount = 5000*10000; //默认队列 5000 万
                }
                else if (_queueMaxCount < 100*10000)
                {
                    _queueMaxCount = 100*10000; //最小队列 100 万
                }
                _queueMaxCount = value;
            }
        }

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
            var key = item.LogID.ToString("n"); //根据日志id从缓存中获取其他日志信息
            var list = _cache[key] as List<LogItem>;

            if (list == null)
            {
                if (item.IsLast)
                {
                    Logger?.Exit(); //如果缓存不存在,当前日志为最后一条,直接忽略
                    return;
                }
                list = new List<LogItem>();
                _cache.Add(key, list, new CacheItemPolicy
                {
                    AbsoluteExpiration = DateTimeOffset.Now.AddSeconds(90), //90秒超时后日志将被输出
                    RemovedCallback = RemovedCallback
                });
            }

            if (item.IsFirst && (list.Count > 0)) //日志标识为第一条,但缓存中已有日志
            {
                if (list[0].IsFirst == false) //如果缓存中的日志第一条不是日志头
                {
                    list.Insert(0, item);
                }
            }
            else if (item.IsLast)
            {
                if (list[0].IsFirst)
                {
                    list[0] = item; //替换日志头
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

                    _writer.Append(log.Time.ToString("HH:mm:ss.fff")).Append(_CommaBytes);
                    _writer.Append(log.Level.ToString()).Append(_CommaBytes);
                    _writer.Append(DoubleDecode(log.Category)).Append(_CommaBytes);
                    _writer.Append(DoubleDecode(log.Message)).Append(_CommaBytes);
                    _writer.Append(DoubleDecode(log.Callstack)).Append(_CommaBytes);
                    Write(log.Content);
                    _writer.Append(_NewlineBytes);
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
                        if (_SpaceKeysBytes.Contains(bytes[j]))
                        {
                            bytes[j] = _SpaceBytes;
                        }
                    }
                    _writer.AppendWhiteSpace();
                }
            }
            _writer.Flush();
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

        /// <summary>
        /// 写入日志正文
        /// </summary>
        /// <param name="content"></param>
        private void Write(object content)
        {
            var ex = content as Exception;
            if (ex == null)
            {
                _writer.Append(DoubleDecode(content?.ToString()));
                return;
            }

            _writer.Append(_AssemblyBytes).Append(_NewlineBytes);
            _writer.Append(DoubleDecode(ex.GetType().AssemblyQualifiedName));
            if (ex.TargetSite != null)
            {
                _writer.Append(_MethodBytes).Append(_NewlineBytes);
                _writer.Append(DoubleDecode(ex.TargetSite.ReflectedType?.ToString()));
                _writer.Append(_PlusBytes).Append(_NewlineBytes);
                _writer.Append(DoubleDecode(ex.TargetSite.ToString()));
            }
            _writer.Append(_DetailBytes).Append(_NewlineBytes);
            _writer.Append(DoubleDecode(ex.ToString()));
            if (ex.Data.Count == 0)
            {
                return;
            }
            _writer.Append(_DataBytes).Append(_NewlineBytes);
            foreach (DictionaryEntry item in ex.Data)
            {
                var value = item.Value;
                _writer.Append(DoubleDecode(item.Key?.ToString())).AppendWhiteSpace().AppendColon().AppendWhiteSpace();
                if (value == null)
                {
                    _writer.Append(_NullBytes);
                }
                else
                {
                    _writer.Append(DoubleDecode(value.ToString()));
                }
                _writer.Append(_NewlineBytes);
            }
        }
    }
}