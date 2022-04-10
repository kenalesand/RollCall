using CommandLine;
using System.Diagnostics;
using RollCallCopy;
using RollCall;
using Serilog;

Parser.Default.ParseArguments<Arguments>(args).WithParsed(DoRollCallSend);

// Get batch of files (from args)
// - directory (or hierarchy)
// - filtered to a name (with wildcards?)
// While transfer of each batch (i.e. declared in a single roll) is sequenced, files
// within each batch have no order guarantee. If the sequencing of each file is
// important then send each one individually (i.e. single file per roll).

// Get sequence scope and sequence number (and optional retransmit sequence number)
// - pass in as parameters (S.E.P.)
// The sequence number is scoped to the sequence scope. This is the delivery order
// which will be observed at the wormhole exit.

// Create Roll
// pass in collection of:
// FileInfo, root directory, sequence scope, sequence number, retransmission number
// Comment line for sequence details or go JSON?
// Filename? seqscope_seqno.roll
// Save in the specified root directory

// Copy files to destination (wormhole entry)
// Copy the roll file (and wait for it to be taken by the wormhole? - option)
// Copy the file(s) to the destination (wait for the wormhole?)
// What to do on file collision? overwrite.

// Retransmission
// The next sequence number is assigned as normal (this is used to sequence the roll files)
// The retransmission number is set to the original sequence number.  The file details MAY
// be sent, and MUST be the same as the original roll - all files with same lengths and hashes.

// Example commands:
// rcc --source-root .  --file * --scope photos-2022 --transmit-number 27 --wormhole-entry \\wormhole\outbound\route66 --delete-originals

void DoRollCallSend(Arguments args)
{
    var orderAlphabetically = false;

    Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();

    var directory = new DirectoryInfo(args.SourceDir);

    var timer = new Stopwatch();
    if (args.ReportTimes)
    {
        timer.Start();
    }

    //rollcall.WriteLine($"# Roll for {directory.FullName} at {logtimestamp}");
    var fileCollection = directory.EnumerateFiles(args.SourceFile, new EnumerationOptions { RecurseSubdirectories = args.Recurse });
    if (orderAlphabetically)
    {
        fileCollection = fileCollection.OrderBy(f => f.FullName);
    }

    IRollWriter rollWriter = new RollWriterTextV01();
    FileInfo rollFile = null;
    try
    {
        rollFile = rollWriter.GenerateSequencedRoll(args.Scope, args.Sequence, directory, fileCollection);
        Log.Information("Created roll: {RollFile}", rollFile.FullName);
    }
    catch (Exception ex)
    {
        Log.Fatal(ex, "Failed to create roll");
        Environment.Exit(1);
    }

    if (! new RollReaderText().TryReadRoll(rollFile, out var roll))
    {
        Log.Fatal("Failed to parse created roll");
        Environment.Exit(1);
    }

    try
    {
        // Copy the roll file to the wormhole entrance
        File.Copy(rollFile.FullName, args.WormholeSubDir, overwrite: true);

        // Move the file(s) to the wormhole entrance
        string basepath = Path.GetDirectoryName(rollFile.FullName) ?? String.Empty;
        roll.Files.ForEach(fe => File.Move(Path.Combine(basepath, fe.RelativePath),
                                           Path.Combine(args.WormholeSubDir, fe.RelativePath),
                                           overwrite: true));
    }
    catch
    {
        // TODO: add error handling / retry. Poly might help here...
    }

    if (args.ReportTimes)
    {
        timer.Stop();
        Log.Information("Roll generated and files copied in {TimeTaken}", timer.Elapsed);
    }

    Log.CloseAndFlush();
}
