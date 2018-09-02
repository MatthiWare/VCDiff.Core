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
using MatthiWare.Compression.VCDiff.Shared;

namespace MatthiWare.Compression.VCDiff.Encoders
{
    public class BlockHash
    {
        static int blockSize = 16;
        public static int BlockSize
        {
            get
            {
                return blockSize;
            }
            set
            {
                if (value < 2) return;
                blockSize = value;
            }
        }
        static int maxMatchesToCheck = (blockSize >= 32) ? 32 : (32 * (32 / blockSize));
        const int maxProbes = 16;
        long offset;
        ulong hashTableMask;
        long lastBlockAdded = 0;
        long[] hashTable;
        long[] nextBlockTable;
        long[] lastBlockTable;
        RollingHash hasher;

        public IByteBuffer Source { get; }

        public long SourceSize => Source.Length;

        /// <summary>
        /// Create a hash lookup table for the data
        /// </summary>
        /// <param name="sin">the data to create the table for</param>
        /// <param name="offset">the offset usually 0</param>
        /// <param name="hasher">the hashing method</param>
        public BlockHash(IByteBuffer sin, int offset, RollingHash hasher)
        {
            maxMatchesToCheck = (blockSize >= 32) ? 32 : (32 * (32 / blockSize));
            this.hasher = hasher;
            Source = sin;
            this.offset = offset;
            TableSize = CalcTableSize();

            if (TableSize == 0)
            {
                throw new Exception("BlockHash Table Size is Invalid == 0");
            }

            hashTableMask = (ulong)TableSize - 1;
            hashTable = new long[TableSize];
            nextBlockTable = new long[BlocksCount];
            lastBlockTable = new long[BlocksCount];
            lastBlockAdded = -1;
            SetTablesToInvalid();
        }

        void SetTablesToInvalid()
        {
            for (int i = 0; i < nextBlockTable.Length; i++)
            {
                lastBlockTable[i] = -1;
                nextBlockTable[i] = -1;
            }
            for (int i = 0; i < hashTable.Length; i++)
            {
                hashTable[i] = -1;
            }
        }

        long CalcTableSize()
        {
            long min = (Source.Length / sizeof(int)) + 1;
            long size = 1;

            while (size < min)
            {
                size <<= 1;

                if (size <= 0)
                {
                    return 0;
                }
            }

            if ((size & (size - 1)) != 0)
            {
                return 0;
            }

            if ((Source.Length > 0) && (size > (min * 2)))
            {
                return 0;
            }
            return size;
        }

        public void AddOneIndexHash(int index, ulong hash)
        {
            if (index == NextIndexToAdd)
            {
                AddBlock(hash);
            }
        }

        public long NextIndexToAdd => (lastBlockAdded + 1) * blockSize;

        public void AddAllBlocksThroughIndex(long index)
        {
            if (index > Source.Length)
            {
                return;
            }

            long lastAdded = lastBlockAdded * blockSize;
            if (index <= lastAdded)
            {
                return;
            }

            if (Source.Length < blockSize)
            {
                return;
            }

            long endLimit = index;
            long lastLegalHashIndex = (Source.Length - blockSize);

            if (endLimit > lastLegalHashIndex)
            {
                endLimit = lastLegalHashIndex + 1;
            }

            long offset = Source.Position + NextIndexToAdd;
            long end = Source.Position + endLimit;
            Source.Position = offset;
            while (offset < end)
            {
                AddBlock(hasher.Hash(Source.ReadBytes(blockSize)));
                offset += blockSize;
            }
        }

        public long BlocksCount => Source.Length / blockSize;

        public long TableSize { get; } = 0;

        long GetTableIndex(ulong hash)
        {
            return (long)(hash & hashTableMask);
        }


        /// <summary>
        /// Finds the best matching block for the candidate
        /// </summary>
        /// <param name="hash">the hash to look for</param>
        /// <param name="candidateStart">the start position</param>
        /// <param name="targetStart">the target start position</param>
        /// <param name="targetSize">the data left to encode</param>
        /// <param name="target">the target buffer</param>
        /// <param name="m">the match object to use</param>
        public void FindBestMatch(ulong hash, long candidateStart, long targetStart, long targetSize, IByteBuffer target, Match m)
        {
            int matchCounter = 0;

            for (long blockNumber = FirstMatchingBlock(hash, candidateStart, target);
                blockNumber >= 0 && !TooManyMatches(ref matchCounter);
                blockNumber = NextMatchingBlock(blockNumber, candidateStart, target))
            {
                long sourceMatchOffset = blockNumber * blockSize;
                long sourceStart = blockNumber * blockSize;
                long sourceMatchEnd = sourceMatchOffset + blockSize;
                long targetMatchOffset = candidateStart - targetStart;
                long targetMatchEnd = targetMatchOffset + blockSize;

                long matchSize = blockSize;

                long limitBytesToLeft = Math.Min(sourceMatchOffset, targetMatchOffset);
                long leftMatching = MatchingBytesToLeft(sourceMatchOffset, targetStart + targetMatchOffset, target, limitBytesToLeft);
                sourceMatchOffset -= leftMatching;
                targetMatchOffset -= leftMatching;
                matchSize += leftMatching;

                long sourceBytesToRight = Source.Length - sourceMatchEnd;
                long targetBytesToRight = targetSize - targetMatchEnd;
                long rightLimit = Math.Min(sourceBytesToRight, targetBytesToRight);

                long rightMatching = MatchingBytesToRight(sourceMatchEnd, targetStart + targetMatchEnd, target, rightLimit);
                matchSize += rightMatching;
                sourceMatchEnd += rightMatching;
                targetMatchEnd += rightMatching;
                m.ReplaceIfBetterMatch(matchSize, sourceMatchOffset + offset, targetMatchOffset);
            }
        }

        public void AddBlock(ulong hash)
        {
            long blockNumber = lastBlockAdded + 1;
            long totalBlocks = BlocksCount;
            if (blockNumber >= totalBlocks)
            {
                return;
            }

            if (nextBlockTable[blockNumber] != -1)
            {
                return;
            }

            long tableIndex = GetTableIndex(hash);
            long firstMatching = hashTable[tableIndex];
            if (firstMatching < 0)
            {
                hashTable[tableIndex] = blockNumber;
                lastBlockTable[blockNumber] = blockNumber;
            }
            else
            {
                long lastMatching = lastBlockTable[firstMatching];
                if (nextBlockTable[lastMatching] != -1)
                {
                    return;
                }
                nextBlockTable[lastMatching] = blockNumber;
                lastBlockTable[firstMatching] = blockNumber;
            }
            lastBlockAdded = blockNumber;
        }

        public void AddAllBlocks()
        {
            AddAllBlocksThroughIndex(Source.Length);
        }

        public bool BlockContentsMatch(long block1, long toffset, IByteBuffer target)
        {
            //this sets up the positioning of the buffers
            //as well as testing the first byte
            Source.Position = block1 * blockSize;
            if (!Source.CanRead) return false;
            byte lb = Source.ReadByte();
            target.Position = toffset;
            if (!target.CanRead) return false;
            byte rb = target.ReadByte();

            if (lb != rb)
            {
                return false;
            }

            return BlockCompareWords(target);
        }

        //this doesn't appear to be used anywhere even though it is included in googles code
        public bool BlockCompareWords(IByteBuffer target)
        {
            //we already compared the first byte so moving on!
            int i = 1;

            long srcLength = Source.Length;
            long trgLength = target.Length;
            long offset1 = Source.Position;
            long offset2 = target.Position;

            while (i < blockSize)
            {
                if (i + offset1 >= srcLength || i + offset2 >= trgLength)
                {
                    return false;
                }
                byte lb = Source.ReadByte();
                byte rb = target.ReadByte();
                if (lb != rb)
                {
                    return false;
                }
                i++;
            }

            return true;
        }

        public long FirstMatchingBlock(ulong hash, long toffset, IByteBuffer target)
        {
            return SkipNonMatchingBlocks(hashTable[GetTableIndex(hash)], toffset, target);
        }


        public long NextMatchingBlock(long blockNumber, long toffset, IByteBuffer target)
        {
            if (blockNumber >= BlocksCount)
            {
                return -1;
            }

            return SkipNonMatchingBlocks(nextBlockTable[blockNumber], toffset, target);
        }

        public long SkipNonMatchingBlocks(long blockNumber, long toffset, IByteBuffer target)
        {
            int probes = 0;
            while ((blockNumber >= 0) && !BlockContentsMatch(blockNumber, toffset, target))
            {
                if (++probes > maxProbes)
                {
                    return -1;
                }
                blockNumber = nextBlockTable[blockNumber];
            }
            return blockNumber;
        }

        public long MatchingBytesToLeft(long start, long tstart, IByteBuffer target, long maxBytes)
        {
            long bytesFound = 0;
            long sindex = start;
            long tindex = tstart;

            while (bytesFound < maxBytes)
            {
                --sindex;
                --tindex;
                if (sindex < 0 || tindex < 0) break;
                //has to be done this way or a race condition will happen
                //if the sourcce and target are the same buffer
                Source.Position = sindex;
                byte lb = Source.ReadByte();
                target.Position = tindex;
                byte rb = target.ReadByte();
                if (lb != rb) break;
                ++bytesFound;
            }
            return bytesFound;
        }

        public long MatchingBytesToRight(long end, long tstart, IByteBuffer target, long maxBytes)
        {
            long sindex = end;
            long tindex = tstart;
            long bytesFound = 0;
            long srcLength = Source.Length;
            long trgLength = target.Length;
            Source.Position = end;
            target.Position = tstart;
            while (bytesFound < maxBytes)
            {
                if (sindex >= srcLength || tindex >= trgLength) break;
                if (!Source.CanRead) break;
                byte lb = Source.ReadByte();
                if (!target.CanRead) break;
                byte rb = target.ReadByte();
                if (lb != rb) break;
                ++tindex;
                ++sindex;
                ++bytesFound;
            }
            return bytesFound;
        }

        public bool TooManyMatches(ref int matchCounter)
        {
            ++matchCounter;
            return (matchCounter > maxMatchesToCheck);
        }

        public class Match
        {
            public void ReplaceIfBetterMatch(long csize, long sourcOffset, long targetOffset)
            {
                if (csize > Size)
                {
                    Size = csize;
                    SourceOffset = sourcOffset;
                    TargetOffset = targetOffset;
                }
            }

            public long Size { get; private set; } = 0;

            public long SourceOffset { get; private set; } = 0;

            public long TargetOffset { get; private set; } = 0;
        }
    }
}
