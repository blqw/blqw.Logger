using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Caching;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace blqw.Logger
{
    /// <summary>
    /// 用于以SLS的格式写入日志到文件
    /// </summary>
    internal sealed class SLSWriter : FileWriter, IFlushAsync
    {
        #region Public Constructors

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="dir"> 文件输出路径 </param>
        /// <param name="writedLevel"> 写入日志的等级 </param>
        public SLSWriter(string dir, SourceLevels writedLevel)
        {
            _writedLevel = writedLevel;
            _cache = new MemoryCache(Guid.NewGuid().ToString());
            DirectoryPath = dir;
            _queue = new ConcurrentQueue<List<LogItem>>();
            FileMaxSize = DEFAULT_FILE_MAX_SIZE;
            BatchMaxWait = TimeSpan.FromSeconds(5);
        }

        #endregion Public Constructors
        
        static class UTF8Bytes
        {
            public static byte[] Assembly { get; } = Encoding.UTF8.GetBytes("Assembly : ");
            public static byte[] Comma { get; } = Encoding.UTF8.GetBytes("%2C");
            public static byte[] Star { get; } = Encoding.UTF8.GetBytes("*");
            public static byte[] Data { get; } = Encoding.UTF8.GetBytes("Data : ");
            public static byte[] Detail { get; } = Encoding.UTF8.GetBytes("Detail : ");
            public static byte[] Method { get; } = Encoding.UTF8.GetBytes("Method : ");
            public static byte[] Newline { get; } = Encoding.UTF8.GetBytes("%0D%0A");
            public static byte[] Newline2 { get; } = Encoding.UTF8.GetBytes("%250D%250A");
            public static byte[] Null { get; } = Encoding.UTF8.GetBytes("<null>");
            public static byte[] Plus { get; } = Encoding.UTF8.GetBytes(" + ");
            public static byte Space { get; } = Encoding.UTF8.GetBytes(" ")[0];
            private static readonly byte[] _Number = Encoding.UTF8.GetBytes("0123456789");
            public static byte NumberToByte(int i) => _Number[i];
            private static readonly HashSet<byte> _InvalidChar = new HashSet<byte>(Encoding.UTF8.GetBytes("\r\n,\"\'"));
            public static bool IsInvalidChars(byte b) => _InvalidChar.Contains(b);
        }

        #region Private Fields

        //单个文件容量阈值
        private const long DEFAULT_FILE_MAX_SIZE = 5*1024*1024; //兆

        /// <summary>
        /// 需要转义的字符
        /// </summary>
        private static readonly char[] _ReplaceChars = { '\n', '\r', '%', '"', ',', '\0' };

        private readonly SourceLevels _writedLevel;

        /// <summary>
        /// 队列
        /// </summary>
        private readonly ConcurrentQueue<List<LogItem>> _queue;

        /// <summary>
        /// 缓存
        /// </summary>
        private MemoryCache _cache;

        /// <summary>
        /// 异步刷新内容到文件的任务
        /// </summary>
        private Task _flushTask;

        #endregion Private Fields

        #region Public Properties

        /// <summary>
        /// 写入器名称
        /// </summary>
        public override string Name => nameof(SLSWriter);

        #endregion Public Properties

        #region Public Methods

        /// <summary>
        /// 追加日志
        /// </summary>
        /// <param name="item"> </param>
        public override void Append(LogItem item)
        {
            Logger?.Entry();
            var key = item.LogGroupID.ToString("n"); //根据日志id从缓存中获取其他日志信息
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

        /// <summary>
        /// 执行与释放或重置非托管资源关联的应用程序定义的任务。
        /// </summary>
        /// <filterpriority> 2 </filterpriority>
        public override void Dispose()
        {
            var cache = Interlocked.Exchange(ref _cache, null);
            cache?.Dispose();
            base.Dispose();
        }

        /// <summary>
        /// 刷新缓存
        /// </summary>
        /// <exception cref="IOException"> 发生了 I/O 错误。- 或 -另一个线程可能已导致操作系统的文件句柄位置发生意外更改。 </exception>
        /// <exception cref="ObjectDisposedException"> 流已关闭。 </exception>
        /// <exception cref="NotSupportedException"> 当前流实例不支持写入。 </exception>
        public override void Flush()
        {
            Logger?.Entry();
            List<LogItem> logs;
            while (_queue.TryDequeue(out logs))
            {
                if ((logs == null) || (logs.Count == 0))
                {
                    Logger?.Exit();
                    return;
                }
                var log = logs[0];
                if (((int) _writedLevel & (int) log.Level) == 0)
                {
                    continue;
                }

                ChangeFileIfFull();
                AppendLine();
                base.Append(log.Time.ToString("yyyy-MM-dd HH:mm:ss"));
                base.AppendComma();
                base.Append(log.LogGroupID.ToString("n"));
                base.AppendComma();
                WriteLevel(log.Level);
                AppendComma();
                base.Append(log.Listener.Name);
                base.AppendComma();
                for (int i = 1, length = logs.Count; i < length; i++)
                {
                    log = logs[i];
                    var message = log.Message;
                    base.Append(log.Time.ToString("HH:mm:ss.fff"));
                    base.Append(UTF8Bytes.Comma);
                    WriteLevel(log.Level);
                    base.Append(UTF8Bytes.Comma);
                    if (log.Level > TraceEventType.Warning || string.IsNullOrEmpty(log.Category))
                    {
                        base.Append(DoubleDecode(log.Category ?? log.Source)); //没有分类时,显示来源
                    }
                    else if (log.Category[0] == '*' && log.Category[log.Category.Length - 1] == '*')
                    {
                        base.Append(DoubleDecode(log.Category)); //没有分类时,显示来源
                    }
                    else
                    {
                        base.Append(UTF8Bytes.Star);
                        base.Append(DoubleDecode(log.Category)); //没有分类时,显示来源
                        base.Append(UTF8Bytes.Star);
                    }
                    base.Append(UTF8Bytes.Comma);
                    base.Append(DoubleDecode(message ?? "无"));
                    base.Append(UTF8Bytes.Comma);
                    WriteCallstack(log);
                    base.Append(UTF8Bytes.Comma);
                    WriteContent(log.Content);
                    base.Append(UTF8Bytes.Newline);
                }
                AppendComma();
                //追加索引
                for (int i = 1, length = logs.Count; i < length; i++)
                {
                    log = logs[i];
                    var message = log.Message;
                    if (string.IsNullOrWhiteSpace(message))
                    {
                        continue;
                    }
                    var bytes = Encoding.UTF8.GetBytes(message);
                    for (int j = 0, l = bytes.Length; j < l; j++)
                    {
                        if (UTF8Bytes.IsInvalidChars(bytes[j]))
                        {
                            bytes[j] = UTF8Bytes.Space;
                        }
                    }
                    Append(bytes);
                    AppendWhiteSpace();
                }
            }
            base.Flush();
            Logger?.Exit();
        }

        private void WriteCallstack(LogItem log)
        {
            if (log.File != null)
            {
                base.Append(DoubleDecode(log.File));
                base.Append("%252C"); //逗号
                base.Append(DoubleDecode(log.Message));
                base.Append(":");
                base.Append(log.LineNumber.ToString());
            }
            if (log.Callstack != null)
            {
                if (log.File!=null)
                {
                    base.Append(UTF8Bytes.Newline2);
                }
                base.Append(DoubleDecode(log.Callstack));
            }
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

        #endregion Public Methods

        #region Private Methods

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
        /// 缓存被移除事件
        /// </summary>
        /// <param name="arguments"> </param>
        private void RemovedCallback(CacheEntryRemovedArguments arguments)
        {
            Logger?.Entry();
            var list = arguments?.CacheItem?.Value as List<LogItem>;
            if (list != null)
            {
                _queue.Enqueue(list);
            }
            Logger?.Exit();
        }

        /// <summary>
        /// 写入日志正文
        /// </summary>
        /// <param name="content"> </param>
        private void WriteContent(object content)
        {
            if (content == null)
            {
                return;
            }
            var ex = content as Exception;
            if (ex != null)
            {
                base.Append(UTF8Bytes.Assembly);
                base.Append(UTF8Bytes.Newline2);
                base.Append(DoubleDecode(ex.GetType().AssemblyQualifiedName));
                if (ex.TargetSite != null)
                {
                    base.Append(UTF8Bytes.Method);
                    base.Append(UTF8Bytes.Newline2);
                    base.Append(DoubleDecode(ex.TargetSite.ReflectedType?.ToString()));
                    base.Append(UTF8Bytes.Plus);
                    base.Append(UTF8Bytes.Newline2);
                    base.Append(DoubleDecode(ex.TargetSite.ToString()));
                }
                base.Append(UTF8Bytes.Detail);
                base.Append(UTF8Bytes.Newline2);
                base.Append(DoubleDecode(ex.ToString()));
                if (ex.Data.Count == 0)
                {
                    return;
                }
                base.Append(UTF8Bytes.Data);
                base.Append(UTF8Bytes.Newline2);
                foreach (DictionaryEntry item in ex.Data)
                {
                    var value = item.Value;
                    base.Append(DoubleDecode(item.Key?.ToString()));
                    base.AppendWhiteSpace();
                    base.AppendColon();
                    base.AppendWhiteSpace();
                    if (value == null)
                    {
                        base.Append(UTF8Bytes.Null);
                    }
                    else
                    {
                        base.Append(DoubleDecode(value.ToString()));
                    }
                    base.Append(UTF8Bytes.Newline2);
                }
            }
            var ee = (content as IEnumerable)?.GetEnumerator()
                     ?? content as IEnumerator;
            if (ee != null)
            {

                base.Append(UTF8Bytes.Assembly);
                base.AppendColon();
                base.Append(DoubleDecode(ee.GetType().AssemblyQualifiedName));
                var i = 0;
                while (ee.MoveNext())
                {
                    base.Append(UTF8Bytes.Newline2);
                    base.Append(i.ToString());
                    base.AppendColon();
                    base.AppendWhiteSpace();
                    base.Append(DoubleDecode(ee.Current.ToString()));
                    i++;
                }
                return;
            }
            base.Append(DoubleDecode(content.ToString()));
            return;
            
        }

        private void WriteLevel(TraceEventType logLevel)
        {
            switch (logLevel)
            {
                case TraceEventType.Critical:
                case TraceEventType.Error:
                    AppendByte(UTF8Bytes.NumberToByte(1));
                    break;

                case TraceEventType.Warning:
                    AppendByte(UTF8Bytes.NumberToByte(2));
                    break;

                case TraceEventType.Information:
                    AppendByte(UTF8Bytes.NumberToByte(3));
                    break;

                case TraceEventType.Verbose:
                case TraceEventType.Start:
                case TraceEventType.Stop:
                case TraceEventType.Suspend:
                case TraceEventType.Resume:
                case TraceEventType.Transfer:
                default:
                    AppendByte(UTF8Bytes.NumberToByte(4));
                    break;
            }
        }

        #endregion Private Methods
    }
}