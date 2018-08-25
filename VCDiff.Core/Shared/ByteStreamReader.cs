/* LICENSE

   Copyright 2008 The open-vcdiff Authors.
   Copyright 2017 Metric (https://github.com/Metric)
   Copyright 2018 MatthiWare (https://github.com/Matthiee)

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/

using System;
using System.Collections.Generic;
using System.IO;

namespace MatthiWare.Compression.VCDiff.Shared
{
    //Wrapper Class for any stream that supports Position
    //and Length to make reading bytes easier
    //also has a helper function for reading all the bytes in at once
    public class ByteStreamReader : IByteBuffer, IDisposable
    {
        private Stream buffer;
        private int lastLenRead;
        private bool readAll;
        private IList<byte> internalBuffer;
        private long offset;

        public ByteStreamReader(Stream stream)
        {
            buffer = stream;
        }

        public long Position
        {
            get
            {
                if (readAll)
                {
                    return offset;
                }
                return buffer.Position;
            }
            set
            {
                if (readAll)
                {
                    if (value >= 0)
                        offset = value;
                }
                if (buffer.CanRead && value >= 0)
                    buffer.Position = value;
            }
        }

        public long Length
        {
            get
            {
                if (readAll)
                {
                    return internalBuffer.Count;
                }

                if (buffer.CanRead)
                    return buffer.Length;

                return 0;
            }
        }

        public bool CanRead
        {
            get
            {
                if (readAll)
                {
                    return offset < internalBuffer.Count;
                }

                return buffer.CanRead && buffer.Position < buffer.Length;
            }
        }

        public void BufferAll()
        {
            if (!readAll)
            {
                offset = 0;
                internalBuffer = new List<byte>(16); // default gets initialized to 0 and when first item is added gets initialized to 4
                readAll = true;

                byte[] buff = new byte[1024 * 8];

                lastLenRead = buffer.Read(buff, 0, buff.Length);

                while (lastLenRead > 0 && buffer.CanRead)
                {
                    for (int i = 0; i < lastLenRead; i++)
                    {
                        internalBuffer.Add(buff[i]);
                    }

                    lastLenRead = buffer.Read(buff, 0, buff.Length);
                }
            }
        }

        public byte[] PeekBytes(int len)
        {
            if (readAll)
            {

                int end = (int)offset + len > internalBuffer.Count ? internalBuffer.Count : (int)offset + len;
                int realLen = (int)offset + len > internalBuffer.Count ? (int)internalBuffer.Count - (int)offset : len;

                byte[] rbuff = new byte[realLen];
                int rcc = 0;
                for (int i = (int)offset; i < end; i++)
                {
                    rbuff[rcc] = internalBuffer[i];
                    rcc++;
                }
                return rbuff;
            }

            long oldPos = buffer.Position;
            byte[] buf = new byte[len];

            int actualRead = buffer.Read(buf, 0, len);
            lastLenRead = actualRead;
            if (actualRead > 0)
            {
                if (actualRead == len)
                {
                    buffer.Position = oldPos;
                    return buf;
                }

                byte[] actualData = new byte[actualRead];
                for (int i = 0; i < actualRead; i++)
                {
                    actualData[i] = buf[i];
                }

                buffer.Position = oldPos;
                return actualData;
            }

            buffer.Position = oldPos;
            return new byte[0];
        }

        public byte ReadByte()
        {
            if (!CanRead) throw new Exception("Trying to read past end of buffer");
            if (readAll)
            {
                return internalBuffer[(int)offset++];
            }
            lastLenRead = buffer.ReadByte();
            if (lastLenRead > -1)
                return (byte)lastLenRead;
            return 0;
        }

        public byte[] ReadBytes(int len)
        {
            if (readAll)
            {
                int end = (int)offset + len > internalBuffer.Count ? internalBuffer.Count : (int)offset + len;
                int realLen = (int)offset + len > internalBuffer.Count ? (int)internalBuffer.Count - (int)offset : len;

                byte[] rbuff = new byte[realLen];
                int rcc = 0;
                for (int i = (int)offset; i < end; i++)
                {
                    rbuff[rcc] = internalBuffer[i];
                    rcc++;
                }
                offset += len;
                return rbuff;
            }

            byte[] buf = new byte[len];

            int actualRead = buffer.Read(buf, 0, len);
            lastLenRead = actualRead;
            if (actualRead > 0)
            {
                if (actualRead == len)
                {
                    return buf;
                }

                byte[] actualData = new byte[actualRead];
                for (int i = 0; i < actualRead; i++)
                {
                    actualData[i] = buf[i];
                }

                return actualData;
            }

            return new byte[0];
        }

        public byte PeekByte()
        {
            if (!CanRead) throw new Exception("Trying to read past end of buffer");
            if (readAll)
            {
                return internalBuffer[(int)offset];
            }
            long lastPos = buffer.Position;
            byte b = ReadByte();
            buffer.Position = lastPos;
            return b;
        }

        //increases the offset by 1
        public void Next()
        {
            buffer.Position++;
        }

        public void Skip(int len)
        {
            buffer.Position += len;
        }

        public void Dispose()
        {
            buffer.Dispose();
        }
    }
}
