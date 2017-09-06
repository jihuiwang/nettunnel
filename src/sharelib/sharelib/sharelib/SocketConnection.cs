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
        //private int receiveCount;
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
            //receiveCount = 0;
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
                        continueTask = new Task(async () => await schedule());
                    }
                }                
            }
            catch (Exception ex)
            {
                Console.WriteLine("from schedule: " + ex.Message);
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
