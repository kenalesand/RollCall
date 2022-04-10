namespace RollCall;

public static class Extensions
{
    private static readonly Dictionary<char, byte> CharToNibble = new()
    {
        { '0', 0 },
        { '1', 1 },
        { '2', 2 },
        { '3', 3 },
        { '4', 4 },
        { '5', 5 },
        { '6', 6 },
        { '7', 7 },
        { '8', 8 },
        { '9', 9 },
        { 'A', 10 },
        { 'B', 11 },
        { 'C', 12 },
        { 'D', 13 },
        { 'E', 14 },
        { 'F', 15 },
        { 'a', 10 },
        { 'b', 11 },
        { 'c', 12 },
        { 'd', 13 },
        { 'e', 14 },
        { 'f', 15 }
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
                bytes[i] = (byte)(CharToNibble[hex[i * 2]] * 16 + CharToNibble[hex[i * 2 + 1]]);
            }

            return bytes;
        }
        catch
        {
            throw new InvalidDataException("Ensure string only has hexadecimal characters and no whitespace");
        }
    }
}
