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
    /// 获取跟踪侦听器支持的自定义特性。
    /// </summary>
    /// <returns> 为跟踪侦听器支持的自定义特性命名的字符串数组；或者如果没有自定义特性，则为 null。 </returns>
    protected override string[] GetSupportedAttributes() => UnionArray(new[] { "level", "queueMaxLength" }, base.GetSupportedAttributes());
    

    protected override void Initialize()
    {
        QueueMaxCount = 5000*10000;
        base.Initialize();
        int i;
        if (int.TryParse(Attributes["queueMaxLength"], out i))
        {
            if (i < 10000)
            {
                throw new ArgumentOutOfRangeException("queueMaxLength", "[queueMaxLength]不能小于10000");
            }
            QueueMaxCount = i;
        }
        if (Debugger.IsAttached)
        {
            BatchMaxWait = TimeSpan.FromSeconds(1);
        }
    }

    private IWriter _writer;
    /// <summary>
    /// 获取写入器实例
    /// </summary>
    protected override IWriter Writer
    {
        get
        {
            if (_writer == null)
            {
                var dir = string.IsNullOrWhiteSpace(InitializeData)
                        ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..\\sls_logs", Name)
                        : Path.Combine(InitializeData, Name);
                _writer = new SLSWriter(dir, WritedLevel);
            }
            return _writer;
        }
        set { throw new NotSupportedException(); }
    }
}