using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace sharelib
{
    public class ForwardClient
    {
        TcpClient client;
        string host;
        int port;
        string remote;

        public SocketConnection Connection;
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
            Connection = new SocketConnection(client.Client, new ForwardHandler(remote));
            Connection.StartAsync();
        }

        public async Task SendAsync(List<ArraySegment<byte>> data)
        {
            //Console.WriteLine("data send to: " + client.Client.RemoteEndPoint.ToString() + " data length: " + data.Count);
            //Console.WriteLine(Encoding.UTF8.GetString(data));
            await client.Client.SendAsync(data, SocketFlags.None);
        }

        public void SendAsync(byte[] data)
        {
            Console.WriteLine("data send to: " + client.Client.RemoteEndPoint.ToString() + " data length: " + data.Length);
            //Console.WriteLine(Encoding.UTF8.GetString(data));
            Task.Run(() => client.Client.SendAsync(new ArraySegment<byte>(data), SocketFlags.None));
        }
    }

    public class ForwardHandler : ISocketHandler
    {
        string remote;

        public ForwardHandler(string remote)
        {
            this.remote = remote;
        }

        public async Task<bool> ProcessData(SocketConnection conn)
        {
            try
            {
                if (conn.CurrentFrame == null)
                {
                    conn.CurrentFrame = new DataResponseFrame();
                }

                DataResponseFrame frame = (DataResponseFrame)conn.CurrentFrame;
                frame.Remote = remote;
                frame.Data = conn.Buffer.SplitAll();

                if (frame.Data == null)
                {
                    return true;
                }

                await frame.Send(ClientManager.TunnelConnection);

                conn.CurrentFrame = null;

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw ex;
            }
        }
    }
}
