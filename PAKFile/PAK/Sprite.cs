using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace PAKFile
{
    public class Spritea
    {
        public List<SpriteRectangle> spriteRectangles { get; set; } = null!;

        public Bitmap? sprite = null;

        private Spritea()
        { }

        public void GetAcceptedImageFileTypes(out string extension, out string Filter)
        {
            extension = sprite!.RawFormat.Guid switch
            {
                var g when g == ImageFormat.Png.Guid => ".png",
                var g when g == ImageFormat.Bmp.Guid => ".bmp",
                var g when g == ImageFormat.Jpeg.Guid => ".jpg",
                var g when g == ImageFormat.Gif.Guid => ".gif",
                _ => throw new NotSupportedException("Unsupported image format.")
            };
            Filter = $"Image Files (*{extension})|*{extension}";
        }

        public static void GetAllAcceptedImageFileTypes(out string extensions, out string filter)
        {
            var formats = new List<ImageFormat>
            {
                ImageFormat.Png,
                ImageFormat.Bmp,
                ImageFormat.Jpeg,
                ImageFormat.Gif
            };
            extensions = string.Join(";", formats.Select(f => $"*{f.ToString()}"));
            filter = $"Image Files ({extensions})|{extensions}";
        }

        public void ExportSprite()
        {
            using SaveFileDialog sfd = new SaveFileDialog
            {
                Title = "Export Sprite",
                RestoreDirectory = true
            };

            byte[] bytes = SaveBitmapToByteArrayAuto(sprite!);
            GetAcceptedImageFileTypes(out string extension, out string filter);
            sfd.Filter = filter;

            if (sfd.ShowDialog() != DialogResult.OK)
                return;

            string newPath = Path.ChangeExtension(sfd.FileName, extension);
            File.WriteAllBytes(newPath, bytes);
        }

        public static Sprite CreateNewSprite(Bitmap bmp)
        {
            Sprite sprite = new Sprite();
            sprite.spriteRectangles = new List<SpriteRectangle>();
            sprite.sprite = bmp;
            return sprite;
        }

        public static Sprite ReadFromStream(EndianBinaryReader reader)
        {
            if(reader.ReadString(SpriteHeader.GetDefaultHeader().Magic.Length) != SpriteHeader.GetDefaultHeader().Magic)
            {
                throw new InvalidDataException("Invalid sprite file header magic.");
            }
            reader.Skip(80);

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

            _ = reader.ReadBytes(4);

            ImageHeaderInfo info = ImageHeaderReader.ReadHeader(reader);

            if (info.Format == ImageFormat.Bmp && info.fileSize == 0)
            {
                BmpHeader bmpHeader = BmpHeader.ReadFromStream(reader);
                info.fileSize = bmpHeader.BfSize;
            }
            sprite.sprite = LoadBitmapFromStream(reader, info);

            return sprite;
        }

        private static Bitmap LoadBitmapFromStream(EndianBinaryReader reader, ImageHeaderInfo header)
        {
            byte[] bmpBytes = reader.ReadBytes((int)header.fileSize);
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

            byte[] spriteBytes = SaveBitmapToByteArrayAuto(sprite!);
            writer.Write((uint)spriteBytes.Length);
            writer.Write(spriteBytes);
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

        public static Bitmap ConvertTo8bppBitmap(Bitmap source)
        {
            if (source.PixelFormat == PixelFormat.Format8bppIndexed)
                return source;

            int width = source.Width;
            int height = source.Height;
            Bitmap preprocessed = new Bitmap(width, height, PixelFormat.Format32bppArgb);

            bool IsPremultiplied(Color c) => c.A == 0 || (c.R <= c.A && c.G <= c.A && c.B <= c.A);

            // Sample check for premultiplication
            bool premultiplied = true;
            for (int y = 0; y < Math.Min(height, 16); y++)
            {
                for (int x = 0; x < Math.Min(width, 16); x++)
                {
                    if (!IsPremultiplied(source.GetPixel(x, y)))
                    {
                        premultiplied = false;
                        break;
                    }
                }
                if (!premultiplied) break;
            }

            // Preprocess pixels
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color src = source.GetPixel(x, y);
                    if (src.A == 0)
                    {
                        preprocessed.SetPixel(x, y, Color.Fuchsia);
                    }
                    else if (src.A < 255 && !premultiplied)
                    {
                        byte r = (byte)((src.R * src.A) / 255);
                        byte g = (byte)((src.G * src.A) / 255);
                        byte b = (byte)((src.B * src.A) / 255);
                        preprocessed.SetPixel(x, y, Color.FromArgb(r, g, b));
                    }
                    else
                    {
                        preprocessed.SetPixel(x, y, Color.FromArgb(src.R, src.G, src.B));
                    }
                }
            }

            var quantizer = new OctreeQuantizer(256);
            Bitmap quantized = quantizer.Quantize(preprocessed);

            // Ensure palette index 0 is fuchsia
            ColorPalette palette = quantized.Palette;

            // Only replace index 0 with fuchsia if it's already present in the image
            for (int i = 0; i < palette.Entries.Length; i++)
            {
                if (palette.Entries[i].ToArgb() == Color.Fuchsia.ToArgb())
                {
                    palette.Entries[i] = Color.Fuchsia;
                    break;
                }
            }

            quantized.Palette = palette;

            return quantized;
        }

        public Bitmap? ImportSprite()
        {
            using OpenFileDialog ofd = new OpenFileDialog
            {
                Title = "Import Sprite",
                RestoreDirectory = true,
                Multiselect = false,
                CheckFileExists = true,
                CheckPathExists = true
            };
            GetAllAcceptedImageFileTypes(out string extension, out string filter);
            ofd.Filter = filter;
            if (ofd.ShowDialog() != DialogResult.OK)
                return null;

            string filePath = ofd.FileName;
            sprite = new Bitmap(filePath);
            return sprite;
        }
    }
}
