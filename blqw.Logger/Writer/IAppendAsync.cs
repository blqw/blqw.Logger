using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace blqw.Logger.Writer
{
    public interface IAppendAsync
    {
        /// <summary>
        /// 异步写入日志
        /// </summary>
        /// <param name="item"></param>
        /// <param name="token"> </param>
        /// <returns></returns>
        Task AppendAsync(LogItem item, CancellationToken token);
    }
}
