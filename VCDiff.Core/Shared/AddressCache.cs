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

namespace MatthiWare.Compression.VCDiff.Shared
{
    /// <summary>
    /// The address cache implementation as described in the RFC doc.
    /// </summary>
    public class AddressCache
    {

        private const byte DefaultNearCacheSize = 4;
        private const byte DefaultSameCacheSize = 3;
        private long[] nearCache;
        private long[] sameCache;
        private int nextSlot;

        public byte NearSize { get; }

        public byte SameSize { get; }

        public byte FirstNear => (byte)VCDiffModes.FIRST;

        public byte FirstSame => (byte)(VCDiffModes.FIRST + NearSize);

        public byte Last => (byte)(FirstSame + SameSize - 1);

        public static byte DefaultLast => (byte)(VCDiffModes.FIRST + DefaultNearCacheSize + DefaultSameCacheSize - 1);

        public AddressCache(byte nearSize, byte sameSize)
        {
            this.SameSize = sameSize;
            this.NearSize = nearSize;
            nearCache = new long[nearSize];
            sameCache = new long[sameSize * 256];
            nextSlot = 0;
        }

        public AddressCache()
        {
            SameSize = DefaultSameCacheSize;
            NearSize = DefaultNearCacheSize;
            nearCache = new long[NearSize];
            sameCache = new long[SameSize * 256];
            nextSlot = 0;
        }

        static bool IsSelfMode(byte mode)
        {
            return mode == (byte)VCDiffModes.SELF;
        }

        static bool IsHereMode(byte mode)
        {
            return mode == (byte)VCDiffModes.HERE;
        }

        bool IsNearMode(byte mode)
        {
            return (mode >= FirstNear) && (mode < FirstSame);
        }

        bool IsSameMode(byte mode)
        {
            return (mode >= FirstSame) && (mode <= Last);
        }

        static long DecodeSelfAddress(long encoded)
        {
            return encoded;
        }

        static long DecodeHereAddress(long encoded, long here)
        {
            return here - encoded;
        }

        long DecodeNearAddress(byte mode, long encoded)
        {
            return NearAddress(mode - FirstNear) + encoded;
        }

        long DecodeSameAddress(byte mode, byte encoded)
        {
            return SameAddress(((mode - FirstSame) * 256) + encoded);
        }

        public bool WriteAddressAsVarint(byte mode)
        {
            return !IsSameMode(mode);
        }

        long NearAddress(int pos)
        {
            return nearCache[pos];
        }

        long SameAddress(int pos)
        {
            return sameCache[pos];
        }

        void UpdateCache(long address)
        {
            if (NearSize > 0)
            {
                nearCache[nextSlot] = address;
                nextSlot = (nextSlot + 1) % NearSize;
            }
            if (SameSize > 0)
            {
                sameCache[(int)(address % (SameSize * 256))] = address;
            }
        }

        public byte EncodeAddress(long address, long here, out long encoded)
        {
            if (address < 0)
            {
                encoded = 0;
                return (byte)0;
            }
            if (address >= here)
            {
                encoded = 0;
                return (byte)0;
            }

            if (SameSize > 0)
            {
                int pos = (int)(address % (SameSize * 256));
                if (SameAddress(pos) == address)
                {
                    UpdateCache(address);
                    encoded = (pos % 256);
                    return (byte)(FirstSame + (pos / 256));
                }
            }

            byte bestMode = (byte)VCDiffModes.SELF;
            long bestEncoded = address;

            long hereEncoded = here - address;
            if (hereEncoded < bestEncoded)
            {
                bestMode = (byte)VCDiffModes.HERE;
                bestEncoded = hereEncoded;
            }

            for (int i = 0; i < NearSize; ++i)
            {
                long nearEncoded = address - NearAddress(i);
                if ((nearEncoded >= 0) && (nearEncoded < bestEncoded))
                {
                    bestMode = (byte)(FirstNear + i);
                    bestEncoded = nearEncoded;
                }
            }

            UpdateCache(address);
            encoded = bestEncoded;
            return bestMode;
        }

        bool IsDecodedAddressValid(long decoded, long here)
        {
            if (decoded < 0)
            {
                return false;
            }
            else if (decoded >= here)
            {
                return false;
            }

            return true;
        }

        public long DecodeAddress(long here, byte mode, ByteBuffer sin)
        {
            long start = sin.Position;
            if (here < 0)
            {
                return (int)VCDiffResult.Error;
            }

            if (!sin.CanRead)
            {
                return (int)VCDiffResult.EOD;
            }

            long decoded = 0;
            if (IsSameMode(mode))
            {
                byte encoded = sin.ReadByte();
                decoded = DecodeSameAddress(mode, encoded);
            }
            else
            {
                int encoded = VarIntBE.ParseInt32(sin);

                switch (encoded)
                {
                    case (int)VCDiffResult.Error:
                        return encoded;
                    case (int)VCDiffResult.EOD:
                        sin.Position = start;
                        return encoded;
                    default:
                        break;
                }

                if (IsSelfMode(mode))
                {
                    decoded = DecodeSelfAddress(encoded);
                }
                else if (IsHereMode(mode))
                {
                    decoded = DecodeHereAddress(encoded, here);
                }
                else if (IsNearMode(mode))
                {
                    decoded = DecodeNearAddress(mode, encoded);
                }
                else
                {
                    return (int)VCDiffResult.Error;
                }
            }

            if (!IsDecodedAddressValid(decoded, here))
            {
                return (int)VCDiffResult.Error;
            }
            UpdateCache(decoded);
            return decoded;
        }
    }
}
