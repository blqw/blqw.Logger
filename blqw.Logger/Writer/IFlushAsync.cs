using System.Threading;
using System.Threading.Tasks;

namespace blqw.Logger
{
    /// <summary>
    /// 异步刷新日志接口
    /// </summary>
    public interface IFlushAsync
    {
        /// <summary>
        /// 异步刷新
        /// </summary>
        /// <param name="token"> </param>
        /// <returns> </returns>
        Task FlushAsync(CancellationToken token);
    }
}