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
    public abstract class IByteBuffer : IDisposable
    {
        public abstract long Length
        {
            get;
        }

        public abstract long Position
        {
            get; set;
        }

        public abstract bool CanRead
        {
            get;
        }
        public abstract byte[] ReadBytes(int len);
        public abstract byte ReadByte();
        public abstract byte[] PeekBytes(int len);
        public abstract byte PeekByte();
        public abstract void Skip(int len);
        public abstract void Next();
        public abstract void BufferAll();

        public abstract void Dispose();
    }
}
