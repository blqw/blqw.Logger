using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using blqw.Logger.Writer;

namespace blqw.Logger
{
    /// <summary>
    /// 写入队列
    /// </summary>
    internal class WriteQueue
    {
        /// <summary>
        /// 默认批处理最大数量
        /// </summary>
        public const int DEFAULT_BATCH_MAX_COUNT = 2000; //条

        /// <summary>
        /// 默认批处理等待时间
        /// </summary>
        public const int DEFAULT_BATCH_WAIT_MILLISECONDS = 5 * 1000; //毫秒

        /// <summary>
        /// 默认队列最大长度
        /// </summary>
        public const int DEFAULT_QUEUE_MAX_COUNT = 100000; //条

        public TraceSource Logger { get; set; } = TraceSourceExtensions.InternalSource;

        /// <summary>
        /// 批处理最大数量
        /// </summary>
        private readonly int _batchMaxCount;

        /// <summary>
        /// 批处理等待时间
        /// </summary>
        private readonly int _batchWaitMilliseconds;

        /// <summary>
        /// 队列允许最大长度
        /// </summary>
        private readonly int _queueMaxCount;

        /// <summary>
        /// 写入任务队列
        /// </summary>
        private readonly ConcurrentQueue<LogItem> _items = new ConcurrentQueue<LogItem>();

        /// <summary>
        /// 写入器
        /// </summary>
        private readonly IWriter _writer;

        /// <summary>
        /// 最后刷新时间
        /// </summary>
        private DateTime _lastFlushTime;

        /// <summary>
        /// 初始化写队列
        /// </summary>
        /// <param name="writer"> </param>
        /// <param name="batchMaxCount"> </param>
        /// <param name="batchMaxWait"> </param>
        /// <param name="queueMaxCount"> </param>
        /// <exception cref="ArgumentNullException"><paramref name="writer"/> is <see langword="null" />.</exception>
        public WriteQueue(IWriter writer, int batchMaxCount, TimeSpan batchMaxWait, int queueMaxCount)
        {
            Logger?.Entry();
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            _writer = writer;
            _task = new SingletonTask(WriteAsync, writer.Logger);
            _batchMaxCount = GetNotDefault(batchMaxCount, _writer.BatchMaxCount, DEFAULT_BATCH_MAX_COUNT);
            _batchWaitMilliseconds = GetNotDefault((int)batchMaxWait.TotalMilliseconds, (int)_writer.BatchMaxWait.TotalMilliseconds, DEFAULT_BATCH_WAIT_MILLISECONDS);
            _queueMaxCount = GetNotDefault(queueMaxCount, _writer.QueueMaxCount, DEFAULT_QUEUE_MAX_COUNT);
            Logger?.Exit();

        }

        private static T GetNotDefault<T>(T value1, T value2, T value3)
        {
            if (Equals(value1, default(T)) == false)
                return value1;
            if (Equals(value2, default(T)) == false)
                return value2;
            return value3;
        }

        /// <summary>
        /// 写日志线程是否正在工作
        /// </summary>
        public bool IsWriting => _task.IsRunning;

        /// <summary>
        /// 队列中的数目
        /// </summary>
        public int Count => _items.Count;

        private readonly SingletonTask _task;
        /// <summary>
        /// 追加写入对象
        /// </summary>
        /// <param name="item"> 文件写入任务 </param>
        public void Add(LogItem item)
        {
            Logger?.Entry();
            if (_items.Count >= _queueMaxCount && item.IsLast == false)
            {
                Logger?.Log(TraceEventType.Warning, "日志队列超过最大数量,日志被抛弃", "数量:" + _items.Count);
                Logger?.Exit();
                return;
            }
            _items.Enqueue(item);
            _task.RunIfStop();
            Logger?.Exit();
        }

        /// <summary>
        /// 异步写入任务
        /// </summary>
        /// <returns> </returns>
        private async Task WriteAsync(ActivityTokenSource tokenSource)
        {
            Logger?.Entry();
            _lastFlushTime = DateTime.Now;
            var batch = 0; //当前批处数量
            while (true)
            {
                tokenSource.Activity(); //表示一次活动,如果任务已经取消,这里抛出异常
                LogItem log;
                //队列中没有对象
                if (_items.TryDequeue(out log) == false)
                {
                    if (batch == 0)
                    {
                        Logger?.Exit();
                        return;
                    }
                    log = LogItem.Null;
                }

                var runtime = (DateTime.Now - _lastFlushTime).TotalMilliseconds;
                if (log.IsNull)
                {
                    //判断时间边界
                    if (runtime < _batchWaitMilliseconds)
                    {
                        Thread.Sleep(500);
                        Logger?.Log(TraceEventType.Verbose, "Sleep(500)");
                        continue;
                    }
                }
                else
                {
                    var @async = _writer as IAppendAsync;
                    if (@async == null)
                        _writer.Append(log);
                    else
                    {
                        var task = @async.AppendAsync(log, tokenSource.Token);
                        if (task != null)
                        {
                            await task.ConfigureAwait(false);
                        }
                    }
                    batch++;
                    //判断批处理上限 ,判断时间边界
                    if (batch < _batchMaxCount && runtime < _batchWaitMilliseconds)
                    {
                        continue;
                    }
                }


                //调用写入器的刷新方法
                {
                    var @async = _writer as IFlushAsync;
                    if (@async == null)
                        _writer.Flush();
                    else
                    {
                        var task = @async.FlushAsync(tokenSource.Token);
                        if (task != null)
                        {
                            await task.ConfigureAwait(false);
                        }
                    }
                    _lastFlushTime = DateTime.Now; //最后刷新时间
                    batch = 0; //重置批数量
                    Logger?.Log(TraceEventType.Verbose, "Flush Complete", $"LastFlushTime: {_lastFlushTime:yyyy-MM-dd HH:mm:ss}");
                }
            }
        }
    }
}