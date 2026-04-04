using System.IO;

namespace Security
{
    public partial class EncryptedReadStream : Stream
    {
        private readonly Stream _stream;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _stream.Length;

        partial void SyncContext(int length);
        partial void ProcessResult(ref int value);
        partial void ProcessBlock(byte[] buffer, int offset, int count);

        public EncryptedReadStream(Stream stream, int length)
        {
            _stream = stream;
            SyncContext(length);
        }

        public override int ReadByte()
        {
            int b = _stream.ReadByte();
            if (b != -1)
            {
                ProcessResult(ref b);
            }
            return b;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = _stream.Read(buffer, offset, count);
            if (read > 0)
            {
                ProcessBlock(buffer, offset, read);
            }
            return read;
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