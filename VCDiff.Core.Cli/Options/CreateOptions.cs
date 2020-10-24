using MatthiWare.CommandLine.Core.Attributes;

namespace MatthiWare.Compression.VCDiff.Cli.Options
{
    public class CreateOptions
    {
        [Required, Name("o", "old"), OptionOrder(1), Description("Path to the old version of the file")]
        public string OldFile { get; set; }

        [Required, Name("n", "new"), OptionOrder(2), Description("Path to the new version of the file")]
        public string NewFile { get; set; }

        [Required, Name("d", "delta"), OptionOrder(2), Description("Output path of the delta patch file")]
        public string DeltaFile { get; set; }

        [Name("b", "buffer"), Description("Optional buffer size to use"), DefaultValue(1)]
        public int BufferSize { get; set; }
    }
}
