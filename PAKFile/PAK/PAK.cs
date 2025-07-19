using System.Text;

namespace PAKFile
{
    public class PAK
    {
        public const string FilePattern = "PAK Files (*.pak)|*.pak";

        public string FilePath { get; set; } = string.Empty;

        public PAKFileHeader pakFileHeader { get; set; } = null!;
        public int SpriteCount { get; set; } = 0;
        public List<int> SpriteEntryOffsets { get; set; } = null!;
        public List<int> SpriteEntryEndsets { get; set; } = null!;
        public List<Sprite> Sprites { get; set; } = null!;

        private PAK()
        { }

        public static PAK CreateNewPak()
        {
            PAK pak = new PAK
            {
                pakFileHeader = PAKFileHeader.GetDefaultHeader(),
                SpriteCount = 0,
                SpriteEntryOffsets = new List<int>(),
                SpriteEntryEndsets = new List<int>(),
                Sprites = new List<Sprite>()
            };
            return pak;
        }

        public static PAK OpenPakFile(string path)
        {
            PAK pak = new PAK();
            using (MemoryStream ms = new MemoryStream(File.ReadAllBytes(path)))
            using (EndianBinaryReader reader = new EndianBinaryReader(ms))
            {
                pak.pakFileHeader = PAKFileHeader.ReadFromStream(reader);
                reader.Skip(3);
                
                pak.SpriteCount = reader.ReadInt32();
                
                pak.SpriteEntryOffsets = new List<int>(pak.SpriteCount);
                pak.SpriteEntryEndsets = new List<int>(pak.SpriteCount);
                for (int i = 0; i < pak.SpriteCount; i++)
                {
                    pak.SpriteEntryOffsets.Add(reader.ReadInt32());
                    pak.SpriteEntryEndsets.Add(reader.ReadInt32());
                }

                pak.Sprites = new List<Sprite>(pak.SpriteCount);
                for(int i = 0;i < pak.SpriteCount; i++)
                {
                    Sprite sprite = Sprite.ReadFromStream(reader);
                    pak.Sprites.Add(sprite);
                }
            }
            pak.FilePath = path;
            return pak;
        }

        public void Save(string filePath)
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(ms))
            {
                PAKFileHeader.WriteToStream(writer);
                writer.Write(new byte[3]); // Padding

                writer.Write(Sprites.Count);

                List<long> offsetPlaceholders = new List<long>();
                List<long> endsetPlaceholders = new List<long>();

                foreach (var _ in Sprites)
                {
                    offsetPlaceholders.Add(ms.Position);
                    writer.Write(new byte[sizeof(int)]);      // offset placeholder
                    endsetPlaceholders.Add(ms.Position);
                    writer.Write(new byte[sizeof(int)]);      // endset placeholder
                }

                List<int> spriteOffsets = new List<int>();
                List<int> spriteEndsets = new List<int>();
                foreach (var sprite in Sprites)
                {
                    spriteOffsets.Add((int)ms.Position);
                    sprite.WriteToStream(writer);
                    spriteEndsets.Add((int)ms.Position);
                }

                // Back-patch sprite offsets
                for (int i = 0; i < Sprites.Count; i++)
                {
                    ms.Position = offsetPlaceholders[i];
                    writer.Write(spriteOffsets[i]);
                    ms.Position = endsetPlaceholders[i];
                    writer.Write(spriteEndsets[i]);
                }

                writer.Flush();
                File.WriteAllBytes(filePath, ms.ToArray());
            }
        }
    }
}
