using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using blqw.Logger;

/// <summary>
/// 按照SLS的方式输出日志
/// </summary>
public sealed class SLSTraceListener : BaseTraceListener
{
    private SourceLevels _writedLevel;
    private bool _isInitialized;
    private int _queueMaxLength;
    /// <exception cref="ArgumentOutOfRangeException">level</exception>
    public SLSTraceListener()
    {
        Logger = InternalLogger.Instance;
    }

    /// <exception cref="ArgumentOutOfRangeException">level</exception>
    public SLSTraceListener(string initializeData) : base(initializeData)
    {
        Logger = InternalLogger.Instance;
    }

    internal SLSTraceListener(string dirPath,InternalLogger logger)
        : base(dirPath)
    {
        Logger = logger;
    }

    internal override InternalLogger Logger { get; }

    private void Initialize()
    {
        if (_isInitialized) return;
        if (Enum.TryParse(Attributes["level"] ?? "All", true, out _writedLevel) == false)
        {
            throw new ArgumentOutOfRangeException("level", "level属性值无效,请参考: System.Diagnostics.SourceLevels");
        }
        if (int.TryParse(Attributes["queueMaxLength"] ?? "100000", out _queueMaxLength) == false || _queueMaxLength < 100000)
        {
            throw new ArgumentOutOfRangeException("queueMaxLength", "queueMaxLength属性值无效,必须大于100000");
        }
        _isInitialized = true;
    }

    protected override IWriter CreateWriter()
    {
        Initialize();
        var dir = InitializeData;
        dir = Path.Combine(string.IsNullOrWhiteSpace(dir) ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..\\sls_logs") : dir, Name, "{0:yyyyMMddHH}");
        return new SLSWriter(dir, Logger) { QueueMaxCount = _queueMaxLength };
    }

    /// <summary>
    /// 获取当前线程中的日志跟踪等级
    /// </summary>
    protected override SourceLevels WritedLevel
    {
        get
        {
            Initialize();
            return _writedLevel;
        }
    }



    /// <summary>
    /// 获取跟踪侦听器支持的自定义特性。
    /// </summary>
    /// <returns>为跟踪侦听器支持的自定义特性命名的字符串数组；或者如果没有自定义特性，则为 null。</returns>
    protected override string[] GetSupportedAttributes() => new[] { "level", "queueMaxLength" };
}

