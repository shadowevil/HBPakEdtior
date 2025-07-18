using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PAKFile
{
    public enum Endianness { LittleEndian, BigEndian }

    public class EndianBinaryReader : IDisposable
    {
        private readonly BinaryReader _reader;

        public EndianBinaryReader(Stream input)
        {
            _reader = new BinaryReader(input);
        }

        public byte[] ReadBytes(int count, Endianness endianness = Endianness.LittleEndian)
        {
            var data = _reader.ReadBytes(count);
            if (endianness == Endianness.BigEndian)
                Array.Reverse(data);
            return data;
        }

        public string ReadString(int length = -1)
        {
            if (length < 0)
            {
                var bytes = new List<byte>();
                byte b;
                while ((b = _reader.ReadByte()) != 0)
                    bytes.Add(b);
                return Encoding.UTF8.GetString(bytes.ToArray());
            }
            else
            {
                return Encoding.UTF8.GetString(ReadBytes(length));
            }
        }


        public short ReadInt16(Endianness endianness = Endianness.LittleEndian) => BitConverter.ToInt16(ReadBytes(2, endianness), 0);
        public ushort ReadUInt16(Endianness endianness = Endianness.LittleEndian) => BitConverter.ToUInt16(ReadBytes(2, endianness), 0);
        public int ReadInt32(Endianness endianness = Endianness.LittleEndian) => BitConverter.ToInt32(ReadBytes(4, endianness), 0);
        public uint ReadUInt32(Endianness endianness = Endianness.LittleEndian) => BitConverter.ToUInt32(ReadBytes(4, endianness), 0);
        public long ReadInt64(Endianness endianness = Endianness.LittleEndian) => BitConverter.ToInt64(ReadBytes(8, endianness), 0);
        public ulong ReadUInt64(Endianness endianness = Endianness.LittleEndian) => BitConverter.ToUInt64(ReadBytes(8, endianness), 0);
        public float ReadSingle(Endianness endianness = Endianness.LittleEndian) => BitConverter.ToSingle(ReadBytes(4, endianness), 0);
        public double ReadDouble(Endianness endianness = Endianness.LittleEndian) => BitConverter.ToDouble(ReadBytes(8, endianness), 0);
        public byte ReadByte() => _reader.ReadByte();
        public long Position => _reader.BaseStream.Position;
        public void Seek(long offset, SeekOrigin origin) => _reader.BaseStream.Seek(offset, origin);
        public void Skip(long numBytesToSkip) => _reader.BaseStream.Seek(numBytesToSkip, SeekOrigin.Current);

        public void Dispose()
        {
            _reader.Dispose();
        }
    }
}
