using System;
using System.Collections;
using System.Diagnostics;
using System.Text;

namespace blqw.Logger
{
    /// <summary>
    /// 日志项
    /// </summary>
    public struct LogItem 
    {
        private readonly bool _notNull;
        private string _message;
        private object _content;

        public LogItem(TraceEventType level)
            : this()
        {
            Level = level;
            Time = DateTime.Now;
            _notNull = true;
        }

        /// <summary>
        /// 是否为空项
        /// </summary>
        public bool IsNull => !_notNull;

        /// <summary>
        /// 日志组id
        /// </summary>
        public Guid LogGroupID { get; internal set; }

        /// <summary>
        /// 日志等级
        /// </summary>
        public TraceEventType Level { get; }

        /// <summary>
        /// 日志时间
        /// </summary>
        public DateTime Time { get; set; }

        /// <summary>
        /// 日志堆栈
        /// </summary>
        public string Callstack { get; set; }

        /// <summary>
        /// 日志消息
        /// </summary>
        public string Message
        {
            get
            {
                return _message 
                    ?? _content as string
                    ?? (_content as Exception)?.Message
                    ?? (_content as IConvertible)?.ToString(null)
                    ?? (_content as IFormattable)?.ToString(null, null);
            }
            set { _message = value; }
        }

        /// <summary>
        /// 日志内容
        /// </summary>
        public object Content
        {
            get
            {
                if (_message == null)
                {
                    if (_content is string || _content is IConvertible || _content is IFormattable)
                    {
                        return null;
                    }
                }
                return _content;
            }
            set { _content = value; }
        }

        /// <summary>
        /// 第一条日志
        /// </summary>
        public bool IsFirst { get; set; }

        /// <summary>
        /// 最后一条日志
        /// </summary>
        public bool IsLast { get; set; }

        /// <summary>
        /// 生成日志的监听器
        /// </summary>
        public TraceListener Listener { get; internal set; }

        /// <summary>
        /// 用于组织输出的类别名称
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// 日志的来源
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// 产生日志的代码源文件名
        /// </summary>
        public string File { get; set; }

        /// <summary>
        /// 产生日志的代码方法名
        /// </summary>
        public string Method { get; set; }

        /// <summary>
        /// 产生日志的代码行号
        /// </summary>
        public int LineNumber { get; set; }

        /// <summary>
        /// 事件的数值标识符
        /// </summary>
        public int TraceEventID { get; set; }

        /// <summary>
        /// 是否需要产生一个新行
        /// </summary>
        public bool NewLine { get; set; }

        [ThreadStatic]
        private static StringBuilder _Buffer;
        /// <summary>
        /// 返回该实例的完全限定类型名。
        /// </summary>
        /// <returns>包含完全限定类型名的 <see cref="T:System.String" />。</returns>
        public override string ToString()
        {
            if (IsNull)
            {
                return "无内容";
            }
            if (_Buffer == null)
            {
                _Buffer = new StringBuilder();
            }
            else
            {
                _Buffer.Clear();
            }

            try
            {
                _Buffer.Append("Time:");
                _Buffer.AppendLine(Time.ToString("yyyy-MM-dd HH:mm:ss.ffffff"));
                _Buffer.Append("Level:");
                _Buffer.AppendLine(Level.GetString());
                if (LogGroupID != Guid.Empty)
                {
                    _Buffer.Append("LogGroupID:");
                    _Buffer.Append(LogGroupID);
                    _Buffer.AppendLine();
                }
                if (TraceEventID != 0)
                {
                    _Buffer.Append("TraceEventID:");
                    _Buffer.Append(TraceEventID);
                    _Buffer.AppendLine();
                }
                if (Category != null)
                {
                    _Buffer.Append("Category:");
                    _Buffer.AppendLine(Category);
                }
                if (Source != null)
                {
                    _Buffer.Append("Source:");
                    _Buffer.AppendLine(Source);
                }
                if (Message != null)
                {
                    _Buffer.Append("Message:");
                    _Buffer.AppendLine(Message);
                }
                if (Content != null)
                {
                    var ee = Content is string ? null : (Content as IEnumerable)?.GetEnumerator() ?? Content as IEnumerator;
                    if (ee == null)
                    {
                        _Buffer.Append("Content:");
                        _Buffer.Append(Content);
                        _Buffer.AppendLine();
                    }
                    else
                    {
                        var index = 0;
                        while (ee.MoveNext())
                        {
                            _Buffer.Append("Content[");
                            _Buffer.Append(index);
                            _Buffer.Append("]:");
                            _Buffer.Append(ee.Current);
                            _Buffer.AppendLine();
                        }
                    }
                }
                if (File != null || Method != null)
                {
                    _Buffer.Append(File);
                    _Buffer.Append(',');
                    _Buffer.Append(Method);
                    _Buffer.Append(':');
                    _Buffer.Append(LineNumber);
                }

                if (Callstack != null)
                {
                    _Buffer.Append("Callstack:");
                    _Buffer.AppendLine(Callstack);
                }

                _Buffer.AppendLine();

                return _Buffer.ToString();
            }
            finally
            {
                _Buffer.Clear();
            }
        }
    }
}