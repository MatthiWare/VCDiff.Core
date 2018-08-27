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
using System.IO;

namespace MatthiWare.Compression.VCDiff.Shared
{
    public class ByteStreamWriter : IDisposable
    {
        Stream buffer;

        bool isLittle;

        /// <summary>
        /// Wrapper class for writing to streams
        /// with a little bit easier functionality
        /// also detects whether it is little endian
        /// to encode into BE properly
        /// </summary>
        /// <param name="s"></param>
        public ByteStreamWriter(Stream s)
        {
            buffer = s;
            isLittle = BitConverter.IsLittleEndian;
        }

        public byte[] ToArray()
        {
            if (buffer.GetType().Equals(typeof(MemoryStream)))
            {
                MemoryStream buff = (MemoryStream)buffer;
                return buff.ToArray();
            }

            return new byte[0];
        }

        public long Position => buffer.Position;

        public void writeByte(byte b)
        {
            buffer.WriteByte(b);
        }

        public void writeBytes(byte[] b)
        {
            buffer.Write(b, 0, b.Length);
        }

        public void writeUInt16(ushort s)
        {
            byte[] bytes = BitConverter.GetBytes(s);

            if (isLittle)
            {
                Array.Reverse(bytes);
            }

            writeBytes(bytes);
        }

        public void writeUInt32(uint i)
        {
            byte[] bytes = BitConverter.GetBytes(i);

            if (isLittle)
            {
                Array.Reverse(bytes);
            }

            writeBytes(bytes);
        }

        public void writeFloat(float f)
        {
            byte[] bytes = BitConverter.GetBytes(f);

            if (isLittle)
            {
                Array.Reverse(bytes);
            }

            writeBytes(bytes);
        }

        public void writeDouble(double d)
        {
            byte[] bytes = BitConverter.GetBytes(d);

            if (isLittle)
            {
                Array.Reverse(bytes);
            }

            writeBytes(bytes);
        }

        public void Dispose()
        {
            buffer.Dispose();
        }
    }
}
