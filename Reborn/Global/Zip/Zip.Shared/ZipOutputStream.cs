using System;
using System.Threading;
using System.Collections.Generic;
using System.IO;
using Ionic.Zip;

namespace Ionic.Zip
{

    public class ZipOutputStream : Stream
    {

        public ZipOutputStream(Stream stream) : this(stream, false) { }


        public ZipOutputStream(Stream stream, bool leaveOpen)
        {
            _Init(stream, leaveOpen, null);
        }

        private void _Init(Stream stream, bool leaveOpen, string name)
        {
            // workitem 9307
            _outputStream = stream.CanRead ? stream : new CountingStream(stream);
            CompressionLevel = Ionic.Zlib.CompressionLevel.Default;
            CompressionMethod = Ionic.Zip.CompressionMethod.Deflate;
            _encryption = EncryptionAlgorithm.None;
            _entriesWritten = new Dictionary<String, ZipEntry>(StringComparer.Ordinal);
            _zip64 = Zip64Option.Never;
            _leaveUnderlyingStreamOpen = leaveOpen;
            Strategy = Ionic.Zlib.CompressionStrategy.Default;
            _name = name ?? "(stream)";
            ParallelDeflateThreshold = -1L;
        }



        public override String ToString()
        {
            return String.Format ("ZipOutputStream::{0}(leaveOpen({1})))", _name, _leaveUnderlyingStreamOpen);
        }




        public int CodecBufferSize
        {
            get;
            set;
        }



        public Ionic.Zlib.CompressionStrategy Strategy
        {
            get;
            set;
        }



        public Ionic.Zlib.CompressionLevel CompressionLevel
        {
            get;
            set;
        }

        public Ionic.Zip.CompressionMethod CompressionMethod
        {
            get;
            set;
        }


        public string Comment
        {
            get { return _comment; }
            set
            {
                if (_disposed)
                {
                    _exceptionPending = true;
                    throw new System.InvalidOperationException("The stream has been closed.");
                }
                _comment = value;
            }
        }



 
        public Zip64Option EnableZip64
        {
            get
            {
                return _zip64;
            }
            set
            {
                if (_disposed)
                {
                    _exceptionPending = true;
                    throw new System.InvalidOperationException("The stream has been closed.");
                }
                _zip64 = value;
            }
        }



        public System.Text.Encoding AlternateEncoding
        {
            get
            {
                return _alternateEncoding;
            }
            set
            {
                _alternateEncoding = value;
            }
        }


        public ZipOption AlternateEncodingUsage
        {
            get
            {
                return _alternateEncodingUsage;
            }
            set
            {
                _alternateEncodingUsage = value;
            }
        }

        public static System.Text.Encoding DefaultEncoding
        {
            get
            {
                return System.Text.Encoding.GetEncoding("IBM437");

            }
        }


        public long ParallelDeflateThreshold
        {
            set
            {
                if ((value != 0) && (value != -1) && (value < 64 * 1024))
                    throw new ArgumentOutOfRangeException("value must be greater than 64k, or 0, or -1");
                _ParallelDeflateThreshold = value;
            }
            get
            {
                return _ParallelDeflateThreshold;
            }
        }


        public int ParallelDeflateMaxBufferPairs
        {
            get
            {
                return _maxBufferPairs;
            }
            set
            {
                if (value < 4)
                    throw new ArgumentOutOfRangeException("ParallelDeflateMaxBufferPairs",
                                                "Value must be 4 or greater.");
                _maxBufferPairs = value;
            }
        }


        internal Stream OutputStream
        {
            get
            {
                return _outputStream;
            }
        }

        internal String Name
        {
            get
            {
                return _name;
            }
        }




        public override void Write(byte[] buffer, int offset, int count)
        {
            if (_disposed)
            {
                _exceptionPending = true;
                throw new System.InvalidOperationException("The stream has been closed.");
            }

            if (buffer==null)
            {
                _exceptionPending = true;
                throw new System.ArgumentNullException("buffer");
            }

            if (_currentEntry == null)
            {
                _exceptionPending = true;
                throw new System.InvalidOperationException("You must call PutNextEntry() before calling Write().");
            }

            if (_currentEntry.IsDirectory)
            {
                _exceptionPending = true;
                throw new System.InvalidOperationException("You cannot Write() data for an entry that is a directory.");
            }

            if (_needToWriteEntryHeader)
                _InitiateCurrentEntry(false);

            if (count != 0)
                _entryOutputStream.Write(buffer, offset, count);
        }



        private void _InitiateCurrentEntry(bool finishing)
        {

            _entriesWritten.Add(_currentEntry.FileName,_currentEntry);
            _entryCount++; // could use _entriesWritten.Count, but I don't want to incur
            // the cost.

            if (_entryCount > 65534 && _zip64 == Zip64Option.Never)
            {
                _exceptionPending = true;
                throw new System.InvalidOperationException("Too many entries. Consider setting ZipOutputStream.EnableZip64.");
            }

            _currentEntry.WriteHeader(_outputStream, finishing ? 99 : 0);
            _currentEntry.StoreRelativeOffset();

            if (!_currentEntry.IsDirectory)
            {
                _currentEntry.WriteSecurityMetadata(_outputStream);
                _currentEntry.PrepOutputStream(_outputStream,
                                               finishing ? 0 : -1,
                                               out _outputCounter,
                                               out _encryptor,
                                               out _deflater,
                                               out _entryOutputStream);
            }
            _needToWriteEntryHeader = false;
        }



        private void _FinishCurrentEntry()
        {
            if (_currentEntry != null)
            {
                if (_needToWriteEntryHeader)
                    _InitiateCurrentEntry(true); // an empty entry - no writes

                _currentEntry.FinishOutputStream(_outputStream, _outputCounter, _encryptor, _deflater, _entryOutputStream);
                _currentEntry.PostProcessOutput(_outputStream);
                // workitem 12964
                if (_currentEntry.OutputUsedZip64!=null)
                    _anyEntriesUsedZip64 |= _currentEntry.OutputUsedZip64.Value;

                // reset all the streams
                _outputCounter = null; _encryptor = _deflater = null; _entryOutputStream = null;
            }
        }




        protected override void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing) // not called from finalizer
            {
                // handle pending exceptions
                if (!_exceptionPending)
                {
                    _FinishCurrentEntry();
                    _directoryNeededZip64 = ZipOutput.WriteCentralDirectoryStructure(_outputStream,
                                                                                     _entriesWritten.Values,
                                                                                     1, // _numberOfSegmentsForMostRecentSave,
                                                                                     _zip64,
                                                                                     Comment,
                                                                                     new ZipContainer(this));
                    Stream wrappedStream = null;
                    CountingStream cs = _outputStream as CountingStream;
                    if (cs != null)
                    {
                        wrappedStream = cs.WrappedStream;
                        cs.Dispose();
                    }
                    else
                    {
                        wrappedStream = _outputStream;
                    }

                    if (!_leaveUnderlyingStreamOpen)
                    {
                        wrappedStream.Dispose();
                    }
                    _outputStream = null;
                }
            }
            _disposed = true;
        }



        public override bool CanRead { get { return false; } }


        public override bool CanSeek { get { return false; } }


        public override bool CanWrite { get { return true; } }


        public override long Length { get { throw new NotSupportedException(); } }


        public override long Position
        {
            get { return _outputStream.Position; }
            set { throw new NotSupportedException(); }
        }


        public override void Flush() { }


        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException("Read");
        }


        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException("Seek");
        }


        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }


#pragma warning disable CS0414 // ѕолю "ZipOutputStream._encryption" присвоено значение, но оно ни разу не использовано.
        private EncryptionAlgorithm _encryption;
#pragma warning restore CS0414 // ѕолю "ZipOutputStream._encryption" присвоено значение, но оно ни разу не использовано.
#pragma warning disable CS0169 // ѕоле "ZipOutputStream._timestamp" никогда не используетс€.
        private ZipEntryTimestamp _timestamp;
#pragma warning restore CS0169 // ѕоле "ZipOutputStream._timestamp" никогда не используетс€.
#pragma warning disable CS0649 // ѕолю "ZipOutputStream._password" нигде не присваиваетс€ значение, поэтому оно всегда будет иметь значение по умолчанию null.
        internal String _password;
#pragma warning restore CS0649 // ѕолю "ZipOutputStream._password" нигде не присваиваетс€ значение, поэтому оно всегда будет иметь значение по умолчанию null.
        private String _comment;
        private Stream _outputStream;
#pragma warning disable CS0649 // ѕолю "ZipOutputStream._currentEntry" нигде не присваиваетс€ значение, поэтому оно всегда будет иметь значение по умолчанию null.
        private ZipEntry _currentEntry;
#pragma warning restore CS0649 // ѕолю "ZipOutputStream._currentEntry" нигде не присваиваетс€ значение, поэтому оно всегда будет иметь значение по умолчанию null.
        internal Zip64Option _zip64;
        private Dictionary<String, ZipEntry> _entriesWritten;
        private int _entryCount;
        private ZipOption _alternateEncodingUsage = ZipOption.Never;
        private System.Text.Encoding _alternateEncoding
            = System.Text.Encoding.GetEncoding("IBM437"); // default = IBM437

        private bool _leaveUnderlyingStreamOpen;
        private bool _disposed;
        private bool _exceptionPending; // **see note below
        private bool _anyEntriesUsedZip64, _directoryNeededZip64;
        private CountingStream _outputCounter;
        private Stream _encryptor;
        private Stream _deflater;
        private Ionic.Crc.CrcCalculatorStream _entryOutputStream;
        private bool _needToWriteEntryHeader;
        private string _name;
#pragma warning disable CS0169 // ѕоле "ZipOutputStream._DontIgnoreCase" никогда не используетс€.
        private bool _DontIgnoreCase;
#pragma warning restore CS0169 // ѕоле "ZipOutputStream._DontIgnoreCase" никогда не используетс€.
        internal Ionic.Zlib.ParallelDeflateOutputStream ParallelDeflater;
        private long _ParallelDeflateThreshold;
        private int _maxBufferPairs = 16;


    }



    internal class ZipContainer
    {
        private ZipFile _zf;
        private ZipOutputStream _zos;
        private ZipInputStream _zis;

        public ZipContainer(Object o)
        {
            _zf = (o as ZipFile);
            _zos = (o as ZipOutputStream);
            _zis = (o as ZipInputStream);
        }

        public ZipFile ZipFile
        {
            get { return _zf; }
        }

        public ZipOutputStream ZipOutputStream
        {
            get { return _zos; }
        }

        public string Name
        {
            get
            {
                if (_zf != null) return _zf.Name;
                if (_zis != null) throw new NotSupportedException();
                return _zos.Name;
            }
        }

        public string Password
        {
            get
            {
                if (_zf != null) return _zf._Password;
                if (_zis != null) return _zis._Password;
                return _zos._password;
            }
        }

        public Zip64Option Zip64
        {
            get
            {
                if (_zf != null) return _zf._zip64;
                if (_zis != null) throw new NotSupportedException();
                return _zos._zip64;
            }
        }

        public int BufferSize
        {
            get
            {
                if (_zf != null) return _zf.BufferSize;
                if (_zis != null) throw new NotSupportedException();
                return 0;
            }
        }

        public Ionic.Zlib.ParallelDeflateOutputStream ParallelDeflater
        {
            get
            {
                if (_zf != null) return _zf.ParallelDeflater;
                if (_zis != null) return null;
                return _zos.ParallelDeflater;
            }
            set
            {
                if (_zf != null) _zf.ParallelDeflater = value;
                else if (_zos != null) _zos.ParallelDeflater = value;
            }
        }

        public long ParallelDeflateThreshold
        {
            get
            {
                if (_zf != null) return _zf.ParallelDeflateThreshold;
                return _zos.ParallelDeflateThreshold;
            }
        }
        public int ParallelDeflateMaxBufferPairs
        {
            get
            {
                if (_zf != null) return _zf.ParallelDeflateMaxBufferPairs;
                return _zos.ParallelDeflateMaxBufferPairs;
            }
        }

        public int CodecBufferSize
        {
            get
            {
                if (_zf != null) return _zf.CodecBufferSize;
                if (_zis != null) return _zis.CodecBufferSize;
                return _zos.CodecBufferSize;
            }
        }

        public Ionic.Zlib.CompressionStrategy Strategy
        {
            get
            {
                if (_zf != null) return _zf.Strategy;
                return _zos.Strategy;
            }
        }

        public Zip64Option UseZip64WhenSaving
        {
            get
            {
                if (_zf != null) return _zf.UseZip64WhenSaving;
                return _zos.EnableZip64;
            }
        }

        public System.Text.Encoding AlternateEncoding
        {
            get
            {
                if (_zf != null) return _zf.AlternateEncoding;
                if (_zos!=null) return _zos.AlternateEncoding;
                return null;
            }
        }
        public System.Text.Encoding DefaultEncoding
        {
            get
            {
                if (_zf != null) return ZipFile.DefaultEncoding;
                if (_zos!=null) return ZipOutputStream.DefaultEncoding;
                return null;
            }
        }
        public ZipOption AlternateEncodingUsage
        {
            get
            {
                if (_zf != null) return _zf.AlternateEncodingUsage;
                if (_zos!=null) return _zos.AlternateEncodingUsage;
                return ZipOption.Never; // n/a
            }
        }

        public Stream ReadStream
        {
            get
            {
                if (_zf != null) return _zf.ReadStream;
                return _zis.ReadStream;
            }
        }
    }

}