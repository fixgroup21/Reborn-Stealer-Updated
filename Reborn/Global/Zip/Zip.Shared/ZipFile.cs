

using System;
using System.IO;
using System.Collections.Generic;
using Interop = System.Runtime.InteropServices;


namespace Ionic.Zip
{

    [Interop.GuidAttribute("ebc25cf6-9120-4283-b972-0e5520d00005")]
    [Interop.ComVisible(true)]
    [Interop.ClassInterface(Interop.ClassInterfaceType.AutoDispatch)]
    public partial class ZipFile :
    System.Collections.IEnumerable,
    System.Collections.Generic.IEnumerable<ZipEntry>,
    IDisposable
    {

        #region public properties


        public bool FullScan
        {
            get;
            set;
        }

        public bool SortEntriesBeforeSaving
        {
            get;
            set;
        }



        public bool AddDirectoryWillTraverseReparsePoints { get; set; }


        public int BufferSize
        {
            get { return _BufferSize; }
            set { _BufferSize = value; }
        }


        public int CodecBufferSize
        {
            get;
            set;
        }

  


        public Ionic.Zlib.CompressionStrategy Strategy
        {
            get { return _Strategy; }
            set { _Strategy = value; }
        }


        public string Name
        {
            get { return _name; }
            set { _name = value; }
        }

        public Ionic.Zlib.CompressionLevel CompressionLevel
        {
            get;
            set;
        }


        public Ionic.Zip.CompressionMethod CompressionMethod
        {
            get
            {
                return _compressionMethod;
            }
            set
            {
                _compressionMethod = value;
            }
        }


        public string Comment
        {
            get { return _Comment; }
            set
            {
                _Comment = value;
                _contentsChanged = true;
            }
        }


   

        internal bool Verbose
        {
            get { return (_StatusMessageTextWriter != null); }
        }



        public bool CaseSensitiveRetrieval
        {
            get
            {
                return _CaseSensitiveRetrieval;
            }
            set
            {
                _CaseSensitiveRetrieval = value;
            }
        }


        private Dictionary<string, ZipEntry> RetrievalEntries
        {
            get { return CaseSensitiveRetrieval ? _entries : _entriesInsensitive; }
        }


        public bool IgnoreDuplicateFiles
        {
            get { return _IgnoreDuplicateFiles; }
            set { _IgnoreDuplicateFiles = value; }
        }

        public Zip64Option UseZip64WhenSaving
        {
            get
            {
                return _zip64;
            }
            set
            {
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

        public TextWriter StatusMessageTextWriter
        {
            get { return _StatusMessageTextWriter; }
            set { _StatusMessageTextWriter = value; }
        }

        public String TempFileFolder
        {
            get { return _TempFileFolder; }

            set
            {
                _TempFileFolder = value;
                if (value == null) return;

                if (!Directory.Exists(value))
                    throw new FileNotFoundException(String.Format("That directory ({0}) does not exist.", value));

            }
        }


  


        public ZipErrorAction ZipErrorAction
        {
            get
            {
                if (ZipError != null)
                    _zipErrorAction = ZipErrorAction.InvokeErrorEvent;
                return _zipErrorAction;
            }
            set
            {
                _zipErrorAction = value;
                if (_zipErrorAction != ZipErrorAction.InvokeErrorEvent && ZipError != null)
                    ZipError = null;
            }
        }

        public EncryptionAlgorithm Encryption
        {
            get
            {
                return _Encryption;
            }
            set
            {
                if (value == EncryptionAlgorithm.Unsupported)
                    throw new InvalidOperationException("You may not set Encryption to that value.");
                _Encryption = value;
            }
        }


        public SetCompressionCallback SetCompression
        {
            get;
            set;
        }


 

        public Int64 MaxOutputSegmentSize64
        {
            get
            {
                return _maxOutputSegmentSize;
            }
            set
            {
                if (value < 65536 && value != 0)
                    throw new ZipException("The minimum acceptable segment size is 65536.");
                _maxOutputSegmentSize = value;
            }
        }


 


        public long ParallelDeflateThreshold
        {
            set
            {
                if ((value != 0) && (value != -1) && (value < 64 * 1024))
                    throw new ArgumentOutOfRangeException("ParallelDeflateThreshold should be -1, 0, or > 65536");
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


        public override String ToString()
        {
            return String.Format("ZipFile::{0}", Name);
        }



        internal void NotifyEntryChanged()
        {
            _contentsChanged = true;
        }


        internal Stream StreamForDiskNumber(uint diskNumber)
        {
            if (diskNumber + 1 == this._diskNumberWithCd ||
                (diskNumber == 0 && this._diskNumberWithCd == 0))
            {
                //return (this.ReadStream as FileStream);
                return this.ReadStream;
            }
            return ZipSegmentedStream.ForReading(this._readName ?? this._name,
                                                 diskNumber, _diskNumberWithCd);
        }



        // called by ZipEntry in ZipEntry.Extract(), when there is no stream set for the
        // ZipEntry.
        internal void Reset(bool whileSaving)
        {
            if (_JustSaved)
            {
                // read in the just-saved zip archive
                using (ZipFile x = new ZipFile())
                {
                    if (File.Exists(this._readName ?? this._name))
                    {
                        // workitem 10735
                        x._readName = x._name = whileSaving
                            ? (this._readName ?? this._name)
                            : this._name;
                    }
                    else // if we just saved to a stream no file is available to read from
                    {
                        if (_readstream.CanSeek)
                            _readstream.Seek(0, SeekOrigin.Begin);
                        x._readstream = _readstream;
                    }
                    x.AlternateEncoding = this.AlternateEncoding;
                    x.AlternateEncodingUsage = this.AlternateEncodingUsage;
                    ReadIntoInstance(x);
                    // copy the contents of the entries.
                    // cannot just replace the entries - the app may be holding them
                    foreach (ZipEntry e1 in x)
                    {
                        var e2 = this[e1.FileName];
                        if (e2 != null && !e2.IsChanged)
                        {
                            e2.CopyMetaData(e1);
                        }
                    }
                }
                _JustSaved = false;
            }
        }


        #endregion

        #region Constructors

        




        public ZipFile()
        {
            if (DefaultEncoding == null)
            {
                _alternateEncoding = System.Text.Encoding.UTF8;
                AlternateEncodingUsage = ZipOption.Always;
            }
            else
            {
                _alternateEncoding = DefaultEncoding;
            }
            _InitInstance(null, null);
        }


    
        public ZipFile(System.Text.Encoding encoding)
        {
            AlternateEncoding = encoding;
            AlternateEncodingUsage = ZipOption.Always;
            _InitInstance(null, null);
        }



     

        private void _InitInstance(string zipFileName, TextWriter statusMessageWriter)
        {
            // create a new zipfile
            _name = zipFileName;
            _StatusMessageTextWriter = statusMessageWriter;
            _contentsChanged = true;
            AddDirectoryWillTraverseReparsePoints = true;  // workitem 8617
            CompressionLevel = Ionic.Zlib.CompressionLevel.Default;
            ParallelDeflateThreshold = 512 * 1024;
            // workitem 7685, 9868
            _entries = new Dictionary<string, ZipEntry>(StringComparer.Ordinal);
            _entriesInsensitive = new Dictionary<string, ZipEntry>(StringComparer.OrdinalIgnoreCase);

            if (File.Exists(_name))
            {
                if (FullScan)
                    ReadIntoInstance_Orig(this);
                else
                    ReadIntoInstance(this);
                this._fileAlreadyExists = true;
            }

            return;
        }
        #endregion



        #region Indexers and Collections

      
        public ZipEntry this[String fileName]
        {
            get
            {
                var entries = RetrievalEntries;
                var key = SharedUtilities.NormalizePathForUseInZipFile(fileName);
                if (entries.ContainsKey(key))
                    return entries[key];
                // workitem 11056
                key = key.Replace("/", "\\");
                if (entries.ContainsKey(key))
                    return entries[key];
                return null;


            }
        }


        public System.Collections.Generic.ICollection<ZipEntry> Entries
        {
            get
            {
                return _entries.Values;
            }
        }


        public System.Collections.Generic.ICollection<ZipEntry> EntriesSorted
        {
            get
            {
                var coll = new System.Collections.Generic.List<ZipEntry>();
                foreach (var e in this.Entries)
                {
                    coll.Add(e);
                }
                StringComparison sc = (CaseSensitiveRetrieval) ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

                coll.Sort((x, y) => { return String.Compare(x.FileName, y.FileName, sc); });
                return coll.AsReadOnly();
            }
        }


    

  
        public void RemoveEntry(ZipEntry entry)
        {
            //if (!_entries.Values.Contains(entry))
            //    throw new ArgumentException("The entry you specified does not exist in the zip archive.");
            if (entry == null)
                throw new ArgumentNullException("entry");

            var path = SharedUtilities.NormalizePathForUseInZipFile(entry.FileName);
            _entries.Remove(path);
            if (!AnyCaseInsensitiveMatches(path))
                _entriesInsensitive.Remove(path);
            _zipEntriesAsList = null;

#if NOTNEEDED
            if (_direntries != null)
            {
                bool FoundAndRemovedDirEntry = false;
                foreach (ZipDirEntry de1 in _direntries)
                {
                    if (entry.FileName == de1.FileName)
                    {
                        _direntries.Remove(de1);
                        FoundAndRemovedDirEntry = true;
                        break;
                    }
                }

                if (!FoundAndRemovedDirEntry)
                    throw new BadStateException("The entry to be removed was not found in the directory.");
            }
#endif
            _contentsChanged = true;
        }


        private bool AnyCaseInsensitiveMatches(string path)
        {
            // this has to search _entries rather than _caseInsensitiveEntries because it's used to determine whether to update the latter
            foreach (var entry in _entries.Values)
            {
                if (String.Equals(entry.FileName, path, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }


       

        public void RemoveEntry(String fileName)
        {
            string modifiedName = ZipEntry.NameInArchive(fileName, null);
            ZipEntry e = this[modifiedName];
            if (e == null)
                throw new ArgumentException("The entry you specified was not found in the zip archive.");

            RemoveEntry(e);
        }


        #endregion

        #region Destructors and Disposers

      
        public void Dispose()
        {
            // dispose of the managed and unmanaged resources
            Dispose(true);

            // tell the GC that the Finalize process no longer needs
            // to be run for this object.
            GC.SuppressFinalize(this);
        }


        protected virtual void Dispose(bool disposeManagedResources)
        {
            if (!this._disposed)
            {
                if (disposeManagedResources)
                {
                    // dispose managed resources
                    if (_ReadStreamIsOurs)
                    {
                        if (_readstream != null)
                        {
                            // workitem 7704
                            _readstream.Dispose();
                            _readstream = null;
                        }
                    }
                    // only dispose the writestream if there is a backing file
                    if ((_temporaryFileName != null) && (_name != null))
                        if (_writestream != null)
                        {
                            // workitem 7704
                            _writestream.Dispose();
                            _writestream = null;
                        }

                    // workitem 10030
                    if (this.ParallelDeflater != null)
                    {
                        this.ParallelDeflater.Dispose();
                        this.ParallelDeflater = null;
                    }
                }
                this._disposed = true;
            }
        }
        #endregion


        #region private properties

        internal Stream ReadStream
        {
            get
            {
                if (_readstream == null)
                {
                    if (_readName != null || _name !=null)
                    {
                        _readstream = File.Open(_readName ?? _name,
                                                FileMode.Open,
                                                FileAccess.Read,
                                                FileShare.Read | FileShare.Write);
                        _ReadStreamIsOurs = true;
                    }
                }
                return _readstream;
            }
        }



        private Stream WriteStream
        {
            // workitem 9763
            get
            {
                if (_writestream != null) return _writestream;
                if (_name == null) return _writestream;

                if (_maxOutputSegmentSize != 0)
                {
                    _writestream = ZipSegmentedStream.ForWriting(this._name, _maxOutputSegmentSize);
                    return _writestream;
                }

                SharedUtilities.CreateAndOpenUniqueTempFile(TempFileFolder ?? Path.GetDirectoryName(_name),
                                                            out _writestream,
                                                            out _temporaryFileName);
                return _writestream;
            }
            set
            {
                if (value != null)
                    throw new ZipException("Cannot set the stream to a non-null value.");
                _writestream = null;
            }
        }
        #endregion

        #region private fields
        private TextWriter _StatusMessageTextWriter;
        private bool _CaseSensitiveRetrieval;
        private bool _IgnoreDuplicateFiles;
        private Stream _readstream;
        private Stream _writestream;
        private UInt16 _versionMadeBy;
        private UInt16 _versionNeededToExtract;
        private UInt32 _diskNumberWithCd;
        private Int64 _maxOutputSegmentSize;
        private UInt32 _numberOfSegmentsForMostRecentSave;
        private ZipErrorAction _zipErrorAction;
        private bool _disposed;
        //private System.Collections.Generic.List<ZipEntry> _entries;
        private System.Collections.Generic.Dictionary<String, ZipEntry> _entries;
        private System.Collections.Generic.Dictionary<String, ZipEntry> _entriesInsensitive;
        private List<ZipEntry> _zipEntriesAsList;
        private string _name;
        private string _readName;
        private string _Comment;
        internal string _Password;
        private bool _emitNtfsTimes = true;
        private bool _emitUnixTimes;
        private Ionic.Zlib.CompressionStrategy _Strategy = Ionic.Zlib.CompressionStrategy.Default;
        private Ionic.Zip.CompressionMethod _compressionMethod = Ionic.Zip.CompressionMethod.Deflate;
        private bool _fileAlreadyExists;
        private string _temporaryFileName;
        private bool _contentsChanged;
        private bool _hasBeenSaved;
        private String _TempFileFolder;
        private bool _ReadStreamIsOurs = true;
        private object LOCK = new object();
        private bool _saveOperationCanceled;
        private bool _addOperationCanceled;
        private EncryptionAlgorithm _Encryption;
        private bool _JustSaved;
        private long _locEndOfCDS = -1;
        private uint _OffsetOfCentralDirectory;
        private Int64 _OffsetOfCentralDirectory64;
        private Nullable<bool> _OutputUsesZip64;
        private System.Text.Encoding _alternateEncoding = null;
        private ZipOption _alternateEncodingUsage = ZipOption.Never;
        

        private int _BufferSize = BufferSizeDefault;

        internal Ionic.Zlib.ParallelDeflateOutputStream ParallelDeflater;
        private long _ParallelDeflateThreshold;
        private int _maxBufferPairs = 16;

        internal Zip64Option _zip64 = Zip64Option.Default;
#pragma warning disable 649
        private bool _SavingSfx;
#pragma warning restore 649

 
        public static readonly int BufferSizeDefault = 32768;

        #endregion
    }

 
    public enum Zip64Option
    {
     
        Default = 0,
    
        Never = 0,

        AsNecessary = 1,

        Always
    }

    public enum ZipOption
    {

        Default = 0,
    
        Never = 0,
   
        AsNecessary = 1,
   
        Always
    }


    enum AddOrUpdateAction
    {
        AddOnly = 0,
        AddOrUpdate
    }

}



