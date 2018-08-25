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

using System.IO;
using MatthiWare.Compression.VCDiff.Includes;
using MatthiWare.Compression.VCDiff.Shared;

namespace MatthiWare.Compression.VCDiff.Decoders
{
    public class CustomCodeTableDecoder
    {
        public byte NearSize { get; private set; }

        public byte SameSize { get; private set; }

        public CodeTable CustomTable { get; private set; }

        public CustomCodeTableDecoder()
        {

        }

        public VCDiffResult Decode(IByteBuffer source)
        {
            VCDiffResult result = VCDiffResult.SUCCESS;

            //the custom codetable itself is a VCDiff file but it is required to be encoded with the standard table
            //the length should be the first thing after the hdr_indicator if not supporting compression
            //at least according to the RFC specs.
            int lengthOfCodeTable = VarIntBE.ParseInt32(source);

            if (lengthOfCodeTable == 0) return VCDiffResult.ERRROR;

            ByteBuffer codeTable = new ByteBuffer(source.ReadBytes(lengthOfCodeTable));

            //according to the RFC specifications the next two items will be the size of near and size of same
            //they are bytes in the RFC spec, but for some reason Google uses the varint to read which does
            //the same thing if it is a single byte
            //but I am going to just read in bytes because it is the RFC standard
            NearSize = codeTable.ReadByte();
            SameSize = codeTable.ReadByte();

            if (NearSize == 0 || SameSize == 0 || NearSize > byte.MaxValue || SameSize > byte.MaxValue)
            {
                return VCDiffResult.ERRROR;
            }

            CustomTable = new CodeTable();
            //get the original bytes of the default codetable to use as a dictionary
            IByteBuffer dictionary = CustomTable.GetBytes();

            //Decode the code table VCDiff file itself
            //stream the decoded output into a memory stream
            using (MemoryStream sout = new MemoryStream())
            {
                VCDecoder decoder = new VCDecoder(dictionary, codeTable, sout);
                result = decoder.Start();

                if (result != VCDiffResult.SUCCESS)
                {
                    return result;
                }

                long bytesWritten = 0;
                result = decoder.Decode(out bytesWritten);

                if (result != VCDiffResult.SUCCESS || bytesWritten == 0)
                {
                    return VCDiffResult.ERRROR;
                }

                //set the new table data that was decoded
                if (!CustomTable.SetBytes(sout.ToArray()))
                {
                    result = VCDiffResult.ERRROR;
                }
            }

            return result;
        }
    }
}
