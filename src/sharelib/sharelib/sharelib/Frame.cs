using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace sharelib
{
    public class FrameFactory
    {
        //create frame from buff
        public static Frame Create(NetBuffer buff)
        {
            if (buff.Count < 5)
            {
                return null;
            }

            int length = bytesToInt(buff, 0);
            byte type = buff[4];

            switch (type)
            {
                case 0x00:
                    return new CommandFrame { DataLength = length };
                case 0x01:
                    return new DataForwardFrame() { DataLength = length };
                case 0x02:
                    return new DataResponseFrame() { DataLength = length };
                default:
                    throw new Exception("unknow data package");
            }
        }

        private static int bytesToInt(NetBuffer src, int offset)
        {
            int value;
            value = (int)((src[offset] & 0xFF)
                    | ((src[offset + 1] & 0xFF) << 8)
                    | ((src[offset + 2] & 0xFF) << 16)
                    | ((src[offset + 3] & 0xFF) << 24));
            return value;
        }
    }

    public abstract class Frame
    {
        public int DataLength { get; set; }//4 bytes total length after this 
        public byte Type { get; set; }  //1 byte for data type 
        public NetBuffer Data { get; set; }

        public abstract Task Process(SocketConnection conn);
        public abstract bool Parse(NetBuffer buff);
        public abstract bool Exam(NetBuffer buff);
        public abstract Task Send(SocketConnection conn);
    }

    public class DataResponseFrame : Frame
    {
        public string Remote { get; set; }

        public DataResponseFrame()
        {
            Type = 0x02;
        }

        public override bool Exam(NetBuffer buff)
        {
            if (buff.Count >= DataLength + 5)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public override async Task Process(SocketConnection conn)
        {
            int pos = 0;
            byte[] remotebytes = new byte[35];
            List<ArraySegment<byte>> result = new List<ArraySegment<byte>>();
            SocketConnection sc = null;

            foreach (BufferSeg item in Data)
            {
                if (pos < 35)
                {
                    if (item.Count >= 35 - pos)
                    {
                        Array.Copy(item.Buff, item.Start, remotebytes, pos, 35 - pos);
                        Remote = Encoding.UTF8.GetString(remotebytes, 5, 30).Replace(" ", "");
                        sc = ConnectionManager.GetRequestSocket(Remote);
                        if (sc == null)
                        {
                            return;
                        }
                        if (item.Count > 35 - pos)
                        {
                            result.Add(new ArraySegment<byte>(item.Buff, item.Start + 35 - pos, item.Count - (35 - pos)));
                        }
                    }
                    else
                    {
                        Array.Copy(item.Buff, item.Start, remotebytes, pos, item.Count);                        
                    }
                    pos += item.Count;
                }
                else
                {
                    result.Add(new ArraySegment<byte>(item.Buff, item.Start, item.Count));
                }
            }
            
            await sc?.Socket.SendAsync(result, SocketFlags.None);
        }

        public override bool Parse(NetBuffer buff)
        {
            Data = buff.Split(DataLength + 5);
            if (Data == null)
            {
                return false;
            }
            return true;
        }

        public override async Task Send(SocketConnection conn)
        {
            byte[] head = new byte[35];
            byte[] length = TunnelHelper.IntToBytes(Data.Count + 30);
            byte[] remote = Encoding.UTF8.GetBytes(Remote.PadRight(30));

            Array.Copy(length, head, 4);
            head[4] = Type;
            Array.Copy(remote, 0, head, 5, remote.Length);

            List<ArraySegment<byte>> result = new List<ArraySegment<byte>>();
            result.Add(new ArraySegment<byte>(head));

            foreach (BufferSeg item in Data)
            {
                result.Add(new ArraySegment<byte>(item.Buff, item.Start, item.Count));
            }

            await conn.Socket.SendAsync(result, SocketFlags.None);
        }
    }

    public class DataForwardFrame : Frame
    {
        public string Remote { get; set; }

        public DataForwardFrame()
        {
            Type = 0x01;
        }

        public override bool Exam(NetBuffer buff)
        {
            if (buff.Count >= DataLength + 5)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public override async Task Process(SocketConnection conn)
        {            
            int pos = 0;
            byte[] remotebytes = new byte[35];
            List<ArraySegment<byte>> result = new List<ArraySegment<byte>>();
            ForwardClient client = null;

            try
            {
                foreach (BufferSeg item in Data)
                {
                    if (pos < 35)
                    {
                        if (item.Count >= 35 - pos)
                        {
                            Array.Copy(item.Buff, item.Start, remotebytes, pos, 35 - pos);
                            Remote = Encoding.UTF8.GetString(remotebytes, 5, 30).Replace(" ", "");
                            client = ClientManager.GetOrAdd(Remote, new ForwardClient(ClientManager.ForwardHost, ClientManager.ForwardPort, Remote));

                            if (client == null)
                            {
                                return;
                            }

                            if (!client.IsConnected)
                            {
                                await client.ConnectAsync();
                            }

                            if (item.Count >= 35 - pos)
                            {
                                result.Add(new ArraySegment<byte>(item.Buff, item.Start + 35 - pos, item.Count - (35 - pos)));
                            }

                        }
                        else
                        {
                            Array.Copy(item.Buff, item.Start, remotebytes, pos, item.Count);
                        }
                        pos += item.Count;
                    }
                    else
                    {
                        result.Add(new ArraySegment<byte>(item.Buff, item.Start, item.Count));
                    }
                }

                await client?.SendAsync(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            
        }

        public override bool Parse(NetBuffer buff)
        {
            Data = buff.Split(DataLength + 5);
            if (Data == null)
            {
                return false;
            }

            return true;
        }

        public override async Task Send(SocketConnection conn)
        {
            byte[] head = new byte[35];
            byte[] length = TunnelHelper.IntToBytes(Data.Count + 30);
            byte[] remote = Encoding.UTF8.GetBytes(Remote.PadRight(30));

            Array.Copy(length, head, 4);
            head[4] = Type;
            Array.Copy(remote, 0, head, 5, remote.Length);

            List<ArraySegment<byte>> result = new List<ArraySegment<byte>>();
            result.Add(new ArraySegment<byte>(head));

            foreach (BufferSeg item in Data)
            {
                result.Add(new ArraySegment<byte>(item.Buff, item.Start, item.Count));
            }

            await conn.Socket.SendAsync(result, SocketFlags.None);
        }
    }

    public class CommandFrame : Frame
    {
        public Command Command { get; set; }

        public CommandFrame()
        {
            Type = 0x00;
        }

        public override bool Exam(NetBuffer buff)
        {
            if (buff.Count >= DataLength + 5)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public override bool Parse(NetBuffer buff)
        {
            NetBuffer data = buff.Split(DataLength + 5);
            if (data == null)
            {
                return false;
            }

            Command = CommandFactory.Create(data);
            if (Command == null)
            {
                return false;
            }

            return true;
        }

        public override async Task Process(SocketConnection conn)
        {
            await Command.Execute(conn);
        }

        public override async Task Send(SocketConnection conn)
        {
            byte[] bytes = Command.GetBytes();
            byte[] l = TunnelHelper.IntToBytes(bytes.Length);
            byte[] t = new byte[1] { Type };

            List<ArraySegment<byte>> d = new List<ArraySegment<byte>>();
            d.Add(new ArraySegment<byte>(l));
            d.Add(new ArraySegment<byte>(t));
            d.Add(new ArraySegment<byte>(bytes));

            await conn.Socket.SendAsync(d, SocketFlags.None);
        }
    }

    public class HttpFrame : Frame
    {
        public string Hostname { get; set; }

        private int examed = 0;
        //private int lineCount = 0;

        public override bool Exam(NetBuffer buff)
        {
            if (!string.IsNullOrEmpty(Hostname))
            {
                return true;
            }

            int i = examed;
            for (; i < buff.Count - 1; i++)
            {
                if (buff[i] == 0x0d && buff[i + 1] == 0x0a && buff[i + 2] == 0x0d && buff[i + 3] == 0x0a)
                {                    
                    examed = i;
                    setHostname(buff);
                    return true;
                }
            }

            examed = i;
            return false;
        }

        public override async Task Process(SocketConnection conn)
        {
            if (conn.MapConnection == null)
            {
                conn.MapConnection = ConnectionManager.GetTunnelSocket(Hostname);
            }
            SocketConnection sc = conn.MapConnection;

            if (sc == null)
            {
                await conn.Socket.SendAsync(new ArraySegment<byte>(Encoding.ASCII.GetBytes(page404)), SocketFlags.None);
                throw new Exception("no tunnel has been bound to this domain: " + Hostname);
            }

            string remoteEnd = conn.Socket.RemoteEndPoint.ToString();
            if (!conn.IsRequestAdded)
            {
                conn.IsRequestAdded = ConnectionManager.AddRequest(remoteEnd, conn);
            }            

            DataForwardFrame f = new DataForwardFrame();
            f.Remote = remoteEnd;
            f.Data = Data;

            await f.Send(sc);            
        }

        public override bool Parse(NetBuffer buff)
        {
            throw new NotImplementedException();
        }

        private void setHostname(NetBuffer buff)
        {
            Hostname = "";
            try
            {
                string req = buff.GetString();

                foreach (var item in req.Split("\r\n"))
                {
                    if (item.IndexOf("Host") == 0)
                    {
                        Hostname = item.Split(':')[1].Replace(" ", "");
                    }
                } 
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public override Task Send(SocketConnection conn)
        {
            throw new NotImplementedException();
        }

        #region defaultData
        static string page404 = @"HTTP/1.1 404 Not Found
Server: nettunnel
Content-Type: text/html
Content-Length: 25

Oops! Resource not found!";

        #endregion
    }

    public abstract class FramePayLoad
    {
        public abstract byte[] GetBytes();
    }
}
