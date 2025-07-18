using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PAKFile
{
    public class SpriteHeader
    {
        public string Magic { get; set; } = null!;

        public static SpriteHeader GetDefaultHeader()
        {
            return new SpriteHeader
            {
                Magic = "<Sprite File Header>"
            };
        }

        public static void WriteToStream(BinaryWriter writer)
        {
            writer.Write(Encoding.UTF8.GetBytes(GetDefaultHeader().Magic));
        }
    }
}
