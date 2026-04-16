using Microsoft.Maui.Controls;

namespace IoTSharp.Client.Services;

internal static class Base64BitmapFactory
{
    private const byte PngSignature0 = 0x89;
    private const byte PngSignature1 = 0x50;
    private const byte PngSignature2 = 0x4E;
    private const byte PngSignature3 = 0x47;
    private const byte GifSignature0 = 0x47;
    private const byte GifSignature1 = 0x49;
    private const byte GifSignature2 = 0x46;
    private const byte JpegSignature0 = 0xFF;
    private const byte JpegSignature1 = 0xD8;

    public static Base64Image? Create(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64))
        {
            return null;
        }

        var bytes = Convert.FromBase64String(base64);
        var (width, height) = ReadImageSize(bytes);
        return new Base64Image(ImageSource.FromStream(() => new MemoryStream(bytes)), width, height);
    }

    private static (int Width, int Height) ReadImageSize(byte[] bytes)
    {
        if (bytes.Length >= 24 && bytes[0] == PngSignature0 && bytes[1] == PngSignature1 && bytes[2] == PngSignature2 && bytes[3] == PngSignature3)
        {
            return (ReadBigEndianInt32(bytes, 16), ReadBigEndianInt32(bytes, 20));
        }

        if (bytes.Length >= 10 && bytes[0] == GifSignature0 && bytes[1] == GifSignature1 && bytes[2] == GifSignature2)
        {
            return (bytes[6] | (bytes[7] << 8), bytes[8] | (bytes[9] << 8));
        }

        if (bytes.Length >= 4 && bytes[0] == JpegSignature0 && bytes[1] == JpegSignature1)
        {
            return ReadJpegSize(bytes);
        }

        return (0, 0);
    }

    private static int ReadBigEndianInt32(byte[] bytes, int start)
        => (bytes[start] << 24) | (bytes[start + 1] << 16) | (bytes[start + 2] << 8) | bytes[start + 3];

    private static (int Width, int Height) ReadJpegSize(byte[] bytes)
    {
        var index = 2;
        while (index + 8 < bytes.Length)
        {
            while (index < bytes.Length && bytes[index] != 0xFF)
            {
                index++;
            }

            while (index < bytes.Length && bytes[index] == 0xFF)
            {
                index++;
            }

            if (index >= bytes.Length)
            {
                break;
            }

            var marker = bytes[index++];
            if (marker is 0xD8 or 0xD9)
            {
                continue;
            }

            if (index + 1 >= bytes.Length)
            {
                break;
            }

            var segmentLength = (bytes[index] << 8) | bytes[index + 1];
            if (segmentLength < 2 || index + segmentLength > bytes.Length)
            {
                break;
            }

            if (IsStartOfFrameMarker(marker))
            {
                var height = (bytes[index + 3] << 8) | bytes[index + 4];
                var width = (bytes[index + 5] << 8) | bytes[index + 6];
                return (width, height);
            }

            index += segmentLength;
        }

        return (0, 0);
    }

    private static bool IsStartOfFrameMarker(byte marker)
        => marker is 0xC0 or 0xC1 or 0xC2 or 0xC3 or 0xC5 or 0xC6 or 0xC7 or 0xC9 or 0xCA or 0xCB or 0xCD or 0xCE or 0xCF;
}

internal sealed record Base64Image(ImageSource ImageSource, int Width, int Height);
