using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace sharelib
{
    public class NetBuffer : IEnumerable, IEnumerator
    {
        private BufferSeg start;
        private BufferSeg end;
        private BufferSeg segPos;
        private Dictionary<int, int> countIndex = new Dictionary<int, int>();
        private object lockobj = new object();

        public event Func<Task> OnReceived;

        public int Count { get { return count; } }
        private int count;

        public void Add(ArraySegment<byte> data)
        {
            lock (lockobj)
            {
                BufferSeg buff = new BufferSeg(data);
                if (start == null)
                {
                    start = end = buff;
                }
                else
                {
                    end.Next = buff;
                    end = buff;
                }

                count += data.Count;

                if (OnReceived != null)
                {
                    Task.Run(async () => await OnReceived.Invoke()).ConfigureAwait(false);
                }
            }
        }

        public void Add(BufferSeg data)
        {
            lock (lockobj)
            {
                if (start == null)
                {
                    start = end = data;
                }
                else
                {
                    end.Next = data;
                    end = data;
                }

                count += data.Count;
            }
        }

        //read only
        public byte this[int index]
        {
            get
            {
                lock (lockobj)
                {
                    try
                    {
                        if (index < 0 || index > count)
                        {
                            throw new ArgumentOutOfRangeException("index out of range");
                        }

                        BufferSeg current = start;
                        int acc = 0;
                        if (current.Count >= index)
                        {
                            return current.Buff[current.Start + index];
                        }
                        acc += current.Count;

                        while (current.Next != null)
                        {
                            current = current.Next;
                            if (acc + current.Count >= index)
                            {
                                break;
                            }
                            acc += current.Count;
                        }

                        return current.Buff[current.Start + index - acc];
                    }
                    catch (Exception ex)
                    {
                        throw ex;
                    }
                }
            }
        }

        public NetBuffer Split(int length)
        {
            NetBuffer outBuff = new NetBuffer();
            
            lock (lockobj)
            {
                int before = this.count;

                try
                {
                    if (length > count)
                    {
                        return null;
                    }

                    BufferSeg current = start;
                    int acc = 0;
                    while (current != null)
                    {
                        acc += current.Count;
                        if (acc >= length)
                        {
                            break;
                        }
                        outBuff.Add(current);
                        current = current.Next;
                    }

                    if (current == null)
                    {
                        return null;
                    }

                    if (acc == length)
                    {
                        start = current.Next;
                        end = start == null ? null : (start.Next == null ? start : end);

                        current.Next = null;
                        outBuff.Add(current);
                    }
                    else
                    {
                        int delta = current.Count - (acc - length);
                        start = new BufferSeg(current, current.Start + delta, current.End);
                        start.Next = current.Next;
                        end = start.Next == null ? start : end;

                        current.End = current.Start + delta - 1;
                        current.Next = null;
                        outBuff.Add(current);
                    }
                    count -= length;
                }
                catch
                {

                }

                return outBuff;
            }
        }

        public NetBuffer SplitAll()
        {
            NetBuffer result = Split(this.count);
            return result;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this;
        }

        public bool MoveNext()
        {
            if (start == null)
            {
                return false;
            }

            if (segPos == null)
            {
                segPos = start;
                return true;
            }
            else
            {
                segPos = segPos.Next;
                return segPos == null ? false : true;
            }
        }

        public void Reset()
        {
            segPos = null;
        }

        public object Current
        {
            get
            {
                return segPos;
            }
        }

        public byte[] GetBytes()
        {
            lock (lockobj)
            {
                byte[] bytes = new byte[count];
                int pos = 0;

                BufferSeg seg = start;
                while (seg != null)
                {
                    Array.Copy(seg.Buff, seg.Start, bytes, pos, seg.Count);
                    pos += seg.Count;
                    seg = seg.Next;
                }

                return bytes;
            }            
        }

        public string GetString()
        {
            return Encoding.UTF8.GetString(GetBytes());
        }
    }

    public class BufferSeg
    {
        public byte[] Buff { get { return buff; } }
        private byte[] buff;

        public int Start { get; set; }
        public int End { get; set; }

        public BufferSeg Next { get; set; }
        public int Count { get { return End - Start + 1; } }

        public BufferSeg(ArraySegment<byte> data)
        {
            if (data.Count <= 0)
            {
                throw new ArgumentNullException("data empty");
            }

            buff = data.Array;
            Start = data.Offset;
            End = data.Offset + data.Count - 1;
        }

        public BufferSeg(BufferSeg seg, int start, int end)
        {
            if (seg == null)
            {
                throw new ArgumentNullException("seg");
            }

            this.buff = seg.Buff;
            this.Start = start;
            this.End = end;
        }
    }
}
