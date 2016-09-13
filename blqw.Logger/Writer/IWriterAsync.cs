using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace blqw.Logger
{
    /// <summary>
    /// 异步日志写入器
    /// </summary>
    public interface IWriterAsync : IWriter
    {
        /// <summary>
        /// 异步写入日志
        /// </summary>
        /// <param name="item"></param>
        /// <param name="token"> </param>
        /// <returns></returns>
        Task AppendAsync(LogItem item, CancellationToken token);

        /// <summary>
        /// 异步刷新
        /// </summary>
        /// <param name="token"> </param>
        /// <returns></returns>
        Task FlushAsync(CancellationToken token);
    }
}
