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
    public class WindowDecoder
    {
        private IByteBuffer buffer;
        private long deltaEncodingLength;
        private ParseableChunk chunk;
        private byte deltaIndicator;
        private long dictionarySize;
        private byte winIndicator;
        private long sourceLength;
        private long sourcePosition;
        private long targetLength;
        private long addRunLength;
        private long instructionAndSizesLength;
        private long addressForCopyLength;
        private uint checksum;

        public byte[] AddRunData { get; private set; }

        public byte[] InstructionsAndSizesData { get; private set; }

        public byte[] AddressesForCopyData { get; private set; }

        public long AddRunLength => addRunLength;

        public long InstructionAndSizesLength => instructionAndSizesLength;

        public long AddressesForCopyLength => addressForCopyLength;

        public byte WinIndicator => winIndicator;

        public long SourcePosition => sourcePosition;

        public long SourceLength => sourceLength;

        public long DecodedDeltaLength => targetLength;

        public long DeltaStart { get; private set; }

        public long DeltaLength => DeltaStart + deltaEncodingLength;

        public byte DeltaIndicator => deltaIndicator;

        public uint Checksum => checksum;

        public bool HasChecksum { get; private set; }

        public int Result { get; private set; }

        /// <summary>
        /// Parses the window from the data
        /// </summary>
        /// <param name="dictionarySize">the dictionary size</param>
        /// <param name="buffer">the buffer containing the incoming data</param>
        public WindowDecoder(long dictionarySize, IByteBuffer buffer)
        {
            this.dictionarySize = dictionarySize;
            this.buffer = buffer;
            chunk = new ParseableChunk(buffer.Position, buffer.Length);
            Result = (int)VCDiffResult.Succes;
        }

        /// <summary>
        /// Decodes the window header - Parses it basically
        /// </summary>
        /// <param name="googleVersion">if true will check for checksum and if interleaved</param>
        /// <returns></returns>
        public bool Decode(bool googleVersion)
        {
            bool success = (ParseWindowIndicatorAndSegment(dictionarySize, 0, false, out winIndicator, out sourceLength, out sourcePosition)
                && ParseWindowLengths(out targetLength)
                && ParseDeltaIndicator());

            if (!success) return false;

            HasChecksum = false;
            if ((winIndicator & (int)VCDiffWindowFlags.VCDCHECKSUM) != 0 && googleVersion)
            {
                HasChecksum = true;
            }

            success = ParseSectionLengths(HasChecksum, out addRunLength, out instructionAndSizesLength, out addressForCopyLength, out checksum);

            if (!success) return false;

            if (googleVersion && addRunLength == 0 && addressForCopyLength == 0 && instructionAndSizesLength > 0) return true;

            if (buffer.CanRead)
            {
                AddRunData = buffer.ReadBytes((int)addRunLength);
            }

            if (buffer.CanRead)
            {
                InstructionsAndSizesData = buffer.ReadBytes((int)instructionAndSizesLength);
            }

            if (buffer.CanRead)
            {
                AddressesForCopyData = buffer.ReadBytes((int)addressForCopyLength);
            }

            return true;
        }

        private bool ParseByte(out byte value)
        {
            if ((int)VCDiffResult.Succes != Result)
            {
                value = 0;
                return false;
            }
            if (chunk.IsEmpty)
            {
                value = 0;
                Result = (int)VCDiffResult.EOD;
                return false;
            }
            value = buffer.ReadByte();
            chunk.Position = buffer.Position;
            return true;
        }

        private bool ParseInt32(out int value)
        {
            if ((int)VCDiffResult.Succes != Result)
            {
                value = 0;
                return false;
            }
            if (chunk.IsEmpty)
            {
                value = 0;
                Result = (int)VCDiffResult.EOD;
                return false;
            }

            int parsed = VarIntBE.ParseInt32(buffer);
            switch (parsed)
            {
                case (int)VCDiffResult.Error:
                    value = 0;
                    return false;

                case (int)VCDiffResult.EOD:
                    value = 0;
                    return false;

                default:
                    break;
            }
            chunk.Position = buffer.Position;
            value = parsed;
            return true;
        }

        private bool ParseUInt32(out uint value)
        {
            if ((int)VCDiffResult.Succes != Result)
            {
                value = 0;
                return false;
            }
            if (chunk.IsEmpty)
            {
                value = 0;
                Result = (int)VCDiffResult.EOD;
                return false;
            }

            long parsed = VarIntBE.ParseInt64(buffer);
            switch (parsed)
            {
                case (int)VCDiffResult.Error:
                    value = 0;
                    return false;

                case (int)VCDiffResult.EOD:
                    value = 0;
                    return false;

                default:
                    break;
            }
            if (parsed > 0xFFFFFFFF)
            {
                Result = (int)VCDiffResult.Error;
                value = 0;
                return false;
            }
            chunk.Position = buffer.Position;
            value = (uint)parsed;
            return true;
        }

        private bool ParseSourceSegmentLengthAndPosition(long from, out long sourceLength, out long sourcePosition)
        {
            int outLength;
            if (!ParseInt32(out outLength))
            {
                sourceLength = 0;
                sourcePosition = 0;
                return false;
            }
            sourceLength = outLength;
            if (sourceLength > from)
            {
                Result = (int)VCDiffResult.Error;
                sourceLength = 0;
                sourcePosition = 0;
                return false;
            }
            int outPos;
            if (!ParseInt32(out outPos))
            {
                sourcePosition = 0;
                sourceLength = 0;
                return false;
            }
            sourcePosition = outPos;
            if (sourcePosition > from)
            {
                Result = (int)VCDiffResult.Error;
                sourceLength = 0;
                sourcePosition = 0;
                return false;
            }

            long segmentEnd = sourcePosition + sourceLength;
            if (segmentEnd > from)
            {
                Result = (int)VCDiffResult.Error;
                sourceLength = 0;
                sourcePosition = 0;
                return false;
            }

            return true;
        }

        private bool ParseWindowIndicatorAndSegment(long dictionarySize, long decodedTargetSize, bool allowVCDTarget, out byte winIndicator, out long sourceSegmentLength, out long sourceSegmentPosition)
        {
            if (!ParseByte(out winIndicator))
            {
                winIndicator = 0;
                sourceSegmentLength = 0;
                sourceSegmentPosition = 0;
                return false;
            }

            int sourceFlags = winIndicator & ((int)VCDiffWindowFlags.VCDSOURCE | (int)VCDiffWindowFlags.VCDTARGET);

            switch (sourceFlags)
            {
                case (int)VCDiffWindowFlags.VCDSOURCE:
                    return ParseSourceSegmentLengthAndPosition(dictionarySize, out sourceSegmentLength, out sourceSegmentPosition);

                case (int)VCDiffWindowFlags.VCDTARGET:
                    if (!allowVCDTarget)
                    {
                        winIndicator = 0;
                        sourceSegmentLength = 0;
                        sourceSegmentPosition = 0;
                        Result = (int)VCDiffResult.Error;
                        return false;
                    }
                    return ParseSourceSegmentLengthAndPosition(decodedTargetSize, out sourceSegmentLength, out sourceSegmentPosition);

                case (int)VCDiffWindowFlags.VCDSOURCE | (int)VCDiffWindowFlags.VCDTARGET:
                    winIndicator = 0;
                    sourceSegmentPosition = 0;
                    sourceSegmentLength = 0;
                    return false;
            }

            winIndicator = 0;
            sourceSegmentPosition = 0;
            sourceSegmentLength = 0;
            return false;
        }

        private bool ParseWindowLengths(out long targetWindowLength)
        {
            int deltaLength;
            if (!ParseInt32(out deltaLength))
            {
                targetWindowLength = 0;
                return false;
            }
            deltaEncodingLength = deltaLength;

            DeltaStart = chunk.ParsedSize;
            int outTargetLength;
            if (!ParseInt32(out outTargetLength))
            {
                targetWindowLength = 0;
                return false;
            }
            targetWindowLength = outTargetLength;
            return true;
        }

        private bool ParseDeltaIndicator()
        {
            if (!ParseByte(out deltaIndicator))
            {
                Result = (int)VCDiffResult.Error;
                return false;
            }
            if ((deltaIndicator & ((int)VCDiffCompressFlags.VCDDATACOMP | (int)VCDiffCompressFlags.VCDINSTCOMP | (int)VCDiffCompressFlags.VCDADDRCOMP)) > 0)
            {
                Result = (int)VCDiffResult.Error;
                return false;
            }
            return true;
        }

        public bool ParseSectionLengths(bool hasChecksum, out long addRunLength, out long instructionsLength, out long addressLength, out uint checksum)
        {
            int outAdd;
            int outInstruct;
            int outAddress;
            ParseInt32(out outAdd);
            ParseInt32(out outInstruct);
            ParseInt32(out outAddress);
            checksum = 0;

            if (hasChecksum)
            {
                ParseUInt32(out checksum);
            }

            addRunLength = outAdd;
            addressLength = outAddress;
            instructionsLength = outInstruct;

            if (Result != (int)VCDiffResult.Succes)
            {
                return false;
            }

            long deltaHeaderLength = chunk.ParsedSize - DeltaStart;
            long totalLen = deltaHeaderLength + addRunLength + instructionsLength + addressLength;

            if (deltaEncodingLength != totalLen)
            {
                Result = (int)VCDiffResult.Error;
                return false;
            }

            return true;
        }

        public class ParseableChunk
        {
            private long position;

            public long UnparsedSize => End - position;

            public long End { get; }

            public bool IsEmpty => 0 == UnparsedSize;

            public long Start { get; }

            public long ParsedSize => position - Start;

            public long Position
            {
                get
                {
                    return position;
                }
                set
                {
                    if (position < Start)
                    {
                        return;
                    }
                    if (position > End)
                    {
                        return;
                    }
                    position = value;
                }
            }

            public ParseableChunk(long s, long len)
            {
                Start = s;
                End = s + len;
                position = s;
            }
        }
    }
}