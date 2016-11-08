using System;
using System.Diagnostics;

namespace blqw.Logger
{
    /// <summary>
    /// 监听器基础抽象类 
    /// </summary>
    public abstract class TraceListenerBase : TraceListener
    {

        /// <summary>
        /// 以线程为单位记录和输出日志 构造函数 
        /// </summary>
        /// <param name="isThreadSafe"> 指示跟踪侦听器是否是线程安全的 </param>
        /// <param name="initializeData"> 文件路径 </param>
        protected TraceListenerBase(bool isThreadSafe, string initializeData = null)
        {
            IsThreadSafe = isThreadSafe;
            InitializeData = initializeData;
            WritedLevel = SourceLevels.All;
            _initialize = OnInitialize;
        }

        /// <summary>
        /// 如果当前实例尚未执行过 <see cref="Initialize" /> 则立即触发该方法,否则无任何作用 
        /// </summary>
        protected void OnInitialize()
        {
            if (_initialize == null)
            {
                return;
            }
            lock (this)
            {
                if (_initialize == null)
                {
                    return;
                }
                Initialize();
                _queue = CreateQueue();
                _initialize = null;
            }
        }

        /// <summary>
        /// 追加日志到队列 
        /// </summary>
        protected internal void AppendToQueue(LogItem log, TraceEventCache eventCache = null)
        {
            if (log.IsNull)
            {
                return;
            }

            InnerLogger?.Entry();
            var context = new LoggerContext();
            log.Listener = this;
            log.LogGroupID = context.ContextID;
            context.MinLevel = log.Level;

            if (context.IsNew)
            {
                InnerLogger?.Log(TraceEventType.Verbose, "StartLog");
                _queue.Add(new LogItem(0) { Listener = this, IsFirst = true, LogGroupID = context.ContextID });
            }

            if (log.Content is LogItem)
            {
                var nlog = (LogItem) log.Content;
                nlog.Listener = this;
                if (nlog.LogGroupID == Guid.Empty)
                {
                    nlog.LogGroupID = log.LogGroupID;
                }
                context.MinLevel = nlog.Level;
                _queue.Add(nlog);
            }
            else
            {
                if ((log.Callstack == null) && (TraceOutputOptions.HasFlag(TraceOptions.Callstack) || (log.Level <= TraceEventType.Error))) //是否需要获取堆栈信息
                {
                    log.Callstack = eventCache?.Callstack ?? new StackTrace(2, true).ToString();
                }

                _queue.Add(log);
            }

            InnerLogger?.Exit();
        }

        /// <summary>
        /// 初始化方法 
        /// </summary>
        private Action _initialize;

        /// <summary>
        /// 为Name属性提供值 
        /// </summary>
        private string _name;

        /// <summary>
        /// 写入队列 
        /// </summary>
        private WriteQueue _queue;

        /// <summary>
        /// 还没有输出的缓存数量 
        /// </summary>
        public int CacheCount => _queue.Count;

        /// <summary>
        /// 日志记录器 
        /// </summary>
        public TraceSource InnerLogger { get; set; }

        /// <summary>
        /// 队列是否正在休息 
        /// </summary>
        public bool IsSleep => _queue.IsWriting == false;

        /// <summary>
        /// 获取一个值，该值指示跟踪侦听器是否是线程安全的。 
        /// </summary>
        /// <returns> 如果跟踪侦听器是线程安全的，则为 true；否则为 false。默认值为 false。 </returns>
        public sealed override bool IsThreadSafe { get; }

        /// <summary>
        /// 获取或设置此 <see cref="T:System.Diagnostics.TraceListener" /> 的名称。 
        /// </summary>
        /// <exception cref="NotSupportedException" accessor="set"> 当前状态无法设置监听器名称 </exception>
        /// <exception cref="ArgumentNullException" accessor="set">
        /// <see cref="Name" /> is <see langword="null" />.
        /// </exception>
        public override string Name
        {
            get { return _name ?? AppDomain.CurrentDomain.FriendlyName; } //当没有设置名称的时候使用系统默认名称
            set
            {
                if ((_name != null) && (_name != value))
                {
                    throw new NotSupportedException("已经设置了监听器的名称");
                }
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentNullException(nameof(Name), "监听器名称不能为空");
                }
                _name = value;
            }
        }

        /// <summary>
        /// 初始化数据,在xml中定义,构造函数中传入 
        /// </summary>
        protected string InitializeData { get; }

        /// <summary>
        /// 获取当前线程中的日志跟踪等级 
        /// </summary>
        protected virtual SourceLevels WritedLevel { get; private set; }

        /// <summary>
        /// 关闭监听器，清除所有已记录的日志。 
        /// </summary>
        public override void Close()
        {
            _initialize?.Invoke();
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
            _initialize?.Invoke();
            InnerLogger?.Entry();
            try
            {
                var context = new LoggerContext();
                if (context.Exists)
                {
                    InnerLogger?.Log(TraceEventType.Verbose, "EndLog");
                    _queue.Add(new LogItem(context.MinLevel) { IsLast = true, Listener = this, LogGroupID = context.ContextID });
                }
                else
                {
                    InnerLogger?.Log(TraceEventType.Verbose, $"{nameof(LoggerContext)} is null");
                }
            }
            catch (Exception ex)
            {
                InnerLogger.Error(ex, "Flush 发生错误");
            }
            finally
            {
                InnerLogger?.Exit();
                InnerLogger?.FlushAll();
                LoggerContext.Clear();
            }
        }

        private enum MyLevel
        {
            All = -1,
            Off = 0,
            Critical = 1,
            Error = 3,
            Warning = 7,
            Information = 15,
            Verbose = 31,
            ActivityTracing = 65280,
            Start = 256,
            Stop = 512,
            Suspend = 1024,
            Resume = 2048,
            Transfer = 4096
        }

        /// <summary>
        /// 组合一个或多个数组中的元素 
        /// </summary>
        /// <param name="strings"></param>
        /// <returns></returns>
        protected static string[] UnionArray(params string[][] strings)
        {
            if ((strings == null) || (strings.Length == 0))
            {
                return new string[0];
            }
            if (strings.Length == 1)
            {
                return strings[0] ?? new string[0];
            }
            var length = 0;
            for (var i = 0; i < strings.Length; i++)
            {
                length += strings[i]?.Length ?? 0;
            }
            var result = new string[length];
            var index = 0;
            for (var i = 0; i < strings.Length; i++)
            {
                strings[i]?.CopyTo(result, index);
                index += strings[i]?.Length ?? 0;
            }
            return result;
        }

        /// <summary>
        /// 创建一个队列 
        /// </summary>
        /// <returns></returns>
        protected abstract WriteQueue CreateQueue();

        /// <summary>
        /// 获取跟踪侦听器支持的自定义特性。 
        /// </summary>
        /// <returns> 为跟踪侦听器支持的自定义特性命名的字符串数组；或者如果没有自定义特性，则为 null。 </returns>
        protected override string[] GetSupportedAttributes() => new[] { "level" };

        /// <summary>
        /// 初始化当前实例 
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"> level属性值无效 </exception>
        protected virtual void Initialize()
        {
            MyLevel level;
            if (Enum.TryParse(Attributes["level"] ?? "All", true, out level) == false)
            {
                throw new ArgumentOutOfRangeException(nameof(level), "level属性值无效,请参考: System.Diagnostics.SourceLevels 与 System.Diagnostics.TraceEventType");
            }
            WritedLevel = (SourceLevels) level;
        }

        /// <summary>
        /// 根据当前事件类型判断是否需要输出日志 
        /// </summary>
        protected virtual bool ShouldTrace(TraceEventCache cache, string source, TraceEventType eventType, int id,
            string formatOrMessage, object[] args, object data1, object[] data)
        {
            InnerLogger?.Entry();
            if (Filter != null)
            {
                return Filter.ShouldTrace(cache, source, eventType, id, formatOrMessage, args, data1, data);
            }
            var level = WritedLevel;
            if (level == SourceLevels.Off)
            {
                return false;
            }
            var b = ((int) level & (int) eventType) != 0;
            InnerLogger?.Return(b.ToString());
            return b;
        }

        #region WriteLog

        /// <summary>
        /// 实现 <see cref="T:System.Diagnostics.TraceListener" /> 类时，向所创建的侦听器发出错误信息。 
        /// </summary>
        /// <param name="message"> 要发出的消息。 </param>
        public override void Fail(string message)
        {
            _initialize?.Invoke();
            InnerLogger?.Entry();
            if (ShouldTrace(null, null, TraceEventType.Error, 0, message, null, null, null))
            {
                AppendToQueue(new LogItem(TraceEventType.Error) { Message = message });
            }
            InnerLogger?.Exit();
        }

        /// <summary>
        /// 实现 <see cref="T:System.Diagnostics.TraceListener" /> 类时，向所创建的侦听器发出错误信息和详细错误信息。 
        /// </summary>
        /// <param name="message"> 要发出的消息。 </param>
        /// <param name="detailMessage"> 要发出的详细消息。 </param>
        public override void Fail(string message, string detailMessage)
        {
            _initialize?.Invoke();
            InnerLogger?.Entry();
            if (ShouldTrace(null, null, TraceEventType.Error, 0, message, null, detailMessage, null))
            {
                AppendToQueue(new LogItem(TraceEventType.Error) { Message = message, Content = detailMessage });
            }
            InnerLogger?.Exit();
        }

        /// <summary>
        /// 向特定于侦听器的输出中写入跟踪信息、数据对象和事件信息。 
        /// </summary>
        /// <param name="eventCache">
        /// 包含当前进程 ID、线程 ID 以及堆栈跟踪信息的 <see cref="T:System.Diagnostics.TraceEventCache" /> 对象。
        /// </param>
        /// <param name="source"> 标识输出时使用的名称，通常为生成跟踪事件的应用程序的名称。 </param>
        /// <param name="eventType"> <see cref="T:System.Diagnostics.TraceEventType" /> 值之一，指定引发跟踪的事件类型。 </param>
        /// <param name="id"> 事件的数值标识符。 </param>
        /// <param name="data"> 要发出的跟踪数据。 </param>
        public override void TraceData(TraceEventCache eventCache, string source, TraceEventType eventType, int id,
            object data)
        {
            _initialize?.Invoke();
            InnerLogger?.Entry();
            if (ShouldTrace(eventCache, source, eventType, id, null, null, data, null))
            {
                AppendToQueue(new LogItem(eventType) { TraceEventID = id, Source = source, Content = data }, eventCache);
            }
            InnerLogger?.Exit();
        }

        /// <summary>
        /// 向特定于侦听器的输出中写入跟踪信息、数据对象的数组和事件信息。 
        /// </summary>
        /// <param name="eventCache">
        /// 包含当前进程 ID、线程 ID 以及堆栈跟踪信息的 <see cref="T:System.Diagnostics.TraceEventCache" /> 对象。
        /// </param>
        /// <param name="source"> 标识输出时使用的名称，通常为生成跟踪事件的应用程序的名称。 </param>
        /// <param name="eventType"> <see cref="T:System.Diagnostics.TraceEventType" /> 值之一，指定引发跟踪的事件类型。 </param>
        /// <param name="id"> 事件的数值标识符。 </param>
        /// <param name="data"> 要作为数据发出的对象数组。 </param>
        public override void TraceData(TraceEventCache eventCache, string source, TraceEventType eventType, int id,
            params object[] data)
        {
            _initialize?.Invoke();
            InnerLogger?.Entry();
            object data1 = null;
            if (data?.Length == 1)
            {
                data1 = data[0];
                data = null;
            }
            if (ShouldTrace(eventCache, source, eventType, id, null, null, data1, data))
            {
                AppendToQueue(new LogItem(eventType) { TraceEventID = id, Source = source, Content = data1 ?? data }, eventCache);
            }
            InnerLogger?.Exit();
        }

        /// <summary>
        /// 向特定于侦听器的输出中写入跟踪信息、消息和事件信息。 
        /// </summary>
        /// <param name="eventCache">
        /// 包含当前进程 ID、线程 ID 以及堆栈跟踪信息的 <see cref="T:System.Diagnostics.TraceEventCache" /> 对象。
        /// </param>
        /// <param name="source"> 标识输出时使用的名称，通常为生成跟踪事件的应用程序的名称。 </param>
        /// <param name="eventType"> <see cref="T:System.Diagnostics.TraceEventType" /> 值之一，指定引发跟踪的事件类型。 </param>
        /// <param name="id"> 事件的数值标识符。 </param>
        /// <param name="message"> 要写入的消息。 </param>
        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id,
            string message)
        {
            _initialize?.Invoke();
            InnerLogger?.Entry();
            if (ShouldTrace(eventCache, source, eventType, id, message, null, null, null))
            {
                AppendToQueue(new LogItem(eventType) { TraceEventID = id, Source = source, Message = message }, eventCache);
            }
            InnerLogger?.Exit();
        }

        /// <summary>
        /// 向特定于侦听器的输出写入跟踪和事件信息。 
        /// </summary>
        /// <param name="eventCache">
        /// 包含当前进程 ID、线程 ID 以及堆栈跟踪信息的 <see cref="T:System.Diagnostics.TraceEventCache" /> 对象。
        /// </param>
        /// <param name="source"> 标识输出时使用的名称，通常为生成跟踪事件的应用程序的名称。 </param>
        /// <param name="eventType"> <see cref="T:System.Diagnostics.TraceEventType" /> 值之一，指定引发跟踪的事件类型。 </param>
        /// <param name="id"> 事件的数值标识符。 </param>
        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id)
        {
            _initialize?.Invoke();
            InnerLogger?.Entry();
            if (ShouldTrace(eventCache, source, eventType, id, null, null, null, null))
            {
                AppendToQueue(new LogItem(eventType) { Source = source, TraceEventID = id }, eventCache);
            }
            InnerLogger?.Exit();
        }

        /// <summary>
        /// 向特定于侦听器的输出中写入跟踪信息、格式化对象数组和事件信息。 
        /// </summary>
        /// <param name="eventCache">
        /// 包含当前进程 ID、线程 ID 以及堆栈跟踪信息的 <see cref="T:System.Diagnostics.TraceEventCache" /> 对象。
        /// </param>
        /// <param name="source"> 标识输出时使用的名称，通常为生成跟踪事件的应用程序的名称。 </param>
        /// <param name="eventType"> <see cref="T:System.Diagnostics.TraceEventType" /> 值之一，指定引发跟踪的事件类型。 </param>
        /// <param name="id"> 事件的数值标识符。 </param>
        /// <param name="format"> 包含零个或多个格式项的格式字符串，这些项与 <paramref name="args" /> 数组中的对象相对应。 </param>
        /// <param name="args"> 包含零个或多个要格式化的对象的 object 数组。 </param>
        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id,
            string format, params object[] args)
        {
            _initialize?.Invoke();
            InnerLogger?.Entry();
            if (ShouldTrace(eventCache, source, eventType, id, format, args, null, null))
            {
                AppendToQueue(new LogItem(eventType) { Source = source, Message = string.Format(format, args), TraceEventID = id }, eventCache);
            }
            InnerLogger?.Exit();
        }

        /// <summary>
        /// 向侦听器特定的输出中写入跟踪信息、消息、相关活动标识和事件信息。 
        /// </summary>
        /// <param name="eventCache">
        /// 包含当前进程 ID、线程 ID 以及堆栈跟踪信息的 <see cref="T:System.Diagnostics.TraceEventCache" /> 对象。
        /// </param>
        /// <param name="source"> 标识输出时使用的名称，通常为生成跟踪事件的应用程序的名称。 </param>
        /// <param name="id"> 事件的数值标识符。 </param>
        /// <param name="message"> 要写入的消息。 </param>
        /// <param name="relatedActivityId"> 标识相关活动的 <see cref="T:System.Guid" /> 对象。 </param>
        public override void TraceTransfer(TraceEventCache eventCache, string source, int id, string message,
            Guid relatedActivityId)
        {
            _initialize?.Invoke();
            InnerLogger?.Entry();
            if (ShouldTrace(eventCache, source, TraceEventType.Transfer, id, message, null, null, null))
            {
                AppendToQueue(new LogItem(TraceEventType.Transfer) { TraceEventID = id, Source = source, Message = message }, eventCache);
            }
            InnerLogger?.Exit();
        }

        /// <summary>
        /// 向在该派生类中所创建的侦听器写入指定消息。 
        /// </summary>
        /// <param name="message"> 要写入的消息。 </param>
        public override void Write(string message)
        {
            _initialize?.Invoke();
            InnerLogger?.Entry();
            if (ShouldTrace(null, null, TraceEventType.Verbose, 0, message, null, null, null))
            {
                AppendToQueue(new LogItem(TraceEventType.Verbose) { Message = message });
            }
            InnerLogger?.Exit();
        }

        /// <summary>
        /// 向所创建的侦听器写入对象。 
        /// </summary>
        /// <param name="o"> 要为其编写完全限定类名的 <see cref="T:System.Object" />。 </param>
        public override void Write(object o)
        {
            _initialize?.Invoke();
            InnerLogger?.Entry();
            var eventType = o is Exception ? TraceEventType.Error : TraceEventType.Verbose;
            if (ShouldTrace(null, null, eventType, 0, null, null, o, null))
            {
                AppendToQueue(new LogItem(eventType) { Content = o });
            }
            InnerLogger?.Exit();
        }

        /// <summary>
        /// 向所创建的侦听器写入类别名称和消息。 
        /// </summary>
        /// <param name="message"> 要写入的消息。 </param>
        /// <param name="category"> 用于组织输出的类别名称。 </param>
        public override void Write(string message, string category)
        {
            _initialize?.Invoke();
            InnerLogger?.Entry();
            if (ShouldTrace(null, category, TraceEventType.Verbose, 0, message, null, null, null))
            {
                AppendToQueue(new LogItem(TraceEventType.Verbose) { Category = category, Message = message });
            }
            InnerLogger?.Exit();
        }

        /// <summary>
        /// 向所创建的侦听器写入类别名称和对象 
        /// </summary>
        /// <param name="o"> 要为其编写完全限定类名的 <see cref="T:System.Object" />。 </param>
        /// <param name="category"> 用于组织输出的类别名称。 </param>
        public override void Write(object o, string category)
        {
            _initialize?.Invoke();
            InnerLogger?.Entry();
            var eventType = o is Exception ? TraceEventType.Error : TraceEventType.Verbose;
            if (ShouldTrace(null, category, eventType, 0, null, null, o, null))
            {
                AppendToQueue(new LogItem(eventType) { Category = category, Content = o });
            }
            InnerLogger?.Exit();
        }

        /// <summary>
        /// 向在该派生类中所创建的侦听器写入消息，后跟行结束符。 
        /// </summary>
        /// <param name="message"> 要写入的消息。 </param>
        public override void WriteLine(string message)
        {
            _initialize?.Invoke();
            InnerLogger?.Entry();
            if (ShouldTrace(null, null, TraceEventType.Verbose, 0, message, null, null, null))
            {
                AppendToQueue(new LogItem(TraceEventType.Verbose) { Message = message, NewLine = true });
            }
            InnerLogger?.Exit();
        }

        /// <summary>
        /// 向所创建的侦听器写入对象，后跟行结束符。 
        /// </summary>
        /// <param name="o"> 要为其编写完全限定类名的 <see cref="T:System.Object" />。 </param>
        public override void WriteLine(object o)
        {
            _initialize?.Invoke();
            InnerLogger?.Entry();
            var eventType = o is Exception ? TraceEventType.Error : TraceEventType.Verbose;
            if (ShouldTrace(null, null, eventType, 0, null, null, o, null))
            {
                AppendToQueue(new LogItem(eventType) { Content = o, NewLine = true });
            }
            InnerLogger?.Exit();
        }

        /// <summary>
        /// 向所创建的侦听器写入类别名称和消息，后跟行结束符。 
        /// </summary>
        /// <param name="message"> 要写入的消息。 </param>
        /// <param name="category"> 用于组织输出的类别名称。 </param>
        public override void WriteLine(string message, string category)
        {
            _initialize?.Invoke();
            InnerLogger?.Entry();
            if (ShouldTrace(null, category, TraceEventType.Verbose, 0, message, null, null, null))
            {
                AppendToQueue(new LogItem(TraceEventType.Verbose) { Category = category, Message = message, NewLine = true });
            }
            InnerLogger?.Exit();
        }

        /// <summary>
        /// 向所创建的侦听器写入类别名称和对象，后跟行结束符。 
        /// </summary>
        /// <param name="o"> 要为其编写完全限定类名的 <see cref="T:System.Object" />。 </param>
        /// <param name="category"> 用于组织输出的类别名称。 </param>
        public override void WriteLine(object o, string category)
        {
            _initialize?.Invoke();
            InnerLogger?.Entry();
            var eventType = o is Exception ? TraceEventType.Error : TraceEventType.Verbose;
            if (ShouldTrace(null, category, eventType, 0, null, null, o, null))
            {
                AppendToQueue(new LogItem(eventType) { Category = category, Content = o, NewLine = true });
            }
            InnerLogger?.Exit();
        }

        #endregion WriteLog
    }
}