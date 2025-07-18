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
            reader.Skip(4);
            return new BmpHeader
            {
                BfType = reader.ReadUInt16(),
                BfSize = reader.ReadUInt32(),
                BfReserved1 = reader.ReadUInt16(),
                BfReserved2 = reader.ReadUInt16(),
                BfOffBits = reader.ReadUInt32(),

                BiSize = reader.ReadUInt32(),
                BiWidth = reader.ReadInt32(),
                BiHeight = reader.ReadInt32(),
                BiPlanes = reader.ReadUInt16(),
                BiBitCount = reader.ReadUInt16(),
                BiCompression = reader.ReadUInt32(),
                BiSizeImage = reader.ReadUInt32(),
                BiXPelsPerMeter = reader.ReadInt32(),
                BiYPelsPerMeter = reader.ReadInt32(),
                BiClrUsed = reader.ReadUInt32(),
                BiClrImportant = reader.ReadUInt32()
            };
        }
    }
}
