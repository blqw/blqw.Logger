using System;
using System.Diagnostics;
using System.IO;
using blqw.Logger;
// ReSharper disable ExceptionNotDocumented

/// <summary>
/// 按照SLS的方式输出日志
/// </summary>
// ReSharper disable once InconsistentNaming
public sealed class SLSTraceListener : BaseTraceListener
{
    private SourceLevels _writedLevel;

    /// <summary>
    /// 初始化侦听器
    /// </summary>
    public SLSTraceListener()
    {
        InnerLogger = TraceSourceExtensions.InternalSource;
    }

    /// <summary>
    /// 按照SLS的方式输出日志
    /// </summary>
    public SLSTraceListener(string initializeData)
        : base(initializeData)
    {
        InnerLogger = TraceSourceExtensions.InternalSource;
    }


    /// <summary>
    /// 日志记录器
    /// </summary>
    protected override TraceSource InnerLogger { get; }

    /// <summary>
    /// 获取当前线程中的日志跟踪等级
    /// </summary>
    protected override SourceLevels WritedLevel => _writedLevel;

    /// <summary>
    /// 初始化操作
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">level属性值无效,请参考: <seealso cref="SourceLevels"/></exception>
    protected override void Initialize()
    {
        if (Enum.TryParse(Attributes["level"] ?? "All", true, out _writedLevel) == false)
        {
            // ReSharper disable once NotResolvedInText
            throw new ArgumentOutOfRangeException("level", "level属性值无效,请参考: System.Diagnostics.SourceLevels");
        }
    }


    /// <summary>
    /// 创建一个写入器
    /// </summary>
    /// <returns> </returns>
    /// <exception cref="AppDomainUnloadedException">该操作尝试对已卸载的应用程序域中。</exception>
    protected override IWriter CreateWriter()
    {
        var dir = string.IsNullOrWhiteSpace(InitializeData)
                ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..\\sls_logs", Name)
                : Path.Combine(InitializeData, Name);

        int queueMaxLength;
        int.TryParse(Attributes["queueMaxLength"], out queueMaxLength);
        return new SLSWriter(dir, InnerLogger) { QueueMaxCount = queueMaxLength };
    }


    /// <summary>
    /// 获取跟踪侦听器支持的自定义特性。
    /// </summary>
    /// <returns> 为跟踪侦听器支持的自定义特性命名的字符串数组；或者如果没有自定义特性，则为 null。 </returns>
    protected override string[] GetSupportedAttributes() => new[] { "level", "queueMaxLength" };
}