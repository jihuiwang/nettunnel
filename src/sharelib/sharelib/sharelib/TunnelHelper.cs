using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace sharelib
{
    public class TunnelHelper
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

            int datalength = GetLength(data);
            List<ArraySegment<byte>> result = new List<ArraySegment<byte>>();

            if (datalength < HeaderLength)
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

            int len = BytesToInt(length, 0);

            if (datalength < HeaderLength + len)
            {
                return null;
            }

            int count = len + HeaderLength; //data frame length
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
            //string remote;
            //if (data[0].Count > HeaderLength)
            //{
            //    remote = Encoding.UTF8.GetString(data[0].Array, data[0].Offset + 4, 28).Replace(" ", "");
            //    data[0] = new ArraySegment<byte>(data[0].Array, data[0].Offset + HeaderLength, data[0].Count - HeaderLength);
            //}
            //else if (data[0].Count == HeaderLength)
            //{
            //    remote = Encoding.UTF8.GetString(data[0].Array, data[0].Offset + 4, 28).Replace(" ", "");
            //    data.RemoveAt(0);
            //}
            //else
            //{
            //    byte[] r = Concat(data[0].Array, data[0].Offset + 4, data[0].Count - 4, data[1].Array, data[1].Offset, HeaderLength - data[0].Count);
            //    remote = Encoding.UTF8.GetString(r, 0, 28).Replace(" ", "");
            //    data[1] = new ArraySegment<byte>(data[1].Array, data[1].Offset + HeaderLength - data[0].Count, data[1].Count - (HeaderLength - data[0].Count));
            //    data.RemoveAt(0);
            //}

            //Socket socket = null;
            //if (!Relation.OutSockets.TryGetValue(remote, out socket))
            //{
            //    return;
            //}

            //Console.WriteLine("response recieved from " + remote + " data length " + GetLength(data));
            //await socket.SendAsync(data, SocketFlags.None);
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
