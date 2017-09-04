using Microsoft.Extensions.Configuration;
using sharelib;
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
        private static ManualResetEventSlim semaphore = new ManualResetEventSlim(true);
        public static IConfigurationRoot Configuration { get; set; }

        static void Main(string[] args)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("config.json");

            Configuration = builder.Build();
            ClientManager.ServerDomain = Configuration["servername"];
            ClientManager.ServerPort = Convert.ToInt32(Configuration["serverport"]);
            ClientManager.ForwardHost = Configuration["forwardname"];
            ClientManager.ForwardPort = Convert.ToInt32(Configuration["forwardport"]);
            ClientManager.SubDomain = Configuration["subdomain"]; 

            Task.Run(() => Start());

            Console.WriteLine("Staring...");
            Console.Read();
        }

        static async Task Start()
        {
            try
            {
                await ClientManager.InitailTunnel();
                Console.WriteLine("server connected, local: " + ClientManager.TunnelClient.Client.LocalEndPoint.ToString());
                //await Tcp.Client.SendAsync(new ArraySegment<byte>(new byte[34]), SocketFlags.None);
                await StartHeartBeat();
                await RegisterTunnel();

                SocketConnection conn = new SocketConnection(ClientManager.TunnelClient.Client, new TunnelHandler());
                conn.OnClose += () =>
                {                      
                    Task.Run(() => Start());
                };
                conn.StartAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return;
            }

            /*
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
            */      
        }

        static async Task RegisterTunnel()
        {
            CommandFrame frame = new CommandFrame();
            frame.Command = new RegisterTunnelCommand() { Domain = ClientManager.SubDomain };

            await frame.Send(ClientManager.TunnelConnection);
        }

        static async Task StartHeartBeat()
        {
            //while (true)
            //{
            //    try
            //    {
                    CommandFrame frame = new CommandFrame();
                    frame.Command = new MessageCommand() { Message = "heart beat" };

                    await frame.Send(ClientManager.TunnelConnection);
             //       await Task.Delay(600000);
             //   }
             //   catch
             //   {
             //   }
            //}
        }

        /*
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
        }*/
    }
    
    public class TunnelHandler : ISocketHandler
    {
        public async Task<bool> ProcessData(SocketConnection conn)
        {
            try
            {
                if (conn.CurrentFrame == null)
                {
                    conn.CurrentFrame = FrameFactory.Create(conn.Buffer);

                    if (conn.CurrentFrame == null)
                    {
                        return true;
                    }
                }

                Frame f = conn.CurrentFrame;
                if (!f.Exam(conn.Buffer))
                {
                    return true;
                }

                f.Parse(conn.Buffer);
                await f.Process(conn);

                conn.CurrentFrame = null;

                return true;
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }
    }
}