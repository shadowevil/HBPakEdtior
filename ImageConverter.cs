using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

public enum ImageType
{
    PNG,
    BMP
}

public static class ImageConverter
{
    private static readonly Color TransparentKey = Color.FromArgb(255, 255, 0, 255);

    public static T Convert<T>(Bitmap source, ImageType targetFormat) where T : class
    {
        var result = ConvertBitmap(source, targetFormat);
        return ConvertOutput<T>(result, targetFormat);
    }

    public static T Convert<T>(Bitmap source, string targetFormat) where T : class
        => Convert<T>(source, ParseFormat(targetFormat));

    public static T Convert<T>(byte[] source, ImageType targetFormat) where T : class
    {
        using var ms = new MemoryStream(source);
        using var bitmap = new Bitmap(ms);
        var result = ConvertBitmap(bitmap, targetFormat);
        return ConvertOutput<T>(result, targetFormat);
    }

    public static T Convert<T>(byte[] source, string targetFormat) where T : class
        => Convert<T>(source, ParseFormat(targetFormat));

    private static ImageType ParseFormat(string format) => format.ToUpperInvariant() switch
    {
        "PNG" => ImageType.PNG,
        "BMP" => ImageType.BMP,
        _ => throw new ArgumentException($"Unsupported format: {format}")
    };

    private static Bitmap ConvertBitmap(Bitmap source, ImageType targetFormat) => targetFormat switch
    {
        ImageType.BMP => ConvertToBmp(source),
        ImageType.PNG => ConvertToPng(source),
        _ => throw new ArgumentException("Unsupported format")
    };

    private static Bitmap ConvertToBmp(Bitmap source)
    {
        var result = new Bitmap(source.Width, source.Height, PixelFormat.Format16bppRgb565);
        using var g = Graphics.FromImage(result);
        g.Clear(TransparentKey);
        g.DrawImage(source, 0, 0, source.Width, source.Height);
        return result;
    }

    private static Bitmap ConvertToPng(Bitmap source)
    {
        var result = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        var srcRect = new Rectangle(0, 0, source.Width, source.Height);

        var srcData = source.LockBits(srcRect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var dstData = result.LockBits(srcRect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

        try
        {
            int bytes = srcData.Stride * source.Height;
            var srcBuffer = new byte[bytes];
            var dstBuffer = new byte[bytes];

            Marshal.Copy(srcData.Scan0, srcBuffer, 0, bytes);

            for (int i = 0; i < bytes; i += 4)
            {
                byte b = srcBuffer[i];
                byte g = srcBuffer[i + 1];
                byte r = srcBuffer[i + 2];

                if (r >= 250 && g <= 5 && b >= 250)
                {
                    dstBuffer[i] = 0;
                    dstBuffer[i + 1] = 0;
                    dstBuffer[i + 2] = 0;
                    dstBuffer[i + 3] = 0;
                }
                else
                {
                    dstBuffer[i] = b;
                    dstBuffer[i + 1] = g;
                    dstBuffer[i + 2] = r;
                    dstBuffer[i + 3] = 255;
                }
            }

            Marshal.Copy(dstBuffer, 0, dstData.Scan0, bytes);
        }
        finally
        {
            source.UnlockBits(srcData);
            result.UnlockBits(dstData);
        }

        return result;
    }

    private static T ConvertOutput<T>(Bitmap bitmap, ImageType format) where T : class
    {
        if (typeof(T) == typeof(Bitmap))
            return (bitmap as T)!;

        if (typeof(T) == typeof(byte[]))
        {
            using var ms = new MemoryStream();
            var imgFormat = format == ImageType.PNG ? ImageFormat.Png : ImageFormat.Bmp;
            bitmap.Save(ms, imgFormat);
            bitmap.Dispose();
            return (ms.ToArray() as T)!;
        }

        throw new ArgumentException("T must be Bitmap or byte[]");
    }
}