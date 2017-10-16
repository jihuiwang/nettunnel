using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Threading;

namespace sharelib
{
    public class SocketConnection
    {
        private NetBuffer buffer;
        private int buffsize;
        private Socket socket;
        private ISocketHandler handler;
        private int processCount;
        private int isHandling = 0;
        private Task tReceive;

        public Frame CurrentFrame { get; set; }
        public NetBuffer Buffer { get { return buffer; } }
        public Socket Socket { get { return socket; } }
        public SocketConnection MapConnection { get; set; }
        public bool IsRequestAdded { get; set; }

        public event Action OnClose;

        public SocketConnection(Socket socket, ISocketHandler handler)
        {
            buffer = new NetBuffer();
            this.buffsize = 2048;
            this.socket = socket;
            this.handler = handler;
            this.processCount = 0;
        }

        public void StartAsync()
        {
            //buffer.OnReceived += async () => await schedule();
            buffer.OnReceived += schedule;
            tReceive = StartReceive();
        }

        public async Task StartReceive()
        {
            try
            {
                while (true)
                {
                    ArraySegment<byte> seg = new ArraySegment<byte>(GetBuffer());
                    var bytesReceived = await socket.ReceiveAsync(seg, SocketFlags.None);

                    if (bytesReceived == 0)
                    {
                        // FIN
                        break;
                    }
                    buffer.Add(new ArraySegment<byte>(seg.Array, 0, bytesReceived));
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex.Message);
                Console.WriteLine(ex.Message);
            }
            finally
            {
                closeConnection();
            }
        }

        private void closeConnection()
        {
            OnClose?.Invoke();
            Console.WriteLine("socket closed: " + socket.RemoteEndPoint.ToString());
            socket.Dispose();
        }

        private async Task schedule()
        {
            Task continueTask = null;

            if (Interlocked.CompareExchange(ref isHandling, 1, 0) == 1)
            {
                return;
            }

            try
            {
                if (await handler.ProcessData(this))
                {
                    if(buffer.Count > 0)
                    {
                        processCount++;
                        continueTask = new Task(async () => await schedule());
                    }
                    else
                    {
                        processCount = 0;
                    }

                    if (processCount > 100)
                    {
                        throw new Exception("max process count in handler");
                    }
                }                
            }
            catch (Exception ex)
            {
                Logger.Log("from schedule: " + ex.Message);
                Console.WriteLine("from schedule: " + ex.Message);
                continueTask = null;
                closeConnection();
            }
            finally
            {
                Interlocked.Decrement(ref isHandling);
                if (continueTask != null)
                {
                    continueTask.Start();
                }
            }
        }

        public byte[] GetBuffer()
        {
            //todo: add a memory pool to improve performance
            return new byte[buffsize];
        }
    }
}
