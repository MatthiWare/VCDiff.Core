using MatthiWare.CommandLine.Core.Attributes;

namespace MatthiWare.Compression.VCDiff.Cli.Options
{
    public class PatchOptions
    {
        [Required, Name("o", "old"), Description("Path to the old version of the file")]
        public string OldFile { get; set; }

        [Required, Name("n", "new"), Description("Output path of the patched file")]
        public string NewFile { get; set; }

        [Required, Name("d", "delta"), Description("Path of the delta patch file")]
        public string DeltaFile { get; set; }
    }
}
