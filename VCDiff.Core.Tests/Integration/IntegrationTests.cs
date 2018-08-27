using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MatthiWare.Compression.VCDiff.Decoders;
using MatthiWare.Compression.VCDiff.Encoders;
using MatthiWare.Compression.VCDiff.Includes;
using Xunit;

namespace VCDiff.Core.Tests.Integration
{

    public class IntegrationTests
    {

        [Fact]
        public void TestEncodeAndDecodeShouldBeTheSame()
        {
            int size = 50 * 1024 * 1024; // 50 MB

            byte[] oldData = CreateRandomByteArray(size);
            byte[] newData = new byte[size];

            oldData.CopyTo(newData, 0);

            AddRandomPiecesIn(oldData);

            var sOld = new MemoryStream(oldData);
            var sNew = new MemoryStream(newData);
            var sDelta = new MemoryStream(new byte[size], true);

            var coder = new VCCoder(sOld, sNew, sDelta);
            Assert.Equal(VCDiffResult.SUCCESS, coder.Encode());

            sDelta.SetLength(sDelta.Position);
            sDelta.Position = 0;
            sOld.Position = 0;
            sNew.Position = 0;

            var sPatched = new MemoryStream(new byte[size], true);

            var decoder = new VCDecoder(sOld, sDelta, sPatched);
            Assert.Equal(VCDiffResult.SUCCESS, decoder.Start());
            Assert.Equal(VCDiffResult.SUCCESS, decoder.Decode(out _));

            Assert.Equal(sNew.ToArray(), sPatched.ToArray());
        }

        private Random random = new Random(DateTime.Now.GetHashCode());

        private byte[] CreateRandomByteArray(int size)
        {
            byte[] buffer = new byte[size];

            random.NextBytes(buffer);

            return buffer;
        }

        private void AddRandomPiecesIn(byte[] input)
        {
            int size = 1024 * 100; // 100 KB

            for (int i = 0; i < 100; i++)
            {
                byte[] difference = CreateRandomByteArray(size);

                int index = random.Next(0, input.Length - size - 1);

                for (int x = 0; x < size; x++)
                {
                    input[x + index] = difference[x];
                }
            }
        }

    }
}
