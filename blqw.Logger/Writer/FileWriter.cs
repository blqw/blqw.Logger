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

        private static readonly byte colon = Encoding.UTF8.GetBytes(":")[0];
        private static readonly byte semicolon = Encoding.UTF8.GetBytes(";")[0];
        private static readonly byte comma = Encoding.UTF8.GetBytes(",")[0];
        private static readonly byte space = Encoding.UTF8.GetBytes(" ")[0];
        private static readonly byte[] newline = Encoding.UTF8.GetBytes(Environment.NewLine);
        private readonly long _filesize;
        private readonly string _path;
        private FileStream _writer;

        /// <summary>
        /// 初始化文件写入器
        /// </summary>
        /// <param name="path"> 文件默认路径 </param>
        /// <param name="filesize"> 单个文件大小 </param>
        /// <exception cref="ArgumentNullException"> <paramref name="path" /> is <see langword="null" />. </exception>
        /// <exception cref="ArgumentOutOfRangeException"> <paramref name="filesize" />小于1 </exception>
        public FileWriter(string path, long filesize)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }
            if (filesize <= 1)
            {
                throw new ArgumentOutOfRangeException(nameof(filesize));
            }
            _path = Path.Combine(path, "{0:yyyyMMddHH}");
            _filesize = filesize;
            ChangeFileIfFull();
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
        /// <exception cref="NullReferenceException"> The address of <paramref name="_writer" /> is a null pointer. </exception>
        public void Dispose()
        {
            var writer = Interlocked.Exchange(ref _writer, null);
            writer?.Close();
            writer?.Dispose();
        }

        /// <summary>
        /// 如果文件已满则改变当前文件
        /// </summary>
        /// <exception cref="ObjectDisposedException">流已关闭</exception>
        public void ChangeFileIfFull()
        {
            if (_writer == null)
            {
                throw new ObjectDisposedException("流已关闭");
            }
            Logger?.Entry();
            if (_writer.Length < _filesize)
            {
                Logger?.Exit();
                return;
            }
            if (SetTomorrow(ref _NextDeleteFileTicks))
            {
                Task.Run(() => Delete(2));
            }
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, string.Format(_path, DateTime.Now));
            var max = GetMaxFileNumber(path);
            while (true)
            {
                var file = GetFile(path, max);
                if (file.Directory?.Exists == false)
                {
                    file.Directory.Create();
                }
                try
                {
                    _writer = file.Open(FileMode.Append, FileAccess.Write, FileShare.Read);
                    CurrentFilePath = file.FullName;
                    break;
                }
                catch (Exception ex)
                {
                    max++;
                    Logger?.Error(ex, $"文件({file.FullName})打开失败");
                }
            }
            Logger?.Exit();
        }

        /// <summary>
        /// 将 <paramref name="ticks"/> 
        /// </summary>
        /// <param name="ticks"></param>
        /// <returns></returns>
        private bool SetTomorrow(ref long ticks)
        {
            var saved = ticks;
            var now = DateTime.Today.AddDays(1).Ticks;
            if (saved >= now)
            {
                return false;
            }
            if (Interlocked.CompareExchange(ref ticks, now, saved) == saved)
            {
                return saved < now;
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
                if (file.Length < _filesize) //文件大小没有超过限制
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
        /// 
        /// </summary>
        /// <param name="days"></param>
        private void Delete(int days)
        {
            Logger?.Entry();
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, string.Format(_path, DateTime.MinValue));
            var root = Directory.GetParent(path);
            if (root.Exists == false)
            {
                Logger?.Exit();
                return;
            }
            var time = DateTime.Today.AddDays(-days);
            foreach (var dir in root.GetDirectories())
            {
                if (dir.LastWriteTime <= time)
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
        /// <param name="encoding"> 字符编码 </param>
        /// <exception cref="IOException"> 发生了 I/O 错误。- 或 -另一个线程可能已导致操作系统的文件句柄位置发生意外更改。 </exception>
        /// <exception cref="ObjectDisposedException"> 流已关闭。 </exception>
        /// <exception cref="NotSupportedException"> 当前流实例不支持写入。 </exception>
        public FileWriter Append(string text, Encoding encoding = null)
        {
            if (_writer == null)
            {
                throw new ObjectDisposedException("流已关闭");
            }
            if (string.IsNullOrWhiteSpace(text))
            {
                return this;
            }
            var buffer = (encoding ?? Encoding.UTF8).GetBytes(text);
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
            if (buffer == null || buffer.Length == 0)
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
            switch (newline.Length)
            {
                case 1:
                    _writer.WriteByte(newline[0]);
                    break;
                case 2:
                    _writer.WriteByte(newline[0]);
                    _writer.WriteByte(newline[1]);
                    break;
                default:
                    _writer.Write(newline, 0, newline.Length);
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
            _writer.WriteByte(space);
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
            _writer.WriteByte(semicolon);
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
            _writer.WriteByte(colon);
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
            _writer.WriteByte(comma);
            return this;
        }
    }
}