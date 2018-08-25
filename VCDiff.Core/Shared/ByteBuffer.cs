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

namespace VCDiff.Shared
{
    public class ByteBuffer : IByteBuffer, IDisposable
    {
        byte[] bytes;
        int length;
        long offset;

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
                this.length = bytes.Length;
            }
            else
            {
                this.length = 0;
            }
        }

        public override bool CanRead
        {
            get
            {
                return offset < length;
            }
        }

        public override long Position
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

        public override void BufferAll()
        {
            //not implemented in this one
            //since it already contains the full buffered data
        }

        public override long Length
        {
            get
            {
                return length;
            }
        }

        public override byte PeekByte()
        {
            if (offset >= length) throw new Exception("Trying to read past End of Buffer");
            return this.bytes[offset];
        }

        public override byte[] PeekBytes(int len)
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

        public override byte ReadByte()
        {
            if (offset >= length) throw new Exception("Trying to read past End of Buffer");
            return this.bytes[offset++];
        }

        public override byte[] ReadBytes(int len)
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

        public override void Next()
        {
            offset++;
        }

        public override void Skip(int len)
        {
            offset += len;
        }

        public override void Dispose()
        {
            bytes = null;
        }
    }
}
