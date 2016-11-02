using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Demo
{
    class Program
    {
        static void Main(string[] args)
        {
            Trace.WriteLine((object)"b", "a");
            //for (int i = 0; i < 100; i++)
            //{
            //    Trace.Write("aaaaa");
            //    Trace.WriteLine(Guid.NewGuid(), "uuid");
            //}

            //try
            //{
            //    int i = 0;
            //    i = i / i;
            //}
            //catch (Exception e)
            //{
            //    Trace.WriteLine(e, "出现异常");
            //}
            Trace.Flush();
            Console.Read();
        }
    }
}
