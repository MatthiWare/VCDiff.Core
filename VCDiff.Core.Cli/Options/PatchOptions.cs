using MatthiWare.CommandLine.Core.Attributes;

namespace MatthiWare.Compression.VCDiff.Cli.Options
{
    public class PatchOptions
    {
        [Required, Name("o", "old"), OptionOrder(1), Description("Path to the old version of the file")]
        public string OldFile { get; set; }

        [Required, Name("d", "delta"), OptionOrder(2), Description("Path of the delta patch file")]
        public string DeltaFile { get; set; }

        [Required, Name("n", "new"), OptionOrder(3), Description("Output path of the patched file")]
        public string NewFile { get; set; }
    }
}
