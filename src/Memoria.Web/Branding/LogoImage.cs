namespace Memoria.Web.Branding;

/// <summary>
/// An uploaded logo, told apart by what it holds rather than by what it is called.
/// </summary>
/// <remarks>
/// Only PNG, JPEG and WebP. An SVG can carry script, and the logo is served from this
/// application's own origin, so a file that opened on its own would run with an operator's
/// session; a raster image cannot. The first bytes decide, so an SVG renamed <c>.png</c> is
/// refused like any other.
/// </remarks>
internal sealed record LogoImage(byte[] Bytes, string FileName)
{
    /// <summary>The largest logo accepted, in bytes: a header mark has no use for more.</summary>
    public const int MaxBytes = 512 * 1024;

    private static readonly (string FileName, string ContentType, Func<byte[], bool> Holds)[] Kinds =
    [
        ("logo.png", "image/png", bytes => bytes.AsSpan().StartsWith((byte[])[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])),
        ("logo.jpg", "image/jpeg", bytes => bytes.AsSpan().StartsWith((byte[])[0xFF, 0xD8, 0xFF])),
        ("logo.webp", "image/webp", bytes => bytes.Length >= 12 &&
                                             bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
                                             bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8))
    ];

    /// <summary>Every file name a logo can be kept under.</summary>
    public static IEnumerable<string> FileNames => Kinds.Select(kind => kind.FileName);

    /// <summary>The media type a logo kept under <paramref name="fileName"/> is served as.</summary>
    public static string? ContentTypeOf(string fileName) =>
        Kinds.FirstOrDefault(kind => kind.FileName == fileName).ContentType;

    /// <summary>Reads an upload, refusing anything too large or not one of the three kinds.</summary>
    /// <exception cref="InvalidDataException">The upload is over the limit or not an image of a kind accepted.</exception>
    public static LogoImage Read(Stream content)
    {
        // One byte past the limit is read, and no more, so an upload too large is known to be
        // without being held in memory whole.
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;

        while ((read = content.Read(chunk, 0, chunk.Length)) > 0)
        {
            buffer.Write(chunk, 0, read);

            if (buffer.Length > MaxBytes)
            {
                throw new InvalidDataException("The logo must be 512 KB or smaller.");
            }
        }

        var bytes = buffer.ToArray();
        var kind = Kinds.FirstOrDefault(kind => kind.Holds(bytes));

        return kind.FileName is null
            ? throw new InvalidDataException("The logo must be a PNG, JPEG or WebP image.")
            : new LogoImage(bytes, kind.FileName);
    }
}
