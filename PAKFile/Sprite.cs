using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace PAKFile
{

    public class Sprite
    {
        public List<SpriteRectangle> spriteRectangles { get; set; } = null!;
        public Bitmap? sprite = null;

        private Sprite()
        { }

        public static Sprite ReadFromStream(EndianBinaryReader reader)
        {
            if(reader.ReadString(SpriteHeader.GetDefaultHeader().Magic.Length) != SpriteHeader.GetDefaultHeader().Magic)
            {
                throw new InvalidDataException("Invalid sprite file header magic.");
            }
            reader.Skip(100 - SpriteHeader.GetDefaultHeader().Magic.Length);

            Sprite sprite = new Sprite();
            int rectangleCount = reader.ReadInt32();
            sprite.spriteRectangles = new List<SpriteRectangle>(rectangleCount);
            for (int i = 0; i < rectangleCount; i++)
            {
                SpriteRectangle rectangle = new SpriteRectangle();
                rectangle.x = reader.ReadInt16();
                rectangle.y = reader.ReadInt16();
                rectangle.width = reader.ReadInt16();
                rectangle.height = reader.ReadInt16();
                rectangle.pivotX = reader.ReadInt16();
                rectangle.pivotY = reader.ReadInt16();
                sprite.spriteRectangles.Add(rectangle);
            }
            
            BmpHeader bmpHeader = BmpHeader.ReadFromStream(reader);

            sprite.sprite = LoadBitmapFromStream(reader, bmpHeader);

            return sprite;
        }

        private static Bitmap LoadBitmapFromStream(EndianBinaryReader reader, BmpHeader header)
        {
            reader.Seek(-54, SeekOrigin.Current);
            byte[] bmpBytes = reader.ReadBytes((int)header.BfSize);
            using var ms = new MemoryStream(bmpBytes);
            return new Bitmap(ms);
        }

        public void WriteToStream(BinaryWriter writer)
        {
            SpriteHeader.WriteToStream(writer);
            writer.Write(new byte[100 - SpriteHeader.GetDefaultHeader().Magic.Length]); // Padding
            writer.Write((int)spriteRectangles.Count);
            foreach (var rectangle in spriteRectangles)
            {
                writer.Write(rectangle.x);
                writer.Write(rectangle.y);
                writer.Write(rectangle.width);
                writer.Write(rectangle.height);
                writer.Write(rectangle.pivotX);
                writer.Write(rectangle.pivotY);
            }

            writer.Write(new byte[4]);
            writer.Write(SaveBitmapToByteArrayAuto(sprite!));
        }

        public static byte[] SaveBitmapToByteArrayAuto(Bitmap bitmap)
        {
            if (bitmap.PixelFormat == PixelFormat.Format8bppIndexed)
                return WriteBitmap8bppToBmp(bitmap);

            using var ms = new MemoryStream();
            bitmap.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }

        public static byte[] WriteBitmap8bppToBmp(Bitmap bitmap)
        {
            if (bitmap.PixelFormat != PixelFormat.Format8bppIndexed)
                throw new ArgumentException("Bitmap must be 8bpp indexed.");

            int width = bitmap.Width;
            int height = bitmap.Height;

            var rect = new Rectangle(0, 0, width, height);
            var data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, bitmap.PixelFormat);

            int stride = data.Stride;
            int imageSize = stride * height;

            // Determine the actual number of colors in the bitmap's palette.
            int paletteColorCount = bitmap.Palette.Entries.Length;

            // Calculate pixelDataOffset based on the actual palette size.
            // BITMAPFILEHEADER (14 bytes) + BITMAPINFOHEADER (40 bytes) + (paletteColorCount * 4 bytes)
            int pixelDataOffset = 14 + 40 + (paletteColorCount * 4);

            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                // BITMAPFILEHEADER (14 bytes)
                writer.Write((ushort)0x4D42);                            // bfType = 'BM'
                writer.Write((uint)(pixelDataOffset + imageSize));       // bfSize
                writer.Write((ushort)0);                                 // bfReserved1
                writer.Write((ushort)0);                                 // bfReserved2
                writer.Write((uint)pixelDataOffset);                     // bfOffBits

                // BITMAPINFOHEADER (40 bytes)
                writer.Write(40);                                        // header size
                writer.Write(width);
                writer.Write(height);
                writer.Write((short)1);                                  // planes
                writer.Write((short)8);                                  // bpp
                writer.Write(0);                                         // compression (BI_RGB)
                writer.Write(imageSize);                                 // biSizeImage
                writer.Write(2835);                                      // XPelsPerMeter (approx. 72 DPI)
                writer.Write(2835);                                      // YPelsPerMeter (approx. 72 DPI)
                writer.Write((uint)paletteColorCount);                   // biClrUsed (use actual count)
                writer.Write(0);                                         // biClrImportant (0 means all are important)

                // Color Palette
                // Write only the colors that are actually in the palette.
                for (int i = 0; i < paletteColorCount; i++)
                {
                    Color c = bitmap.Palette.Entries[i];
                    writer.Write(c.B);
                    writer.Write(c.G);
                    writer.Write(c.R);
                    writer.Write((byte)0);
                }

                // Pixel Data (bottom-up)
                byte[] row = new byte[stride];
                IntPtr scan0 = data.Scan0;

                for (int y = height - 1; y >= 0; y--)
                {
                    Marshal.Copy(IntPtr.Add(scan0, y * stride), row, 0, stride);
                    writer.Write(row, 0, stride);
                }

                bitmap.UnlockBits(data);
                return ms.ToArray();
            }
        }
    }
}
