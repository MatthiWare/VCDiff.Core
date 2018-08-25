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

namespace MatthiWare.Compression.VCDiff.Encoders
{
    public class RollingHash
    {
        const ulong kMult = 257;
        const ulong kBase = (1 << 23);

        private readonly int size = 0;
        private readonly ulong[] removeTable;
        private readonly ulong multiplier;

        /// <summary>
        /// Rolling Hash Constructor
        /// </summary>
        /// <param name="size">block size</param>
        public RollingHash(int size)
        {
            this.size = size;
            removeTable = new ulong[256];
            multiplier = 1;
            for (int i = 0; i < size - 1; ++i)
            {
                multiplier = ModBase(multiplier * kMult);
            }
            ulong byteTimes = 0;
            for (int i = 0; i < 256; ++i)
            {
                removeTable[i] = FindModBaseInverse(byteTimes);
                byteTimes = ModBase(byteTimes + multiplier);
            }
        }

        /// <summary>
        /// Does the MODULO operation
        /// </summary>
        /// <param name="op"></param>
        /// <returns></returns>
        static ulong ModBase(ulong op)
        {
            return op & (kBase - 1);
        }

        /// <summary>
        /// Finds the inverse of the operation
        /// </summary>
        /// <param name="op"></param>
        /// <returns></returns>
        static ulong FindModBaseInverse(ulong op)
        {
            return ModBase((ulong)0 - op);
        }

        /// <summary>
        /// Performs the next hash encoding step
        /// for creating the hash
        /// </summary>
        /// <param name="partialHash"></param>
        /// <param name="next"></param>
        /// <returns></returns>
        static ulong HashStep(ulong partialHash, byte next)
        {
            return ModBase((partialHash * kMult) + next);
        }

        /// <summary>
        /// only hash the first two bytes if any
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        static ulong HashFirstTwoBytes(byte[] bytes)
        {
            if (bytes.Length == 0) return 1;
            if (bytes.Length == 1) return (bytes[0] * kMult);

            return (bytes[0] * kMult) + bytes[1];
        }

        /// <summary>
        /// Generate a new hash from the bytes
        /// </summary>
        /// <param name="bytes">The bytes to generate the hash for</param>
        /// <returns></returns>
        public ulong Hash(byte[] bytes)
        {
            ulong h = HashFirstTwoBytes(bytes);
            for (int i = 2; i < bytes.Length; i++)
            {
                h = HashStep(h, bytes[i]);
            }
            return h;
        }

        /// <summary>
        /// Rolling update for the hash
        /// First byte must be the first bytee that was used in the data
        /// that was last encoded
        /// new byte is the first byte position + Size
        /// </summary>
        /// <param name="oldHash">the original hash</param>
        /// <param name="firstByte">the original byte of the data for the first hash</param>
        /// <param name="newByte">the first byte of the new data to hash</param>
        /// <returns></returns>
        public ulong UpdateHash(ulong oldHash, byte firstByte, byte newByte)
        {
            ulong partial = RemoveFirstByte(oldHash, firstByte);
            return HashStep(partial, newByte);
        }

        /// <summary>
        /// Removes the first byte from the hash
        /// </summary>
        /// <param name="hash">hash</param>
        /// <param name="first">first byte</param>
        /// <returns></returns>
        ulong RemoveFirstByte(ulong hash, byte first)
        {
            return ModBase(hash + removeTable[first]);
        }
    }
}
