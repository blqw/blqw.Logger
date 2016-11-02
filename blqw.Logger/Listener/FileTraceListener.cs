using System;
using System.Linq;

namespace blqw.Logger
{
    /// <summary>
    /// 本地日志侦听器
    /// </summary>
    public class FileTraceListener : TraceListenerBase
    {
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
        /// 队列中等待的最大数量
        /// </summary>
        public int QueueMaxCount { get; protected set; }

        /// <summary>
        /// 批处理最大数量
        /// </summary>
        public int BatchMaxCount { get; protected set; }

        /// <summary>
        /// 批处理最大等待时间
        /// </summary>
        public TimeSpan BatchMaxWait { get; protected set; }

        /// <summary>
        /// 获取写入器实例
        /// </summary>
        protected virtual IWriter Writer
        {
            get
            {
                if (_writer == null)
                {
                    if (InitializeData == null)
                    {
                        Writer = new FastFileWriter();
                    }
                    else
                    {
                        var type = Type.GetType(InitializeData, false, true);
                        if (type != null)
                        {
                            Writer = (IWriter)Activator.CreateInstance(type);
                        }
                        else
                        {
                            throw new TypeLoadException("找不到类型" + Attributes["writer"]);
                        }
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
            }
        }

        /// <summary>
        /// 创建一个队列
        /// </summary>
        /// <returns> </returns>
        protected sealed override WriteQueue CreateQueue()
        {
            var writer = Writer;
            writer.Logger = InnerLogger;
            writer.Initialize(this);
            Initialize();
            return new WriteQueue(writer, QueueMaxCount, BatchMaxCount, BatchMaxWait);
        }

        protected virtual void Initialize()
        {
            var batchMaxWait = Attributes["batchMaxWait"];
            if (batchMaxWait != null)
            {
                int i;
                if (int.TryParse(Attributes["batchMaxWait"], out i))
                {
                    BatchMaxWait = TimeSpan.FromSeconds(i);
                }
                else
                {
                    throw new ArgumentOutOfRangeException(nameof(batchMaxWait), "[batchMaxWait]属性值有误");
                }
            }
            else if (BatchMaxWait == TimeSpan.Zero)
            {
                BatchMaxWait = TimeSpan.FromSeconds(10);
            }

            var batchMaxCount = Attributes["batchMaxCount"];
            if (batchMaxCount != null)
            {
                int i;
                if (int.TryParse(Attributes["batchMaxCount"], out i))
                {
                    BatchMaxCount = i;
                }
                else
                {
                    throw new ArgumentOutOfRangeException(nameof(batchMaxCount), "[batchMaxCount]属性值有误");
                }
            }
            else if (BatchMaxCount == 0)
            {
                BatchMaxCount = 2000;
            }

            var queueMaxCount = Attributes["queueMaxCount"];
            if (queueMaxCount != null)
            {
                int i;
                if (int.TryParse(Attributes["queueMaxCount"], out i))
                {
                    QueueMaxCount = i;
                }
                else
                {
                    throw new ArgumentOutOfRangeException(nameof(queueMaxCount), "[queueMaxCount]属性值有误");
                }
            }
            else if (QueueMaxCount == 0)
            {
                QueueMaxCount = 1000 * 10000;
            }

        }

        /// <summary> 获取跟踪侦听器支持的自定义特性。 </summary>
        /// <returns> 为跟踪侦听器支持的自定义特性命名的字符串数组；或者如果没有自定义特性，则为 null。 </returns>
        protected override string[] GetSupportedAttributes() => UnionArray(new[] { "queueMaxCount", "batchMaxCount", "batchMaxWait", "writer" }, Writer.GetSupportedAttributes());
    }
}