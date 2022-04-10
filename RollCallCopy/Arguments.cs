namespace RollCallCopy;
using CommandLine;


public class Arguments
{
    [Option('r', "source-root", Required = true, HelpText = "The folder containing files to transfer")]
    public string SourceDir { get; set; } = string.Empty;

    [Option("file", Required = true, HelpText = "File(s) to transfer. Wildcards are accepted. May be a path relative to source-root.")]
    public string SourceFile { get; set; } = string.Empty;

    [Option("recurse", HelpText = "Parse subdirectories for files to transfer")]
    public bool Recurse { get; set; } 

    [Option("delete-originals", HelpText = "Delete original files (i.e. move not copy)")]
    public bool Move { get; set; }

    [Option("scope", Required = true, HelpText = "Scope identifier for a sequence of file transmissions.")]
    public string Scope { get; set; } = string.Empty;

    [Option("transmit-number", Required = true, HelpText = "The transmit number for this transfer, scoped to Scope. Starts at 1. Never reused. Increment by 1. Set to zero to indicate no ordering.")]
    public ulong Sequence { get; set; }

    [Option("resend", Default = 0, HelpText = "Normally set to 0. Otherwise set the original transmit number this transfer replicates.")]
    public ulong Retransmit { get; set; }

    [Option("wormhole-entry", Required = true, HelpText = "Path the folder which is the entry to the wormhole")]
    public string WormholeEntry { get; set; } = string.Empty;

    [Option("dest-subdir", HelpText = "Subdirectory under the wormhole entry path. Will be created. Also where the rollcall file is created at the wormhole exit")]
    public string WormholeSubDir { get; set; } = string.Empty;

    [Option("with-stats", HelpText = "Report processing times in the log and console")]
    public bool ReportTimes { get; set; }





    //[Option('c', "check-roll", Group = "Operation", HelpText = "Compares the roll file with the files present")]
    //public bool Check { get; set; }

    //[Option('r', "root", Default = ".", HelpText = "The folder containing files to examine, and where the rollcall file is created")]
    //public string Root { get; set; } = ".";

    //[Option("no-recurse", HelpText = "Do not include subdirectories")]
    //public bool TopLevelOnly { get; set; }


    //[Option('q', "quick", HelpText = "Do not generate hashes and do not check hashes. Just check presence and size.")]
    //public bool NoHashes { get; set; }

    //[Option("for-testing", HelpText = "Enable features which help testing but hurt performance, e.g. order the files alphabetically")]
    //public bool ForTesting { get; set; }
}
