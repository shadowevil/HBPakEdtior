using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PAKFile
{
    public class BmpHeader
    {
        public ushort BfType { get; private set; }
        public uint BfSize { get; private set; }
        public uint BfReserved1 { get; private set; } // Skipped in earlier version
        public uint BfReserved2 { get; private set; } // Skipped in earlier version
        public uint BfOffBits { get; private set; }

        public uint BiSize { get; private set; }
        public int BiWidth { get; private set; }
        public int BiHeight { get; private set; }
        public ushort BiPlanes { get; private set; }
        public ushort BiBitCount { get; private set; }
        public uint BiCompression { get; private set; }
        public uint BiSizeImage { get; private set; }
        public int BiXPelsPerMeter { get; private set; }
        public int BiYPelsPerMeter { get; private set; }
        public uint BiClrUsed { get; private set; }
        public uint BiClrImportant { get; private set; }

        private BmpHeader() { }

        public static BmpHeader ReadFromStream(EndianBinaryReader reader)
        {
            var savedPosition = reader.Position;
            var bmpHeader = new BmpHeader
            {
                BfType = reader.ReadUInt16()
            };
            if (bmpHeader.BfType != 0x4D42) // 'BM' in ASCII
            {
                throw new InvalidDataException("Invalid BMP file header.");
            }

            bmpHeader.BfSize            = reader.ReadUInt32();
            bmpHeader.BfReserved1       = reader.ReadUInt16();
            bmpHeader.BfReserved2       = reader.ReadUInt16();
            bmpHeader.BfOffBits         = reader.ReadUInt32();
            bmpHeader.BiSize            = reader.ReadUInt32();
            bmpHeader.BiWidth           = reader.ReadInt32();
            bmpHeader.BiHeight          = reader.ReadInt32();
            bmpHeader.BiPlanes          = reader.ReadUInt16();
            bmpHeader.BiBitCount        = reader.ReadUInt16();
            bmpHeader.BiCompression     = reader.ReadUInt32();
            bmpHeader.BiSizeImage       = reader.ReadUInt32();
            bmpHeader.BiXPelsPerMeter   = reader.ReadInt32();
            bmpHeader.BiYPelsPerMeter   = reader.ReadInt32();
            bmpHeader.BiClrUsed         = reader.ReadUInt32();
            bmpHeader.BiClrImportant    = reader.ReadUInt32();

            if (bmpHeader.BiWidth <= 0 || bmpHeader.BiHeight == 0)
            {
                throw new InvalidDataException("Invalid BMP dimensions. Width and height must be positive.");
            }
            reader.Seek(savedPosition, SeekOrigin.Begin);
            return bmpHeader;
        }
    }
}
