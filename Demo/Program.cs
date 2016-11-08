using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using blqw;
using blqw.IOC;
using blqw.Logger;

namespace Demo
{
    class Program
    {
        private static TraceSource Logger { get; } = InitLogger();

        private static TraceSource InitLogger()
        {
            var logger = new LoggerSource("test", SourceLevels.All);
            //logger.Listeners.Clear();
            //logger.Listeners.Add(new FileTraceListener(new MyLogWriter("d:\\test_logs")));
            return logger;
        }

        //private static TraceSource Logger { get; } = new LoggerSource("test", SourceLevels.All);

        static void Main(string[] args)
        {
            Console.WriteLine(typeof(MyLogWriter).AssemblyQualifiedName);
            Logger.Log(TraceEventType.Verbose, "测试");
            try
            {
                int i = 0;
                i = i / i;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "测试异常");
            }
            Logger.Flush();
            Console.Read();
        }
    }

    public class MyLogWriter : FileWriter
    {
        public MyLogWriter() { }

        public MyLogWriter(string dir)
        {
            DirectoryPath = dir;
        }

        /// <summary>
        /// 追加日志
        /// </summary>
        /// <param name="item"> </param>
        public override void Append(LogItem item)
        {
            if (!item.IsFirst && !item.IsLast)
            {
                base.Append(item.ToJsonString());
                base.AppendLine();
            }
        }
    }
}
