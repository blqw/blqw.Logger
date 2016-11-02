using System;
using System.Linq;

namespace blqw.Logger
{
    /// <summary>
    /// 本地日志侦听器
    /// </summary>
    public class FileTraceListener : TraceListenerBase
    {
        private int _batchMaxCount = -1;
        private TimeSpan _batchMaxWait = TimeSpan.Zero;
        private int _queueMaxCount = -1;

        private IWriter _writer;

        /// <summary>
        /// 以线程为单位记录和输出日志 构造函数
        /// </summary>
        public FileTraceListener()
            : base(true, null)
        {
        }

        protected FileTraceListener(bool isThreadSafe, string initializeData = null) : base(isThreadSafe, initializeData)
        {
        }


        /// <summary>
        /// 批处理最大数量
        /// </summary>
        protected int QueueMaxCount
        {
            get
            {
                if (_queueMaxCount < 0)
                {
                    int i;
                    if (int.TryParse(Attributes["QueueMaxCount"], out i))
                    {
                        return i;
                    }
                    throw new NotSupportedException("该属性未初始化");
                }
                return _queueMaxCount;
            }
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "value不能小于0");
                }
                _queueMaxCount = value;
            }
        }

        /// <summary>
        /// 批处理最大数量
        /// </summary>
        protected int BatchMaxCount
        {
            get
            {
                if (_batchMaxCount < 0)
                {
                    int i;
                    if (int.TryParse(Attributes["BatchMaxCount"], out i))
                    {
                        return i;
                    }
                    throw new NotSupportedException("该属性未初始化");
                }
                return _batchMaxCount;
            }
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "value不能小于0");
                }
                _batchMaxCount = value;
            }
        }

        /// <summary>
        /// 批处理最大等待时间
        /// </summary>
        protected TimeSpan BatchMaxWait
        {
            get
            {
                if (_batchMaxWait == TimeSpan.Zero)
                {
                    int i;
                    if (int.TryParse(Attributes["BatchMaxWait"], out i))
                    {
                        return TimeSpan.FromSeconds(i);
                    }
                    throw new NotSupportedException("该属性未初始化");
                }
                return _batchMaxWait;
            }
            set
            {
                if (value == TimeSpan.Zero)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "value不能等于TimeSpan.Zero");
                }
                _batchMaxWait = value;
            }
        }

        /// <summary>
        /// 获取写入器实例
        /// </summary>
        protected virtual IWriter Writer
        {
            get
            {
                if (_writer == null)
                {
                    var type = Type.GetType(Attributes["Writer"], false, true);
                    if (type != null)
                    {
                        var ctor = type.GetConstructor(new[] { typeof(string) });
                        Writer = (IWriter)(ctor?.Invoke(new object[] { InitializeData }) ?? Activator.CreateInstance(type));
                    }
                    else if (Attributes["Writer"] == null)
                    {
                        Writer = new FastFileWriter();
                    }
                    else
                    {
                        throw new TypeLoadException("找不到类型" + Attributes["Writer"]);
                    }
                }
                return _writer;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "value不能为null");
                }
                _writer = value;
                _writer.Logger = InnerLogger;
                _writer.Initialize(this);
            }
        }

        /// <summary>
        /// 创建一个队列
        /// </summary>
        /// <returns> </returns>
        protected override WriteQueue CreateQueue() => new WriteQueue(Writer, QueueMaxCount, BatchMaxCount, BatchMaxWait);


        /// <summary> 获取跟踪侦听器支持的自定义特性。 </summary>
        /// <returns> 为跟踪侦听器支持的自定义特性命名的字符串数组；或者如果没有自定义特性，则为 null。 </returns>
        protected override string[] GetSupportedAttributes() => UnionArray(new[] { "QueueMaxCount", "BatchMaxCount", "BatchMaxWait", "Writer" }, Writer.GetSupportedAttributes());
    }
}