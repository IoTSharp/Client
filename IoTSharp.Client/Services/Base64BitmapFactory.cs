using Avalonia.Media.Imaging;

namespace IoTSharp.Client.Services;

internal static class Base64BitmapFactory
{
    public static Bitmap? Create(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64))
        {
            return null;
        }

        var bytes = Convert.FromBase64String(base64);
        using var stream = new MemoryStream(bytes);
        return new Bitmap(stream);
    }
}
