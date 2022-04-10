namespace RollCall;


public class Roll
{
    public FormatType FormatVersion { get; set; }
    public string Scope { get; set; } = string.Empty;
    public ulong Sequence { get; set; }
    public ulong Retransmit { get; set; }
    public List<FileEntry> Files { get; set; } = new List<FileEntry>();
}

public record FileEntry(string RelativePath, long Size, string HashSha256);

public enum FormatType : ushort
{
    TEXT_V01 = 0,
    JSON_V01 = 256,
    XML_V01 = 512,
}


public interface IRollWriter
{
    FileInfo GenerateSequencedRoll(string sequenceScope, ulong txSequence, DirectoryInfo filesRoot, IEnumerable<FileInfo> filesToAdd);

    FileInfo GenerateRetransmit(string sequenceScope, ulong txSequence, ulong retransmitOf, FileInfo rollPath, IEnumerable<FileInfo> filesToAdd);
    FileInfo GenerateRetransmit(ulong txSequence, FileInfo originalRoll);
    void GenerateAdhocRoll(string rollPrefix, FileInfo rollPath, IEnumerable<FileInfo> filesToAdd);
}

public interface IRollReader
{
    bool TryReadRoll(FileInfo file, out Roll roll);
}
