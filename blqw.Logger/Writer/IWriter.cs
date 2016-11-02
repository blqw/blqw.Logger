using System;
using System.Diagnostics;

namespace blqw.Logger
{
    /// <summary>
    /// 日志写入器
    /// </summary>
    public interface IWriter : IDisposable
    {
        /// <summary>
        /// 批处理最大数量
        /// </summary>
        int BatchMaxCount { get; }

        /// <summary>
        /// 批处理最大等待时间
        /// </summary>
        TimeSpan BatchMaxWait { get; }

        /// <summary>
        /// 写入器名称
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 日志跟踪器
        /// </summary>
        TraceSource Logger { get; set; }

        /// <summary>
        /// 初始化写入器
        /// </summary>
        /// <param name="listener"> </param>
        void Initialize(TraceListener listener);

        /// <summary>
        /// 追加日志
        /// </summary>
        /// <param name="item"> </param>
        void Append(LogItem item);

        /// <summary>
        /// 刷新缓存
        /// </summary>
        void Flush();

        /// <summary>
        /// 获取跟踪侦听器支持的自定义特性。
        /// </summary>
        /// <returns> 为跟踪侦听器支持的自定义特性命名的字符串数组；或者如果没有自定义特性，则为 null。 </returns>
        string[] GetSupportedAttributes();
    }
}