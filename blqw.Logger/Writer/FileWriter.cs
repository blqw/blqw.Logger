using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace blqw.Logger
{
    /// <summary>
    /// 文件写入器
    /// </summary>
    public class FileWriter : IWriter
    {

        #region Private Fields

        /// <summary>
        /// 冒号(:)
        /// </summary>
        private static readonly byte _Colon = Encoding.UTF8.GetBytes(":")[0];

        /// <summary>
        /// 逗号(,)
        /// </summary>
        private static readonly byte _Comma = Encoding.UTF8.GetBytes(",")[0];

        /// <summary>
        /// 新行( <seealso cref="Environment.NewLine" />)
        /// </summary>
        private static readonly byte[] _Newline = Encoding.UTF8.GetBytes(Environment.NewLine);

        /// <summary>
        /// 分号(;)
        /// </summary>
        private static readonly byte _Semicolon = Encoding.UTF8.GetBytes(";")[0];

        /// <summary>
        /// 空格( )
        /// </summary>
        private static readonly byte _Space = Encoding.UTF8.GetBytes(" ")[0];

        /// <summary>
        /// UTF8格式的txt文件头
        /// </summary>
        private static readonly byte[] _Utf8Head = Encoding.UTF8.GetPreamble();

        /// <summary>
        /// 下一次删除文件的时间
        /// </summary>
        private static long _NextDeleteFileTicks;

        /// <summary>
        /// 文件流
        /// </summary>
        private FileStream _innerStream;

        #endregion Private Fields

        #region Public Properties

        /// <summary>
        /// 批处理最大数量
        /// </summary>
        public virtual int BatchMaxCount { get; protected set; }

        /// <summary>
        /// 批处理最大等待时间
        /// </summary>
        public virtual TimeSpan BatchMaxWait { get; protected set; }

        /// <summary>
        /// 文件保留天数
        /// </summary>
        public virtual int FileRetentionDays { get; protected set; }

        /// <summary>
        /// 当前正在写入的文件
        /// </summary>
        public string FilePath { get; private set; }

        /// <summary>
        /// 日志文件限制大小
        /// </summary>
        public virtual long FileMaxSize { get; protected set; }

        /// <summary>
        /// 日志文件所在文件夹路径
        /// </summary>
        public virtual string DirectoryPath { get; protected set; }

        /// <summary>
        /// 日志写入
        /// </summary>
        public TraceSource Logger { get; set; }

        /// <summary>
        /// 写入器名称
        /// </summary>
        public virtual string Name { get; protected set; }

        /// <summary>
        /// 文件流
        /// </summary>
        public FileStream InnerStream
        {
            get
            {
                if (_innerStream == null)
                {
                    if (DirectoryPath == null)
                    {
                        throw new NotSupportedException("尚未初始化");
                    }
                    throw new ObjectDisposedException("对象已释放");
                }
                return _innerStream;
            }
        }

        #endregion Public Properties

        #region Public Methods

        #region Append
        /// <summary>
        /// 追加字符串到文件流
        /// </summary>
        /// <param name="text"> 被追加到文件的字符串 </param>
        /// <exception cref="IOException"> 发生了 I/O 错误。- 或 -另一个线程可能已导致操作系统的文件句柄位置发生意外更改。 </exception>
        /// <exception cref="ObjectDisposedException"> 流已关闭。 </exception>
        /// <exception cref="NotSupportedException"> 当前流实例不支持写入。 </exception>
        public void Append(string text) => Append(text == null ? null : Encoding.UTF8.GetBytes(text));


        /// <summary>
        /// 追加字节到文件流
        /// </summary>
        /// <param name="buffer"> 被追加到文件的字节数组 </param>
        /// <exception cref="IOException"> 发生了 I/O 错误。- 或 -另一个线程可能已导致操作系统的文件句柄位置发生意外更改。 </exception>
        /// <exception cref="ObjectDisposedException"> 流已关闭。 </exception>
        /// <exception cref="NotSupportedException"> 当前流实例不支持写入。 </exception>
        public void Append(byte[] buffer)
        {
            var length = buffer?.Length ?? 0;
            if (length > 0)
            {
                InnerStream.Write(buffer, 0, buffer.Length);
            }
        }

        /// <summary>
        /// 追加日志
        /// </summary>
        /// <param name="item"> </param>
        public virtual void Append(LogItem item) => Append(item.ToString());

        /// <summary>
        /// 追加字节
        /// </summary>
        /// <param name="value"> </param>
        /// <returns> </returns>
        public void AppendByte(byte value) => InnerStream.WriteByte(value);

        /// <summary>
        /// 追加冒号
        /// </summary>
        /// <exception cref="ObjectDisposedException"> 流已关闭。 </exception>
        /// <exception cref="NotSupportedException"> 当前流实例不支持写入。 </exception>
        public void AppendColon() => InnerStream.WriteByte(_Colon);

        /// <summary>
        /// 追加逗号
        /// </summary>
        /// <exception cref="ObjectDisposedException"> 流已关闭。 </exception>
        /// <exception cref="NotSupportedException"> 当前流实例不支持写入。 </exception>
        public void AppendComma() => InnerStream.WriteByte(_Comma);

        /// <summary>
        /// 追加新行
        /// </summary>
        /// <exception cref="IOException"> 发生了 I/O 错误。- 或 -另一个线程可能已导致操作系统的文件句柄位置发生意外更改。 </exception>
        /// <exception cref="ObjectDisposedException"> 流已关闭。 </exception>
        /// <exception cref="NotSupportedException"> 当前流实例不支持写入。 </exception>
        public void AppendLine() => InnerStream.Write(_Newline, 0, _Newline.Length);

        /// <summary>
        /// 追加分号
        /// </summary>
        /// <exception cref="ObjectDisposedException"> 流已关闭。 </exception>
        /// <exception cref="NotSupportedException"> 当前流实例不支持写入。 </exception>
        public void AppendSemicolon() => InnerStream.WriteByte(_Semicolon);

        /// <summary>
        /// 追加空格
        /// </summary>
        /// <exception cref="ObjectDisposedException"> 流已关闭。 </exception>
        /// <exception cref="NotSupportedException"> 当前流实例不支持写入。 </exception>
        public void AppendWhiteSpace() => InnerStream.WriteByte(_Space);

        #endregion

        /// <summary>
        /// 如果文件已满则改变当前文件
        /// </summary>
        /// <exception cref="ObjectDisposedException"> 流已关闭 </exception>
        public void ChangeFileIfFull()
        {
            Logger?.Entry();
            if (InnerStream.Length < FileMaxSize)
            {
                Logger?.Exit();
                return;
            }
            SetNewWirteFile();
            Logger?.Exit();
        }

        /// <summary>
        /// 执行与释放或重置非托管资源关联的应用程序定义的任务。
        /// </summary>
        public virtual void Dispose()
        {
            using (var writer = Interlocked.Exchange(ref _innerStream, null))
            {
                writer?.Flush();
            }
        }

        /// <summary>
        /// 刷新缓存到
        /// </summary>
        /// <exception cref="IOException"> 发生了 I/O 错误。 </exception>
        /// <exception cref="ObjectDisposedException"> 流已关闭。 </exception>
        public virtual void Flush() => _innerStream?.Flush();

        /// <summary>
        /// 获取跟踪侦听器支持的自定义特性。
        /// </summary>
        /// <returns> 为跟踪侦听器支持的自定义特性命名的字符串数组；或者如果没有自定义特性，则为 null。 </returns>
        public virtual string[] GetSupportedAttributes() => new[] { "directoryPath", "fileMaxSize", "fileRetentionDays" };

        /// <summary>
        /// 初始化写入器
        /// </summary>
        /// <param name="listener"> </param>
        /// <exception cref="ArgumentOutOfRangeException"> fileLimit 属性错误:文件限制大小不能小于1048576(1MB)或大于1073741824(1GB) </exception>
        /// <exception cref="ArgumentNullException"> initializeData 属性不能为空 </exception>
        /// <exception cref="ArgumentNullException"> 参数 <see cref="listener" /> 不能为空 </exception>
        public virtual void Initialize(TraceListener listener)
        {
            if (listener == null)
            {
                throw new ArgumentNullException(nameof(listener));
            }
            var directoryPath = listener.Attributes["directoryPath"];
            var fileMaxSize = listener.Attributes["fileMaxSize"];
            var fileRetentionDays = listener.Attributes["fileRetentionDays"];
            if (directoryPath != null)
            {
                DirectoryPath = directoryPath;
            }
            else if (DirectoryPath == null)
            {
                throw new ArgumentNullException(nameof(directoryPath), "[directoryPath]不能为空");
            }
            if (fileMaxSize != null)
            {
                long limit;
                long.TryParse(fileMaxSize, out limit);
                if ((limit < 1*1024*1024) || (limit > 1024*1024*1024))
                {
                    throw new ArgumentOutOfRangeException(nameof(fileMaxSize), "[fileMaxSize]文件限制大小不能小于1048576(1MB)或大于1073741824(1GB)");
                }
                FileMaxSize = limit;
            }
            else if (FileMaxSize == 0)
            {
                FileMaxSize = 5 * 1024 * 1024; //兆
            }
            if (fileRetentionDays != null)
            {
                int days;
                int.TryParse(fileRetentionDays, out days);
                if (days < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(fileRetentionDays), "[fileRetentionDays]文件保留天数不能少于1天");
                }
                FileRetentionDays = days;
            }
            else if (FileRetentionDays == 0)
            {
                FileRetentionDays = 2;
            }

            SetNewWirteFile();
        }

        #endregion Public Methods

        #region Private Methods


        /// <summary>
        /// 获取文件的最大编号
        /// </summary>
        /// <param name="path"> </param>
        /// <returns> </returns>
        private static int GetMaxFileNumber(string path)
        {
            if (!Directory.Exists(path))
            {
                return 0;
            }

            var number = 0;
            var files = Directory.GetFiles(path, "*.log", SearchOption.TopDirectoryOnly);
            foreach (var f in files)
            {
                int i;
                if (int.TryParse(Path.GetFileNameWithoutExtension(f), out i) && (i > number))
                {
                    number = i;
                }
            }
            return number;
        }

        /// <summary>
        /// 检查并设置删除文件的时间,如果设置成功返回true
        /// </summary>
        /// <returns> </returns>
        private bool CheckAndSetDeletedFileTime()
        {
            var prev = _NextDeleteFileTicks;
            var last = DateTime.Today.AddDays(1).Ticks; //时间加1天
            if (prev >= last)
            {
                return false;
            }
            if (Interlocked.CompareExchange(ref _NextDeleteFileTicks, last, prev) == prev) //原子操作
            {
                return prev < last;
            }
            return false;
        }

        /// <summary>
        /// 删除文件
        /// </summary>
        /// <param name="days"> 删除几天之前的文件 </param>
        private void Delete(int days)
        {
            Logger?.Entry();
            var root = new DirectoryInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DirectoryPath));
            if (root.Exists == false) //如果不存在,放弃操作
            {
                Logger?.Exit();
                return;
            }
            var time = DateTime.Today.AddDays(-days);
            foreach (var dir in root.GetDirectories()) //遍历文件夹中的所有子文件夹
            {
                if (dir.CreationTime <= time) //创建时间小于指定时间则删除
                {
                    try
                    {
                        dir.Delete(true);
                    }
                    catch (Exception ex)
                    {
                        Logger?.Error(ex, $"删除({dir.FullName})下文件失败");
                    }
                }
            }
            Logger?.Exit();
        }

        /// <summary>
        /// 获取一个可以写入数据的文件
        /// </summary>
        /// <param name="path"> 文件路径 </param>
        /// <param name="fileNumber"> 文件编号 </param>
        /// <returns> </returns>
        private FileInfo GetFile(string path, int fileNumber)
        {
            while (true)
            {
                var file = new FileInfo(Path.Combine(path, fileNumber + ".log"));
                if (file.Exists == false)
                {
                    return file;
                }
                if (file.Length < FileMaxSize) //文件大小没有超过限制
                {
                    return file;
                }
                fileNumber = fileNumber + 1;
            }
        }

        /// <summary>
        /// 设置当前写入文件
        /// </summary>
        private void SetNewWirteFile()
        {
            if (CheckAndSetDeletedFileTime()) //检查时间,判断是否启动删除文件程序
            {
                Task.Run(() => Delete(FileRetentionDays));
            }
            //获取写入文件夹
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DirectoryPath, DateTime.Now.ToString("yyyyMMddHH"));
            if (Directory.Exists(path) == false)
            {
                Directory.CreateDirectory(path);
            }
            //获取最大文件编号
            var max = GetMaxFileNumber(path);
            var tryCount = 0;
            while (tryCount < 50)
            {
                var file = GetFile(path, max);
                try
                {
                    var stream = new FileStream(file.FullName, FileMode.Append, FileAccess.Write, FileShare.Read, (int)FileMaxSize + 4069); //尝试打开文件
                    var source = Interlocked.Exchange(ref _innerStream, stream); //切换文件流
                    source?.Flush();
                    source?.Dispose(); //释放前一个文件流写入通道
                    if (stream.Position == 0)
                    {
                        stream.Write(_Utf8Head, 0, _Utf8Head.Length); //如果文件是新的,写入文件头
                    }
                    FilePath = file.FullName;
                    return;
                }
                catch (Exception ex)
                {
                    max++; //如果文件打开失败,忽略这个文件
                    Logger?.Error(ex, $"文件({file.FullName})打开失败");
                }
                tryCount++;
            }
            throw new InvalidOperationException($"日志尝试写入路径`{path}`失败");
        }

        #endregion Private Methods
    }
}