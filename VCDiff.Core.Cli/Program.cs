using System;
using System.IO;
using MatthiWare.CommandLine;
using MatthiWare.Compression.VCDiff.Cli.Options;
using MatthiWare.Compression.VCDiff.Decoders;
using MatthiWare.Compression.VCDiff.Encoders;
using MatthiWare.Compression.VCDiff.Includes;

namespace MatthiWare.Compression.VCDiff.Cli
{
    class Program
    {
        static int Main(string[] args)
        {
            var cliParserOptions = new CommandLineParserOptions
            {
                AppName = "VCDiff.Core.Cli"
            };

            var result = VCDiffResult.ERROR;

            var parser = new CommandLineParser();

            parser.AddCommand<CreateOptions>()
                .Name("create")
                .Required(false)
                .Description("Creates a delta patch from the given input")
                .OnExecuting((o, opt) => result = Create(opt));

            parser.AddCommand<PatchOptions>()
                .Name("patch")
                .Required(false)
                .Description("Appplies the give patch to the file")
                .OnExecuting((o, opt) => result = Patch(opt));

            var parserResult = parser.Parse(args);

            if (parserResult.HasErrors && !parserResult.HelpRequested)
                result = VCDiffResult.ERROR;

            switch (result)
            {
                case VCDiffResult.SUCCESS:
                default:
                    return 0;
                case VCDiffResult.ERROR:
                    Console.Error.WriteLine("Unexpected error occured");

                    return -1;
                case VCDiffResult.EOD:
                    Console.Error.WriteLine("Unexpected end of data");

                    return -2;
            }
        }

        private static VCDiffResult Patch(PatchOptions opt)
        {
            using (var sold = File.OpenRead(opt.OldFile)) // old file
            using (var snew = File.OpenRead(opt.DeltaFile)) // delta file
            using (var sout = File.OpenWrite(opt.NewFile)) // out file
                return Decode(sold, snew, sout);
        }

        private static VCDiffResult Create(CreateOptions opt)
        {
            using (var sold = File.OpenRead(opt.OldFile)) // old file
            using (var snew = File.OpenRead(opt.NewFile)) // new file
            using (var sout = File.OpenWrite(opt.DeltaFile)) // delta file
                return Encode(sold, snew, sout, opt.BufferSize);
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
