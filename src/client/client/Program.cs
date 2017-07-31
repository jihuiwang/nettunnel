using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace client
{
    class Program
    {
        public static TcpClient Tcp;
        public static ConcurrentDictionary<string, ForwardClient> AllClients;
        private static ManualResetEventSlim semaphore = new ManualResetEventSlim(true);
        private static List<ArraySegment<byte>> buffer = new List<ArraySegment<byte>>();
        private static ArraySegment<byte> heartbeatdata = new ArraySegment<byte>(new byte[34]);
        public static IConfigurationRoot Configuration { get; set; }
        private static string serverDomain;
        private static int serverPort;

        static void Main(string[] args)
        {
            var builder = new ConfigurationBuilder()
             .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("config.json");

            Configuration = builder.Build();
            serverDomain = Configuration["servername"];
            serverPort = Convert.ToInt32(Configuration["serverport"]);

            Tcp = new TcpClient();
            AllClients = new ConcurrentDictionary<string, ForwardClient>();

            Task.Run(() => Start());

            Console.WriteLine("Staring...");
            Console.Read();
        }

        static async Task Start()
        {
            try
            {
                if (!Tcp.Connected)
                {
                    await Tcp.ConnectAsync(serverDomain, serverPort);
                    Console.WriteLine("server connected, local: " + Tcp.Client.LocalEndPoint.ToString());
                    //await Tcp.Client.SendAsync(new ArraySegment<byte>(new byte[34]), SocketFlags.None);
                    _ = StartHeartBeat();
                }
            }
            catch (Exception ex)
            {
                _ = Task.Run(() => Start());
                return;
            }

            try
            {
                while (true)
                {
                    ArraySegment<byte> seg = new ArraySegment<byte>(new byte[2048]);
                    var bytesReceived = await Tcp.Client.ReceiveAsync(seg, SocketFlags.None);

                    if (bytesReceived == 0)
                    {
                        // FIN
                        break;
                    }

                    try
                    {
                        AppendData(new ArraySegment<byte>(seg.Array, 0, bytesReceived));
                        await ProcessData();
                    }
                    catch
                    {

                    }
                }
                
            }
            finally
            {
                Tcp = new TcpClient();
                _ = Task.Run(() => Start());
            }            
        }

        static async Task StartHeartBeat()
        {
            while (true)
            {
                await Tcp.Client.SendAsync(heartbeatdata, SocketFlags.None);
                await Task.Delay(60000);
            }
        }

        static async Task ProcessData()
        {
            WaitBuffer();

            try
            {
                List<ArraySegment<byte>> data = Helper.SplitArraySeg(buffer);

                while (data != null)
                {
                    await Helper.SendToTarget(data);
                    data = Helper.SplitArraySeg(buffer);
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                ReleaseBuffer();
            }

            //string remote = Encoding.UTF8.GetString(data.Array, data.Offset, pos - data.Offset);

            //ForwardClient client = AllClients.GetOrAdd(remote, new ForwardClient("localhost", 3300, remote));
            //if (!client.IsConnected)
            //{
            //    await client.ConnectAsync();
            //    client.StartRecieve();
            //}

            ////byte[] fdata = new byte[data.Length - pos - 2];
            //ArraySegment<byte> fdata = new ArraySegment<byte>(data.Array, pos + 2, data.Count - (pos - data.Offset) - 2);
            ////Array.Copy(data, pos + 2, fdata, 0, fdata.Length);

            //client.SendAsync(fdata);
        }

        static void WaitBuffer()
        {
            semaphore.Wait();
        }

        static void ReleaseBuffer()
        {
            semaphore.Set();
        }

        static void AppendData(ArraySegment<byte> data)
        {
            WaitBuffer();
            buffer.Add(data);
            ReleaseBuffer();
        }
    }

    public class ForwardClient
    {
        TcpClient client;
        string host;
        int port;
        string remote;
        
        public bool IsConnected { get { return client.Connected; } }

        public ForwardClient(string host, int port, string remote)
        {
            this.host = host;
            this.port = port;
            this.remote = remote;
            client = new TcpClient();
        }

        public async Task ConnectAsync()
        {
            await client.ConnectAsync(host, port);

        }

        public void SendAsync(List<ArraySegment<byte>> data)
        {
            //Console.WriteLine("data send to: " + client.Client.RemoteEndPoint.ToString() + " data length: " + data.Count);
            //Console.WriteLine(Encoding.UTF8.GetString(data));
            Task.Run(() => client.Client.SendAsync(data, SocketFlags.None));
        }

        public void SendAsync(byte[] data)
        {
            Console.WriteLine("data send to: " + client.Client.RemoteEndPoint.ToString() + " data length: " + data.Length);
            //Console.WriteLine(Encoding.UTF8.GetString(data));
            Task.Run(() => client.Client.SendAsync(new ArraySegment<byte>(data), SocketFlags.None));
        }

        public void StartRecieve()
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    while (true)
                    {
                        ArraySegment<byte> seg = new ArraySegment<byte>(new byte[2048]);
                        var bytesReceived = await client.Client.ReceiveAsync(seg, SocketFlags.None);

                        if (bytesReceived == 0)
                        {
                            // FIN
                            break;
                        }

                        ArraySegment<byte> target = new ArraySegment<byte>(seg.Array, 0, bytesReceived);
                        //Console.WriteLine("received from : " + client.Client.RemoteEndPoint.ToString() + " data length: " + bytesReceived.ToString());

                        try
                        {
                            await Program.Tcp.Client.SendAsync(encapsulate(target), SocketFlags.None);                            
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                        }
                    }

                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
                finally
                {
                    ForwardClient c;
                    Program.AllClients.TryRemove(remote, out c);
                    Console.WriteLine("socket end: " + remote);
                }
            });
        }

        private List<ArraySegment<byte>> encapsulate(ArraySegment<byte> data)
        {
            List<ArraySegment<byte>> result = new List<ArraySegment<byte>>();
            result.Add(new ArraySegment<byte>(intToBytes(data.Count)));
            result.Add(new ArraySegment<byte>(Encoding.UTF8.GetBytes(remote.PadRight(30))));
            result.Add(data);

            return result;
        }

        private byte[] intToBytes(int value)
        {
            byte[] src = new byte[4];
            src[3] = (byte)((value >> 24) & 0xFF);
            src[2] = (byte)((value >> 16) & 0xFF);
            src[1] = (byte)((value >> 8) & 0xFF);
            src[0] = (byte)(value & 0xFF);
            return src;
        }
    }

    public class Helper
    {
        public static int HeaderLength { get { return 34; } }

        public static int GetLength(IList<ArraySegment<byte>> data)
        {
            int result = 0;
            foreach (var item in data)
            {
                result += item.Count;
            }

            return result;
        }

        public static byte[] IntToBytes(int value)
        {
            byte[] src = new byte[4];
            src[3] = (byte)((value >> 24) & 0xFF);
            src[2] = (byte)((value >> 16) & 0xFF);
            src[1] = (byte)((value >> 8) & 0xFF);
            src[0] = (byte)(value & 0xFF);
            return src;
        }

        public static int BytesToInt(byte[] src, int offset)
        {
            int value;
            value = (int)((src[offset] & 0xFF)
                    | ((src[offset + 1] & 0xFF) << 8)
                    | ((src[offset + 2] & 0xFF) << 16)
                    | ((src[offset + 3] & 0xFF) << 24));
            return value;
        }

        public static List<ArraySegment<byte>> SplitArraySeg(List<ArraySegment<byte>> data)
        {
            if (data.Count <= 0)
            {
                return null;
            }

            int datalength = Helper.GetLength(data);
            List<ArraySegment<byte>> result = new List<ArraySegment<byte>>();

            if (datalength < Helper.HeaderLength)
            {
                return null;
            }

            //get frame length
            byte[] length = new byte[4];
            if (data[0].Count < 4)
            {
                Array.Copy(data[0].Array, data[0].Offset, length, 0, data[0].Count);
                Array.Copy(data[1].Array, data[1].Offset, length, data[0].Count, 4 - data[0].Count);
            }
            else
            {
                Array.Copy(data[0].Array, data[0].Offset, length, 0, 4);
            }

            int len = Helper.BytesToInt(length, 0);

            if (datalength < Helper.HeaderLength + len)
            {
                return null;
            }

            int count = len + Helper.HeaderLength; //data frame length
            for (int i = 0; i < data.Count; i++)
            {
                if (data[i].Count < count)
                {
                    result.Add(new ArraySegment<byte>(data[i].Array, data[i].Offset, data[i].Count));
                    count -= data[i].Count;
                }
                else if (data[i].Count == count)
                {
                    result.Add(new ArraySegment<byte>(data[i].Array, data[i].Offset, data[i].Count));
                    data.RemoveRange(0, i + 1);
                    break;
                }
                else
                {
                    result.Add(new ArraySegment<byte>(data[i].Array, data[i].Offset, count));
                    data[i] = new ArraySegment<byte>(data[i].Array, data[i].Offset + count, data[i].Count - count);
                    if (i > 0)
                    {
                        data.RemoveRange(0, i);
                    }
                    break;
                }
            }

            return result;
        }

        public static async Task SendToTarget(List<ArraySegment<byte>> data)
        {
            string remote;
            if (data[0].Count > Helper.HeaderLength)
            {
                remote = Encoding.UTF8.GetString(data[0].Array, data[0].Offset + 4, 28).Replace(" ", "");
                data[0] = new ArraySegment<byte>(data[0].Array, data[0].Offset + Helper.HeaderLength, data[0].Count - Helper.HeaderLength);
            }
            else if (data[0].Count == Helper.HeaderLength)
            {
                remote = Encoding.UTF8.GetString(data[0].Array, data[0].Offset + 4, 28).Replace(" ", "");
                data.RemoveAt(0);
            }
            else
            {
                byte[] r = Concat(data[0].Array, data[0].Offset + 4, data[0].Count - 4, data[1].Array, data[1].Offset, Helper.HeaderLength - data[0].Count);
                remote = Encoding.UTF8.GetString(r, 0, 28).Replace(" ", "");
                data[1] = new ArraySegment<byte>(data[1].Array, data[1].Offset + Helper.HeaderLength - data[0].Count, data[1].Count - (Helper.HeaderLength - data[0].Count));
                data.RemoveAt(0);
            }

            ForwardClient client = Program.AllClients.GetOrAdd(remote, new ForwardClient(Program.Configuration["forwardname"], Convert.ToInt32(Program.Configuration["forwardport"]), remote));
            if (!client.IsConnected)
            {
                await client.ConnectAsync();
                client.StartRecieve();
            }

            client.SendAsync(data);
        }

        public static byte[] Concat(byte[] b1, int i1, int c1, byte[] b2, int i2, int c2)
        {
            byte[] result = new byte[c1 + c2];
            Array.Copy(b1, i1, result, 0, c1);
            Array.Copy(b2, i2, result, c1, c2);
            return result;
        }
    }
}