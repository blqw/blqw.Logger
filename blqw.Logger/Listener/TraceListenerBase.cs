using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace blqw.Logger
{
    /// <summary>
    /// 监听器基础抽象类
    /// </summary>
    public abstract class TraceListenerBase : TraceListener
    {
        /// <summary>
        /// 为Name属性提供值
        /// </summary>
        private string _name;

        /// <summary>
        /// 写入队列
        /// </summary>
        private WriteQueue _queue;

        /// <summary>
        /// 以线程为单位记录和输出日志 构造函数
        /// </summary>
        /// <param name="initializeData"> 文件路径 </param>
        protected TraceListenerBase(bool isThreadSafe, string initializeData = null)
        {
            IsThreadSafe = isThreadSafe;
            InitializeData = initializeData;
            WritedLevel = SourceLevels.All;
        }

        /// <summary>
        /// 获取用于写入数据的队列,如果不存在则新建
        /// </summary>
        private WriteQueue Queue
        {
            get
            {
                if (_queue != null)
                {
                    return _queue;
                }
                Interlocked.MemoryBarrier();
                if (_queue == null)
                {
                    Interlocked.CompareExchange(ref _queue, CreateQueue(), null);
                }
                return _queue;
            }
        }

        /// <summary>
        /// 还没有输出的缓存数量
        /// </summary>
        public int CacheCount => Queue.Count;

        /// <summary>
        /// 队列是否正在休息
        /// </summary>
        public bool IsSleep => Queue.IsWriting == false;

        /// <summary>
        /// 获取当前线程中的日志跟踪等级
        /// </summary>
        protected virtual SourceLevels WritedLevel { get; }

        /// <summary>
        /// 初始化数据,在xml中定义,构造函数中传入
        /// </summary>
        protected string InitializeData { get; }

        /// <summary>
        /// 日志记录器
        /// </summary>
        public TraceSource InnerLogger { get; set; }

        /// <summary>
        /// 获取或设置此 <see cref="T:System.Diagnostics.TraceListener" /> 的名称。
        /// </summary>
        /// <exception cref="NotSupportedException" accessor="set"> 当前状态无法设置监听器名称 </exception>
        /// <exception cref="ArgumentNullException" accessor="set"> <see cref="Name" /> is <see langword="null" />. </exception>
        public override string Name
        {
            get { return _name; }
            set
            {
                if (_name != null)
                {
                    throw new NotSupportedException("当前状态无法设置监听器名称");
                }
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentNullException(nameof(Name), "监听器名称不能为空");
                }
                _name = value;
            }
        }

        /// <summary>
        /// 获取一个值，该值指示跟踪侦听器是否是线程安全的。
        /// </summary>
        /// <returns>
        /// 如果跟踪侦听器是线程安全的，则为 true；否则为 false。默认值为 false。
        /// </returns>
        public sealed override bool IsThreadSafe { get; }
        
        /// <summary>
        /// 创建一个队列
        /// </summary>
        /// <returns> </returns>
        protected abstract WriteQueue CreateQueue();

        /// <summary>
        /// 关闭监听器，清除所有已记录的日志。
        /// </summary>
        public override void Close()
        {
            InnerLogger?.Entry();
            LoggerContext.Clear();
            Trace.CorrelationManager.ActivityId = Guid.Empty;
            InnerLogger?.Exit();
        }

        /// <summary>
        /// 刷新输出缓冲区。
        /// </summary>
        public override void Flush()
        {
            InnerLogger?.Entry();
            try
            {
                if (Trace.AutoFlush)
                {
                    //判断当前方法是否是由于主动调用 .Flush() 触发的
                    if ("Flush".Equals(new StackFrame(1, false).GetMethod().Name, StringComparison.Ordinal))
                    {
                        return;
                    }
                }

                var context = new LoggerContext();
                if (context.Exists)
                {
                    InnerLogger?.Log(TraceEventType.Verbose, "EndLog");
                    Queue.Add(new LogItem
                    {
                        LogID = context.LogID,
                        Time = DateTime.Now,
                        Level = context.MinLevel,
                        LoggerName = Name,
                        IsLast = true
                    });
                }
                else
                {
                    InnerLogger?.Log(TraceEventType.Verbose, $"{nameof(LoggerContext)} is null");
                }
            }
            finally
            {
                InnerLogger?.Exit();
                InnerLogger?.FlushAll();
                LoggerContext.Clear();
            }
        }

        /// <summary>
        /// 追加日志到队列
        /// </summary>
        protected void AppendToQueue(
            TraceEventCache eventCache,
            TraceEventType eventType,
            int id,
            string title,
            object messageOrContent,
            object[] datas = null,
            [CallerMemberName] string member = null, [CallerLineNumber] int line = 0)
        {
            if (member != null)
            {
                // ReSharper disable ExplicitCallerInfoArgument
                InnerLogger?.Entry(member, line);
                // ReSharper restore ExplicitCallerInfoArgument
            }
            InnerLogger?.Entry();
            var context = new LoggerContext();
            if (context.IsNew)
            {
                InnerLogger?.Log(TraceEventType.Verbose, "StartLog");
                Queue.Add(new LogItem
                {
                    LogID = context.LogID,
                    Time = DateTime.Now,
                    LoggerName = Name,
                    Level = 0,
                    IsFirst = true
                });
            }
            context.MinLevel = eventType;

            if (messageOrContent is LogItem)
            {
                var item = (LogItem)messageOrContent;
                if (item.Level == 0)
                {
                    item.Level = eventType;
                }
                Queue.Add(item);
                InnerLogger?.Exit();
                return;
            }
            if (id != 0)
            {
                if (messageOrContent == null)
                {
                    messageOrContent = $"id={id}";
                }
                else if (messageOrContent is string)
                {
                    messageOrContent = $"id={id}; message={messageOrContent}";
                }
            }

            if (id != 0)
            {
                title = $"{id}:{title}";
            }



            if (datas == null)
            {
                var log = new LogItem
                {
                    LogID = context.LogID,
                    Time = DateTime.Now,
                    Level = eventType,
                    Title = title,
                    MessageOrContent = messageOrContent,
                    LoggerName = Name
                };

                if (TraceOutputOptions.HasFlag(TraceOptions.Callstack) || (eventType <= TraceEventType.Error))
                {
                    log.Callstack = eventCache?.Callstack ?? new StackTrace(2, true).ToString();
                }

                Queue.Add(log);
            }
            else
            {
                var length = datas.Length;
                var log = new LogItem
                {
                    LogID = context.LogID,
                    Time = DateTime.Now,
                    Level = eventType,
                    Title = title,
                    MessageOrContent = $"contents:{length}",
                    LoggerName = Name
                };
                Queue.Add(log);
                for (int i = 0; i < length; i++)
                {
                    log = new LogItem
                    {
                        LogID = context.LogID,
                        Time = DateTime.Now,
                        Level = eventType,
                        Title = i.ToString(),
                        MessageOrContent = datas[i],
                        LoggerName = Name
                    };
                    Queue.Add(log);
                }
            }


            InnerLogger?.Exit();
        }


        /// <summary>
        /// 根据当前事件类型判断是否需要输出日志
        /// </summary>
        protected virtual bool ShouldTrace(TraceEventCache cache, string source, TraceEventType eventType, int id,
            string formatOrMessage, object[] args, object data1, object[] data)
        {
            if (Filter != null)
            {
                return Filter.ShouldTrace(cache, source, eventType, id, formatOrMessage, args, data1, data);
            }
            var level = WritedLevel;
            if (level == SourceLevels.Off)
            {
                return false;
            }
            return ((int)level & (int)eventType) != 0;
        }


        /// <summary>
        /// 向侦听器特定的输出中写入跟踪信息、消息、相关活动标识和事件信息。
        /// </summary>
        /// <param name="eventCache">
        /// 包含当前进程 ID、线程 ID 以及堆栈跟踪信息的
        /// <see cref="T:System.Diagnostics.TraceEventCache" /> 对象。
        /// </param>
        /// <param name="source"> 标识输出时使用的名称，通常为生成跟踪事件的应用程序的名称。 </param>
        /// <param name="id"> 事件的数值标识符。 </param>
        /// <param name="message"> 要写入的消息。 </param>
        /// <param name="relatedActivityId"> 标识相关活动的 <see cref="T:System.Guid" /> 对象。 </param>
        public override void TraceTransfer(TraceEventCache eventCache, string source, int id, string message,
            Guid relatedActivityId)
        {
            if (ShouldTrace(eventCache, source, TraceEventType.Transfer, id, message, null, null, null))
            {
                AppendToQueue(eventCache, TraceEventType.Transfer, id, source, message);
            }
        }

        /// <summary>
        /// 向特定于侦听器的输出中写入跟踪信息、数据对象和事件信息。
        /// </summary>
        /// <param name="eventCache">
        /// 包含当前进程 ID、线程 ID 以及堆栈跟踪信息的
        /// <see cref="T:System.Diagnostics.TraceEventCache" />
        /// 对象。
        /// </param>
        /// <param name="source"> 标识输出时使用的名称，通常为生成跟踪事件的应用程序的名称。 </param>
        /// <param name="eventType">
        /// <see cref="T:System.Diagnostics.TraceEventType" />
        /// 值之一，指定引发跟踪的事件类型。
        /// </param>
        /// <param name="id"> 事件的数值标识符。 </param>
        /// <param name="data"> 要发出的跟踪数据。 </param>
        public override void TraceData(TraceEventCache eventCache, string source, TraceEventType eventType, int id,
            object data)
        {
            if (ShouldTrace(eventCache, source, eventType, id, null, null, data, null))
            {
                AppendToQueue(eventCache, eventType, id, source, data);
            }
        }

        /// <summary>
        /// 向特定于侦听器的输出中写入跟踪信息、消息和事件信息。
        /// </summary>
        /// <param name="eventCache"> 包含当前进程 ID、线程 ID 以及堆栈跟踪信息的 <see cref="T:System.Diagnostics.TraceEventCache" /> 对象。 </param>
        /// <param name="source"> 标识输出时使用的名称，通常为生成跟踪事件的应用程序的名称。 </param>
        /// <param name="eventType">
        /// <see cref="T:System.Diagnostics.TraceEventType" /> 值之一，指定引发跟踪的事件类型。
        /// </param>
        /// <param name="id"> 事件的数值标识符。 </param>
        /// <param name="message"> 要写入的消息。 </param>
        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id,
            string message)
        {
            if (ShouldTrace(eventCache, source, eventType, id, message, null, null, null))
            {
                AppendToQueue(eventCache, eventType, id, source, message);
            }
        }

        /// <summary>
        /// 向特定于侦听器的输出中写入跟踪信息、数据对象的数组和事件信息。
        /// </summary>
        /// <param name="eventCache">
        /// 包含当前进程 ID、线程 ID 以及堆栈跟踪信息的
        /// <see cref="T:System.Diagnostics.TraceEventCache" />
        /// 对象。
        /// </param>
        /// <param name="source"> 标识输出时使用的名称，通常为生成跟踪事件的应用程序的名称。 </param>
        /// <param name="eventType">
        /// <see cref="T:System.Diagnostics.TraceEventType" />
        /// 值之一，指定引发跟踪的事件类型。
        /// </param>
        /// <param name="id"> 事件的数值标识符。 </param>
        /// <param name="data"> 要作为数据发出的对象数组。 </param>
        public override void TraceData(TraceEventCache eventCache, string source, TraceEventType eventType, int id,
            params object[] data)
        {
            if (ShouldTrace(eventCache, source, eventType, id, null, null, null, data))
            {
                AppendToQueue(eventCache, eventType, id, source, data?.Length == 1 ? data[0] : data);
            }
        }

        /// <summary>
        /// 向特定于侦听器的输出写入跟踪和事件信息。
        /// </summary>
        /// <param name="eventCache">
        /// 包含当前进程 ID、线程 ID 以及堆栈跟踪信息的
        /// <see cref="T:System.Diagnostics.TraceEventCache" />
        /// 对象。
        /// </param>
        /// <param name="source"> 标识输出时使用的名称，通常为生成跟踪事件的应用程序的名称。 </param>
        /// <param name="eventType">
        /// <see cref="T:System.Diagnostics.TraceEventType" />
        /// 值之一，指定引发跟踪的事件类型。
        /// </param>
        /// <param name="id"> 事件的数值标识符。 </param>
        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id)
        {
            if (ShouldTrace(eventCache, source, eventType, id, null, null, null, null))
            {
                AppendToQueue(eventCache, eventType, id, source, null);
            }
        }

        /// <summary>
        /// 向特定于侦听器的输出中写入跟踪信息、格式化对象数组和事件信息。
        /// </summary>
        /// <param name="eventCache">
        /// 包含当前进程 ID、线程 ID 以及堆栈跟踪信息的
        /// <see cref="T:System.Diagnostics.TraceEventCache" />
        /// 对象。
        /// </param>
        /// <param name="source"> 标识输出时使用的名称，通常为生成跟踪事件的应用程序的名称。 </param>
        /// <param name="eventType">
        /// <see cref="T:System.Diagnostics.TraceEventType" />
        /// 值之一，指定引发跟踪的事件类型。
        /// </param>
        /// <param name="id"> 事件的数值标识符。 </param>
        /// <param name="format">
        /// 包含零个或多个格式项的格式字符串，这些项与
        /// <paramref name="args" />
        /// 数组中的对象相对应。
        /// </param>
        /// <param name="args"> 包含零个或多个要格式化的对象的 object 数组。 </param>
        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id,
            string format, params object[] args)
        {
            if (ShouldTrace(eventCache, source, eventType, id, format, args, null, null))
            {
                AppendToQueue(eventCache, eventType, id, source, string.Format(format, args));
            }
        }

        /// <summary>
        /// 实现 <see cref="T:System.Diagnostics.TraceListener" /> 类时，向所创建的侦听器发出错误信息。
        /// </summary>
        /// <param name="message">
        /// 要发出的消息。
        /// </param>
        public override void Fail(string message)
        {
            if (ShouldTrace(null, null, TraceEventType.Error, 0, message, null, null, null))
            {
                AppendToQueue(null, TraceEventType.Error, 0, null, message);
            }
        }

        /// <summary>
        /// 实现 <see cref="T:System.Diagnostics.TraceListener" /> 类时，向所创建的侦听器发出错误信息和详细错误信息。
        /// </summary>
        /// <param name="message">
        /// 要发出的消息。
        /// </param>
        /// <param name="detailMessage">
        /// 要发出的详细消息。
        /// </param>
        public override void Fail(string message, string detailMessage)
        {
            message = $"{message}{Environment.NewLine}{detailMessage}";
            if (ShouldTrace(null, "Fail", TraceEventType.Error, 0, message, null, null, null))
            {
                AppendToQueue(null, TraceEventType.Error, 0, "Fail", message);
            }
        }

        /// <summary>
        /// 向在该派生类中所创建的侦听器写入消息，后跟行结束符。
        /// </summary>
        /// <param name="message"> 要写入的消息。 </param>
        public override void WriteLine(string message)
        {
            if (ShouldTrace(null, "WriteLine", TraceEventType.Verbose, 0, message, null, null, null))
            {
                AppendToQueue(null, TraceEventType.Verbose, 0, "WriteLine", message);
            }
        }

        /// <summary>
        /// 向在该派生类中所创建的侦听器写入指定消息。
        /// </summary>
        /// <param name="message"> 要写入的消息。 </param>
        public override void Write(string message)
        {
            if (ShouldTrace(null, "Write", TraceEventType.Verbose, 0, message, null, null, null))
            {
                AppendToQueue(null, TraceEventType.Verbose, 0, "Write", message);
            }
        }

        /// <summary>
        /// 向所创建的侦听器写入对象。
        /// </summary>
        /// <param name="o"> 要为其编写完全限定类名的 <see cref="T:System.Object" />。 </param>
        public override void Write(object o)
        {
            var eventType = o is Exception ? TraceEventType.Error : TraceEventType.Verbose;
            if (ShouldTrace(null, "Write", eventType, 0, null, null, o, null))
            {
                AppendToQueue(null, eventType, 0, "Write", o);
            }
        }

        /// <summary>
        /// 向所创建的侦听器写入类别名称和消息。
        /// </summary>
        /// <param name="message">
        /// 要写入的消息。
        /// </param>
        /// <param name="category"> 用于组织输出的类别名称。 </param>
        public override void Write(string message, string category)
        {
            if (ShouldTrace(null, category, TraceEventType.Verbose, 0, message, null, null, null))
            {
                AppendToQueue(null, TraceEventType.Verbose, 0, category, message);
            }
        }

        /// <summary>
        /// 向所创建的侦听器写入类别名称和对象
        /// </summary>
        /// <param name="o"> 要为其编写完全限定类名的 <see cref="T:System.Object" />。 </param>
        /// <param name="category"> 用于组织输出的类别名称。 </param>
        public override void Write(object o, string category)
        {
            var eventType = o is Exception ? TraceEventType.Error : TraceEventType.Verbose;
            if (ShouldTrace(null, category, eventType, 0, null, null, o, null))
            {
                AppendToQueue(null, eventType, 0, category, o);
            }
        }

        /// <summary>
        /// 向所创建的侦听器写入对象，后跟行结束符。
        /// </summary>
        /// <param name="o"> 要为其编写完全限定类名的 <see cref="T:System.Object" />。 </param>
        public override void WriteLine(object o)
        {
            var eventType = o is Exception ? TraceEventType.Error : TraceEventType.Verbose;
            if (ShouldTrace(null, "WriteLine", eventType, 0, null, null, o, null))
            {
                AppendToQueue(null, eventType, 0, "WriteLine", o);
            }
        }

        /// <summary>
        /// 向所创建的侦听器写入类别名称和消息，后跟行结束符。
        /// </summary>
        /// <param name="message"> 要写入的消息。 </param>
        /// <param name="category"> 用于组织输出的类别名称。 </param>
        public override void WriteLine(string message, string category)
        {
            if (ShouldTrace(null, category, TraceEventType.Verbose, 0, message, null, null, null))
            {
                AppendToQueue(null, TraceEventType.Verbose, 0, category, message);
            }
        }

        /// <summary>
        /// 向所创建的侦听器写入类别名称和对象，后跟行结束符。
        /// </summary>
        /// <param name="o"> 要为其编写完全限定类名的 <see cref="T:System.Object" />。 </param>
        /// <param name="category"> 用于组织输出的类别名称。 </param>
        public override void WriteLine(object o, string category)
        {
            var eventType = o is Exception ? TraceEventType.Error : TraceEventType.Verbose;
            if (ShouldTrace(null, category, eventType, 0, null, null, o, null))
            {
                AppendToQueue(null, eventType, 0, category, o);
            }
        }
    }
}