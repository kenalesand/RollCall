using CommandLine;
using System.Security.Cryptography;
using System.Diagnostics;

Parser.Default.ParseArguments<Arguments>(args).WithParsed(DoRollCall);
        
void DoRollCall(Arguments args)
{
    var directory = new DirectoryInfo(args.Root);

    if (args.Generate)
    {
        GenerateRollFile(directory, !args.TopLevelOnly, args.ManifestPrefix, args.NoHashes, args.ReportTimes, args.ForTesting);
    }

    if (args.Check)
    {
        RollCall(directory, args.ManifestPrefix, args.NoHashes, args.ReportTimes);
    }
}

/// <summary>
/// Create a roll of file information for the specified directory
/// </summary>
void GenerateRollFile(DirectoryInfo directory,
                      bool recurseDirectories,
                      string filePrefix,
                      bool noHashes,
                      bool reportTimes,
                      bool orderAlphabetically)
{
    var logtimestamp = DateTime.UtcNow.ToString("s");
    var tempRollCallFileName = Path.Combine(directory.FullName, $"{filePrefix}{logtimestamp}");
    using (var rollcall = new StreamWriter(tempRollCallFileName,
                                            new FileStreamOptions
                                            {
                                                Mode = FileMode.Create,
                                                Access = FileAccess.Write,
                                                Share = FileShare.None
                                            }))
    {
        var timer = new Stopwatch();
        if (reportTimes)
        {
            timer.Start();
        }

        rollcall.WriteLine($"# Roll for {directory.FullName} at {logtimestamp}");
        var fileCollection = directory.EnumerateFiles("*", new EnumerationOptions { RecurseSubdirectories = recurseDirectories })
                                        .AsParallel()
                                        .Where(fi => !fi.FullName.Equals(tempRollCallFileName, StringComparison.Ordinal));
        var fileEntriesQuery = noHashes ? fileCollection.Select(fi => MakeFileEntryNoHash(fi, directory)) 
                                                : fileCollection.Select(fi => MakeFileEntry(fi, directory));
        if (orderAlphabetically)
        {
            fileEntriesQuery = fileEntriesQuery.OrderBy(f => f.RelativePath);
        } 

        foreach (FileEntry fe in fileEntriesQuery)
        {
            rollcall.WriteLine($"{fe.RelativePath}|{fe.Size}|{fe.HashSha256}");
        }

        if (reportTimes)
        {
            timer.Stop();
            rollcall.WriteLine($"# Generated in {timer.Elapsed}");
            Console.WriteLine($"Roll generated in {timer.Elapsed}");
        }
    }

    // Rename the completed Rollcall file using the hash of its contents
    var rollHash = BitConverter.ToString(CalculateSha256(new FileInfo(tempRollCallFileName))).Replace("-", "");
    var rollcallFilename = Path.Combine(directory.FullName, $"{filePrefix}{rollHash}.log");
    File.Move(tempRollCallFileName, rollcallFilename, overwrite: true);
    Console.WriteLine($"Roll generated: {Path.GetRelativePath(directory.FullName, rollcallFilename)}");
}


void RollCall(DirectoryInfo directory, string filePrefix, bool noHashes, bool reportTimes)
{
    var timer = new Stopwatch();
    if (reportTimes)
    {
        timer.Start();
    }

    var rollcall = FindRollFile(directory, filePrefix, string.Empty);
    if (!RollIsValid(rollcall))
    {
        System.Console.Error.WriteLine($"Roll file is corrupt: {rollcall.FullName}");
        return;
    }

    Console.WriteLine($"Checking Roll : {Path.GetRelativePath(directory.FullName, rollcall.FullName)}");
    int badLines = 0;
    int files = 0;
    int missing = 0;
    int badLength = 0;
    int badHash = 0;
    int goodNoHash = 0;
    int goodHash = 0;

    // Parse line-by-line (or perhaps chunks, to allow parallel processing - next edition)
    using var roll = rollcall.OpenText();
    string? line;
    while ((line = roll.ReadLine()) != null)
    {
        if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#'))
        {
            // Comments (start with #) and blank lines are ignored.
            continue;
        }

        var elements = line.Split('|');
        if (elements.Length < 2 || elements.Length > 3)
        {
            // Bad line
            ++badLines;
            continue;
        }

        ++files;
        var filepath = elements[0].Trim();
        var finfo = new FileInfo(filepath);
        if (!finfo.Exists)
        {
            ++missing;
            System.Console.Error.WriteLine($"Missing file: {finfo.FullName}");
            continue;
        }

        if (!long.TryParse(elements[1].Trim(), out var length) || finfo.Length != length)
        {
            ++badLength;
            System.Console.Error.WriteLine($"File length mismatch: {finfo.FullName}");
            continue;
        }

        if (noHashes)
        {
            ++goodNoHash;
            continue;
        }

        if (elements.Length > 2 && !string.IsNullOrWhiteSpace(elements[2]))
        {
            byte[] expectedHash = elements[2].Trim().HexToByteArray();
            var hash = CalculateSha256(finfo);
            if (!hash.SequenceEqual(expectedHash))
            {
                ++badHash;
                System.Console.Error.WriteLine($"File hash mismatch: {finfo.FullName}");
            }
            else
            {
                ++goodHash;
            }
        }
        else
        {
            ++goodNoHash;
        }
    }

    if (reportTimes)
    {
        timer.Stop();
        Console.WriteLine($"Check completed in {timer.Elapsed}");
    }

    Console.WriteLine($"Summary files: {files}");
    Console.WriteLine($"        good : hashed={goodHash} unhashed={goodNoHash}");
    if (missing > 0 || badLength > 0 || badHash > 0)
    {
        Console.WriteLine($"        bad  : missing={missing} badLength={badLength} badHash={badHash}");
    }
    if (badLines > 0)
    {
        Console.WriteLine($"        ugly : {badLines} strange lines in roll file");
    }
}


FileInfo FindRollFile(DirectoryInfo directory, string prefix, string overrideName)
{
    var candidates = directory.EnumerateFiles($"{prefix}*");
    if (candidates.Any())
    {
        var latest = candidates.OrderBy(fi => fi.LastWriteTimeUtc).Last();
        return latest;
    }

    throw new Exception("No rollcall file found");
}

/// Yes, one could create a whole new file, but its hash (and hence the name) will be different.
/// Only want to ensure the file was transported free of errors.
bool RollIsValid(FileInfo roll)
{
    try
    {
        // The SHA256 hash is appended to the filename. 256 bits = 32 bytes = 64 hex chars.
        byte[] expectedSha256 = Path.GetFileNameWithoutExtension(roll.FullName)[^64..].HexToByteArray();

        using var hash = SHA256.Create();
        using var currentStream = roll.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
        var h = hash.ComputeHash(currentStream);
        return expectedSha256.SequenceEqual(h);
    }
    catch
    {
        return false;
    }
}


async Task<byte[]> CalculateSha256Async(FileInfo finfo)
{
    try
    {
        using var hash = SHA256.Create();
        using var currentStream = finfo.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
        var h = await hash.ComputeHashAsync(currentStream);
        return h;
    }
    catch (Exception e)
    {
        var message = $"*** Failed to create SHA256 for {finfo.FullName}: {e.Message}";
        Console.Error.WriteLine(message);
        return Array.Empty<byte>();
    }
}

byte[] CalculateSha256(FileInfo finfo)
{
    try
    {
        using var hash = SHA256.Create();
        using var currentStream = finfo.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
        var h = hash.ComputeHash(currentStream);
        return h;
    }
    catch (Exception e)
    {
        var message = $"*** Failed to create SHA256 for {finfo.FullName}: {e.Message}";
        Console.Error.WriteLine(message);
        return Array.Empty<byte>();
    }
}

FileEntry MakeFileEntryNoHash(FileInfo finfo, DirectoryInfo root, bool withHash = true)
{
    try
    {
        return new FileEntry(Path.GetRelativePath(root.FullName, finfo.FullName),
                             finfo.Length,
                             string.Empty);
    }
    catch (Exception e)
    {
        var message = $"*** Failed to process {finfo.FullName}: {e.Message}";
        Console.Error.WriteLine(message);
        return new FileEntry(Path.GetRelativePath(Environment.CurrentDirectory, finfo.FullName), finfo.Length, message);
    }
}

FileEntry MakeFileEntry(FileInfo finfo, DirectoryInfo root)
{
    try
    {
        var hashSha256 = BitConverter.ToString(CalculateSha256(finfo)).Replace("-", "");
        return new FileEntry(Path.GetRelativePath(root.FullName, finfo.FullName),
                             finfo.Length,
                             hashSha256);
    }
    catch (Exception e)
    {
        var message = $"*** Failed to process {finfo.FullName}: {e.Message}";
        Console.Error.WriteLine(message);
        return new FileEntry(Path.GetRelativePath(Environment.CurrentDirectory, finfo.FullName), finfo.Length, message);
    }
}

async Task<FileEntry> MakeFileEntryAsync(FileInfo finfo)
{
    try
    {
        var hashSha256 = BitConverter.ToString(await CalculateSha256Async(finfo)).Replace("-", "");
        return new FileEntry(Path.GetRelativePath(Environment.CurrentDirectory, finfo.FullName), finfo.Length, hashSha256);
    }
    catch (Exception e)
    {
        var message = $"*** Failed to process {finfo.FullName}: {e.Message}";
        Console.Error.WriteLine(message);
        return new FileEntry(Path.GetRelativePath(Environment.CurrentDirectory, finfo.FullName), finfo.Length, message);
    }
}

record FileEntry(string RelativePath, long Size, string HashSha256);


public class Arguments
{
    [Option('g', "generate-roll", Group = "Operation", HelpText = "Creates the roll file.")]
    public bool Generate {get; set;}

    [Option('c', "check-roll", Group = "Operation", HelpText = "Compares the roll file with the files present")]
    public bool Check {get; set;}

    [Option('r', "root", Default = ".", HelpText = "The folder containing files to examine, and where the rollcall file is created")]
    public string Root {get; set;} = ".";

    [Option("no-recurse", HelpText = "Do not include subdirectories")]
    public bool TopLevelOnly {get; set;}

    [Option("manifest-prefix", Default = "RollCall-", HelpText = "The rollcall file has this prefix followed by the hash of its contents and has a .log extension")]
    public string ManifestPrefix {get; set;} = "RollCall-";

    [Option("with-stats", HelpText = "Report processing times in the log and console")]
    public bool ReportTimes {get; set;}

    [Option('q', "quick", HelpText = "Do not generate hashes and do not check hashes. Just check presence and size.")]
    public bool NoHashes {get; set;}

    [Option("for-testing", HelpText = "Enable features which help testing but hurt performance, e.g. order the files alphabetically")]
    public bool ForTesting {get; set;}
}

public static class Extensions
{
    private static readonly Dictionary<char, byte> CharToNibble = new()
    {
        {'0', 0}, {'1', 1}, {'2', 2}, {'3', 3}, {'4', 4}, {'5', 5}, {'6', 6}, {'7', 7}, {'8', 8}, {'9', 9},
        {'A', 10}, {'B', 11}, {'C', 12}, {'D', 13}, {'E', 14}, {'F', 15},
        {'a', 10}, {'b', 11}, {'c', 12}, {'d', 13}, {'e', 14}, {'f', 15}
    };

    public static byte[] HexToByteArray(this string hex)
    {
        try
        {
            if (hex.Length % 2 != 0)
            {
                hex = "0" + hex;
            }

            var size = hex.Length / 2;
            var bytes = new byte[size];

            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = (byte)(CharToNibble[hex[i*2]] * 16 + CharToNibble[hex[i*2+1]]);
            }

            return bytes;
        }
        catch
        {
            throw new InvalidDataException("Ensure string only has hexadecimal characters and no whitespace");
        }
    }
}
