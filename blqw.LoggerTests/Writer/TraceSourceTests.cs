using Microsoft.VisualStudio.TestTools.UnitTesting;
using blqw.Logger;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using blqw.IOC;

namespace blqw.Logger.Tests
{
    [TestClass()]
    public class TraceSourceTests
    {

        private static TraceSource Logger { get; } = InitLogger();

        private static TraceSource InitLogger()
        {
            var logger = new LoggerSource("test", SourceLevels.All);
            return logger;
        }
        
        [TestMethod()]
        public void InitializeTest()
        {
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
            Thread.Sleep(2000);
        }
    }
}