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
        private const long DEFAULT_FILE_MAX_SIZE = 5 * 1024 * 1024; //兆

        /// <summary>
        /// 需要转义的字符
        /// </summary>
        private static readonly char[] _ReplaceChars = { '\n', '\r', '%', '"', ',', '\0' };

        private static readonly byte[] _CommaBytes = Encoding.UTF8.GetBytes("%2C");
        private static readonly byte[] _NewlineBytes = Encoding.UTF8.GetBytes("%0D%0A");
        private static readonly byte[] _NewlineBytes2 = Encoding.UTF8.GetBytes("%250D%250A");
        private static readonly HashSet<byte> _SpaceKeysBytes = new HashSet<byte>(Encoding.UTF8.GetBytes("\r\n,\"\'"));
        private static readonly byte _SpaceBytes = Encoding.UTF8.GetBytes(" ")[0];

        private static readonly byte[] _AssemblyBytes = Encoding.UTF8.GetBytes("Assembly : ");
        private static readonly byte[] _MethodBytes = Encoding.UTF8.GetBytes("Method : ");
        private static readonly byte[] _PlusBytes = Encoding.UTF8.GetBytes(" + ");
        private static readonly byte[] _DetailBytes = Encoding.UTF8.GetBytes("Detail : ");
        private static readonly byte[] _DataBytes = Encoding.UTF8.GetBytes("Data : ");
        private static readonly byte[] _NullBytes = Encoding.UTF8.GetBytes("<null>");

        private static readonly byte[] numberBytes = Encoding.UTF8.GetBytes("0123456789");
        private readonly SourceLevels _writedLevel;

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

        private FileWriter _writer;

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="dir"> 文件输出路径 </param>
        /// <param name="logger"> </param>
        /// <param name="writedLevel"> 写入日志的等级 </param>
        public SLSWriter(string dir, TraceSource logger, SourceLevels writedLevel)
        {
            _writedLevel = writedLevel;
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
            _flushTask = Task.Factory.StartNew(() =>
            {
                try
                {
                    Flush();
                }
                catch (Exception ex)
                {
                    Logger?.Error(ex);
                }
            }, token);
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
                if ((logs == null) || (logs.Count == 0))
                {
                    Logger?.Exit();
                    return;
                }
                var log = logs[0];
                if (((int)_writedLevel & (int)log.Level) == 0)
                {
                    continue;
                }

                _writer.ChangeFileIfFull();
                _writer.AppendLine();
                _writer.Append(log.Time.ToString("yyyy-MM-dd HH:mm:ss")).AppendComma();
                _writer.Append(log.LogID.ToString("n")).AppendComma();
                WriteLevel(log.Level);
                _writer.AppendComma();
                _writer.Append(log.LoggerName).AppendComma();
                for (int i = 1, length = logs.Count; i < length; i++)
                {
                    log = logs[i];
                    var message = log.MessageOrContent as string;
                    _writer.Append(log.Time.ToString("HH:mm:ss.fff")).Append(_CommaBytes);
                    WriteLevel(log.Level);
                    _writer.Append(_CommaBytes);
                    _writer.Append(DoubleDecode(log.Title)).Append(_CommaBytes);
                    _writer.Append(DoubleDecode(message ?? "无")).Append(_CommaBytes);
                    _writer.Append(DoubleDecode(log.Callstack)).Append(_CommaBytes);
                    if (message == null)
                    {
                        WriteContent(log.MessageOrContent);
                    }
                    _writer.Append(_NewlineBytes);
                }
                _writer.AppendComma();
                //追加索引
                for (int i = 1, length = logs.Count; i < length; i++)
                {
                    log = logs[i];
                    var message = log.MessageOrContent as string;
                    if (string.IsNullOrWhiteSpace(message))
                    {
                        continue;
                    }
                    var bytes = Encoding.UTF8.GetBytes(message);
                    for (int j = 0, l = bytes.Length; j < l; j++)
                    {
                        if (_SpaceKeysBytes.Contains(bytes[j]))
                        {
                            bytes[j] = _SpaceBytes;
                        }
                    }
                    _writer.Append(bytes);
                    _writer.AppendWhiteSpace();
                }
            }
            _writer.Flush();
            Logger?.Exit();
        }

        private void WriteLevel(TraceEventType logLevel)
        {
            switch (logLevel)
            {
                case TraceEventType.Critical:
                case TraceEventType.Error:
                    _writer.AppendByte(numberBytes[1]);
                    break;
                case TraceEventType.Warning:
                    _writer.AppendByte(numberBytes[2]);
                    break;
                case TraceEventType.Information:
                    _writer.AppendByte(numberBytes[3]);
                    break;
                case TraceEventType.Verbose:
                case TraceEventType.Start:
                case TraceEventType.Stop:
                case TraceEventType.Suspend:
                case TraceEventType.Resume:
                case TraceEventType.Transfer:
                default:
                    _writer.AppendByte(numberBytes[4]);
                    break;
            }
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
        /// <param name="content"> </param>
        private void WriteContent(object content)
        {
            var ex = content as Exception;
            if (ex == null)
            {
                _writer.Append(DoubleDecode(content?.ToString()));
                return;
            }

            _writer.Append(_AssemblyBytes).Append(_NewlineBytes2);
            _writer.Append(DoubleDecode(ex.GetType().AssemblyQualifiedName));
            if (ex.TargetSite != null)
            {
                _writer.Append(_MethodBytes).Append(_NewlineBytes2);
                _writer.Append(DoubleDecode(ex.TargetSite.ReflectedType?.ToString()));
                _writer.Append(_PlusBytes).Append(_NewlineBytes2);
                _writer.Append(DoubleDecode(ex.TargetSite.ToString()));
            }
            _writer.Append(_DetailBytes).Append(_NewlineBytes2);
            _writer.Append(DoubleDecode(ex.ToString()));
            if (ex.Data.Count == 0)
            {
                return;
            }
            _writer.Append(_DataBytes).Append(_NewlineBytes2);
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
                _writer.Append(_NewlineBytes2);
            }
        }
    }
}