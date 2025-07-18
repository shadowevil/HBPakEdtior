using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PAKFile
{
    public class PAKFileHeader
    {
        public string Magic { get; set; } = null!;

        public static PAKFileHeader GetDefaultHeader()
        {
            return new PAKFileHeader
            {
                Magic = "<Pak file header>"
            };
        }

        public static PAKFileHeader ReadFromStream(EndianBinaryReader reader)
        {
            PAKFileHeader header = new PAKFileHeader();
            header.Magic = reader.ReadString(GetDefaultHeader().Magic.Length);
            if (header.Magic != GetDefaultHeader().Magic)
            {
                throw new InvalidDataException("Invalid PAK file header magic.");
            }
            return header;
        }

        public static void WriteToStream(BinaryWriter writer)
        {
            writer.Write(Encoding.UTF8.GetBytes(GetDefaultHeader().Magic));
        }
    }
}
