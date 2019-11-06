using MatthiWare.CommandLine.Core.Attributes;

namespace MatthiWare.Compression.VCDiff.Cli.Options
{
    public class ProgramOptions
    {
        [Required(false), Name("v", "verbose"), DefaultValue(false), Description("Verbose output")]
        public bool Verbose { get; set; }
    }
}
