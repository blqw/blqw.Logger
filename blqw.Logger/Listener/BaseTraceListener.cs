using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace blqw.Logger
{
    /// <summary>
    /// 监听器基础抽象类
    /// </summary>
    public abstract class BaseTraceListener : TraceListener
    {
        /// <summary>
        /// 是否已完成初始化操作
        /// </summary>
        private int _isInitialized;

        /// <summary>
        /// 为Name属性提供值
        /// </summary>
        private string _name;

        /// <summary>
        /// 写入队列
        /// </summary>
        private WriteQueue _queue;

        /// <summary>
        /// 初始化监听器
        /// </summary>
        protected BaseTraceListener()
            : this(null)
        {
        }

        /// <summary>
        /// 以线程为单位记录和输出日志 构造函数
        /// </summary>
        /// <param name="initializeData"> 文件路径 </param>
        protected BaseTraceListener(string initializeData)
        {
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
                    Interlocked.CompareExchange(ref _queue,
                        new WriteQueue(CreateWriter(), 0, default(TimeSpan), 0) { Logger = InnerLogger }, null);
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
        protected virtual TraceSource InnerLogger { get; } = null;

        /// <summary>
        /// 获取或设置此 <see cref="T:System.Diagnostics.TraceListener" /> 的名称。
        /// </summary>
        /// <exception cref="NotSupportedException" accessor="set"> 当前状态无法设置监听器名称 </exception>
        /// <exception cref="ArgumentNullException" accessor="set"> <see cref="Name"/> is <see langword="null" />. </exception>
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
        public override bool IsThreadSafe { get; } = true;

        /// <summary>
        /// 初始化方法
        /// </summary>
        private void InternalInitialize()
        {
            if (_isInitialized > 0)
            {
                return;
            }

            if (Interlocked.Exchange(ref _isInitialized, 1) == 1)
            {
                return;
            }

            Initialize();
        }

        /// <summary>
        /// 可用于子类重写的初始化方法
        /// </summary>
        protected virtual void Initialize()
        {
        }

        /// <summary>
        /// 创建一个写入器
        /// </summary>
        /// <returns> </returns>
        protected abstract IWriter CreateWriter();

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
                    Queue.Add(new LogItem
                    {
                        LogID = context.LogID,
                        Time = DateTime.Now,
                        Level = context.MinLevel,
                        Module = Name,
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
        /// <param name="logLevel"> 日志等级 </param>
        /// <param name="category"> 日志类别 </param>
        /// <param name="message"> 日志消息 </param>
        /// <param name="value"> 日志正文内容 </param>
        /// <param name="callstack"> 日志堆栈 </param>
        /// <param name="member"> 调用当前方法的对象 </param>
        /// <param name="line"> 调用当前方法的行号 </param>
        protected void AppendToQueue(TraceLevel logLevel, string category = null, string message = null,
            object value = null,
            string callstack = null, [CallerMemberName] string member = null, [CallerLineNumber] int line = 0)
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
                InnerLogger?.Log(TraceEventType.Verbose, "NewLog");
                Queue.Add(new LogItem
                {
                    LogID = context.LogID,
                    Time = DateTime.Now,
                    Module = Name,
                    IsFirst = true
                });
            }
            else
            {
                context.MinLevel = logLevel;
            }

            if (value is LogItem)
            {
                var item = (LogItem) value;
                if (item.Level == TraceLevel.Off)
                {
                    item.Level = logLevel;
                }
                Queue.Add(item);
                InnerLogger?.Exit();
                return;
            }

            object content;

            var ex = value as Exception;
            if (ex != null)
            {
                ex.Data["logid"] = context.LogID;
                category = "*" + category + "*";
                if (message == null)
                {
                    message = ex.Message;
                }
                content = ex;
            }
            else if ((value == null) || ReferenceEquals(message, value))
            {
                content = null;
            }
            else if ((message == null) && value is string)
            {
                message = value.ToString();
                content = null;
            }
            else
            {
                if (message == null)
                {
                    message = "无";
                }
                content = value;
            }

            if (TraceOutputOptions.HasFlag(TraceOptions.Callstack) ||
                ((callstack == null) && (logLevel == TraceLevel.Error)))
            {
                callstack = new StackTrace(2, true).ToString();
            }
            Queue.Add(new LogItem
            {
                LogID = context.LogID,
                Time = DateTime.Now,
                Level = logLevel,
                Category = category,
                Content = content,
                Callstack = callstack,
                Message = message,
                Module = Name
            });
            InnerLogger?.Exit();
        }

        /// <summary>
        /// 将当前事件类型转换为跟踪等级
        /// </summary>
        /// <param name="eventType"> 事件类型 </param>
        private static TraceLevel ConvertToLevel(TraceEventType eventType)
        {
            switch (eventType)
            {
                case TraceEventType.Critical:
                case TraceEventType.Error:
                    return TraceLevel.Error;
                case TraceEventType.Information:
                    return TraceLevel.Info;
                case TraceEventType.Warning:
                    return TraceLevel.Warning;
                case TraceEventType.Resume:
                case TraceEventType.Start:
                case TraceEventType.Stop:
                case TraceEventType.Suspend:
                case TraceEventType.Transfer:
                case TraceEventType.Verbose:
                    return TraceLevel.Verbose;
                default:
                    break;
            }

            if (eventType.HasFlag(TraceEventType.Error) || eventType.HasFlag(TraceEventType.Critical))
            {
                return TraceLevel.Error;
            }
            if (eventType.HasFlag(TraceEventType.Warning))
            {
                return TraceLevel.Warning;
            }
            if (eventType.HasFlag(TraceEventType.Information))
            {
                return TraceLevel.Info;
            }
            return TraceLevel.Verbose;
        }

        /// <summary>
        /// 根据当前事件类型判断是否需要输出日志
        /// </summary>
        /// <param name="eventType"> 事件类型 </param>
        /// <param name="value"> 日志正文内容 </param>
        /// <param name="traceLevel"> 返回日志等级 </param>
        protected bool ShouldTrace(TraceEventType eventType, object value, out TraceLevel traceLevel)
        {
            InternalInitialize();
            var level = WritedLevel;
            if (level == SourceLevels.Off)
            {
                traceLevel = TraceLevel.Off;
                return false;
            }

            if (value is Exception)
            {
                traceLevel = TraceLevel.Error;
                return WritedLevel.HasFlag(SourceLevels.Error);
            }

            traceLevel = ConvertToLevel(eventType);
            return ((int) level & (int) eventType) != 0;
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
            TraceLevel traceLevel;
            if (ShouldTrace(TraceEventType.Transfer, null, out traceLevel))
            {
                AppendToQueue(traceLevel, source, message, null,
                    traceLevel == TraceLevel.Error ? eventCache.Callstack : null);
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
            TraceLevel traceLevel;
            if (ShouldTrace(eventType, data, out traceLevel))
            {
                AppendToQueue(traceLevel, source, null, CheckOrWrapContent(id, data, eventType),
                    traceLevel == TraceLevel.Error ? eventCache.Callstack : null);
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
            TraceLevel traceLevel;
            if (ShouldTrace(eventType, null, out traceLevel))
            {
                AppendToQueue(traceLevel, source, message, null,
                    traceLevel == TraceLevel.Error ? eventCache.Callstack : null);
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
            TraceLevel traceLevel;
            if (ShouldTrace(eventType, null, out traceLevel))
            {
                AppendToQueue(traceLevel, source, null,
                    CheckOrWrapContent(id, string.Join(Environment.NewLine, data), eventType),
                    traceLevel == TraceLevel.Error ? eventCache.Callstack : null);
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
            TraceLevel traceLevel;
            if (ShouldTrace(eventType, null, out traceLevel))
            {
                AppendToQueue(traceLevel, source, null, null,
                    traceLevel == TraceLevel.Error ? eventCache.Callstack : null);
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
            TraceLevel traceLevel;
            if (ShouldTrace(eventType, null, out traceLevel))
            {
                AppendToQueue(traceLevel, source, string.Format(format, args), null,
                    traceLevel == TraceLevel.Error ? eventCache.Callstack : null);
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
            TraceLevel traceLevel;
            if (ShouldTrace(TraceEventType.Error, null, out traceLevel))
            {
                AppendToQueue(traceLevel, "*Fail*", message);
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
            TraceLevel traceLevel;
            if (ShouldTrace(TraceEventType.Error, null, out traceLevel))
            {
                AppendToQueue(traceLevel, "*Fail*", message, detailMessage);
            }
        }

        /// <summary>
        /// 向在该派生类中所创建的侦听器写入消息，后跟行结束符。
        /// </summary>
        /// <param name="message"> 要写入的消息。 </param>
        public override void WriteLine(string message)
        {
            TraceLevel traceLevel;
            if (ShouldTrace(TraceEventType.Verbose, null, out traceLevel))
            {
                AppendToQueue(traceLevel, null, message);
            }
        }

        /// <summary>
        /// 向在该派生类中所创建的侦听器写入指定消息。
        /// </summary>
        /// <param name="message"> 要写入的消息。 </param>
        public override void Write(string message)
        {
            TraceLevel traceLevel;
            if (ShouldTrace(TraceEventType.Verbose, null, out traceLevel))
            {
                AppendToQueue(traceLevel, null, message);
            }
        }

        /// <summary>
        /// 向所创建的侦听器写入对象。
        /// </summary>
        /// <param name="o"> 要为其编写完全限定类名的 <see cref="T:System.Object" />。 </param>
        public override void Write(object o)
        {
            TraceLevel traceLevel;
            if (ShouldTrace(TraceEventType.Verbose, o, out traceLevel))
            {
                AppendToQueue(traceLevel, null, null, o);
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
            TraceLevel traceLevel;
            if (ShouldTrace(TraceEventType.Verbose, null, out traceLevel))
            {
                AppendToQueue(traceLevel, category, message);
            }
        }

        /// <summary>
        /// 向所创建的侦听器写入类别名称和对象
        /// </summary>
        /// <param name="o"> 要为其编写完全限定类名的 <see cref="T:System.Object" />。 </param>
        /// <param name="category"> 用于组织输出的类别名称。 </param>
        public override void Write(object o, string category)
        {
            TraceLevel traceLevel;
            if (ShouldTrace(TraceEventType.Verbose, o, out traceLevel))
            {
                AppendToQueue(traceLevel, category, null, o);
            }
        }

        /// <summary>
        /// 向所创建的侦听器写入对象，后跟行结束符。
        /// </summary>
        /// <param name="o"> 要为其编写完全限定类名的 <see cref="T:System.Object" />。 </param>
        public override void WriteLine(object o)
        {
            TraceLevel traceLevel;
            if (ShouldTrace(TraceEventType.Verbose, o, out traceLevel))
            {
                AppendToQueue(traceLevel, null, null, o);
            }
        }

        /// <summary>
        /// 向所创建的侦听器写入类别名称和消息，后跟行结束符。
        /// </summary>
        /// <param name="message"> 要写入的消息。 </param>
        /// <param name="category"> 用于组织输出的类别名称。 </param>
        public override void WriteLine(string message, string category)
        {
            TraceLevel traceLevel;
            if (ShouldTrace(TraceEventType.Verbose, null, out traceLevel))
            {
                AppendToQueue(traceLevel, category, message);
            }
        }

        /// <summary>
        /// 向所创建的侦听器写入类别名称和对象，后跟行结束符。
        /// </summary>
        /// <param name="o"> 要为其编写完全限定类名的 <see cref="T:System.Object" />。 </param>
        /// <param name="category"> 用于组织输出的类别名称。 </param>
        public override void WriteLine(object o, string category)
        {
            TraceLevel traceLevel;
            if (ShouldTrace(TraceEventType.Verbose, o, out traceLevel))
            {
                AppendToQueue(traceLevel, category, null, o);
            }
        }

        /// <summary>
        /// 获取日志内容或包装日志内容
        /// </summary>
        /// <param name="id">事件的数值标识符</param>
        /// <param name="data">日志内容</param>
        /// <param name="eventType">指定引发跟踪的事件类型</param>
        /// <returns></returns>
        protected static object CheckOrWrapContent(int id, object data, TraceEventType eventType)
        {
            if (data is LogItem)
            {
                return data;
            }
            if (string.IsNullOrWhiteSpace(data as string))
            {
                return null;
            }
            var activityID = Trace.CorrelationManager.ActivityId;
            if (activityID == Guid.Empty)
            {
                return new { ID = id, EventType = eventType, Data = data };
            }
            return new { ID = id, EventType = eventType, Data = data, ActivityID = activityID };
        }
    }
}