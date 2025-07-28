using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PAKFile
{
    public class ImageHeaderInfo
    {
        public ImageFormat Format { get; set; } = ImageFormat.MemoryBmp;
        public uint fileSize { get; set; } = 0;
    }

    public static class ImageSignatures
    {
        private static readonly byte[] PNG      = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];
        private static readonly byte[] BMP      = [0x42, 0x4d];
        private static readonly byte[] JpegJfif = [0xFF, 0xD8, 0xFF, 0xE0];
        private static readonly byte[] Gif89a   = [0x47, 0x49, 0x46, 0x38, 0x39, 0x61];


        public static byte[] GetSignature(ImageFormat format)
        {
            if (format.Equals(ImageFormat.Png))
                return PNG;
            if (format.Equals(ImageFormat.Bmp))
                return BMP;
            if (format.Equals(ImageFormat.Jpeg))
                return JpegJfif;
            if (format.Equals(ImageFormat.Gif))
                return Gif89a;

            throw new ArgumentException("Unsupported image format", nameof(format));
        }
    }

    public static class ImageHeaderReader
    {
        public static ImageHeaderInfo ReadHeader(EndianBinaryReader reader)
        {
            ImageHeaderInfo info = new ImageHeaderInfo() { fileSize = 0, Format = ImageFormat.MemoryBmp };

            if (reader.PeekBytes(ImageSignatures.GetSignature(ImageFormat.Png).Length).SequenceEqual(ImageSignatures.GetSignature(ImageFormat.Png)))
            {
                info = new ImageHeaderInfo { Format = ImageFormat.Png, fileSize = 0 };
            }
            else if (reader.PeekBytes(ImageSignatures.GetSignature(ImageFormat.Bmp).Length).SequenceEqual(ImageSignatures.GetSignature(ImageFormat.Bmp)))
            {
                info = new ImageHeaderInfo { Format = ImageFormat.Bmp, fileSize = 0 };
            }
            else if (reader.PeekBytes(ImageSignatures.GetSignature(ImageFormat.Jpeg).Length).SequenceEqual(ImageSignatures.GetSignature(ImageFormat.Jpeg)))
            {
                info = new ImageHeaderInfo { Format = ImageFormat.Jpeg, fileSize = 0 };
            }
            else if (reader.PeekBytes(ImageSignatures.GetSignature(ImageFormat.Gif).Length).SequenceEqual(ImageSignatures.GetSignature(ImageFormat.Gif)))
            {
                info = new ImageHeaderInfo { Format = ImageFormat.Gif, fileSize = 0 };
            }
            return info;
        }
    }
}
