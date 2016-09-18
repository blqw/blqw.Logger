using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace blqw.Logger
{
    public interface IFlushAsync
    {
        /// <summary>
        /// 异步刷新
        /// </summary>
        /// <param name="token"> </param>
        /// <returns></returns>
        Task FlushAsync(CancellationToken token);
    }
}
