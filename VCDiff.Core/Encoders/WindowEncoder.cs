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
using System.Linq;
using MatthiWare.Compression.VCDiff.Includes;
using MatthiWare.Compression.VCDiff.Shared;

namespace MatthiWare.Compression.VCDiff.Encoders
{
    public class WindowEncoder
    {
        private int maxMode;
        private long dictionarySize;
        private long targetLength;
        private CodeTable table;
        private int lastOpcodeIndex;
        private AddressCache addrCache;
        private InstructionMap instrMap;
        private IList<byte> instructionAndSizes;
        private IList<byte> dataForAddAndRun;
        private IList<byte> addressForCopy;

        public bool HasChecksum { get; }

        public bool IsInterleaved { get; }

        public uint Checksum { get; }

        //This is a window encoder for the VCDIFF format
        //if you are not including a checksum simply pass 0 to checksum
        //it will be ignored
        public WindowEncoder(long dictionarySize, uint checksum, bool interleaved = false, bool hasChecksum = false)
        {
            this.Checksum = checksum;
            this.HasChecksum = hasChecksum;
            this.IsInterleaved = interleaved;
            this.dictionarySize = dictionarySize;

            //The encoder currently doesn't support encoding with a custom table
            //will be added in later since it will be easy as decoding is already implemented
            maxMode = AddressCache.DefaultLast;
            table = CodeTable.DefaultTable;
            addrCache = new AddressCache();
            targetLength = 0;
            lastOpcodeIndex = -1;
            instrMap = new InstructionMap();

            //Separate buffers for each type if not interleaved
            if (!interleaved)
            {
                instructionAndSizes = new List<byte>();
                dataForAddAndRun = new List<byte>();
                addressForCopy = new List<byte>();
            }
            else
            {
                instructionAndSizes = dataForAddAndRun = addressForCopy = new List<byte>();
            }
        }

        void EncodeInstruction(VCDiffInstructionType inst, int size, byte mode = 0)
        {
            if (lastOpcodeIndex >= 0)
            {
                int lastOp = instructionAndSizes[lastOpcodeIndex];

                if (inst == VCDiffInstructionType.ADD && (table.inst1[lastOp] == CodeTable.A))
                {
                    //warning adding two in a row
                    Console.WriteLine("Warning: performing two ADD instructions in a row.");
                }
                int compoundOp = CodeTable.kNoOpcode;
                if (size <= byte.MaxValue)
                {
                    compoundOp = instrMap.LookSecondOpcode((byte)lastOp, (byte)inst, (byte)size, mode);
                    if (compoundOp != CodeTable.kNoOpcode)
                    {
                        instructionAndSizes[lastOpcodeIndex] = (byte)compoundOp;
                        lastOpcodeIndex = -1;
                        return;
                    }
                }

                compoundOp = instrMap.LookSecondOpcode((byte)lastOp, (byte)inst, (byte)0, mode);
                if (compoundOp != CodeTable.kNoOpcode)
                {
                    instructionAndSizes[lastOpcodeIndex] = (byte)compoundOp;
                    //append size to instructionAndSizes
                    VarIntBE.AppendInt32(size, instructionAndSizes);
                    lastOpcodeIndex = -1;
                }
            }

            int opcode = CodeTable.kNoOpcode;
            if (size <= byte.MaxValue)
            {
                opcode = instrMap.LookFirstOpcode((byte)inst, (byte)size, mode);

                if (opcode != CodeTable.kNoOpcode)
                {
                    instructionAndSizes.Add((byte)opcode);
                    lastOpcodeIndex = instructionAndSizes.Count - 1;
                    return;
                }
            }
            opcode = instrMap.LookFirstOpcode((byte)inst, 0, mode);
            if (opcode == CodeTable.kNoOpcode)
            {
                return;
            }

            instructionAndSizes.Add((byte)opcode);
            lastOpcodeIndex = instructionAndSizes.Count - 1;
            VarIntBE.AppendInt32(size, instructionAndSizes);
        }

        public void Add(byte[] data)
        {
            EncodeInstruction(VCDiffInstructionType.ADD, data.Length);

            foreach (var b in data)
                dataForAddAndRun.Add(b);

            targetLength += data.Length;
        }

        public void Copy(int offset, int length)
        {
            long encodedAddr = 0;
            byte mode = addrCache.EncodeAddress(offset, dictionarySize + targetLength, out encodedAddr);
            EncodeInstruction(VCDiffInstructionType.COPY, length, mode);
            if (addrCache.WriteAddressAsVarint(mode))
            {
                VarIntBE.AppendInt64(encodedAddr, addressForCopy);
            }
            else
            {
                addressForCopy.Add((byte)encodedAddr);
            }
            targetLength += length;
        }

        public void Run(int size, byte b)
        {
            EncodeInstruction(VCDiffInstructionType.RUN, size);
            dataForAddAndRun.Add(b);
            targetLength += size;
        }

        int CalculateLengthOfTheDeltaEncoding()
        {
            int extraLength = 0;

            if (HasChecksum)
            {
                extraLength += VarIntBE.CalcInt64Length(Checksum);
            }

            if (!IsInterleaved)
            {
                int lengthOfDelta = VarIntBE.CalcInt32Length((int)targetLength) +
                1 +
                VarIntBE.CalcInt32Length(dataForAddAndRun.Count) +
                VarIntBE.CalcInt32Length(instructionAndSizes.Count) +
                VarIntBE.CalcInt32Length(addressForCopy.Count) +
                dataForAddAndRun.Count +
                instructionAndSizes.Count +
                addressForCopy.Count;

                lengthOfDelta += extraLength;

                return lengthOfDelta;
            }
            else
            {
                int lengthOfDelta = VarIntBE.CalcInt32Length((int)targetLength) +
                1 +
                VarIntBE.CalcInt32Length(0) +
                VarIntBE.CalcInt32Length(instructionAndSizes.Count) +
                VarIntBE.CalcInt32Length(0) +
                0 +
                instructionAndSizes.Count;

                lengthOfDelta += extraLength;

                return lengthOfDelta;
            }
        }

        public void Output(ByteStreamWriter sout)
        {
            int lengthOfDelta = CalculateLengthOfTheDeltaEncoding();
            int windowSize = lengthOfDelta +
            1 +
            VarIntBE.CalcInt32Length((int)dictionarySize) +
            VarIntBE.CalcInt32Length(0);
            VarIntBE.CalcInt32Length(lengthOfDelta);

            //Google's Checksum Implementation Support
            if (HasChecksum)
            {
                sout.writeByte((byte)VCDiffWindowFlags.VCDSOURCE | (byte)VCDiffWindowFlags.VCDCHECKSUM); //win indicator
            }
            else
            {
                sout.writeByte((byte)VCDiffWindowFlags.VCDSOURCE); //win indicator
            }
            VarIntBE.AppendInt32((int)dictionarySize, sout); //dictionary size
            VarIntBE.AppendInt32(0, sout); //dictionary start position 0 is default aka encompass the whole dictionary

            VarIntBE.AppendInt32(lengthOfDelta, sout); //length of delta

            //begin of delta encoding
            Int64 sizeBeforeDelta = sout.Position;
            VarIntBE.AppendInt32((int)targetLength, sout); //final target length after decoding
            sout.writeByte(0x00); //uncompressed

            // [Here is where a secondary compressor would be used
            //  if the encoder and decoder supported that feature.]

            //non interleaved then it is separata areas for each type
            if (!IsInterleaved)
            {
                VarIntBE.AppendInt32(dataForAddAndRun.Count, sout); //length of add/run
                VarIntBE.AppendInt32(instructionAndSizes.Count, sout); //length of instructions and sizes
                VarIntBE.AppendInt32(addressForCopy.Count, sout); //length of addresses for copys

                //Google Checksum Support
                if (HasChecksum)
                {
                    VarIntBE.AppendInt64(Checksum, sout);
                }

                sout.writeBytes(dataForAddAndRun.ToArray()); //data section for adds and runs
                sout.writeBytes(instructionAndSizes.ToArray()); //data for instructions and sizes
                sout.writeBytes(addressForCopy.ToArray()); //data for addresses section copys
            }
            else
            {
                //interleaved everything is woven in and out in one block
                VarIntBE.AppendInt32(0, sout); //length of add/run
                VarIntBE.AppendInt32(instructionAndSizes.Count, sout); //length of instructions and sizes + other data for interleaved
                VarIntBE.AppendInt32(0, sout); //length of addresses for copys

                //Google Checksum Support
                if (HasChecksum)
                {
                    VarIntBE.AppendInt64(Checksum, sout);
                }

                sout.writeBytes(instructionAndSizes.ToArray()); //data for instructions and sizes, in interleaved it is everything
            }
            //end of delta encoding

            Int64 sizeAfterDelta = sout.Position;
            if (lengthOfDelta != sizeAfterDelta - sizeBeforeDelta)
            {
                Console.WriteLine("Delta output length does not match");
            }
            dataForAddAndRun.Clear();
            instructionAndSizes.Clear();
            addressForCopy.Clear();
            if (targetLength == 0)
            {
                Console.WriteLine("Empty target window");
            }
            addrCache = new AddressCache();
        }
    }
}
