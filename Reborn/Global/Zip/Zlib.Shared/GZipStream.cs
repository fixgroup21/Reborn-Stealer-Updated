

using System;
using System.IO;

namespace Ionic.Zlib
{
  
    public class GZipStream : System.IO.Stream
    {

        public String Comment
        {
            get
            {
                return _Comment;
            }
            set
            {
                if (_disposed) throw new ObjectDisposedException("GZipStream");
                _Comment = value;
            }
        }

     
        public String FileName
        {
            get { return _FileName; }
            set
            {
                if (_disposed) throw new ObjectDisposedException("GZipStream");
                _FileName = value;
                if (_FileName == null) return;
                if (_FileName.IndexOf("/") != -1)
                {
                    _FileName = _FileName.Replace("/", "\\");
                }
                if (_FileName.EndsWith("\\"))
                    throw new Exception("Illegal filename");
                if (_FileName.IndexOf("\\") != -1)
                {
                    // trim any leading path
                    _FileName = Path.GetFileName(_FileName);
                }
            }
        }

    
        public DateTime? LastModified;

        public int Crc32 { get { return _Crc32; } }

        private int _headerByteCount;
        internal ZlibBaseStream _baseStream;
        bool _disposed;
        bool _firstReadDone;
        string _FileName;
        string _Comment;
        int _Crc32;


      
        public GZipStream(Stream stream, CompressionMode mode)
            : this(stream, mode, CompressionLevel.Default, false)
        {
        }

     
        public GZipStream(Stream stream, CompressionMode mode, CompressionLevel level)
            : this(stream, mode, level, false)
        {
        }



        public GZipStream(Stream stream, CompressionMode mode, CompressionLevel level, bool leaveOpen)
        {
            _baseStream = new ZlibBaseStream(stream, mode, level, ZlibStreamFlavor.GZIP, leaveOpen);
        }


        #region Stream methods

   
        protected override void Dispose(bool disposing)
        {
            try
            {
                if (!_disposed)
                {
                    if (disposing && (this._baseStream != null))
                    {
                        this._baseStream.Dispose();
                        this._Crc32 = _baseStream.Crc32;
                    }
                    _disposed = true;
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }


     
        public override bool CanRead
        {
            get
            {
                if (_disposed) throw new ObjectDisposedException("GZipStream");
                return _baseStream._stream.CanRead;
            }
        }

        public override bool CanSeek
        {
            get { return false; }
        }


     
        public override bool CanWrite
        {
            get
            {
                if (_disposed) throw new ObjectDisposedException("GZipStream");
                return _baseStream._stream.CanWrite;
            }
        }


        public override void Flush()
        {
            if (_disposed) throw new ObjectDisposedException("GZipStream");
            _baseStream.Flush();
        }


        public override long Length
        {
            get { throw new NotImplementedException(); }
        }


        public override long Position
        {
            get
            {
                if (this._baseStream._streamMode == Ionic.Zlib.ZlibBaseStream.StreamMode.Writer)
                    return this._baseStream._z.TotalBytesOut + _headerByteCount;
                if (this._baseStream._streamMode == Ionic.Zlib.ZlibBaseStream.StreamMode.Reader)
                    return this._baseStream._z.TotalBytesIn + this._baseStream._gzipHeaderByteCount;
                return 0;
            }

            set { throw new NotImplementedException(); }
        }

  
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_disposed) throw new ObjectDisposedException("GZipStream");
            int n = _baseStream.Read(buffer, offset, count);

            // Console.WriteLine("GZipStream::Read(buffer, off({0}), c({1}) = {2}", offset, count, n);
            // Console.WriteLine( Util.FormatByteArray(buffer, offset, n) );

            if (!_firstReadDone)
            {
                _firstReadDone = true;
                FileName = _baseStream._GzipFileName;
                Comment = _baseStream._GzipComment;
            }
            return n;
        }



     
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

    
        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }


        public override void Write(byte[] buffer, int offset, int count)
        {
            if (_disposed) throw new ObjectDisposedException("GZipStream");
            if (_baseStream._streamMode == Ionic.Zlib.ZlibBaseStream.StreamMode.Undefined)
            {
                //Console.WriteLine("GZipStream: First write");
                if (_baseStream._wantCompress)
                {
                    // first write in compression, therefore, emit the GZIP header
                    _headerByteCount = EmitHeader();
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }

            _baseStream.Write(buffer, offset, count);
        }
        #endregion


        internal static readonly System.DateTime _unixEpoch = new System.DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        internal static readonly System.Text.Encoding iso8859dash1 = System.Text.Encoding.GetEncoding("iso-8859-1");


        private int EmitHeader()
        {
            byte[] commentBytes = (Comment == null) ? null : iso8859dash1.GetBytes(Comment);
            byte[] filenameBytes = (FileName == null) ? null : iso8859dash1.GetBytes(FileName);

            int cbLength = (Comment == null) ? 0 : commentBytes.Length + 1;
            int fnLength = (FileName == null) ? 0 : filenameBytes.Length + 1;

            int bufferLength = 10 + cbLength + fnLength;
            byte[] header = new byte[bufferLength];
            int i = 0;
            // ID
            header[i++] = 0x1F;
            header[i++] = 0x8B;

            // compression method
            header[i++] = 8;
            byte flag = 0;
            if (Comment != null)
                flag ^= 0x10;
            if (FileName != null)
                flag ^= 0x8;

            // flag
            header[i++] = flag;

            // mtime
            if (!LastModified.HasValue) LastModified = DateTime.Now;
            System.TimeSpan delta = LastModified.Value - _unixEpoch;
            Int32 timet = (Int32)delta.TotalSeconds;
            Array.Copy(BitConverter.GetBytes(timet), 0, header, i, 4);
            i += 4;

            // xflg
            header[i++] = 0;    // this field is totally useless
            // OS
            header[i++] = 0xFF; // 0xFF == unspecified

            // extra field length - only if FEXTRA is set, which it is not.
            //header[i++]= 0;
            //header[i++]= 0;

            // filename
            if (fnLength != 0)
            {
                Array.Copy(filenameBytes, 0, header, i, fnLength - 1);
                i += fnLength - 1;
                header[i++] = 0; // terminate
            }

            // comment
            if (cbLength != 0)
            {
                Array.Copy(commentBytes, 0, header, i, cbLength - 1);
                i += cbLength - 1;
                header[i++] = 0; // terminate
            }

            _baseStream._stream.Write(header, 0, header.Length);

            return header.Length; // bytes written
        }
    }
}
