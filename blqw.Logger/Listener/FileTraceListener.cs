using System;
using System.Linq;
using blqw.Logger;


/// <summary>
/// 本地日志侦听器
/// </summary>
public class FileTraceListener : TraceListenerBase
{
    private IWriter _writer;

    /// <summary>
    /// 初始化侦听器
    /// </summary>
    public FileTraceListener()
        : base(true, null)
    {
    }

    /// <summary>
    /// 使用写入器类型的名称初始化侦听器
    /// </summary>
    /// <param name="typeAssemblyQualifiedName">写入器类型的名称</param>
    public FileTraceListener(string typeAssemblyQualifiedName)
        : base(true, typeAssemblyQualifiedName)
    {

    }

    /// <summary>
    /// 使用写入器类型初始化侦听器
    /// </summary>
    /// <param name="writerType"></param>
    /// <exception cref="ArgumentNullException"> <paramref name="writerType"/> 不能为空 </exception>
    public FileTraceListener(Type writerType)
        : base(true, null)
    {
        if (writerType == null)
        {
            throw new ArgumentNullException(nameof(writerType));
        }
        Writer = (IWriter)Activator.CreateInstance(writerType);
    }

    /// <summary>
    /// 使用写入器初始化侦听器
    /// </summary>
    /// <param name="writer"></param>
    /// <exception cref="ArgumentNullException"> <paramref name="writer"/> 不能为空 </exception>
    public FileTraceListener(IWriter writer)
        : base(true, null)
    {
        if (writer == null)
        {
            throw new ArgumentNullException(nameof(writer));
        }
        Writer = writer;
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
                        throw new TypeLoadException("[writer]找不到类型" + Attributes["writer"]);
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
        return new WriteQueue(writer, QueueMaxCount, BatchMaxCount, BatchMaxWait);
    }

    /// <summary>
    /// 初始化当前实例
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    protected override void Initialize()
    {
        BatchMaxWait = TimeSpan.FromSeconds(GetAttributeValue("batchMaxWait", 1, int.MaxValue, BatchMaxWait == TimeSpan.Zero ? 10 : (int)BatchMaxWait.TotalSeconds));
        BatchMaxCount = GetAttributeValue("batchMaxCount", 1, int.MaxValue, BatchMaxCount == 0 ? 2000 : BatchMaxCount);
        QueueMaxCount = GetAttributeValue("queueMaxCount", 1, int.MaxValue, QueueMaxCount == 0 ? 1000 * 10000 : QueueMaxCount);
        base.Initialize();
    }

    /// <summary>
    /// 获取属性的值
    /// </summary>
    /// <param name="name"></param>
    /// <param name="minValue"></param>
    /// <param name="maxValue"></param>
    /// <param name="defaultValue"></param>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    /// <exception cref="ArgumentException"></exception>
    protected int GetAttributeValue(string name, int minValue, int maxValue, int defaultValue)
    {
        var value = Attributes[name];
        if (value == null)
        {
            return defaultValue;
        }
        int i;
        if (!int.TryParse(value, out i))
        {
            throw new ArgumentException($"[{name}]属性值错误", name);
        }
        if (i < minValue)
        {
            throw new ArgumentOutOfRangeException(name, $"[{name}]不能小于{minValue}");
        }
        if (i > maxValue)
        {
            throw new ArgumentOutOfRangeException(name, $"[{name}]不能大于{maxValue}");
        }
        return i;
    }


    /// <summary> 获取跟踪侦听器支持的自定义特性。 </summary>
    /// <returns> 为跟踪侦听器支持的自定义特性命名的字符串数组；或者如果没有自定义特性，则为 null。 </returns>
    protected override string[] GetSupportedAttributes() => UnionArray(base.GetSupportedAttributes(), new[] { "queueMaxCount", "batchMaxCount", "batchMaxWait" }, Writer.GetSupportedAttributes());
}
