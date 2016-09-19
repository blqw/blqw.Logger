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
    public sealed class FileWriter : IDisposable
    {
        /// <summary>
        /// 下一次删除文件的时间
        /// </summary>
        private static long _NextDeleteFileTicks;

        /// <summary>
        /// 冒号(:)
        /// </summary>
        private static readonly byte _Colon = Encoding.UTF8.GetBytes(":")[0];

        /// <summary>
        /// 分号(;)
        /// </summary>
        private static readonly byte _Semicolon = Encoding.UTF8.GetBytes(";")[0];

        /// <summary>
        /// 逗号(,)
        /// </summary>
        private static readonly byte _Comma = Encoding.UTF8.GetBytes(",")[0];

        /// <summary>
        /// 空格( )
        /// </summary>
        private static readonly byte _Space = Encoding.UTF8.GetBytes(" ")[0];

        /// <summary>
        /// 新行(<seealso cref="Environment.NewLine" />)
        /// </summary>
        private static readonly byte[] _Newline = Encoding.UTF8.GetBytes(Environment.NewLine);

        /// <summary>
        /// UTF8格式的txt文件头
        /// </summary>
        private static readonly byte[] _Utf8Head = { 239, 187, 191 };

        /// <summary>
        /// 日志文件限制大小
        /// </summary>
        private readonly long _filelimit;

        /// <summary>
        /// 日志文件所在文件夹路径
        /// </summary>
        private readonly string _path;

        /// <summary>
        /// 文件写入器
        /// </summary>
        private FileStream _writer;

        /// <summary>
        /// 初始化文件写入器
        /// </summary>
        /// <param name="path"> 文件默认路径 </param>
        /// <param name="filelimit"> 单个文件大小 </param>
        /// <exception cref="ArgumentNullException"> <paramref name="path" /> is <see langword="null" />. </exception>
        /// <exception cref="ArgumentOutOfRangeException"> <paramref name="filelimit" />小于1 </exception>
        public FileWriter(string path, long filelimit)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }
            if (filelimit <= 1)
            {
                throw new ArgumentOutOfRangeException(nameof(filelimit));
            }
            _path = Path.Combine(path, "{0:yyyyMMddHH}");
            _filelimit = filelimit;
            SetNewWirteFile();
        }

        /// <summary>
        /// 日志写入
        /// </summary>
        public TraceSource Logger { get; set; }

        /// <summary>
        /// 当前正在写入的文件
        /// </summary>
        public string CurrentFilePath { get; private set; }

        /// <summary>
        /// 执行与释放或重置非托管资源关联的应用程序定义的任务。
        /// </summary>
        public void Dispose()
        {
            var writer = Interlocked.Exchange(ref _writer, null);
            try
            {
                writer?.Flush();
            }
            catch
            {
                // ignored
            }
            writer?.Dispose();
        }

        /// <summary>
        /// 设置当前写入文件
        /// </summary>
        private void SetNewWirteFile()
        {
            if (CheckAndSetDeletedFileTime()) //检查时间,判断是否启动删除文件程序
            {
                Task.Run(() => Delete(2));
            }
            //获取写入文件夹
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, string.Format(_path, DateTime.Now));
            if (Directory.Exists(path) == false)
            {
                Directory.CreateDirectory(path);
            }
            //获取最大文件编号
            var max = GetMaxFileNumber(path);
            while (true)
            {
                var file = GetFile(path, max);
                try
                {
                    var writer = new FileStream(file.FullName, FileMode.Append, FileAccess.Write, FileShare.Read,
                        (int) _filelimit); //尝试打开文件
                    var source = Interlocked.Exchange(ref _writer, writer);
                    source?.Flush();
                    source?.Dispose(); //释放前一个文件流写入通道
                    if (writer.Position == 0)
                    {
                        writer.Write(_Utf8Head, 0, _Utf8Head.Length); //如果文件是新的,写入文件头
                    }
                    CurrentFilePath = file.FullName;
                    break;
                }
                catch (Exception ex)
                {
                    max++; //如果文件打开失败,忽略这个文件
                    Logger?.Error(ex, $"文件({file.FullName})打开失败");
                }
            }
        }

        /// <summary>
        /// 如果文件已满则改变当前文件
        /// </summary>
        /// <exception cref="ObjectDisposedException"> 流已关闭 </exception>
        public void ChangeFileIfFull()
        {
            Logger?.Entry();
            if (_writer == null)
            {
                throw new ObjectDisposedException("流已关闭");
            }
            if (_writer.Length < _filelimit)
            {
                Logger?.Exit();
                return;
            }
            SetNewWirteFile();
            Logger?.Exit();
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
                if (file.Length < _filelimit) //文件大小没有超过限制
                {
                    return file;
                }
                fileNumber = fileNumber + 1;
            }
        }

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
        /// 删除文件
        /// </summary>
        /// <param name="days"> 删除几天之前的文件 </param>
        private void Delete(int days)
        {
            Logger?.Entry();
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, string.Format(_path, DateTime.MinValue));
            var root = Directory.GetParent(path); //获取父级文件夹
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
        /// 追加字符串到文件流
        /// </summary>
        /// <param name="text"> 被追加到文件的字符串 </param>
        /// <exception cref="IOException"> 发生了 I/O 错误。- 或 -另一个线程可能已导致操作系统的文件句柄位置发生意外更改。 </exception>
        /// <exception cref="ObjectDisposedException"> 流已关闭。 </exception>
        /// <exception cref="NotSupportedException"> 当前流实例不支持写入。 </exception>
        public FileWriter Append(string text)
        {
            if (_writer == null)
            {
                throw new ObjectDisposedException("流已关闭");
            }
            if (string.IsNullOrWhiteSpace(text))
            {
                return this;
            }
            var buffer = Encoding.UTF8.GetBytes(text);
            _writer.Write(buffer, 0, buffer.Length);
            return this;
        }

        /// <summary>
        /// 追加字节到文件流
        /// </summary>
        /// <param name="buffer"> 被追加到文件的字节数组 </param>
        /// <exception cref="IOException"> 发生了 I/O 错误。- 或 -另一个线程可能已导致操作系统的文件句柄位置发生意外更改。 </exception>
        /// <exception cref="ObjectDisposedException"> 流已关闭。 </exception>
        /// <exception cref="NotSupportedException"> 当前流实例不支持写入。 </exception>
        public FileWriter Append(byte[] buffer)
        {
            if (_writer == null)
            {
                throw new ObjectDisposedException("流已关闭");
            }
            if ((buffer == null) || (buffer.Length == 0))
            {
                return this;
            }
            _writer.Write(buffer, 0, buffer.Length);
            return this;
        }

        /// <summary>
        /// 追加一个新行
        /// </summary>
        /// <exception cref="IOException"> 发生了 I/O 错误。- 或 -另一个线程可能已导致操作系统的文件句柄位置发生意外更改。 </exception>
        /// <exception cref="ObjectDisposedException"> 流已关闭。 </exception>
        /// <exception cref="NotSupportedException"> 当前流实例不支持写入。 </exception>
        public FileWriter AppendLine()
        {
            if (_writer == null)
            {
                throw new ObjectDisposedException("流已关闭");
            }
            switch (_Newline.Length)
            {
                case 1:
                    _writer.WriteByte(_Newline[0]);
                    break;
                case 2:
                    _writer.WriteByte(_Newline[0]);
                    _writer.WriteByte(_Newline[1]);
                    break;
                default:
                    _writer.Write(_Newline, 0, _Newline.Length);
                    break;
            }
            return this;
        }

        /// <summary>
        /// 追加一个空格
        /// </summary>
        /// <exception cref="ObjectDisposedException"> 流已关闭。 </exception>
        /// <exception cref="NotSupportedException"> 当前流实例不支持写入。 </exception>
        public FileWriter AppendWhiteSpace()
        {
            if (_writer == null)
            {
                throw new ObjectDisposedException("流已关闭");
            }
            _writer.WriteByte(_Space);
            return this;
        }

        /// <summary>
        /// 追加一个分号
        /// </summary>
        /// <exception cref="ObjectDisposedException"> 流已关闭。 </exception>
        /// <exception cref="NotSupportedException"> 当前流实例不支持写入。 </exception>
        public FileWriter AppendSemicolon()
        {
            if (_writer == null)
            {
                throw new ObjectDisposedException("流已关闭");
            }
            _writer.WriteByte(_Semicolon);
            return this;
        }

        /// <summary>
        /// 追加一个冒号
        /// </summary>
        /// <exception cref="ObjectDisposedException"> 流已关闭。 </exception>
        /// <exception cref="NotSupportedException"> 当前流实例不支持写入。 </exception>
        public FileWriter AppendColon()
        {
            if (_writer == null)
            {
                throw new ObjectDisposedException("流已关闭");
            }
            _writer.WriteByte(_Colon);
            return this;
        }

        /// <summary>
        /// 追加一个逗号
        /// </summary>
        /// <exception cref="ObjectDisposedException"> 流已关闭。 </exception>
        /// <exception cref="NotSupportedException"> 当前流实例不支持写入。 </exception>
        public FileWriter AppendComma()
        {
            if (_writer == null)
            {
                throw new ObjectDisposedException("流已关闭");
            }
            _writer.WriteByte(_Comma);
            return this;
        }

        /// <summary>
        /// 刷新缓存到
        /// </summary>
        /// <exception cref="IOException"> 发生了 I/O 错误。 </exception>
        /// <exception cref="ObjectDisposedException"> 流已关闭。 </exception>
        public void Flush() => _writer?.Flush();
    }
}