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

namespace MatthiWare.Compression.VCDiff.Shared
{
    public class ByteBuffer : IByteBuffer, IDisposable
    {
        private byte[] bytes;
        private readonly int length;
        private long offset;

        /// <summary>
        /// Basically a simple wrapper for byte[] arrays
        /// for easier reading and parsing
        /// </summary>
        /// <param name="bytes"></param>
        public ByteBuffer(byte[] bytes)
        {
            offset = 0;
            this.bytes = bytes;
            if (bytes != null)
            {
                length = bytes.Length;
            }
            else
            {
                length = 0;
            }
        }

        public bool CanRead => offset < length;

        public long Position
        {
            get
            {
                return offset;
            }
            set
            {
                if (value > bytes.Length || value < 0) return;
                offset = value;
            }
        }

        public void BufferAll() => throw new NotImplementedException("Already contains the full buffer data");

        public long Length => length;

        public byte PeekByte()
        {
            if (offset >= length) throw new IndexOutOfRangeException("Trying to read past End of Buffer");
            return bytes[offset];
        }

        public byte[] PeekBytes(int len)
        {
            int end = (int)offset + len > bytes.Length ? bytes.Length : (int)offset + len;
            int realLen = (int)offset + len > bytes.Length ? (int)bytes.Length - (int)offset : len;

            byte[] rbuff = new byte[realLen];
            int cc = 0;
            for (long i = offset; i < end; i++)
            {
                rbuff[cc] = bytes[i];
                cc++;
            }
            return rbuff;
        }

        public byte ReadByte()
        {
            if (offset >= length) throw new IndexOutOfRangeException("Trying to read past End of Buffer");
            return bytes[offset++];
        }

        public byte[] ReadBytes(int len)
        {
            int end = (int)offset + len > bytes.Length ? bytes.Length : (int)offset + len;
            int realLen = (int)offset + len > bytes.Length ? (int)bytes.Length - (int)offset : len;

            byte[] rbuff = new byte[realLen];
            int cc = 0;
            for (long i = offset; i < end; i++)
            {
                rbuff[cc] = bytes[i];
                cc++;
            }
            offset += len;
            return rbuff;
        }

        public void Next() => offset++;

        public void Skip(int len) => offset += len;

        public void Dispose()
        {
            bytes = null;
        }
    }
}
