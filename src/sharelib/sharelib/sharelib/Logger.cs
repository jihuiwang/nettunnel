using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace sharelib
{
    public class Logger
    {
        private static ConcurrentQueue<string> msgs = new ConcurrentQueue<string>();
        private static string dir = Directory.GetCurrentDirectory() + "/log/";
        private static int isHandling = 0;

        public static void Init()
        {
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }

        public static void Log(string msg)
        {
            msgs.Enqueue(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\n" + msg + "\n\n");
            _ = write();
        }

        private static async Task write()
        {
            if (Interlocked.CompareExchange(ref isHandling, 1, 0) == 1)
            {
                return;
            }

            using (FileStream fs = new FileStream(dir + DateTime.Now.ToString("yyyyMMdd") + ".log", FileMode.OpenOrCreate))
            {
                msgs.TryDequeue(out var str);
                if (!string.IsNullOrWhiteSpace(str))
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(str);
                    fs.Seek(0, SeekOrigin.End);
                    await fs.WriteAsync(bytes, 0, bytes.Length);
                    //await fs.FlushAsync();
                }                
            }

            Interlocked.Decrement(ref isHandling);

            if (msgs.Count > 0)
            {
                _ = Task.Run(write);
            }
        }
    }
}
