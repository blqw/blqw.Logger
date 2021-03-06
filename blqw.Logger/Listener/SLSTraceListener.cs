﻿using System;
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
    /// 根据当前事件类型判断是否需要输出日志
    /// </summary>
    protected override bool ShouldTrace(TraceEventCache cache, string source, TraceEventType eventType, int id, string formatOrMessage,
        object[] args, object data1, object[] data) => WritedLevel != SourceLevels.Off;
    
    /// <summary>
    /// 获取跟踪侦听器支持的自定义特性。
    /// </summary>
    /// <returns> 为跟踪侦听器支持的自定义特性命名的字符串数组；或者如果没有自定义特性，则为 null。 </returns>
    protected override string[] GetSupportedAttributes() => UnionArray(new[] { "level", "queueMaxLength" }, base.GetSupportedAttributes());


    protected override void Initialize()
    {
        QueueMaxCount = GetAttributeValue("queueMaxLength", 10000, int.MaxValue, 5000 * 10000); //兼容之前的一个坑
        base.Initialize();
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

    /// <summary>
    /// 刷新输出缓冲区。
    /// </summary>
    public override void Flush()
    {
        if (Trace.AutoFlush)
        {
            //判断当前方法是否是由于主动调用 .Flush() 触发的
            if (!"Flush".Equals(new StackFrame(1, false).GetMethod().Name, StringComparison.Ordinal))
            {
                return;
            }
        }
        base.Flush();
    }
}