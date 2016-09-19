using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace blqw.Logger
{
    /// <summary>
    /// 将日志写入系统事件的侦听器
    /// </summary>
    public sealed class SystemLogListener : BaseTraceListener
    {
        /// <summary>
        /// 创建一个写入器
        /// </summary>
        /// <returns> </returns>
        protected override IWriter CreateWriter() => new SystemLogWriter("blqw.Logger", Name);

        ///// <summary>
        ///// 日志记录器
        ///// </summary>
        //protected override TraceSource InnerLogger => TraceSourceExtensions.InternalSource;
    }
}
