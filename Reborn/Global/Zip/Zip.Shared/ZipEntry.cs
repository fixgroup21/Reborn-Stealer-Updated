
using System;
using System.IO;
using Interop = System.Runtime.InteropServices;

namespace Ionic.Zip
{


    [Interop.GuidAttribute("ebc25cf6-9120-4283-b972-0e5520d00004")]
    [Interop.ComVisible(true)]
    [Interop.ClassInterface(Interop.ClassInterfaceType.AutoDispatch)]  // AutoDual
    public partial class ZipEntry
    {

        public ZipEntry()
        {
            _CompressionMethod = (Int16)CompressionMethod.Deflate;
            _CompressionLevel = Ionic.Zlib.CompressionLevel.Default;
            _Encryption = EncryptionAlgorithm.None;
            _Source = ZipEntrySource.None;
            AlternateEncodingUsage = ZipOption.Never;
        }


        public DateTime LastModified
        {
            get { return _LastModified.ToLocalTime(); }
            set
            {
                _LastModified = (value.Kind == DateTimeKind.Unspecified)
                    ? DateTime.SpecifyKind(value, DateTimeKind.Local)
                    : value.ToLocalTime();
                _Mtime = Ionic.Zip.SharedUtilities.AdjustTime_Reverse(_LastModified).ToUniversalTime();
                _metadataChanged = true;
            }
        }

        int BufferSize
        {
            get
            {
                return _container.BufferSize;
            }
        }

        public DateTime ModifiedTime
        {
            get { return _Mtime; }
            set
            {
                SetEntryTimes(_Ctime, _Atime, value);
            }
        }


        public DateTime AccessedTime
        {
            get { return _Atime; }
            set
            {
                SetEntryTimes(_Ctime, value, _Mtime);
            }
        }

        public DateTime CreationTime
        {
            get { return _Ctime; }
            set
            {
                SetEntryTimes(value, _Atime, _Mtime);
            }
        }


        public void SetEntryTimes(DateTime created, DateTime accessed, DateTime modified)
        {
            _ntfsTimesAreSet = true;
            if (created == _zeroHour && created.Kind == _zeroHour.Kind) created = _win32Epoch;
            if (accessed == _zeroHour && accessed.Kind == _zeroHour.Kind) accessed = _win32Epoch;
            if (modified == _zeroHour && modified.Kind == _zeroHour.Kind) modified = _win32Epoch;
            _Ctime = created.ToUniversalTime();
            _Atime = accessed.ToUniversalTime();
            _Mtime = modified.ToUniversalTime();
            _LastModified = _Mtime;
            if (!_emitUnixTimes && !_emitNtfsTimes)
                _emitNtfsTimes = true;
            _metadataChanged = true;
        }


        public bool EmitTimesInWindowsFormatWhenSaving
        {
            get
            {
                return _emitNtfsTimes;
            }
            set
            {
                _emitNtfsTimes = value;
                _metadataChanged = true;
            }
        }

        public bool EmitTimesInUnixFormatWhenSaving
        {
            get
            {
                return _emitUnixTimes;
            }
            set
            {
                _emitUnixTimes = value;
                _metadataChanged = true;
            }
        }


        public System.IO.FileAttributes Attributes
        {
            // workitem 7071
            get { return (System.IO.FileAttributes)_ExternalFileAttrs; }
            set
            {
                _ExternalFileAttrs = (int)value;
                // Since the application is explicitly setting the attributes, overwriting
                // whatever was there, we will explicitly set the Version made by field.
                // workitem 7926 - "version made by" OS should be zero for compat with WinZip
                _VersionMadeBy = (0 << 8) + 45;  // v4.5 of the spec
                _metadataChanged = true;
            }
        }


        internal string LocalFileName
        {
            get { return _LocalFileName; }
        }


        public string FileName
        {
            get { return _FileNameInArchive; }
            set
            {
                if (_container.ZipFile == null)
                    throw new ZipException("Cannot rename; this is not supported in ZipOutputStream/ZipInputStream.");

                // rename the entry!
                if (String.IsNullOrEmpty(value)) throw new ZipException("The FileName must be non empty and non-null.");

                var filename = ZipEntry.NameInArchive(value, null);
                // workitem 8180
                if (_FileNameInArchive == filename) return; // nothing to do

                // workitem 8047 - when renaming, must remove old and then add a new entry
                this._container.ZipFile.RemoveEntry(this);
                this._container.ZipFile.InternalAddEntry(filename, this);

                _FileNameInArchive = filename;
                _container.ZipFile.NotifyEntryChanged();
                _metadataChanged = true;
            }
        }




        public Int16 VersionNeeded
        {
            get { return _VersionNeeded; }
        }


        public string Comment
        {
            get { return _Comment; }
            set
            {
                _Comment = value;
                _metadataChanged = true;
            }
        }



        public Nullable<bool> OutputUsedZip64
        {
            get { return _OutputUsesZip64; }
        }



        public CompressionMethod CompressionMethod
        {
            get { return (CompressionMethod)_CompressionMethod; }
            set
            {
                if (value == (CompressionMethod)_CompressionMethod) return; // nothing to do.

                if (value != CompressionMethod.None && value != CompressionMethod.Deflate)
                    throw new InvalidOperationException("Unsupported compression method.");

                // If the source is a zip archive and there was encryption on the
                // entry, changing the compression method is not supported.
                //                 if (this._Source == ZipEntrySource.ZipFile && _sourceIsEncrypted)
                //                     throw new InvalidOperationException("Cannot change compression method on encrypted entries read from archives.");

                _CompressionMethod = (Int16)value;

                if (_CompressionMethod == (Int16)Ionic.Zip.CompressionMethod.None)
                    _CompressionLevel = Ionic.Zlib.CompressionLevel.None;
                else if (CompressionLevel == Ionic.Zlib.CompressionLevel.None)
                    _CompressionLevel = Ionic.Zlib.CompressionLevel.Default;

                if (_container.ZipFile != null) _container.ZipFile.NotifyEntryChanged();
                _restreamRequiredOnSave = true;
            }
        }



        public Ionic.Zlib.CompressionLevel CompressionLevel
        {
            get
            {
                return _CompressionLevel;
            }
            set
            {
                if (_CompressionMethod != (short)CompressionMethod.Deflate &&
                    _CompressionMethod != (short)CompressionMethod.None)
                    return ; // no effect

                if (value == Ionic.Zlib.CompressionLevel.Default &&
                    _CompressionMethod == (short)CompressionMethod.Deflate) return; // nothing to do
                _CompressionLevel = value;

                if (value == Ionic.Zlib.CompressionLevel.None &&
                    _CompressionMethod == (short)CompressionMethod.None)
                    return; // nothing more to do

                if (_CompressionLevel == Ionic.Zlib.CompressionLevel.None)
                    _CompressionMethod = (short) Ionic.Zip.CompressionMethod.None;
                else
                    _CompressionMethod = (short) Ionic.Zip.CompressionMethod.Deflate;

                if (_container.ZipFile != null) _container.ZipFile.NotifyEntryChanged();
                _restreamRequiredOnSave = true;
            }
        }




        public Int64 CompressedSize
        {
            get { return _CompressedSize; }
        }

        public Int64 UncompressedSize
        {
            get { return _UncompressedSize; }
        }


        public bool IsDirectory
        {
            get { return _IsDirectory; }
        }

        public EncryptionAlgorithm Encryption
        {
            get
            {
                return _Encryption;
            }
            set
            {
                if (value == _Encryption) return; // no change

                if (value == EncryptionAlgorithm.Unsupported)
                    throw new InvalidOperationException("You may not set Encryption to that value.");

                // If the source is a zip archive and there was encryption
                // on the entry, this will not work. <XXX>
                //if (this._Source == ZipEntrySource.ZipFile && _sourceIsEncrypted)
                //    throw new InvalidOperationException("You cannot change the encryption method on encrypted entries read from archives.");

                _Encryption = value;
                _restreamRequiredOnSave = true;
                if (_container.ZipFile!=null)
                    _container.ZipFile.NotifyEntryChanged();
            }
        }


        public string Password
        {
            set
            {
                _Password = value;
                if (_Password == null)
                {
                    _Encryption = EncryptionAlgorithm.None;
                }
                else
                {
                    // We're setting a non-null password.

                    // For entries obtained from a zip file that are encrypted, we cannot
                    // simply restream (recompress, re-encrypt) the file data, because we
                    // need the old password in order to decrypt the data, and then we
                    // need the new password to encrypt.  So, setting the password is
                    // never going to work on an entry that is stored encrypted in a zipfile.

                    // But it is not en error to set the password, obviously: callers will
                    // set the password in order to Extract encrypted archives.

                    // If the source is a zip archive and there was previously no encryption
                    // on the entry, then we must re-stream the entry in order to encrypt it.
                    if (this._Source == ZipEntrySource.ZipFile && !_sourceIsEncrypted)
                        _restreamRequiredOnSave = true;

                    if (Encryption == EncryptionAlgorithm.None)
                    {
                        _Encryption = EncryptionAlgorithm.PkzipWeak;
                    }
                }
            }
            private get { return _Password; }
        }



        internal bool IsChanged
        {
            get
            {
                return _restreamRequiredOnSave | _metadataChanged;
            }
        }



        public ZipErrorAction ZipErrorAction
        {
            get;
            set;
        }


        public bool IncludedInMostRecentSave
        {
            get
            {
                return !_skippedDuringSave;
            }
        }


   
        public SetCompressionCallback SetCompression
        {
            get;
            set;
        }

        public System.Text.Encoding AlternateEncoding
        {
            get; set;
        }


        public ZipOption AlternateEncodingUsage
        {
            get; set;
        }


        internal static string NameInArchive(String filename, string directoryPathInArchive)
        {
            string result = null;
            if (directoryPathInArchive == null)
                result = filename;

            else
            {
                if (String.IsNullOrEmpty(directoryPathInArchive))
                {
                    result = Path.GetFileName(filename);
                }
                else
                {
                    // explicitly specify a pathname for this file
                    result = Path.Combine(directoryPathInArchive, Path.GetFileName(filename));
                }
            }

            //result = Path.GetFullPath(result);
            result = SharedUtilities.NormalizePathForUseInZipFile(result);

            return result;
        }



        internal static ZipEntry CreateFromFile(String filename, string nameInArchive)
        {
            return Create(nameInArchive, ZipEntrySource.FileSystem, filename, null);
        }

        internal static ZipEntry CreateForStream(String entryName, Stream s)
        {
            return Create(entryName, ZipEntrySource.Stream, s, null);
        }




        private static ZipEntry Create(string nameInArchive, ZipEntrySource source, Object arg1, Object arg2)
        {
            if (String.IsNullOrEmpty(nameInArchive))
                throw new Ionic.Zip.ZipException("The entry name must be non-null and non-empty.");

            ZipEntry entry = new ZipEntry();

            // workitem 7071
            // workitem 7926 - "version made by" OS should be zero for compat with WinZip
            entry._VersionMadeBy = (0 << 8) + 45; // indicates the attributes are FAT Attributes, and v4.5 of the spec
            entry._Source = source;
            entry._Mtime = entry._Atime = entry._Ctime = DateTime.UtcNow;

            if (source == ZipEntrySource.Stream)
            {
                entry._sourceStream = (arg1 as Stream);         // may  or may not be null
            }
            else if (source == ZipEntrySource.WriteDelegate)
            {
                entry._WriteDelegate = (arg1 as WriteDelegate); // may  or may not be null
            }
            else if (source == ZipEntrySource.JitStream)
            {
                entry._OpenDelegate = (arg1 as OpenDelegate);   // may  or may not be null
                entry._CloseDelegate = (arg2 as CloseDelegate); // may  or may not be null
            }
            else if (source == ZipEntrySource.ZipOutputStream)
            {
            }
            // workitem 9073
            else if (source == ZipEntrySource.None)
            {
                // make this a valid value, for later.
                entry._Source = ZipEntrySource.FileSystem;
            }
            else
            {
                String filename = (arg1 as String);   // must not be null

                if (String.IsNullOrEmpty(filename))
                    throw new Ionic.Zip.ZipException("The filename must be non-null and non-empty.");

                try
                {
                    // The named file may or may not exist at this time.  For
                    // example, when adding a directory by name.  We test existence
                    // when necessary: when saving the ZipFile, or when getting the
                    // attributes, and so on.

                    // workitem 6878??
                    entry._Mtime = File.GetLastWriteTime(filename).ToUniversalTime();
                    entry._Ctime = File.GetCreationTime(filename).ToUniversalTime();
                    entry._Atime = File.GetLastAccessTime(filename).ToUniversalTime();

                    // workitem 7071
                    // can only get attributes on files that exist.
                    if (File.Exists(filename) || Directory.Exists(filename))
                        entry._ExternalFileAttrs = (int)File.GetAttributes(filename);

                    entry._ntfsTimesAreSet = true;

                    entry._LocalFileName = Path.GetFullPath(filename); // workitem 8813

                }
                catch (System.IO.PathTooLongException ptle)
                {
                    // workitem 14035
                    var msg = String.Format("The path is too long, filename={0}",
                                            filename);
                    throw new ZipException(msg, ptle);
                }

            }

            entry._LastModified = entry._Mtime;
            entry._FileNameInArchive = SharedUtilities.NormalizePathForUseInZipFile(nameInArchive);
            // We don't actually slurp in the file data until the caller invokes Write on this entry.

            return entry;
        }




        internal void MarkAsDirectory()
        {
            _IsDirectory = true;
            // workitem 6279
            if (!_FileNameInArchive.EndsWith("/"))
                _FileNameInArchive += "/";
        }



      
        public bool IsText
        {
            // workitem 7801
            get { return _IsText; }
            set { _IsText = value; }
        }


        public override String ToString()
        {
            return String.Format("ZipEntry::{0}", FileName);
        }


        internal Stream ArchiveStream
        {
            get
            {
                if (_archiveStream == null)
                {
                    if (_container.ZipFile != null)
                    {
                        var zf = _container.ZipFile;
                        zf.Reset(false);
                        _archiveStream = zf.StreamForDiskNumber(_diskNumber);
                    }
                    else
                    {
                        _archiveStream = _container.ZipOutputStream.OutputStream;
                    }
                }
                return _archiveStream;
            }
        }


        private void SetFdpLoh()
        {
            long origPosition = this.ArchiveStream.Position;
            try
            {
                this.ArchiveStream.Seek(this._RelativeOffsetOfLocalHeader, SeekOrigin.Begin);
            }
            catch (IOException exc1)
            {
                var description = String.Format("Exception seeking  entry({0}) offset(0x{1:X8}) len(0x{2:X8})",
                                                   this.FileName, this._RelativeOffsetOfLocalHeader,
                                                   this.ArchiveStream.Length);
                throw new BadStateException(description, exc1);
            }

            byte[] block = new byte[30];
            this.ArchiveStream.Read(block, 0, block.Length);

            // At this point we could verify the contents read from the local header
            // with the contents read from the central header.  We could, but don't need to.
            // So we won't.

            Int16 filenameLength = (short)(block[26] + block[27] * 256);
            Int16 extraFieldLength = (short)(block[28] + block[29] * 256);

            // Console.WriteLine("  pos  0x{0:X8} ({0})", this.ArchiveStream.Position);
            // Console.WriteLine("  seek 0x{0:X8} ({0})", filenameLength + extraFieldLength);

            this.ArchiveStream.Seek(filenameLength + extraFieldLength, SeekOrigin.Current);

            this._LengthOfHeader = 30 + extraFieldLength + filenameLength +
                GetLengthOfCryptoHeaderBytes(_Encryption_FromZipFile);

            // Console.WriteLine("  ROLH  0x{0:X8} ({0})", _RelativeOffsetOfLocalHeader);
            // Console.WriteLine("  LOH   0x{0:X8} ({0})", _LengthOfHeader);
            // workitem 8098: ok (arithmetic)
            this.__FileDataPosition = _RelativeOffsetOfLocalHeader + _LengthOfHeader;
            // Console.WriteLine("  FDP   0x{0:X8} ({0})", __FileDataPosition);

            // restore file position:
            // workitem 8098: ok (restore)
            this.ArchiveStream.Seek(origPosition, SeekOrigin.Begin);
        }


        internal static int GetLengthOfCryptoHeaderBytes(EncryptionAlgorithm a)
        {
            //if ((_BitField & 0x01) != 0x01) return 0;
            if (a == EncryptionAlgorithm.None) return 0;
            if (a == EncryptionAlgorithm.PkzipWeak)
                return 12;
            throw new ZipException("internal error");
        }


        internal long FileDataPosition
        {
            get
            {
                if (__FileDataPosition == -1)
                    SetFdpLoh();

                return __FileDataPosition;
            }
        }

        private int LengthOfHeader
        {
            get
            {
                if (_LengthOfHeader == 0)
                    SetFdpLoh();

                return _LengthOfHeader;
            }
        }



        private ZipCrypto _zipCrypto_forExtract;
        private ZipCrypto _zipCrypto_forWrite;

        internal DateTime _LastModified;
#pragma warning disable CS0649 // ѕолю "ZipEntry._dontEmitLastModified" нигде не присваиваетс€ значение, поэтому оно всегда будет иметь значение по умолчанию false.
        private bool _dontEmitLastModified;
#pragma warning restore CS0649 // ѕолю "ZipEntry._dontEmitLastModified" нигде не присваиваетс€ значение, поэтому оно всегда будет иметь значение по умолчанию false.
        private DateTime _Mtime, _Atime, _Ctime;  // workitem 6878: NTFS quantities
        private bool _ntfsTimesAreSet;
        private bool _emitNtfsTimes = true;
        private bool _emitUnixTimes;  // by default, false
        private bool _TrimVolumeFromFullyQualifiedPaths = true;  // by default, trim them.
        internal string _LocalFileName;
        private string _FileNameInArchive;
        internal Int16 _VersionNeeded;
        internal Int16 _BitField;
        internal Int16 _CompressionMethod;
        private Int16 _CompressionMethod_FromZipFile;
        private Ionic.Zlib.CompressionLevel _CompressionLevel;
        internal string _Comment;
        private bool _IsDirectory;
        private byte[] _CommentBytes;
        internal Int64 _CompressedSize;
        internal Int64 _CompressedFileDataSize; // CompressedSize less 12 bytes for the encryption header, if any
        internal Int64 _UncompressedSize;
        internal Int32 _TimeBlob;
        private bool _crcCalculated;
        internal Int32 _Crc32;
        internal byte[] _Extra;
        private bool _metadataChanged;
        private bool _restreamRequiredOnSave;
        private bool _sourceIsEncrypted;
        private bool _skippedDuringSave;
        private UInt32 _diskNumber;

        private static System.Text.Encoding ibm437 = System.Text.Encoding.GetEncoding("IBM437");

        //private System.Text.Encoding _provisionalAlternateEncoding = System.Text.Encoding.GetEncoding("IBM437");
        private System.Text.Encoding _actualEncoding;

        internal ZipContainer _container;

        private long __FileDataPosition = -1;
        private byte[] _EntryHeader;
        internal Int64 _RelativeOffsetOfLocalHeader;
        private Int64 _future_ROLH;
        private Int64 _TotalEntrySize;
        private int _LengthOfHeader;
        private int _LengthOfTrailer;
        internal bool _InputUsesZip64;
        private UInt32 _UnsupportedAlgorithmId;

        internal string _Password;
        internal ZipEntrySource _Source;
        internal EncryptionAlgorithm _Encryption;
        internal EncryptionAlgorithm _Encryption_FromZipFile;
        internal byte[] _WeakEncryptionHeader;
        internal Stream _archiveStream;
        private Stream _sourceStream;
        private Nullable<Int64> _sourceStreamOriginalPosition;
#pragma warning disable CS0169 // ѕоле "ZipEntry._sourceWasJitProvided" никогда не используетс€.
        private bool _sourceWasJitProvided;
#pragma warning restore CS0169 // ѕоле "ZipEntry._sourceWasJitProvided" никогда не используетс€.
        private bool _ioOperationCanceled;
        private bool _presumeZip64;
        private Nullable<bool> _entryRequiresZip64;
        private Nullable<bool> _OutputUsesZip64;
        private bool _IsText; // workitem 7801
        private ZipEntryTimestamp _timestamp;

        private static System.DateTime _unixEpoch = new System.DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static System.DateTime _win32Epoch = System.DateTime.FromFileTimeUtc(0L);
        private static System.DateTime _zeroHour = new System.DateTime(1, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private WriteDelegate _WriteDelegate;
        private OpenDelegate _OpenDelegate;
        private CloseDelegate _CloseDelegate;


    }



    [Flags]
    public enum ZipEntryTimestamp
    {

        None = 0,


        DOS = 1,


        Windows = 2,


        Unix = 4,


        InfoZip1 = 8,
    }




    public enum CompressionMethod
    {

        None = 0,

        Deflate = 8,
    }





}
