using System;
using System.Diagnostics;
using System.Runtime.Remoting.Messaging;

namespace blqw.Logger
{
    /// <summary>
    /// 日志上下文
    /// </summary>
    public struct LoggerContext
    {
        /// <summary>
        /// 上下文字段
        /// </summary>
        private const string CONTEXT_FIELD = "blqw.LoggerContext";

        /// <summary>
        /// 上下文中需要存储的值
        /// </summary>
        private object[] _values;

        private TraceLevel _minLevel;
        private Guid _logId;
        private bool _isNew;
        private bool _isInitialized;

        /// <summary>
        /// 初始化
        /// </summary>
        private bool Initialize(bool create = true)
        {
            if (_isInitialized == false)
            {
                _values = (object[])CallContext.GetData(CONTEXT_FIELD);
                if (_values != null)
                {
                    _logId = (Guid)_values[0];
                    _minLevel = (TraceLevel)_values[1];
                    _isNew = false;
                }
                else if (create)
                {
                    _logId = Trace.CorrelationManager.ActivityId;
                    _minLevel = 0;
                    _isNew = true;
                    if (_logId == Guid.Empty)
                    {
                        Trace.CorrelationManager.ActivityId = _logId = Guid.NewGuid();
                    }
                    _values = new object[] { _logId, _minLevel };
                    CallContext.SetData(CONTEXT_FIELD, _values);
                }
                else
                {
                    return false;
                }
                _isInitialized = true;
            }
            return true;
        }

        /// <summary>
        /// 上下文中的日志最小等级
        /// </summary>
        public TraceLevel MinLevel
        {
            get
            {
                Initialize();
                return _minLevel;
            }
            set
            {
                Initialize();
                if (_minLevel == 0 || value < _minLevel)
                {
                    _values[1] = _minLevel = value;
                }
            }
        }

        /// <summary>
        /// 日志id
        /// </summary>
        public Guid LogID
        {
            get
            {
                Initialize();
                return _logId;
            }
            private set
            {
                Initialize();
                _logId = value;
            }
        }

        /// <summary>
        /// 是否是一个新的上下文
        /// </summary>
        public bool IsNew
        {
            get
            {
                Initialize();
                return _isNew;
            }
            private set
            {
                Initialize();
                _isNew = value;
            }
        }
        
        /// <summary>
        /// 是否存在上下文
        /// </summary>
        public bool Exists => Initialize(false);

        /// <summary>
        /// 清除上下文
        /// </summary>
        public static void Clear()
        {
            CallContext.FreeNamedDataSlot(CONTEXT_FIELD);
        }
    }
}