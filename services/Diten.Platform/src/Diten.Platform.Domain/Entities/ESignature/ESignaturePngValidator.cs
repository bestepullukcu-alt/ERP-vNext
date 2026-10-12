using System.Buffers.Binary;

namespace Diten.Platform.Domain.Entities.ESignature;

/// <summary>
/// MOD-0022 — byte-level PNG checks for a drawn signature (pack §8 "Images", V8–V10). Platform has no image library, so
/// nothing is decoded: the 8-byte signature, the first chunk being a 13-byte IHDR, width/height ≤ 2000 × 800, and the size
/// limit. The browser produces the PNG (canvas); the server only refuses what is plainly not one.
/// </summary>
public static class ESignaturePngValidator
{
    public const int MaxBytes = 1_048_576;
    public const int MaxWidth = 2000;
    public const int MaxHeight = 800;

    private static ReadOnlySpan<byte> Signature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static PngCheck Check(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0)
        {
            return PngCheck.Empty;
        }

        if (bytes.Length > MaxBytes)
        {
            return PngCheck.TooLarge;
        }

        // signature (8) + IHDR length (4) + "IHDR" (4) + width (4) + height (4)
        if (bytes.Length < 24 || !bytes[..8].SequenceEqual(Signature))
        {
            return PngCheck.NotPng;
        }

        var ihdrLength = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(8, 4));
        if (ihdrLength != 13 || bytes[12] != (byte)'I' || bytes[13] != (byte)'H' || bytes[14] != (byte)'D' || bytes[15] != (byte)'R')
        {
            return PngCheck.NotPng;
        }

        var width = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(16, 4));
        var height = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(20, 4));
        return width is 0 or > MaxWidth || height is 0 or > MaxHeight ? PngCheck.NotPng : PngCheck.Ok;
    }
}

public enum PngCheck
{
    Ok = 0,
    Empty = 1,
    NotPng = 2,
    TooLarge = 3
}
