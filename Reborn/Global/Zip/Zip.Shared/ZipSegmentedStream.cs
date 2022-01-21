

using System;
using System.Collections.Generic;
using System.IO;

namespace Ionic.Zip
{
    internal class ZipSegmentedStream : System.IO.Stream
    {
        enum RwMode
        {
            None = 0,
            ReadOnly = 1,
            Write = 2,
            //Update = 3
        }

        private RwMode rwMode;
        private bool _exceptionPending; // **see note below
        private string _baseName;
        private string _baseDir;
        private string _currentName;
        private string _currentTempName;
        private uint _currentDiskNumber;
        private uint _maxDiskNumber;
        private long _maxSegmentSize;
        private System.IO.Stream _innerStream;



        private ZipSegmentedStream() : base()
        {
            _exceptionPending = false;
        }

        public static ZipSegmentedStream ForReading(string name,
                                                    uint initialDiskNumber,
                                                    uint maxDiskNumber)
        {
            ZipSegmentedStream zss = new ZipSegmentedStream()
                {
                    rwMode = RwMode.ReadOnly,
                    CurrentSegment = initialDiskNumber,
                    _maxDiskNumber = maxDiskNumber,
                    _baseName = name,
                };

            // Console.WriteLine("ZSS: ForReading ({0})",
            //                    Path.GetFileName(zss.CurrentName));

            zss._SetReadStream();

            return zss;
        }

        public static ZipSegmentedStream ForWriting(string name, long maxSegmentSize)
        {
            ZipSegmentedStream zss = new ZipSegmentedStream()
                {
                    rwMode = RwMode.Write,
                    CurrentSegment = 0,
                    _baseName = name,
                    _maxSegmentSize = maxSegmentSize,
                    _baseDir = Path.GetDirectoryName(name)
                };

            // workitem 9522
            if (zss._baseDir=="") zss._baseDir=".";

            zss._SetWriteStream(0);

            // Console.WriteLine("ZSS: ForWriting ({0})",
            //                    Path.GetFileName(zss.CurrentName));

            return zss;
        }



        public static Stream ForUpdate(string name, uint diskNumber)
        {

            string fname =
                String.Format("{0}.z{1:D2}",
                                 Path.Combine(Path.GetDirectoryName(name),
                                              Path.GetFileNameWithoutExtension(name)),
                                 diskNumber + 1);

            // Console.WriteLine("ZSS: ForUpdate ({0})",
            //                   Path.GetFileName(fname));

            // This class assumes that the update will not expand the
            // size of the segment. Update is used only for an in-place
            // update of zip metadata. It never will try to write beyond
            // the end of a segment.

            return File.Open(fname,
                             FileMode.Open,
                             FileAccess.ReadWrite,
                             FileShare.None);
        }

        public bool ContiguousWrite
        {
            get;
            set;
        }


        public UInt32 CurrentSegment
        {
            get
            {
                return _currentDiskNumber;
            }
            private set
            {
                _currentDiskNumber = value;
                _currentName = null; // it will get updated next time referenced
            }
        }


        public String CurrentName
        {
            get
            {
                if (_currentName==null)
                    _currentName = _NameForSegment(CurrentSegment);

                return _currentName;
            }
        }


        public String CurrentTempName
        {
            get
            {
                return _currentTempName;
            }
        }

        private string _NameForSegment(uint diskNumber)
        {

            return String.Format("{0}.z{1:D2}",
                                 Path.Combine(Path.GetDirectoryName(_baseName),
                                              Path.GetFileNameWithoutExtension(_baseName)),
                                 diskNumber + 1);
        }



        public UInt32 ComputeSegment(int length)
        {
            if (_innerStream.Position + length > _maxSegmentSize)
                // the block will go AT LEAST into the next segment
                return CurrentSegment + 1;

            // it will fit in the current segment
            return CurrentSegment;
        }


        public override String ToString()
        {
            return String.Format("{0}[{1}][{2}], pos=0x{3:X})",
                                 "ZipSegmentedStream", CurrentName,
                                 rwMode.ToString(),
                                 this.Position);
        }


        private void _SetReadStream()
        {
            if (_innerStream != null)
            {
                _innerStream.Dispose();
            }

            if (CurrentSegment + 1 == _maxDiskNumber)
                _currentName = _baseName;

            // Console.WriteLine("ZSS: SRS ({0})",
            //                   Path.GetFileName(CurrentName));

            _innerStream = File.OpenRead(CurrentName);
        }


  
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (rwMode != RwMode.ReadOnly)
            {
                _exceptionPending = true;
                throw new InvalidOperationException("Stream Error: Cannot Read.");
            }

            int r = _innerStream.Read(buffer, offset, count);
            int r1 = r;

            while (r1 != count)
            {
                if (_innerStream.Position != _innerStream.Length)
                {
                    _exceptionPending = true;
                    throw new ZipException(String.Format("Read error in file {0}", CurrentName));

                }

                if (CurrentSegment + 1 == _maxDiskNumber)
                    return r; // no more to read

                CurrentSegment++;
                _SetReadStream();
                offset += r1;
                count -= r1;
                r1 = _innerStream.Read(buffer, offset, count);
                r += r1;
            }
            return r;
        }



        private void _SetWriteStream(uint increment)
        {
            if (_innerStream != null)
            {
                _innerStream.Dispose();
                if (File.Exists(CurrentName))
                    File.Delete(CurrentName);
                File.Move(_currentTempName, CurrentName);
                // Console.WriteLine("ZSS: SWS close ({0})",
                //                   Path.GetFileName(CurrentName));
            }

            if (increment > 0)
                CurrentSegment += increment;

            SharedUtilities.CreateAndOpenUniqueTempFile(_baseDir,
                                                        out _innerStream,
                                                        out _currentTempName);

            // Console.WriteLine("ZSS: SWS open ({0})",
            //                   Path.GetFileName(_currentTempName));

            if (CurrentSegment == 0)
                _innerStream.Write(BitConverter.GetBytes(ZipConstants.SplitArchiveSignature), 0, 4);
        }



        public override void Write(byte[] buffer, int offset, int count)
        {
            if (rwMode != RwMode.Write)
            {
                _exceptionPending = true;
                throw new InvalidOperationException("Stream Error: Cannot Write.");
            }


            if (ContiguousWrite)
            {
                // enough space for a contiguous write?
                if (_innerStream.Position + count > _maxSegmentSize)
                    _SetWriteStream(1);
            }
            else
            {
                while (_innerStream.Position + count > _maxSegmentSize)
                {
                    long c = _maxSegmentSize - _innerStream.Position;
                    int cnt;
                    if (c > buffer.Length)
                    {
                        cnt = buffer.Length;
                    }
                    else
                    {
                        cnt = (int)c;
                    }

                    _innerStream.Write(buffer, offset, cnt);

                    _SetWriteStream(1);
                    count -= cnt;
                    offset += cnt;
                }
            }

            _innerStream.Write(buffer, offset, count);
        }


        public long TruncateBackward(uint diskNumber, long offset)
        {
            // Console.WriteLine("***ZSS.Trunc to disk {0}", diskNumber);
            // Console.WriteLine("***ZSS.Trunc:  current disk {0}", CurrentSegment);

            if (rwMode != RwMode.Write)
            {
                _exceptionPending = true;
                throw new ZipException("bad state.");
            }

            // Seek back in the segmented stream to a (maybe) prior segment.

            // Check if it is the same segment.  If it is, very simple.
            if (diskNumber == CurrentSegment)
            {
                var x =_innerStream.Seek(offset, SeekOrigin.Begin);
                return x;
            }

            // Seeking back to a prior segment.
            // The current segment and any intervening segments must be removed.
            // First, close the current segment, and then remove it.
            if (_innerStream != null)
            {
                _innerStream.Dispose();
                if (File.Exists(_currentTempName))
                    File.Delete(_currentTempName);
            }

            // Now, remove intervening segments.
            for (uint j= CurrentSegment-1; j > diskNumber; j--)
            {
                string s = _NameForSegment(j);
                // Console.WriteLine("***ZSS.Trunc:  removing file {0}", s);
                if (File.Exists(s))
                    File.Delete(s);
            }

            // now, open the desired segment.  It must exist.
            CurrentSegment = diskNumber;

            // get a new temp file, try 3 times:
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    _currentTempName = Path.Combine(Path.GetDirectoryName(CurrentName), 
                                                    SharedUtilities.InternalGetTempFileName());
                    // move the .z0x file back to a temp name
                    File.Move(CurrentName, _currentTempName);
                    break; // workitem 12403
                }
                catch(IOException)
                {
                    if (i == 2) throw;
                }
            }

            // open it
            _innerStream = new FileStream(_currentTempName, FileMode.Open);

            var r =  _innerStream.Seek(offset, SeekOrigin.Begin);

            return r;
        }



        public override bool CanRead
        {
            get
            {
                return (rwMode == RwMode.ReadOnly &&
                        (_innerStream != null) &&
                        _innerStream.CanRead);
            }
        }


        public override bool CanSeek
        {
            get
            {
                return (_innerStream != null) &&
                        _innerStream.CanSeek;
            }
        }


        public override bool CanWrite
        {
            get
            {
                return (rwMode == RwMode.Write) &&
                        (_innerStream != null) &&
                        _innerStream.CanWrite;
            }
        }

        public override void Flush()
        {
            _innerStream.Flush();
        }

        public override long Length
        {
            get
            {
                return _innerStream.Length;
            }
        }

        public override long Position
        {
            get { return _innerStream.Position; }
            set { _innerStream.Position = value; }
        }

        public override long Seek(long offset, System.IO.SeekOrigin origin)
        {
            var x = _innerStream.Seek(offset, origin);
            return x;
        }

        public override void SetLength(long value)
        {
            if (rwMode != RwMode.Write)
            {
                _exceptionPending = true;
                throw new InvalidOperationException();
            }
            _innerStream.SetLength(value);
        }


        protected override void Dispose(bool disposing)
        {

            try
            {
                if (_innerStream != null)
                {
                    _innerStream.Dispose();
                    //_innerStream = null;
                    if (rwMode == RwMode.Write)
                    {
                        if (_exceptionPending)
                        {
                            // possibly could try to clean up all the
                            // temp files created so far...
                        }
                        else
                        {
                            // // move the final temp file to the .zNN name
                            // if (File.Exists(CurrentName))
                            //     File.Delete(CurrentName);
                            // if (File.Exists(_currentTempName))
                            //     File.Move(_currentTempName, CurrentName);
                        }
                    }
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

    }

}