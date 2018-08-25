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

using MatthiWare.Compression.VCDiff.Includes;
using MatthiWare.Compression.VCDiff.Shared;

namespace MatthiWare.Compression.VCDiff.Decoders
{
    public class InstructionDecoder
    {
        CodeTable table;
        ByteBuffer source;
        int pendingSecond;

        /// <summary>
        /// Decodes the incoming instruction from the buffer
        /// </summary>
        /// <param name="sin">the instruction buffer</param>
        /// <param name="customTable">custom code table if any. Default is null.</param>
        public InstructionDecoder(ByteBuffer sin, CustomCodeTableDecoder customTable = null)
        {
            if (customTable != null)
            {
                table = customTable.CustomTable;
            }
            else
            {
                table = CodeTable.DefaultTable;
            }
            source = sin;
            pendingSecond = CodeTable.kNoOpcode;
        }

        /// <summary>
        /// Gets the next instruction from the buffer
        /// </summary>
        /// <param name="size">the size</param>
        /// <param name="mode">the mode</param>
        /// <returns></returns>
        public VCDiffInstructionType Next(out int size, out byte mode)
        {
            byte opcode = 0;
            byte instructionType = CodeTable.N;
            int instructionSize = 0;
            byte instructionMode = 0;
            int start = (int)source.Position;
            do
            {
                if (pendingSecond != CodeTable.kNoOpcode)
                {
                    opcode = (byte)pendingSecond;
                    pendingSecond = CodeTable.kNoOpcode;
                    instructionType = table.inst2[opcode];
                    instructionSize = table.size2[opcode];
                    instructionMode = table.mode2[opcode];
                    break;
                }

                if (!source.CanRead)
                {
                    size = 0;
                    mode = 0;
                    return VCDiffInstructionType.EOD;
                }

                opcode = source.PeekByte();
                if (table.inst2[opcode] != CodeTable.N)
                {
                    pendingSecond = source.PeekByte();
                }
                source.Next();
                instructionType = table.inst1[opcode];
                instructionSize = table.size1[opcode];
                instructionMode = table.mode1[opcode];
            } while (instructionType == CodeTable.N);

            if (instructionSize == 0)
            {
                switch (size = VarIntBE.ParseInt32(source))
                {
                    case (int)VCDiffResult.ERRROR:
                        mode = 0;
                        size = 0;
                        return VCDiffInstructionType.ERROR;
                    case (int)VCDiffResult.EOD:
                        mode = 0;
                        size = 0;
                        //reset it back before we read the instruction
                        //otherwise when parsing interleave we will miss data
                        source.Position = start;
                        return VCDiffInstructionType.EOD;
                    default:
                        break;
                }
            }
            else
            {
                size = instructionSize;
            }
            mode = instructionMode;
            return (VCDiffInstructionType)instructionType;
        }
    }
}
