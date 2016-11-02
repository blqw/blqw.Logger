using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace blqw.Logger
{
    /// <summary>
    /// 本地日志写入器
    /// </summary>
    public sealed class FastFileWriter : FileWriter
    {
        #region Public Properties

        /// <summary>
        /// 写入器名称
        /// </summary>
        public override string Name => nameof(FastFileWriter);

        #endregion Public Properties

        #region Private Methods

        private void Wirte(byte[] name, string value)
        {
            Append(name);
            for (var i = 13 - name.Length; i >= 0; i--)
            {
                AppendWhiteSpace();
            }
            AppendColon();
            AppendWhiteSpace();
            Append(value);
            AppendLine();
        }

        #endregion Private Methods

        #region Private Classes

        private static class Fields
        {
            #region Public Properties

            public static byte[] Callstack { get; } = Encoding.UTF8.GetBytes("Callstack");
            public static byte[] Category { get; } = Encoding.UTF8.GetBytes("Category");
            public static byte[] Content { get; } = Encoding.UTF8.GetBytes("Content");
            public static byte[] File { get; } = Encoding.UTF8.GetBytes("File");
            public static byte[] Level { get; } = Encoding.UTF8.GetBytes("Level");
            public static byte[] LineNumber { get; } = Encoding.UTF8.GetBytes("LineNumber");
            // ReSharper disable once InconsistentNaming
            public static byte[] LogGroupID { get; } = Encoding.UTF8.GetBytes("LogGroupID");
            public static byte[] Message { get; } = Encoding.UTF8.GetBytes("Message");
            public static byte[] Method { get; } = Encoding.UTF8.GetBytes("Method");
            public static byte[] Source { get; } = Encoding.UTF8.GetBytes("Source");
            public static byte[] Time { get; } = Encoding.UTF8.GetBytes("Time");
            // ReSharper disable once InconsistentNaming
            public static byte[] TraceEventID { get; } = Encoding.UTF8.GetBytes("TraceEventID");

            #endregion Public Properties
        }

        #endregion Private Classes

        #region Private Fields

        //单个文件容量阈值
        private const long DEFAULT_FILE_MAX_SIZE = 5*1024*1024; //兆

        /// <summary>
        /// 间隔号
        /// </summary>
        private static readonly byte[] _Line = Encoding.UTF8.GetBytes(new string('-', 70));

        #endregion Private Fields

        #region Public Methods

        /// <summary>
        /// 追加日志
        /// </summary>
        /// <param name="item"> </param>
        public override void Append(LogItem item)
        {
            if (item.IsFirst != item.IsLast)
            {
                return;
            }
            ChangeFileIfFull(); //如果文件满了就换一个

            Wirte(Fields.Time, item.Time.ToString("yyyy-MM-dd HH:mm:ss.ffffff"));
            Wirte(Fields.Level, item.Level.GetString());
            if (item.LogGroupID != Guid.Empty)
            {
                Wirte(Fields.LogGroupID, item.LogGroupID.ToString());
            }
            if (item.TraceEventID != 0)
            {
                Wirte(Fields.TraceEventID, item.TraceEventID.ToString());
            }
            if (item.Category != null)
            {
                Wirte(Fields.Category, item.Category);
            }
            if (item.Source != null)
            {
                Wirte(Fields.Source, item.Source);
            }
            if (item.Message != null)
            {
                Wirte(Fields.Message, item.Message);
            }
            if (item.Content != null)
            {
                var ee = item.Content is string ? null : (item.Content as IEnumerable)?.GetEnumerator() ?? item.Content as IEnumerator;
                if (ee == null)
                {
                    Wirte(Fields.Content, item.Content.ToString());
                }
                else
                {
                    while (ee.MoveNext())
                    {
                        Wirte(Fields.Content, ee.Current?.ToString());
                    }
                }
            }
            if (item.File != null)
            {
                Wirte(Fields.File, item.File);
            }
            if (item.Method != null)
            {
                Wirte(Fields.Method, item.Method);
            }
            if (item.LineNumber != 0)
            {
                Wirte(Fields.LineNumber, item.LineNumber.ToString());
            }
            if (item.Callstack != null)
            {
                Wirte(Fields.Callstack, item.Callstack);
            }

            Append(_Line);
            AppendLine();
            AppendLine();
        }

        /// <summary>
        /// 初始化写入器
        /// </summary>
        /// <param name="listener"> </param>
        /// <exception cref="ArgumentOutOfRangeException"> fileLimit 属性错误:文件限制大小不能小于1048576(1MB)或大于1073741824(1GB) </exception>
        /// <exception cref="ArgumentNullException"> initializeData 属性不能为空 </exception>
        /// <exception cref="ArgumentNullException"> 参数 <see cref="listener" /> 不能为空 </exception>
        public override void Initialize(TraceListener listener)
        {
            FileMaxSize = DEFAULT_FILE_MAX_SIZE;
            var invalidChars = Path.GetInvalidFileNameChars();
            var name = new string(listener.Name.Where(c => !invalidChars.Contains(c)).ToArray()); //将名字中不符合文件名标准的字符都过滤
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            if (Path.GetDirectoryName(baseDir)?.ToLowerInvariant() == "bin")
            {
                DirectoryPath = Path.Combine(baseDir, "..", name);
            }
            else
            {
                DirectoryPath = Path.Combine(baseDir, name);
            }
            if (Directory.Exists(DirectoryPath) == false)
            {
                Directory.CreateDirectory(DirectoryPath);
            }
            base.Initialize(listener);
        }

        #endregion Public Methods
    }
}