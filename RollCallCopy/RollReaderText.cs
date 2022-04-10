namespace RollCall;

using System.IO;
using System.Security.Cryptography;
using Serilog;

public class RollReaderText : IRollReader
{
    public bool TryReadRoll(FileInfo rollFile, out Roll rollx)
    {
        rollx = null;

        const int checkedFilePosition = 66;
        try
        {
            var RollFile = rollFile.FullName;

            var options = new FileStreamOptions { Mode = FileMode.Open, Access = FileAccess.Read, Share = FileShare.Read };
            using var rollStream = new StreamReader(rollFile.FullName, options);

            // Read the hash and check the roll's integrity.
            var hashInFile = rollStream.ReadLine()![2..];

            // Mixing buffered and unbuffered I/O is a bit tricky. The current position reflects a
            // readahead into a buffer held in the StreamReader. We're going to use the underlying
            // stream in the hash calculation. Save the current position and restore it after this
            // operation.
            var bufferedPos = rollStream.BaseStream.Position;
            rollStream.BaseStream.Position = checkedFilePosition;
            var expectedSha256 = hashInFile.HexToByteArray();
            using var hash = SHA256.Create();
            var currentHash = hash.ComputeHash(rollStream.BaseStream);
            if (!expectedSha256.SequenceEqual(currentHash))
            {
                throw new CryptographicException();
            }

            // Restore the underlying stream's position to match the buffered data.
            rollStream.BaseStream.Position = bufferedPos;
            var formatVersion = Enum.Parse(typeof (FormatType), rollStream.ReadLine()![2..].Trim());
            switch (formatVersion)
            {
                case FormatType.TEXT_V01:
                    break;
                default:
                    throw new FormatException("Format/Version not supported");
            }

            var Scope = rollStream.ReadLine()![2..];
            var Sequence = ulong.Parse(rollStream.ReadLine()![2..]);
            var Retransmit = ulong.Parse(rollStream.ReadLine()![2..]);

            var files = new List<FileEntry>();
            string? line;
            while ((line = rollStream.ReadLine()) != null)
            {
                if (TryParseFileEntry(line, out var fe))
                {
                    files.Add(fe);
                }
            }

            rollx = new Roll { FormatVersion = FormatType.TEXT_V01, Scope = Scope, Sequence = Sequence, Retransmit = Retransmit, Files = files };
            return true;
        }
        catch (CryptographicException)
        {
            Log.Error($"Hash does not match: {rollFile.FullName}");
        }
        catch (FormatException)
        {
            Log.Error($"Format/Version is not supported");
        }
        catch
        {
            Log.Error($"Not a valid roll: {rollFile.FullName}");
        }

        return false;
    }

    public static bool TryParseFileEntry(string? line, out FileEntry entry)
    {
        entry = null;

        if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#'))
        {
            // Comments (start with #) and blank lines are ignored.
            return false;
        }

        var elements = line.Split('|');
        if (elements.Length < 2 || elements.Length > 3)
        {
            // Bad line
            Log.Warning($"Bad file record line");
            //++badLines;
            return false;
        }

        var filepath = elements[0].Trim();

        if (!long.TryParse(elements[1].Trim(), out var length) || length < 0 || length > 100E9)
        {
            Log.Warning($"Could not parse the file length for {filepath}");
            return false;
        }

        var hash = string.Empty;
        if (elements.Length > 2 && !string.IsNullOrWhiteSpace(elements[2]))
        {
            hash = elements[2].Trim();
        }

        entry = new FileEntry(filepath, length, hash);
        return true;
    }
}
