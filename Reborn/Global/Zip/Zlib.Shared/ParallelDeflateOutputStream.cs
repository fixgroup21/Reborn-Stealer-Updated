
using System;
using System.Collections.Generic;
using System.Threading;
using Ionic.Zlib;
using System.IO;


namespace Ionic.Zlib
{
    internal class WorkItem
    {
        public byte[] buffer;
        public byte[] compressed;
        public int crc;
        public int index;
        public int ordinal;
        public int inputBytesAvailable;
        public int compressedBytesAvailable;
        public ZlibCodec compressor;

        public WorkItem(int size,
                        Ionic.Zlib.CompressionLevel compressLevel,
                        CompressionStrategy strategy,
                        int ix)
        {
            this.buffer= new byte[size];
            // alloc 5 bytes overhead for every block (margin of safety= 2)
            int n = size + ((size / 32768)+1) * 5 * 2;
            this.compressed = new byte[n];
            this.compressor = new ZlibCodec();
            this.compressor.InitializeDeflate(compressLevel, false);
            this.compressor.OutputBuffer = this.compressed;
            this.compressor.InputBuffer = this.buffer;
            this.index = ix;
        }
    }

    public class ParallelDeflateOutputStream : System.IO.Stream
    {

        private static readonly int IO_BUFFER_SIZE_DEFAULT = 64 * 1024;  // 128k
        private static readonly int BufferPairsPerCore = 4;

        private System.Collections.Generic.List<WorkItem> _pool;
        private bool                        _leaveOpen;
        private bool                        emitting;
        private System.IO.Stream            _outStream;
        private int                         _maxBufferPairs;
        private int                         _bufferSize = IO_BUFFER_SIZE_DEFAULT;
        private AutoResetEvent              _newlyCompressedBlob;
        //private ManualResetEvent            _writingDone;
        //private ManualResetEvent            _sessionReset;
        private object                      _outputLock = new object();
        private bool                        _isClosed;
        private bool                        _firstWriteDone;
        private int                         _currentlyFilling;
        private int                         _lastFilled;
        private int                         _lastWritten;
        private int                         _latestCompressed;
        private int                         _Crc32;
        private Ionic.Crc.CRC32             _runningCrc;
        private object                      _latestLock = new object();
        private System.Collections.Generic.Queue<int>     _toWrite;
        private System.Collections.Generic.Queue<int>     _toFill;
        private Int64                       _totalBytesProcessed;
        private Ionic.Zlib.CompressionLevel _compressLevel;
        private volatile Exception          _pendingException;
        private bool                        _handlingException;
        private object                      _eLock = new Object();  // protects _pendingException

       

        private TraceBits _DesiredTrace =
            TraceBits.Session |
            TraceBits.Compress |
            TraceBits.WriteTake |
            TraceBits.WriteEnter |
            TraceBits.EmitEnter |
            TraceBits.EmitDone |
            TraceBits.EmitLock |
            TraceBits.EmitSkip |
            TraceBits.EmitBegin;

    

        public ParallelDeflateOutputStream(System.IO.Stream stream,
                                           CompressionLevel level,
                                           CompressionStrategy strategy,
                                           bool leaveOpen)
        {
            TraceOutput(TraceBits.Lifecycle | TraceBits.Session, "-------------------------------------------------------");
            TraceOutput(TraceBits.Lifecycle | TraceBits.Session, "Create {0:X8}", this.GetHashCode());
            _outStream = stream;
            _compressLevel= level;
            Strategy = strategy;
            _leaveOpen = leaveOpen;
            this.MaxBufferPairs = 16; // default
        }


        public CompressionStrategy Strategy
        {
            get;
            private set;
        }

        public int MaxBufferPairs
        {
            get
            {
                return _maxBufferPairs;
            }
            set
            {
                if (value < 4)
                    throw new ArgumentException("MaxBufferPairs",
                                                "Value must be 4 or greater.");
                _maxBufferPairs = value;
            }
        }


        public int BufferSize
        {
            get { return _bufferSize;}
            set
            {
                if (value < 1024)
                    throw new ArgumentOutOfRangeException("BufferSize",
                                                          "BufferSize must be greater than 1024 bytes");
                _bufferSize = value;
            }
        }


        private void _InitializePoolOfWorkItems()
        {
            _toWrite = new Queue<int>();
            _toFill = new Queue<int>();
            _pool = new System.Collections.Generic.List<WorkItem>();
            int nTasks = BufferPairsPerCore * Environment.ProcessorCount;
            nTasks = Math.Min(nTasks, _maxBufferPairs);
            for(int i=0; i < nTasks; i++)
            {
                _pool.Add(new WorkItem(_bufferSize, _compressLevel, Strategy, i));
                _toFill.Enqueue(i);
            }

            _newlyCompressedBlob = new AutoResetEvent(false);
            _runningCrc = new Ionic.Crc.CRC32();
            _currentlyFilling = -1;
            _lastFilled = -1;
            _lastWritten = -1;
            _latestCompressed = -1;
        }




      
        public override void Write(byte[] buffer, int offset, int count)
        {
            bool mustWait = false;

            // This method does this:
            //   0. handles any pending exceptions
            //   1. write any buffers that are ready to be written,
            //   2. fills a work buffer; when full, flip state to 'Filled',
            //   3. if more data to be written,  goto step 1

            if (_isClosed)
                throw new InvalidOperationException();

            // dispense any exceptions that occurred on the BG threads
            if (_pendingException != null)
            {
                _handlingException = true;
                var pe = _pendingException;
                _pendingException = null;
                throw pe;
            }

            if (count == 0) return;

            if (!_firstWriteDone)
            {
                // Want to do this on first Write, first session, and not in the
                // constructor.  We want to allow MaxBufferPairs to
                // change after construction, but before first Write.
                _InitializePoolOfWorkItems();
                _firstWriteDone = true;
            }


            do
            {
                // may need to make buffers available
                EmitPendingBuffers(false, mustWait);

                mustWait = false;
                // use current buffer, or get a new buffer to fill
                int ix = -1;
                if (_currentlyFilling >= 0)
                {
                    ix = _currentlyFilling;
                    TraceOutput(TraceBits.WriteTake,
                                "Write    notake   wi({0}) lf({1})",
                                ix,
                                _lastFilled);
                }
                else
                {
                    TraceOutput(TraceBits.WriteTake, "Write    take?");
                    if (_toFill.Count == 0)
                    {
                        // no available buffers, so... need to emit
                        // compressed buffers.
                        mustWait = true;
                        continue;
                    }

                    ix = _toFill.Dequeue();
                    TraceOutput(TraceBits.WriteTake,
                                "Write    take     wi({0}) lf({1})",
                                ix,
                                _lastFilled);
                    ++_lastFilled; // TODO: consider rollover?
                }

                WorkItem workitem = _pool[ix];

                int limit = ((workitem.buffer.Length - workitem.inputBytesAvailable) > count)
                    ? count
                    : (workitem.buffer.Length - workitem.inputBytesAvailable);

                workitem.ordinal = _lastFilled;

                TraceOutput(TraceBits.Write,
                            "Write    lock     wi({0}) ord({1}) iba({2})",
                            workitem.index,
                            workitem.ordinal,
                            workitem.inputBytesAvailable
                            );

                // copy from the provided buffer to our workitem, starting at
                // the tail end of whatever data we might have in there currently.
                Buffer.BlockCopy(buffer,
                                 offset,
                                 workitem.buffer,
                                 workitem.inputBytesAvailable,
                                 limit);

                count -= limit;
                offset += limit;
                workitem.inputBytesAvailable += limit;
                if (workitem.inputBytesAvailable == workitem.buffer.Length)
                {
                    // No need for interlocked.increment: the Write()
                    // method is documented as not multi-thread safe, so
                    // we can assume Write() calls come in from only one
                    // thread.
                    TraceOutput(TraceBits.Write,
                                "Write    QUWI     wi({0}) ord({1}) iba({2}) nf({3})",
                                workitem.index,
                                workitem.ordinal,
                                workitem.inputBytesAvailable );

#if !PCL
                    if (!ThreadPool.QueueUserWorkItem( _DeflateOne, workitem ))
                        throw new Exception("Cannot enqueue workitem");
#else
                    System.Threading.Tasks.Task.Run(() => _DeflateOne(workitem));
#endif

                    _currentlyFilling = -1; // will get a new buffer next time
                }
                else
                    _currentlyFilling = ix;

                if (count > 0)
                    TraceOutput(TraceBits.WriteEnter, "Write    more");
            }
            while (count > 0);  // until no more to write

            TraceOutput(TraceBits.WriteEnter, "Write    exit");
            return;
        }



        private void _FlushFinish()
        {
            // After writing a series of compressed buffers, each one closed
            // with Flush.Sync, we now write the final one as Flush.Finish,
            // and then stop.
            byte[] buffer = new byte[128];
            var compressor = new ZlibCodec();
            int rc = compressor.InitializeDeflate(_compressLevel, false);
            compressor.InputBuffer = null;
            compressor.NextIn = 0;
            compressor.AvailableBytesIn = 0;
            compressor.OutputBuffer = buffer;
            compressor.NextOut = 0;
            compressor.AvailableBytesOut = buffer.Length;
            rc = compressor.Deflate(FlushType.Finish);

            if (rc != ZlibConstants.Z_STREAM_END && rc != ZlibConstants.Z_OK)
                throw new Exception("deflating: " + compressor.Message);

            if (buffer.Length - compressor.AvailableBytesOut > 0)
            {
                TraceOutput(TraceBits.EmitBegin,
                            "Emit     begin    flush bytes({0})",
                            buffer.Length - compressor.AvailableBytesOut);

                _outStream.Write(buffer, 0, buffer.Length - compressor.AvailableBytesOut);

                TraceOutput(TraceBits.EmitDone,
                            "Emit     done     flush");
            }

            compressor.EndDeflate();

            _Crc32 = _runningCrc.Crc32Result;
        }


        private void _Flush(bool lastInput)
        {
            if (_isClosed)
                throw new InvalidOperationException();

            if (emitting) return;

            // compress any partial buffer
            if (_currentlyFilling >= 0)
            {
                WorkItem workitem = _pool[_currentlyFilling];
                _DeflateOne(workitem);
                _currentlyFilling = -1; // get a new buffer next Write()
            }

            if (lastInput)
            {
                EmitPendingBuffers(true, false);
                _FlushFinish();
            }
            else
            {
                EmitPendingBuffers(false, false);
            }
        }



      
        public override void Flush()
        {
            if (_pendingException != null)
            {
                _handlingException = true;
                var pe = _pendingException;
                _pendingException = null;
                throw pe;
            }
            if (_handlingException)
                return;

            _Flush(false);
        }


#if !PCL
    
        public override void Close()
        {
            InnerClose();
        }
#endif

        private void InnerClose()
        {
            TraceOutput(TraceBits.Session, "Close {0:X8}", this.GetHashCode());

            if (_pendingException != null)
            {
                _handlingException = true;
                var pe = _pendingException;
                _pendingException = null;
                throw pe;
            }

            if (_handlingException)
                return;

            if (_isClosed) return;

            _Flush(true);

            if (!_leaveOpen)
                _outStream.Dispose();

            _isClosed= true;
        }



        // workitem 10030 - implement a new Dispose method

       
        new public void Dispose()
        {
            TraceOutput(TraceBits.Lifecycle, "Dispose  {0:X8}", this.GetHashCode());
            _pool = null;
            Dispose(true);
        }



     
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            InnerClose();
        }

        public void Reset(Stream stream)
        {
            TraceOutput(TraceBits.Session, "-------------------------------------------------------");
            TraceOutput(TraceBits.Session, "Reset {0:X8} firstDone({1})", this.GetHashCode(), _firstWriteDone);

            if (!_firstWriteDone) return;

            // reset all status
            _toWrite.Clear();
            _toFill.Clear();
            foreach (var workitem in _pool)
            {
                _toFill.Enqueue(workitem.index);
                workitem.ordinal = -1;
            }

            _firstWriteDone = false;
            _totalBytesProcessed = 0L;
            _runningCrc = new Ionic.Crc.CRC32();
            _isClosed= false;
            _currentlyFilling = -1;
            _lastFilled = -1;
            _lastWritten = -1;
            _latestCompressed = -1;
            _outStream = stream;
        }




        private void EmitPendingBuffers(bool doAll, bool mustWait)
        {
            // When combining parallel deflation with a ZipSegmentedStream, it's
            // possible for the ZSS to throw from within this method.  In that
            // case, Close/Dispose will be called on this stream, if this stream
            // is employed within a using or try/finally pair as required. But
            // this stream is unaware of the pending exception, so the Close()
            // method invokes this method AGAIN.  This can lead to a deadlock.
            // Therefore, failfast if re-entering.

            if (emitting) return;
            emitting = true;
            if ((doAll && (_latestCompressed != _lastFilled)) || mustWait) {
                _newlyCompressedBlob.WaitOne();
            }

            do
            {
                int firstSkip = -1;
                int millisecondsToWait = doAll ? 200 : (mustWait ? -1 : 0);
                int nextToWrite = -1;

                do
                {
                    if (Monitor.TryEnter(_toWrite, millisecondsToWait))
                    {
                        nextToWrite = -1;
                        try
                        {
                            if (_toWrite.Count > 0)
                                nextToWrite = _toWrite.Dequeue();
                        }
                        finally
                        {
                            Monitor.Exit(_toWrite);
                        }

                        if (nextToWrite >= 0)
                        {
                            WorkItem workitem = _pool[nextToWrite];
                            if (workitem.ordinal != _lastWritten + 1)
                            {
                                // out of order. requeue and try again.
                                TraceOutput(TraceBits.EmitSkip,
                                            "Emit     skip     wi({0}) ord({1}) lw({2}) fs({3})",
                                            workitem.index,
                                            workitem.ordinal,
                                            _lastWritten,
                                            firstSkip);

                                lock(_toWrite)
                                {
                                    _toWrite.Enqueue(nextToWrite);
                                }

                                if (firstSkip == nextToWrite)
                                {
                                    // We went around the list once.
                                    // None of the items in the list is the one we want.
                                    // Now wait for a compressor to signal again.
                                    _newlyCompressedBlob.WaitOne();
                                    firstSkip = -1;
                                }
                                else if (firstSkip == -1)
                                    firstSkip = nextToWrite;

                                continue;
                            }

                            firstSkip = -1;

                            TraceOutput(TraceBits.EmitBegin,
                                        "Emit     begin    wi({0}) ord({1})              cba({2})",
                                        workitem.index,
                                        workitem.ordinal,
                                        workitem.compressedBytesAvailable);

                            _outStream.Write(workitem.compressed, 0, workitem.compressedBytesAvailable);
                            _runningCrc.Combine(workitem.crc, workitem.inputBytesAvailable);
                            _totalBytesProcessed += workitem.inputBytesAvailable;
                            workitem.inputBytesAvailable = 0;

                            TraceOutput(TraceBits.EmitDone,
                                        "Emit     done     wi({0}) ord({1})              cba({2}) mtw({3})",
                                        workitem.index,
                                        workitem.ordinal,
                                        workitem.compressedBytesAvailable,
                                        millisecondsToWait);

                            _lastWritten = workitem.ordinal;
                            _toFill.Enqueue(workitem.index);

                            // don't wait next time through
                            if (millisecondsToWait == -1) millisecondsToWait = 0;
                        }
                    }
                    else
                        nextToWrite = -1;

                } while (nextToWrite >= 0);

            } while (doAll && (_lastWritten != _latestCompressed || _lastWritten != _lastFilled));

            emitting = false;
        }



#if OLD
        private void _PerpetualWriterMethod(object state)
        {
            TraceOutput(TraceBits.WriterThread, "_PerpetualWriterMethod START");

            try
            {
                do
                {
                    // wait for the next session
                    TraceOutput(TraceBits.Synch | TraceBits.WriterThread, "Synch    _sessionReset.WaitOne(begin) PWM");
                    _sessionReset.WaitOne();
                    TraceOutput(TraceBits.Synch | TraceBits.WriterThread, "Synch    _sessionReset.WaitOne(done)  PWM");

                    if (_isDisposed) break;

                    TraceOutput(TraceBits.Synch | TraceBits.WriterThread, "Synch    _sessionReset.Reset()        PWM");
                    _sessionReset.Reset();

                    // repeatedly write buffers as they become ready
                    WorkItem workitem = null;
                    Ionic.Zlib.CRC32 c= new Ionic.Zlib.CRC32();
                    do
                    {
                        workitem = _pool[_nextToWrite % _pc];
                        lock(workitem)
                        {
                            if (_noMoreInputForThisSegment)
                                TraceOutput(TraceBits.Write,
                                               "Write    drain    wi({0}) stat({1}) canuse({2})  cba({3})",
                                               workitem.index,
                                               workitem.status,
                                               (workitem.status == (int)WorkItem.Status.Compressed),
                                               workitem.compressedBytesAvailable);

                            do
                            {
                                if (workitem.status == (int)WorkItem.Status.Compressed)
                                {
                                    TraceOutput(TraceBits.WriteBegin,
                                                   "Write    begin    wi({0}) stat({1})              cba({2})",
                                                   workitem.index,
                                                   workitem.status,
                                                   workitem.compressedBytesAvailable);

                                    workitem.status = (int)WorkItem.Status.Writing;
                                    _outStream.Write(workitem.compressed, 0, workitem.compressedBytesAvailable);
                                    c.Combine(workitem.crc, workitem.inputBytesAvailable);
                                    _totalBytesProcessed += workitem.inputBytesAvailable;
                                    _nextToWrite++;
                                    workitem.inputBytesAvailable= 0;
                                    workitem.status = (int)WorkItem.Status.Done;

                                    TraceOutput(TraceBits.WriteDone,
                                                   "Write    done     wi({0}) stat({1})              cba({2})",
                                                   workitem.index,
                                                   workitem.status,
                                                   workitem.compressedBytesAvailable);


                                    Monitor.Pulse(workitem);
                                    break;
                                }
                                else
                                {
                                    int wcycles = 0;
                                    // I've locked a workitem I cannot use.
                                    // Therefore, wake someone else up, and then release the lock.
                                    while (workitem.status != (int)WorkItem.Status.Compressed)
                                    {
                                        TraceOutput(TraceBits.WriteWait,
                                                       "Write    waiting  wi({0}) stat({1}) nw({2}) nf({3}) nomore({4})",
                                                       workitem.index,
                                                       workitem.status,
                                                       _nextToWrite, _nextToFill,
                                                       _noMoreInputForThisSegment );

                                        if (_noMoreInputForThisSegment && _nextToWrite == _nextToFill)
                                            break;

                                        wcycles++;

                                        // wake up someone else
                                        Monitor.Pulse(workitem);
                                        // release and wait
                                        Monitor.Wait(workitem);

                                        if (workitem.status == (int)WorkItem.Status.Compressed)
                                            TraceOutput(TraceBits.WriteWait,
                                                           "Write    A-OK     wi({0}) stat({1}) iba({2}) cba({3}) cyc({4})",
                                                           workitem.index,
                                                           workitem.status,
                                                           workitem.inputBytesAvailable,
                                                           workitem.compressedBytesAvailable,
                                                           wcycles);
                                    }

                                    if (_noMoreInputForThisSegment && _nextToWrite == _nextToFill)
                                        break;

                                }
                            }
                            while (true);
                        }

                        if (_noMoreInputForThisSegment)
                            TraceOutput(TraceBits.Write,
                                           "Write    nomore  nw({0}) nf({1}) break({2})",
                                           _nextToWrite, _nextToFill, (_nextToWrite == _nextToFill));

                        if (_noMoreInputForThisSegment && _nextToWrite == _nextToFill)
                            break;

                    } while (true);


                    // Finish:
                    // After writing a series of buffers, closing each one with
                    // Flush.Sync, we now write the final one as Flush.Finish, and
                    // then stop.
                    byte[] buffer = new byte[128];
                    ZlibCodec compressor = new ZlibCodec();
                    int rc = compressor.InitializeDeflate(_compressLevel, false);
                    compressor.InputBuffer = null;
                    compressor.NextIn = 0;
                    compressor.AvailableBytesIn = 0;
                    compressor.OutputBuffer = buffer;
                    compressor.NextOut = 0;
                    compressor.AvailableBytesOut = buffer.Length;
                    rc = compressor.Deflate(FlushType.Finish);

                    if (rc != ZlibConstants.Z_STREAM_END && rc != ZlibConstants.Z_OK)
                        throw new Exception("deflating: " + compressor.Message);

                    if (buffer.Length - compressor.AvailableBytesOut > 0)
                    {
                        TraceOutput(TraceBits.WriteBegin,
                                       "Write    begin    flush bytes({0})",
                                       buffer.Length - compressor.AvailableBytesOut);

                        _outStream.Write(buffer, 0, buffer.Length - compressor.AvailableBytesOut);

                        TraceOutput(TraceBits.WriteBegin,
                                       "Write    done     flush");
                    }

                    compressor.EndDeflate();

                    _Crc32 = c.Crc32Result;

                    // signal that writing is complete:
                    TraceOutput(TraceBits.Synch, "Synch    _writingDone.Set()           PWM");
                    _writingDone.Set();
                }
                while (true);
            }
            catch (System.Exception exc1)
            {
                lock(_eLock)
                {
                    // expose the exception to the main thread
                    if (_pendingException!=null)
                        _pendingException = exc1;
                }
            }

            TraceOutput(TraceBits.WriterThread, "_PerpetualWriterMethod FINIS");
        }
#endif




        private void _DeflateOne(Object wi)
        {
            // compress one buffer
            WorkItem workitem = (WorkItem) wi;
            try
            {
                int myItem = workitem.index;
                Ionic.Crc.CRC32 crc = new Ionic.Crc.CRC32();

                // calc CRC on the buffer
                crc.SlurpBlock(workitem.buffer, 0, workitem.inputBytesAvailable);

                // deflate it
                DeflateOneSegment(workitem);

                // update status
                workitem.crc = crc.Crc32Result;
                TraceOutput(TraceBits.Compress,
                            "Compress          wi({0}) ord({1}) len({2})",
                            workitem.index,
                            workitem.ordinal,
                            workitem.compressedBytesAvailable
                            );

                lock(_latestLock)
                {
                    if (workitem.ordinal > _latestCompressed)
                        _latestCompressed = workitem.ordinal;
                }
                lock (_toWrite)
                {
                    _toWrite.Enqueue(workitem.index);
                }
                _newlyCompressedBlob.Set();
            }
            catch (System.Exception exc1)
            {
                lock(_eLock)
                {
                    // expose the exception to the main thread
                    if (_pendingException!=null)
                        _pendingException = exc1;
                }
            }
        }




        private bool DeflateOneSegment(WorkItem workitem)
        {
            ZlibCodec compressor = workitem.compressor;
            int rc= 0;
            compressor.ResetDeflate();
            compressor.NextIn = 0;

            compressor.AvailableBytesIn = workitem.inputBytesAvailable;

            // step 1: deflate the buffer
            compressor.NextOut = 0;
            compressor.AvailableBytesOut =  workitem.compressed.Length;
            do
            {
                compressor.Deflate(FlushType.None);
            }
            while (compressor.AvailableBytesIn > 0 || compressor.AvailableBytesOut == 0);

            // step 2: flush (sync)
            rc = compressor.Deflate(FlushType.Sync);

            workitem.compressedBytesAvailable= (int) compressor.TotalBytesOut;
            return true;
        }


        [System.Diagnostics.ConditionalAttribute("Trace")]
        private void TraceOutput(TraceBits bits, string format, params object[] varParams)
        {
            if ((bits & _DesiredTrace) != 0)
            {
                lock(_outputLock)
                {
#if !PCL
                    int tid = Thread.CurrentThread.GetHashCode();

                    Console.ForegroundColor = (ConsoleColor) (tid % 8 + 8);
                    Console.Write("{0:000} PDOS ", tid);
                    Console.WriteLine(format, varParams);
                    Console.ResetColor();
#endif
                }
            }
        }


        // used only when Trace is defined
        [Flags]
        enum TraceBits : uint
        {
            None         = 0,
            NotUsed1     = 1,
            EmitLock     = 2,
            EmitEnter    = 4,    // enter _EmitPending
            EmitBegin    = 8,    // begin to write out
            EmitDone     = 16,   // done writing out
            EmitSkip     = 32,   // writer skipping a workitem
            EmitAll      = 58,   // All Emit flags
            Flush        = 64,
            Lifecycle    = 128,  // constructor/disposer
            Session      = 256,  // Close/Reset
            Synch        = 512,  // thread synchronization
            Instance     = 1024, // instance settings
            Compress     = 2048,  // compress task
            Write        = 4096,    // filling buffers, when caller invokes Write()
            WriteEnter   = 8192,    // upon entry to Write()
            WriteTake    = 16384,    // on _toFill.Take()
            All          = 0xffffffff,
        }



   
        public override bool CanSeek
        {
            get { return false; }
        }


        public override bool CanRead
        {
            get {return false;}
        }

    
        public override bool CanWrite
        {
            get { return _outStream.CanWrite; }
        }

      
        public override long Length
        {
            get { throw new NotSupportedException(); }
        }

      
        public override long Position
        {
            get { return _outStream.Position; }
            set { throw new NotSupportedException(); }
        }

   
        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, System.IO.SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

      
        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

    }

}

