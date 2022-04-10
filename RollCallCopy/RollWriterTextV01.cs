namespace RollCall;
using System.Security.Cryptography;
using Serilog;

public class RollWriterTextV01 : IRollWriter
{
    public FileInfo GenerateSequencedRoll(string sequenceScope, ulong txSequence, DirectoryInfo filesRoot, IEnumerable<FileInfo> filesToAdd)
    {
        var logtimestamp = DateTime.UtcNow.ToString("s");
        var tempRollCallFileName = Path.Combine(filesRoot.FullName, $"{sequenceScope}-{txSequence}.roll");
        var rollFile = new FileInfo(tempRollCallFileName);

        var options = new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write, Share = FileShare.None };
        using (var rollcall = new StreamWriter(rollFile.FullName, options))
        {
            rollcall.WriteLine("# 0000000000000000000000000000000000000000000000000000000000000000"); // 2 + 64 = 66
            rollcall.WriteLine($"# {FormatType.TEXT_V01}");
            rollcall.WriteLine($"# {sequenceScope}");
            rollcall.WriteLine($"# {txSequence}");
            rollcall.WriteLine($"# 0"); // not a retransmit

            foreach (FileEntry fe in filesToAdd.Select(fi => MakeFileEntry(fi, filesRoot)))
            {
                if (string.IsNullOrWhiteSpace(fe.HashSha256))
                {
                    Log.Error("No hash generated for {RelativePath}", fe.RelativePath);
                    // TODO: do we want to collect files we can't hash? Probably not as it was possibly open (e.g. this file)
                    continue;
                }
                rollcall.WriteLine($"{fe.RelativePath}|{fe.Size}|{fe.HashSha256}");
            }
        }

        // Calculate the hash over the range [66..] and update the start of the file
        var hashSha256 = BitConverter.ToString(CalculateSha256(rollFile, offset: 66)).Replace("-", "");

        options = new FileStreamOptions { Mode = FileMode.Open, Access = FileAccess.ReadWrite, Share = FileShare.None };
        using (var rollcall = new StreamWriter(rollFile.FullName, options))
        {
            rollcall.Write($"# {hashSha256}"); // 2 + 64 = 66
        }

        return rollFile;
    }

    private static byte[] CalculateSha256(FileInfo finfo, long offset = 0)
    {
        try
        {
            using var hash = SHA256.Create();
            using var currentStream = finfo.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
            currentStream.Position = offset;
            var h = hash.ComputeHash(currentStream);
            return h;
        }
        catch (Exception e)
        {
            Log.Error("Failed to create SHA256 for {FileName}: {Message}", finfo.FullName, e.Message);
            return Array.Empty<byte>();
        }
    }

    private static FileEntry MakeFileEntryNoHash(FileInfo finfo, DirectoryInfo root, bool withHash = true)
    {
        try
        {
            return new FileEntry(Path.GetRelativePath(root.FullName, finfo.FullName),
                                 finfo.Length,
                                 string.Empty);
        }
        catch (Exception e)
        {
            Log.Error("Failed to process {FileName}: {Message}", finfo.FullName, e.Message);
            return new FileEntry(Path.GetRelativePath(Environment.CurrentDirectory, finfo.FullName),
                                 finfo.Length,
                                 $"*** Failed to process: {e.Message}");
        }
    }

    private static FileEntry MakeFileEntry(FileInfo finfo, DirectoryInfo root)
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
            Log.Error("Failed to process {FileName}: {Message}", finfo.FullName, e.Message);
            return new FileEntry(Path.GetRelativePath(Environment.CurrentDirectory, finfo.FullName),
                                 finfo.Length,
                                 $"*** Failed to process: {e.Message}");
        }
    }

    public FileInfo GenerateRetransmit(string sequenceScope, ulong txSequence, ulong retransmitOf, FileInfo rollPath, IEnumerable<FileInfo> filesToAdd)
    {
        throw new NotImplementedException();
    }

    public FileInfo GenerateRetransmit(ulong txSequence, FileInfo originalRoll)
    {
        throw new NotImplementedException();
    }

    public void GenerateAdhocRoll(string rollPrefix, FileInfo rollPath, IEnumerable<FileInfo> filesToAdd)
    {
        throw new NotImplementedException();
    }
}
