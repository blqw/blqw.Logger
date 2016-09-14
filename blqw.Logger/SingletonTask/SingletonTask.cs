using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace blqw.Logger
{
    /// <summary>
    /// 单例任务
    /// </summary>
    internal sealed class SingletonTask
    {
        public TraceSource Logger { get; }
        /// <summary>
        /// 健康检查执行时间间隔(秒)
        /// </summary>
        private readonly int _checkInterval;

        /// <summary>
        /// 创建可执行任务的委托
        /// </summary>
        private readonly Func<ActivityTokenSource, Task> _create;

        /// <summary>
        /// 任务的最后执行时间
        /// </summary>
        private DateTime _lastRunTime;

        /// <summary>
        /// 任务标识
        /// </summary>
        private ActivityTokenSource _taskToken;

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="create"> 创建任务的委托 </param>
        /// <param name="checkInterval"> 健康检查间隔时间,单位秒(默认30) </param>
        public SingletonTask(Func<ActivityTokenSource, Task> create, TraceSource logger, int checkInterval = 30)
        {
            Logger = logger;
            Logger?.Entry();
            _create = create;
            _checkInterval = checkInterval;
            _lastRunTime = DateTime.MinValue;
            Logger?.Exit();
        }

        /// <summary>
        /// 判断任务是否正在执行
        /// </summary>
        public bool IsRunning
        {
            get
            {
                Logger?.Entry();
                var b = _taskToken?.CancelIfTimeout();
                if (b == null)
                {
                    Logger?.Return("false");
                    return false;
                }
                var interval = (DateTime.Now - _lastRunTime).TotalSeconds;
                if (interval < _checkInterval)
                {
                    Logger?.Return(b.Value.ToString());
                    return b.Value;
                }
                //如果最后执行时间大于强制检查时间,则强制同步多线程字段
                _lastRunTime = DateTime.Now;
                Interlocked.MemoryBarrier();
                var value = _taskToken?.CancelIfTimeout() ?? false;
                Logger?.Return(value.ToString());
                return value;
            }
        }

        /// <summary>
        /// 如果任务没有在执行则执行任务
        /// </summary>
        public void RunIfStop()
        {
            Logger?.Entry();
            if (IsRunning)
            {
                Logger?.Exit();
                return;
            }

            var token = new ActivityTokenSource(10000); //新建一个任务标识,10秒无响应则取消任务
            //任务标识,如果更新失败,说明其他线程已经更新了,当前线程主动退出
            if (Interlocked.CompareExchange(ref _taskToken, token, null) != null)
            {
                Logger?.Exit();
                return;
            }
            SynchronizationContext.SetSynchronizationContext(null);
            Run(token).ConfigureAwait(false);
            Logger?.Exit();
        }

        /// <summary>
        /// 执行任务,并在任务退出后更新任务标识
        /// </summary>
        /// <param name="token"> </param>
        private async Task Run(ActivityTokenSource token)
        {
            Logger?.Entry();
            await Task.Delay(1);
            try
            {
                var task = _create(token);
                _lastRunTime = DateTime.Now;
                await task;
            }
            catch (Exception ex)
            {
                Logger?.Error(ex, nameof(SingletonTask));
            }
            finally
            {
                Interlocked.CompareExchange(ref _taskToken, null, token);
                Logger?.Exit();
            }
        }
    }
}