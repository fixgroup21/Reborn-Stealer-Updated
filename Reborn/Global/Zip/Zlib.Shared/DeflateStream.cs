// DeflateStream.cs
// ------------------------------------------------------------------
//
// Copyright (c) 2009-2010 Dino Chiesa.
// All rights reserved.
//
// This code module is part of DotNetZip, a zipfile class library.
//
// ------------------------------------------------------------------
//
// This code is licensed under the Microsoft Public License.
// See the file License.txt for the license details.
// More info on: http://dotnetzip.codeplex.com
//
// ------------------------------------------------------------------
//
// last saved (in emacs):
// Time-stamp: <2011-July-31 14:48:11>
//
// ------------------------------------------------------------------
//
// This module defines the DeflateStream class, which can be used as a replacement for
// the System.IO.Compression.DeflateStream class in the .NET BCL.
//
// ------------------------------------------------------------------


using System;

namespace Ionic.Zlib
{
   
    public class DeflateStream : System.IO.Stream
    {
        internal ZlibBaseStream _baseStream;
        internal System.IO.Stream _innerStream;
        bool _disposed;

     
        public DeflateStream(System.IO.Stream stream, CompressionMode mode)
            : this(stream, mode, CompressionLevel.Default, false)
        {
        }

     
        public DeflateStream(System.IO.Stream stream, CompressionMode mode, CompressionLevel level)
            : this(stream, mode, level, false)
        {
        }

        public DeflateStream(System.IO.Stream stream, CompressionMode mode, bool leaveOpen)
            : this(stream, mode, CompressionLevel.Default, leaveOpen)
        {
        }

        public DeflateStream(System.IO.Stream stream, CompressionMode mode, CompressionLevel level, bool leaveOpen)
        {
            _innerStream = stream;
            _baseStream = new ZlibBaseStream(stream, mode, level, ZlibStreamFlavor.DEFLATE, leaveOpen);
        }

        #region Zlib properties

    
        virtual public FlushType FlushMode
        {
            get { return (this._baseStream._flushMode); }
            set
            {
                if (_disposed) throw new ObjectDisposedException("DeflateStream");
                this._baseStream._flushMode = value;
            }
        }

 
       
        public int BufferSize
        {
            get
            {
                return this._baseStream._bufferSize;
            }
            set
            {
                if (_disposed) throw new ObjectDisposedException("DeflateStream");
                if (this._baseStream._workingBuffer != null)
                    throw new ZlibException("The working buffer is already set.");
                if (value < ZlibConstants.WorkingBufferSizeMin)
                    throw new ZlibException(String.Format("Don't be silly. {0} bytes?? Use a bigger buffer, at least {1}.", value, ZlibConstants.WorkingBufferSizeMin));
                this._baseStream._bufferSize = value;
            }
        }

    
        public CompressionStrategy Strategy
        {
            get
            {
                return this._baseStream.Strategy;
            }
            set
            {
            if (_disposed) throw new ObjectDisposedException("DeflateStream");
                this._baseStream.Strategy = value;
            }
        }

    
        virtual public long TotalIn
        {
            get
            {
                return this._baseStream._z.TotalBytesIn;
            }
        }

   
        virtual public long TotalOut
        {
            get
            {
                return this._baseStream._z.TotalBytesOut;
            }
        }

        #endregion

        #region System.IO.Stream methods
    
        protected override void Dispose(bool disposing)
        {
            try
            {
                if (!_disposed)
                {
                    if (disposing && (this._baseStream != null))
                        this._baseStream.Dispose();
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
                if (_disposed) throw new ObjectDisposedException("DeflateStream");
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
                if (_disposed) throw new ObjectDisposedException("DeflateStream");
                return _baseStream._stream.CanWrite;
            }
        }

     
        public override void Flush()
        {
            if (_disposed) throw new ObjectDisposedException("DeflateStream");
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
                    return this._baseStream._z.TotalBytesOut;
                if (this._baseStream._streamMode == Ionic.Zlib.ZlibBaseStream.StreamMode.Reader)
                    return this._baseStream._z.TotalBytesIn;
                return 0;
            }
            set { throw new NotImplementedException(); }
        }


        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_disposed) throw new ObjectDisposedException("DeflateStream");
            return _baseStream.Read(buffer, offset, count);
        }


        public override long Seek(long offset, System.IO.SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

 
        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

   
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (_disposed) throw new ObjectDisposedException("DeflateStream");
            _baseStream.Write(buffer, offset, count);
        }
        #endregion





        public static byte[] CompressString(String s)
        {
            using (var ms = new System.IO.MemoryStream())
            {
                System.IO.Stream compressor =
                    new DeflateStream(ms, CompressionMode.Compress, CompressionLevel.BestCompression);
                ZlibBaseStream.CompressString(s, compressor);
                return ms.ToArray();
            }
        }


        public static byte[] CompressBuffer(byte[] b)
        {
            using (var ms = new System.IO.MemoryStream())
            {
                System.IO.Stream compressor =
                    new DeflateStream( ms, CompressionMode.Compress, CompressionLevel.BestCompression );

                ZlibBaseStream.CompressBuffer(b, compressor);
                return ms.ToArray();
            }
        }


  
        public static String UncompressString(byte[] compressed)
        {
            using (var input = new System.IO.MemoryStream(compressed))
            {
                System.IO.Stream decompressor =
                    new DeflateStream(input, CompressionMode.Decompress);

                return ZlibBaseStream.UncompressString(compressed, decompressor);
            }
        }


   
        public static byte[] UncompressBuffer(byte[] compressed)
        {
            using (var input = new System.IO.MemoryStream(compressed))
            {
                System.IO.Stream decompressor =
                    new DeflateStream( input, CompressionMode.Decompress );

                return ZlibBaseStream.UncompressBuffer(compressed, decompressor);
            }
        }

    }

}

