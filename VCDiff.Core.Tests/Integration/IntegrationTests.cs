using MatthiWare.Compression.VCDiff.Decoders;
using MatthiWare.Compression.VCDiff.Encoders;
using MatthiWare.Compression.VCDiff.Includes;
using NUnit.Framework;
using System;
using System.IO;

namespace VCDiff.Core.Tests.Integration
{
    [TestFixture]
    public class IntegrationTests
    {

        [Test]
        public void TestEncodeAndDecodeShouldBeTheSame()
        {
            int size = 20 * 1024 * 1024; // 20 MB

            byte[] oldData = CreateRandomByteArray(size);
            byte[] newData = new byte[size];

            oldData.CopyTo(newData, 0);

            AddRandomPiecesIn(oldData);

            var sOld = new MemoryStream(oldData);
            var sNew = new MemoryStream(newData);
            var sDelta = new MemoryStream(new byte[size], true);

            var coder = new VCCoder(sOld, sNew, sDelta);
            Assert.AreEqual(VCDiffResult.Succes, coder.Encode());

            TestContext.Out.WriteLine($"Delta is {sDelta.Position / 1024 / 1024} MB's");

            sDelta.SetLength(sDelta.Position);
            sDelta.Position = 0;
            sOld.Position = 0;
            sNew.Position = 0;

            var sPatched = new MemoryStream(new byte[size], true);

            var decoder = new VCDecoder(sOld, sDelta, sPatched);
            Assert.AreEqual(VCDiffResult.Succes, decoder.Start());
            Assert.AreEqual(VCDiffResult.Succes, decoder.Decode(out long bytesWritten));

            TestContext.Out.WriteLine($"Written {bytesWritten / 1024 / 1024} MB's");

            Assert.AreEqual(sNew.ToArray(), sPatched.ToArray());
        }

        private static readonly Random random = new Random(DateTime.Now.GetHashCode());

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
