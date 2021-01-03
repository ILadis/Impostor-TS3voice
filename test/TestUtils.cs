using System;
using System.Text;
using System.IO;

namespace Impostor.TS3mod.Test
{
    public class ServerStream : Stream
    {
        private MemoryStream response = new MemoryStream();

        public string Response
        {
            set
            {
                response.Write(ToBytes(value));
                response.Seek(0, SeekOrigin.Begin);
            }
        }

        private MemoryStream receive = new MemoryStream();

        public string Receive
        {
            get => FromBytes(receive.ToArray());
        }

        public override bool CanRead { get { return true;  } }

        public override bool CanSeek { get { return false; } }

        public override bool CanWrite { get { return true; } }

        public override long Length
        {
            get { throw new NotSupportedException(); }
        }

        public override long Position
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotImplementedException();

        public override int Read(byte[] buffer, int offset, int count) => response.Read(buffer, offset, count);

        public override void Write(byte[] buffer, int offset, int count) => receive.Write(buffer, offset, count);

        public override void Flush()
        {
            response.Flush();
            receive.Flush();
        }

        private byte[] ToBytes(string value) => Encoding.UTF8.GetBytes(value);

        private string FromBytes(byte[] value) => Encoding.UTF8.GetString(value);
    }
}
