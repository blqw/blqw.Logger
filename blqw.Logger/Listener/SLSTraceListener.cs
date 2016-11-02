using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using blqw.Logger;
// ReSharper disable ExceptionNotDocumented

/// <summary>
/// 按照SLS的方式输出日志
/// </summary>
// ReSharper disable once InconsistentNaming
public sealed class SLSTraceListener : FileTraceListener
{
    private SourceLevels _writedLevel;

    /// <summary>
    /// 初始化侦听器
    /// </summary>
    public SLSTraceListener()
        : base(true)
    {
        InnerLogger = TraceSourceExtensions.InternalSource;
    }

    /// <summary>
    /// 按照SLS的方式输出日志
    /// </summary>
    public SLSTraceListener(string initializeData)
        : base(true, initializeData)
    {
        InnerLogger = TraceSourceExtensions.InternalSource;
        QueueMaxCount = 5000 * 10000; //默认队列 5000 万
    }

    /// <summary>
    /// 根据当前事件类型判断是否需要输出日志
    /// </summary>
    protected override bool ShouldTrace(TraceEventCache cache, string source, TraceEventType eventType, int id, string formatOrMessage,
        object[] args, object data1, object[] data) => WritedLevel != SourceLevels.Off;

    private int _initialized = 0;
    /// <summary>
    /// 获取当前线程中的日志跟踪等级
    /// </summary>
    protected override SourceLevels WritedLevel
    {
        get
        {
            if (_initialized == 1)
            {
                return _writedLevel;
            }
            if (Interlocked.Exchange(ref _initialized, 1) == 0)
            {
                if (Enum.TryParse(Attributes["level"] ?? "All", true, out _writedLevel) == false)
                {
                    // ReSharper disable once NotResolvedInText
                    throw new ArgumentOutOfRangeException("level", "level属性值无效,请参考: System.Diagnostics.SourceLevels");
                }
            }
            return _writedLevel;
        }
    }

    /// <summary>
    /// 创建一个队列
    /// </summary>
    /// <returns> </returns>
    /// <exception cref="AppDomainUnloadedException">该操作尝试对已卸载的应用程序域中。</exception>
    protected override WriteQueue CreateQueue()
    {
        var dir = string.IsNullOrWhiteSpace(InitializeData)
                ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..\\sls_logs", Name)
                : Path.Combine(InitializeData, Name);

        int queueMaxLength;
        int.TryParse(Attributes["queueMaxLength"], out queueMaxLength);
        Writer = new SLSWriter(dir, WritedLevel);
        return base.CreateQueue();
    }


    /// <summary>
    /// 获取跟踪侦听器支持的自定义特性。
    /// </summary>
    /// <returns> 为跟踪侦听器支持的自定义特性命名的字符串数组；或者如果没有自定义特性，则为 null。 </returns>
    protected override string[] GetSupportedAttributes() => UnionArray(new[] { "level" }, base.GetSupportedAttributes());
}