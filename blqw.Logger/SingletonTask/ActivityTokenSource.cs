using System;
using System.Threading;

namespace blqw.Logger
{
    /// <summary>
    /// 这是一个检查任务是否处于活动状态的信号标识
    /// </summary>
    internal sealed class ActivityTokenSource : CancellationTokenSource
    {
        /// <summary>
        /// 设定的超时时间
        /// </summary>
        private readonly TimeSpan _timeout;

        /// <summary>
        /// 初始化信号
        /// </summary>
        /// <param name="timeout"> 活动检查超时时间 </param>
        public ActivityTokenSource(TimeSpan timeout)
        {
            _timeout = timeout;
            LastActivityTime = DateTime.Now;
        }

        /// <summary>
        /// 初始化信号
        /// </summary>
        /// <param name="millisecondsTimeout"> 活动检查超时毫秒 </param>
        /// <exception cref="OverflowException">
        /// <paramref name="millisecondsTimeout" /> 是小于 <see cref="F:System.TimeSpan.MinValue" /> 或大于
        /// <see cref="F:System.TimeSpan.MaxValue" />。- 或 -<paramref name="millisecondsTimeout" /> 为
        /// <see cref="F:System.Double.PositiveInfinity" />。- 或 -<paramref name="millisecondsTimeout" /> 为
        /// <see cref="F:System.Double.NegativeInfinity" />。
        /// </exception>
        public ActivityTokenSource(int millisecondsTimeout)
            : this(TimeSpan.FromMilliseconds(millisecondsTimeout))
        {
        }

        /// <summary>
        /// 最后活动时间
        /// </summary>
        public DateTime LastActivityTime { get; private set; }

        /// <summary>
        /// 发送一个活动信号,如果任务已经超时则抛出异常
        /// </summary>
        /// <exception cref="OperationCanceledException"> 任务超过限制时间没有任何活动信号 </exception>
        public void Activity()
        {
            ThrowIfCancellationRequested();
            LastActivityTime = DateTime.Now;
        }

        /// <summary>
        /// 如果任务已经超时则发出一个超时型号
        /// </summary>
        /// <returns> </returns>
        /// <exception cref="ObjectDisposedException">This <see cref="T:System.Threading.CancellationTokenSource" /> has been disposed.</exception>
        /// <exception cref="AggregateException">An aggregate exception containing all the exceptions thrown by the registered callbacks on the associated <see cref="T:System.Threading.CancellationToken" />.</exception>
        public bool CancelIfTimeout()
        {
            if (IsCancellationRequested || Token.IsCancellationRequested)
            {
                return true;
            }

            if (DateTime.Now - LastActivityTime < _timeout)
            {
                return false;
            }
            Cancel();
            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="OperationCanceledException"> 任务超过限制时间没有任何活动信号 </exception>
        private void ThrowIfCancellationRequested()
        {
            if (CancelIfTimeout())
            {
                throw new OperationCanceledException($"任务因 {_timeout} 无动作被取消", Token);
            }
        }
    }
}