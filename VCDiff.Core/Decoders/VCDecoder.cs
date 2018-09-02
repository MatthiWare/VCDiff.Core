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
    public class VCDecoder
    {
        ByteStreamWriter sout;
        IByteBuffer delta;
        IByteBuffer dict;
        CustomCodeTableDecoder customTable;
        static byte[] MagicBytes = new byte[] { 0xD6, 0xC3, 0xC4, 0x00, 0x00 };

        public bool IsSDHCFormat { get; private set; }

        public bool IsStarted { get; private set; }

        /// <summary>
        /// Dict is the dictionary file
        /// Delta is the diff file
        /// Sout is the stream for output
        /// </summary>
        /// <param name="dict">Dictionary</param>
        /// <param name="delta">Target file / Diff / Delta file</param>
        /// <param name="sout">Output Stream</param>
        public VCDecoder(Stream dict, Stream delta, Stream sout)
        {
            this.delta = new ByteStreamReader(delta);
            this.dict = new ByteStreamReader(dict);
            this.sout = new ByteStreamWriter(sout);

            IsStarted = false;
        }

        public VCDecoder(IByteBuffer dict, IByteBuffer delta, Stream sout)
        {
            this.delta = delta;
            this.dict = dict;
            this.sout = new ByteStreamWriter(sout);

            IsStarted = false;
        }

        /// <summary>
        /// Call this before calling decode
        /// This expects at least the header part of the delta file
        /// is available in the stream
        /// </summary>
        /// <returns></returns>
        public VCDiffResult Start()
        {
            if (!delta.CanRead) return VCDiffResult.EOD;

            byte V = delta.ReadByte();

            if (!delta.CanRead) return VCDiffResult.EOD;

            byte C = delta.ReadByte();

            if (!delta.CanRead) return VCDiffResult.EOD;

            byte D = delta.ReadByte();

            if (!delta.CanRead) return VCDiffResult.EOD;

            byte version = delta.ReadByte();

            if (!delta.CanRead) return VCDiffResult.EOD;

            byte hdr = delta.ReadByte();

            if (V != MagicBytes[0])
            {
                return VCDiffResult.ERROR;
            }

            if (C != MagicBytes[1])
            {
                return VCDiffResult.ERROR;
            }

            if (D != MagicBytes[2])
            {
                return VCDiffResult.ERROR;
            }

            if (version != 0x00 && version != 'S')
            {
                return VCDiffResult.ERROR;
            }

            //compression not supported
            if ((hdr & (int)VCDiffCodeFlags.VCDDECOMPRESS) != 0)
            {
                return VCDiffResult.ERROR;
            }

            //custom code table!
            if ((hdr & (int)VCDiffCodeFlags.VCDCODETABLE) != 0)
            {
                if (!delta.CanRead) return VCDiffResult.EOD;

                //try decoding the custom code table
                //since we don't support the compress the next line should be the length of the code table
                customTable = new CustomCodeTableDecoder();
                VCDiffResult result = customTable.Decode(delta);

                if (result != VCDiffResult.SUCCESS)
                {
                    return result;
                }
            }

            IsSDHCFormat = version == 'S';

            IsStarted = true;

            //buffer all the dictionary up front
            dict.BufferAll();

            return VCDiffResult.SUCCESS;
        }

        /// <summary>
        /// Use this after calling Start
        /// Each time the decode is called it is expected
        /// that at least 1 Window header is available in the stream
        /// </summary>
        /// <param name="bytesWritten">bytes decoded for all available windows</param>
        /// <returns></returns>
        public VCDiffResult Decode(out long bytesWritten)
        {
            if (!IsStarted)
            {
                bytesWritten = 0;
                return VCDiffResult.ERROR;
            }

            VCDiffResult result = VCDiffResult.SUCCESS;
            bytesWritten = 0;

            if (!delta.CanRead) return VCDiffResult.EOD;

            while (delta.CanRead)
            {
                //delta is streamed in order aka not random access
                WindowDecoder w = new WindowDecoder(dict.Length, delta);

                if (w.Decode(IsSDHCFormat))
                {
                    using (BodyDecoder body = new BodyDecoder(w, dict, delta, sout))
                    {

                        if (IsSDHCFormat && w.AddRunLength == 0 && w.AddressesForCopyLength == 0 && w.InstructionAndSizesLength > 0)
                        {
                            //interleaved
                            //decodedinterleave actually has an internal loop for waiting and streaming the incoming rest of the interleaved window
                            result = body.DecodeInterleave();

                            if (result != VCDiffResult.SUCCESS && result != VCDiffResult.EOD)
                            {
                                return result;
                            }

                            bytesWritten += body.Decoded;
                        }
                        //technically add could be 0 if it is all copy instructions
                        //so do an or check on those two
                        else if (IsSDHCFormat && (w.AddRunLength > 0 || w.AddressesForCopyLength > 0) && w.InstructionAndSizesLength > 0)
                        {
                            //not interleaved
                            //expects the full window to be available
                            //in the stream

                            result = body.Decode();

                            if (result != VCDiffResult.SUCCESS)
                            {
                                return result;
                            }

                            bytesWritten += body.Decoded;
                        }
                        else if (!IsSDHCFormat)
                        {
                            //not interleaved
                            //expects the full window to be available 
                            //in the stream
                            result = body.Decode();

                            if (result != VCDiffResult.SUCCESS)
                            {
                                return result;
                            }

                            bytesWritten += body.Decoded;
                        }
                        else
                        {
                            //invalid file
                            return VCDiffResult.ERROR;
                        }
                    }
                }
                else
                {
                    return (VCDiffResult)w.Result;
                }
            }

            return result;
        }
    }
}
