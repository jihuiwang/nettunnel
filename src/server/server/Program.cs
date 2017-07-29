using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace server
{
    class Program
    {
        public static void Main()
        {
            SocketServer serverOut = new SocketServer(new SocketHandlerWeb());
            Task.Run(() => serverOut.StartListening(5000)).ConfigureAwait(false);

            SocketServer serverTunnel = new SocketServer(new SocketHandlerTransfer());
            Task.Run(() => serverTunnel.StartListening(5001)).ConfigureAwait(false);

            Console.WriteLine("Starting...");
            Thread.Sleep(Int32.MaxValue);
        }        
    }

    public class SocketServer
    {
        ISocketHandler handler;

        public SocketServer(ISocketHandler handler)
        {
            this.handler = handler;
        }

        public async Task StartListening(int port)
        {
            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, port);

            // Create a TCP/IP socket.
            Socket listener = new Socket(localEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            // Bind the socket to the local endpoint and listen for incoming connections.
            try
            {
                listener.Bind(localEndPoint);
                listener.Listen(256);

                while (true)
                {
                    var acceptSocket = await listener.AcceptAsync();
                    Console.WriteLine("socket accepted: remote: " + acceptSocket.RemoteEndPoint.ToString() + " local:" + acceptSocket.LocalEndPoint.ToString());

                    SocketConnection conn = new SocketConnection(acceptSocket, handler);
                    _ = conn.StartAsync();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }

    public interface ISocketHandler
    {
        Task ProcessData(SocketConnection conn);
    }

    public class SocketHandlerWeb : ISocketHandler
    {

        public async Task ProcessData(SocketConnection conn)
        {
            conn.WaitBuffer();

            Relation.OutSockets.AddOrUpdate(conn.Socket.RemoteEndPoint.ToString(), conn.Socket, (k, v) => conn.Socket);

            try
            {
                if (Relation.TransferSocket != null)
                {                    
                    await Relation.TransferSocket.SendAsync(encapsulate(conn.Socket, conn.Buffer), SocketFlags.None);
                    conn.Buffer.Clear();
                }
            }
            catch
            {
                
                return;
            }
            finally
            {
                conn.ReleaseBuffer();
            }
        }

        private List<ArraySegment<byte>> encapsulate(Socket socket, IList<ArraySegment<byte>> data)
        {
            List<ArraySegment<byte>> result = new List<ArraySegment<byte>>();
            result.Add(new ArraySegment<byte>(Helper.IntToBytes(Helper.GetLength(data))));
            result.Add(new ArraySegment<byte>(Encoding.UTF8.GetBytes(socket.RemoteEndPoint.ToString().PadRight(30))));
            result.AddRange(data);

            return result;
        }        
    }

    public class SocketHandlerTransfer : ISocketHandler
    {
        static byte[] lastData = new byte[0];

        public async Task ProcessData(SocketConnection conn)
        {
            Relation.TransferSocket = conn.Socket;

            conn.WaitBuffer();

            try
            {
                List<ArraySegment<byte>> data = Helper.SplitArraySeg(conn.Buffer);

                while (data != null)
                {
                    await Helper.SendData(data);
                    data = Helper.SplitArraySeg(conn.Buffer);
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                conn.ReleaseBuffer();
            }

            #region old 
            //try
            //{
            //    byte[] data = concat(lastData, context.AllData);

            //    int pos = indexOfArray(data, footer);

            //    while(pos >= 0)
            //    {
            //        byte[] beforeFooter = new byte[pos];
            //        byte[] afterFooter = new byte[data.Length - pos - 22];

            //        if (beforeFooter.Length > 0)
            //        {
            //            Array.Copy(data, beforeFooter, pos);
            //            await sendData(beforeFooter);
            //        }

            //        Array.Copy(data, pos + 22, afterFooter, 0, afterFooter.Length);
            //        //if (context.AllData.Length < 2048)
            //        //{
            //        //    await sendData(afterHeader);
            //        //    lastData = new byte[0];
            //        //}
            //        //else
            //        //{
            //        //    lastData = afterHeader;
            //        //}
            //        data = afterFooter;

            //        pos = indexOfArray(data, footer);
            //    }

            //    lastData = data;

            //    //if (pos < 0)
            //    //{
            //    //    lastData = data;
            //    //    return;
            //    //    //if (context.AllData.Length < 2048)
            //    //    //{
            //    //    //    await sendData(data);
            //    //    //    lastData = new byte[0];
            //    //    //}
            //    //    //else
            //    //    //{
            //    //    //    lastData = data;
            //    //    //}
            //    //}
            //    //else
            //    //{
            //    //    byte[] beforeFooter = new byte[pos];
            //    //    byte[] afterFooter = new byte[data.Length - pos - 22];

            //    //    if (beforeFooter.Length > 0)
            //    //    {
            //    //        Array.Copy(data, beforeFooter, pos);
            //    //        await sendData(beforeFooter);
            //    //    }

            //    //    Array.Copy(data, pos + 22, afterFooter, 0, afterFooter.Length);
            //    //    //if (context.AllData.Length < 2048)
            //    //    //{
            //    //    //    await sendData(afterHeader);
            //    //    //    lastData = new byte[0];
            //    //    //}
            //    //    //else
            //    //    //{
            //    //    //    lastData = afterHeader;
            //    //    //}
            //    //    lastData = afterFooter;
            //    //}
            //}
            //catch (Exception ex)
            //{
            //    return;
            //}
        }

        //List<ArraySegment<byte>> splitArraySeg(List<ArraySegment<byte>> data, byte[] sep)
        //{
        //    bool found = false;
        //    int allcount = 0;
        //    foreach (var d in data)
        //    {
        //        allcount += d.Count;
        //    }

        //    int j = 0;
        //    int k = 0;
        //    for (int i = 0; i < allcount - sep.Length + 1; i++)
        //    {
        //        if(k >= data[j].Count)
        //        {
        //            j++;
        //            k = 0;
        //        }

        //        if (isEqual(data, j, k, sep))
        //        {
        //            found = true;
        //            break;
        //        }
        //        k++;
        //    }

        //    if (!found)
        //    {
        //        return null;
        //    }

        //    List<ArraySegment<byte>> result = new List<ArraySegment<byte>>();

        //    for (int i = 0; i < j; i++)
        //    {
        //        result.Add(data[i]);
        //    }
        //    if (k + sep.Length < data[j].Count)
        //    {
        //        result.Add(new ArraySegment<byte>(data[j].Array, data[j].Offset, k));
        //        data[j] = new ArraySegment<byte>(data[j].Array, data[j].Offset + k + sep.Length, data[j].Count - k - sep.Length);
        //        if (j > 0)
        //        {
        //            data.RemoveRange(0, j);
        //        }
        //    }
        //    else if (k + sep.Length == data[j].Count)
        //    {
        //        result.Add(new ArraySegment<byte>(data[j].Array, data[j].Offset, k));
        //        data.RemoveRange(0, j + 1);
        //    }
        //    else
        //    {
        //        result.Add(new ArraySegment<byte>(data[j].Array, data[j].Offset, k));
        //        //data[j] = new ArraySegment<byte>(data[j].Array, data[j].Offset + k + sep.Length, data[j].Count - k - sep.Length);
        //        data[j + 1] = new ArraySegment<byte>(data[j + 1].Array, data[j + 1].Offset + k + sep.Length - data[j].Count, data[j + 1].Count - (k + sep.Length - data[j].Count));
        //        if (j >= 0)
        //        {
        //            data.RemoveRange(0, j + 1);
        //        }
        //    }

        //    return result;
        //}

        //bool isEqual(List<ArraySegment<byte>> data, int a, int b, byte[] target)
        //{
        //    int j = a;
        //    int k = b;

        //    for (int i = 0; i < target.Length; i++)
        //    {
        //        if (k >= data[j].Count)
        //        {
        //            j++;
        //            k = 0;
        //        }

        //        if (data[j].Array[k+data[j].Offset] != target[i])
        //        {
        //            return false;
        //        }

        //        k++;
        //    }
        //    return true;
        //}



        //async Task sendData(byte[] data)
        //{
        //    Console.WriteLine("sequence: " + Encoding.UTF8.GetString(data, 29, 2)); //sequence
        //    string remote = Encoding.UTF8.GetString(data, 0, 28).Replace(" ", "");
        //    byte[] fdata = new byte[data.Length - 30];
        //    Array.Copy(data, 30, fdata, 0, fdata.Length);

        //    Socket socket = null;
        //    if (!Relation.OutSockets.TryGetValue(remote, out socket))
        //    {
        //        return;
        //    }

        //    await socket.SendAsync(new ArraySegment<byte>(fdata), SocketFlags.None);
        //    //socket.Send(fdata);

        //}        

        //byte[] concat(byte[] b1, byte[] b2)
        //{
        //    byte[] result = new byte[b1.Length + b2.Length];
        //    b1.CopyTo(result, 0);
        //    b2.CopyTo(result, b1.Length);
        //    return result;
        //}

        //int indexOfArray(byte[] source, byte[] target)
        //{
        //    for (int i = 0; i < source.Length - target.Length + 1; i++)
        //    {
        //        if (isArrayEqual(source,i, target,0, target.Length))
        //        {
        //            return i;
        //        }
        //    }

        //    return -1;
        //}

        //bool isArrayEqual(byte[] a1, int s1, byte[] a2, int s2, int length)
        //{
        //    for (int i = 0; i < length; i++)
        //    {
        //        if (a1[s1+i] != a2[s2+i])
        //        {
        //            return false;
        //        }
        //    }

        //    return true;
        //}
        #endregion 
    }

    public class SocketConnection
    {
        private List<ArraySegment<byte>> buffer;
        private int buffsize;
        private Socket socket;
        private ISocketHandler handler;
        private ManualResetEventSlim semaphore = new ManualResetEventSlim(true);

        public List<ArraySegment<byte>> Buffer { get { return buffer; } }
        public Socket Socket { get { return socket; } }

        public SocketConnection(Socket socket, ISocketHandler handler)
        {
            buffer = new List<ArraySegment<byte>>();
            this.buffsize = 2048;
            this.socket = socket;
            this.handler = handler;
        }

        public async Task StartAsync()
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
                    //Console.WriteLine("socket data received: " + socket.RemoteEndPoint.ToString() + " data length: " + bytesReceived.ToString());
                    AppendData(new ArraySegment<byte>(seg.Array, 0, bytesReceived));

                    _ = handler.ProcessData(this);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                Socket s;
                Relation.OutSockets.TryRemove(socket.RemoteEndPoint.ToString(), out s);
                Console.WriteLine("socket closed: " + socket.RemoteEndPoint.ToString());
                socket.Dispose();
            }
        } 

        public byte[] GetBuffer()
        {
            //todo: this should be a memory pool to improve performance
            return new byte[buffsize];
        }

        public void WaitBuffer()
        {
            semaphore.Wait();
        }

        public void ReleaseBuffer()
        {
            semaphore.Set();
        }

        public void AppendData(ArraySegment<byte> data)
        {
            WaitBuffer();
            buffer.Add(data);
            ReleaseBuffer();
        }   
    }

    public class Relation
    {
        private static ConcurrentDictionary<string, Socket> outSockets = new ConcurrentDictionary<string, Socket>();
        public static ConcurrentDictionary<string, Socket> OutSockets
        {
            get { return outSockets; }
        }

        public static Socket TransferSocket;
        
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

        public static async Task SendData(List<ArraySegment<byte>> data)
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

            Socket socket = null;
            if (!Relation.OutSockets.TryGetValue(remote, out socket))
            {
                return;
            }

            Console.WriteLine("response recieved from " + remote + " data length " + GetLength(data));
            await socket.SendAsync(data, SocketFlags.None);
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