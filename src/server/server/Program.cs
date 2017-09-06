using sharelib;
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
            Console.WriteLine("Starting...");

            SocketServer serverOut = new SocketServer(new SockerHandlerFactory("Web"));
            Task.Run(() => serverOut.StartListening(5000)).ConfigureAwait(false);

            SocketServer serverTunnel = new SocketServer(new SockerHandlerFactory("Transfer"));
            Task.Run(() => serverTunnel.StartListening(5001)).ConfigureAwait(false);

            Thread.Sleep(Int32.MaxValue);
        }        
    }

    public class SocketServer
    {
        SockerHandlerFactory facotry;

        public SocketServer(SockerHandlerFactory handlerFactory)
        {
            this.facotry = handlerFactory;
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

                    SocketConnection conn = new SocketConnection(acceptSocket, facotry.Create());
                    conn.StartAsync();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }

    public class SockerHandlerFactory
    {
        string handlerName;

        public SockerHandlerFactory(string name)
        {
            handlerName = name;
        }

        public ISocketHandler Create()
        {
            switch (handlerName)
            {
                case "Web":
                    return new SocketHandlerWeb();
                case "Transfer":
                    return new SocketHandlerTransfer();
                default:
                    return null;
            }
        }
    }

    public class SocketHandlerWeb : ISocketHandler
    {
        public async Task<bool> ProcessData(SocketConnection conn)
        {

            try
            {
                if (conn.CurrentFrame == null)
                {
                    conn.CurrentFrame = new HttpFrame();
                }

                HttpFrame frame = (HttpFrame)conn.CurrentFrame;

                if (!frame.Exam(conn.Buffer))
                {
                    return true;
                }

                frame.Data = conn.Buffer.SplitAll();
                await frame.Process(conn);

                //conn.CurrentFrame = null;

                return true;            
            }
            catch (Exception ex) 
            {
                throw ex;
            }
        }      
    }

    public class SocketHandlerTransfer : ISocketHandler
    {
        SocketConnection connection;

        public async Task<bool> ProcessData(SocketConnection conn)
        {
            connection = conn;

            if (connection.CurrentFrame == null)
            {
                connection.CurrentFrame = FrameFactory.Create(connection.Buffer);

                if (connection.CurrentFrame == null)
                {
                    return true;
                }
            }

            Frame f = connection.CurrentFrame;
            if (!f.Exam(connection.Buffer))
            {
                return true;
            }

            f.Parse(connection.Buffer);
            await f.Process(connection);

            connection.CurrentFrame = null;

            return true;
        }

            #region old 2
            //Relation.TransferSocket = conn.Socket;

            //conn.WaitBuffer();

            //try
            //{
            //    List<ArraySegment<byte>> data = TunnelHelper.SplitArraySeg(conn.Buffer);

            //    while (data != null)
            //    {
            //        await Helper.SendData(data);
            //        data = Helper.SplitArraySeg(conn.Buffer);
            //    }

            //}
            //catch (Exception ex)
            //{
            //    Console.WriteLine(ex.Message);
            //}
            //finally
            //{
            //    conn.ReleaseBuffer();
            //}
            #endregion

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
            //}

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
    

}