using System.IO;

namespace Security
{
    public class EncryptedReadStream : Stream
    {
        private readonly Stream _stream;
        private uint _w;
        private uint _z;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _stream.Length;

        public EncryptedReadStream(Stream stream, int length)
        {
            _stream = stream;

            // ОТНИМАЕМ 1 БАЙТ ЧЕКСУММЫ, ЧТОБЫ КЛЮЧИ СОВПАЛИ СО СБОРЩИКОМ!
            uint actualSize = (uint)(length - 1);

            _w = 0x12345678 ^ actualSize;
            _z = 0x87654321 ^ actualSize;
        }

        public override int ReadByte()
        {
            int b = _stream.ReadByte();
            if (b == -1) return -1;
            return b ^ (byte)Random();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = _stream.Read(buffer, offset, count);
            for (int i = 0; i < read; i++)
            {
                buffer[offset + i] ^= (byte)Random();
            }
            return read;
        }

        private uint Random()
        {
            _z = 36969 * (_z & 65535) + (_z >> 16);
            _w = 18000 * (_w & 65535) + (_w >> 16);
            return (_z << 16) + _w;
        }

        public override long Position
        {
            get => _stream.Position;
            set => throw new System.InvalidOperationException();
        }

        public override void Flush() => throw new System.InvalidOperationException();
        public override long Seek(long offset, SeekOrigin origin) => throw new System.InvalidOperationException();
        public override void SetLength(long value) => throw new System.InvalidOperationException();
        public override void Write(byte[] buffer, int offset, int count) => throw new System.InvalidOperationException();
    }
}