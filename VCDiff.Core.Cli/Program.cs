using System;
using System.IO;
using MatthiWare.Compression.VCDiff.Decoders;
using MatthiWare.Compression.VCDiff.Encoders;
using MatthiWare.Compression.VCDiff.Includes;

namespace VCDiff.Core.Cli
{
    class Program
    {
        static int Main(string[] args)
        {
            string type = args[0]; // -e -d
            var oldPath = args[1];
            var newPath = args[2];
            var outPath = args[3];

            if (type != "-e" && type != "-d") throw new ArgumentException("Invalid parameters, use -e or -d");

            var result = VCDiffResult.SUCCESS;

            if (type == "-e")
            {
                var size = int.Parse(args[4]);

                using (var sold = File.OpenRead(oldPath)) // old file
                using (var snew = File.OpenRead(newPath)) // new file
                using (var sout = File.OpenWrite(outPath)) // delta file
                    result = Encode(sold, snew, sout, size);
            }
            else if (type == "-d")
            {
                using (var sold = File.OpenRead(oldPath)) // old file
                using (var snew = File.OpenRead(newPath)) // delta file
                using (var sout = File.OpenWrite(outPath)) // out file
                    result = Decode(sold, snew, sout);
            }

            switch (result)
            {
                case VCDiffResult.SUCCESS:
                default:
                    return 0;
                case VCDiffResult.ERROR:
                    return -1;
                case VCDiffResult.EOD:
                    return -2;
            }
        }

        private static VCDiffResult Encode(Stream sold, Stream snew, Stream sout, int bufferSize)
        {
            VCCoder encoder = new VCCoder(sold, snew, sout, bufferSize);

            return encoder.Encode();
        }

        private static VCDiffResult Decode(Stream sold, Stream sdelta, Stream sout)
        {
            VCDecoder decoder = new VCDecoder(sold, sdelta, sout);

            var result = decoder.Start();

            if (result != VCDiffResult.SUCCESS)
                return result;

            return decoder.Decode(out _);
        }
    }
}
