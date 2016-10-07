using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using blqw.Logger;


/// <summary>
/// 将日志写入系统事件的侦听器
/// </summary>
public sealed class SystemLogTraceListener : TraceListenerBase
{
    /// <summary>
    /// 以线程为单位记录和输出日志 构造函数
    /// </summary>
    public SystemLogTraceListener() : base(true, null)
    {
    }
    /// <summary>
    /// 创建一个队列
    /// </summary>
    /// <returns> </returns>
    protected override WriteQueue CreateQueue() => new WriteQueue(new SystemLogWriter("blqw.Logger", Name), int.MaxValue) { Logger = InnerLogger };
}

