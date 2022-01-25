
using System;
using System.IO;

namespace Ionic.Zip
{
    public partial class ZipEntry
    {
        private int _readExtraDepth;
        private void ReadExtraField()
        {
            _readExtraDepth++;
            // workitem 8098: ok (restore)
            long posn = this.ArchiveStream.Position;
            this.ArchiveStream.Seek(this._RelativeOffsetOfLocalHeader, SeekOrigin.Begin);

            byte[] block = new byte[30];
            this.ArchiveStream.Read(block, 0, block.Length);
            int i = 26;
            Int16 filenameLength = (short)(block[i++] + block[i++] * 256);
            Int16 extraFieldLength = (short)(block[i++] + block[i++] * 256);

            // workitem 8098: ok (relative)
            this.ArchiveStream.Seek(filenameLength, SeekOrigin.Current);

            ProcessExtraField(this.ArchiveStream, extraFieldLength);

            // workitem 8098: ok (restore)
            this.ArchiveStream.Seek(posn, SeekOrigin.Begin);
            _readExtraDepth--;
        }


        private static bool ReadHeader(ZipEntry ze, System.Text.Encoding defaultEncoding)
        {
            int bytesRead = 0;

            // change for workitem 8098
            ze._RelativeOffsetOfLocalHeader = ze.ArchiveStream.Position;

            int signature = Ionic.Zip.SharedUtilities.ReadEntrySignature(ze.ArchiveStream);
            bytesRead += 4;

            // Return false if this is not a local file header signature.
            if (ZipEntry.IsNotValidSig(signature))
            {
                // Getting "not a ZipEntry signature" is not always wrong or an error.
                // This will happen after the last entry in a zipfile.  In that case, we
                // expect to read :
                //    a ZipDirEntry signature (if a non-empty zip file) or
                //    a ZipConstants.EndOfCentralDirectorySignature.
                //
                // Anything else is a surprise.

                ze.ArchiveStream.Seek(-4, SeekOrigin.Current); // unread the signature
                if (ZipEntry.IsNotValidZipDirEntrySig(signature) && (signature != ZipConstants.EndOfCentralDirectorySignature))
                {
                    throw new BadReadException(String.Format("  Bad signature (0x{0:X8}) at position  0x{1:X8}", signature, ze.ArchiveStream.Position));
                }
                return false;
            }

            byte[] block = new byte[26];
            int n = ze.ArchiveStream.Read(block, 0, block.Length);
            if (n != block.Length) return false;
            bytesRead += n;

            int i = 0;
            ze._VersionNeeded = (Int16)(block[i++] + block[i++] * 256);
            ze._BitField = (Int16)(block[i++] + block[i++] * 256);
            ze._CompressionMethod_FromZipFile = ze._CompressionMethod = (Int16)(block[i++] + block[i++] * 256);
            ze._TimeBlob = block[i++] + block[i++] * 256 + block[i++] * 256 * 256 + block[i++] * 256 * 256 * 256;
            // transform the time data into something usable (a DateTime)
            ze._LastModified = Ionic.Zip.SharedUtilities.PackedToDateTime(ze._TimeBlob);
            ze._timestamp |= ZipEntryTimestamp.DOS;

            if ((ze._BitField & 0x01) == 0x01)
            {
                ze._Encryption_FromZipFile = ze._Encryption = EncryptionAlgorithm.PkzipWeak; // this *may* change after processing the Extra field
                ze._sourceIsEncrypted = true;
            }

            // NB: if ((ze._BitField & 0x0008) != 0x0008), then the Compressed, uncompressed and
            // CRC values are not true values; the true values will follow the entry data.
            // But, regardless of the status of bit 3 in the bitfield, the slots for
            // the three amigos may contain marker values for ZIP64.  So we must read them.
            {
                ze._Crc32 = (Int32)(block[i++] + block[i++] * 256 + block[i++] * 256 * 256 + block[i++] * 256 * 256 * 256);
                ze._CompressedSize = (uint)(block[i++] + block[i++] * 256 + block[i++] * 256 * 256 + block[i++] * 256 * 256 * 256);
                ze._UncompressedSize = (uint)(block[i++] + block[i++] * 256 + block[i++] * 256 * 256 + block[i++] * 256 * 256 * 256);

                if ((uint)ze._CompressedSize == 0xFFFFFFFF ||
                    (uint)ze._UncompressedSize == 0xFFFFFFFF)

                    ze._InputUsesZip64 = true;
            }

            Int16 filenameLength = (short)(block[i++] + block[i++] * 256);
            Int16 extraFieldLength = (short)(block[i++] + block[i++] * 256);

            block = new byte[filenameLength];
            n = ze.ArchiveStream.Read(block, 0, block.Length);
            bytesRead += n;

            // if the UTF8 bit is set for this entry, override the
            // encoding the application requested.

            if ((ze._BitField & 0x0800) == 0x0800)
            {
                // workitem 12744
                ze.AlternateEncoding = System.Text.Encoding.UTF8;
                ze.AlternateEncodingUsage = ZipOption.Always;
            }

            ze._FileNameInArchive = ze.AlternateEncoding.GetString(block);

            // workitem 6898
            if (ze._FileNameInArchive.EndsWith("/")) ze.MarkAsDirectory();

            bytesRead += ze.ProcessExtraField(ze.ArchiveStream, extraFieldLength);

            ze._LengthOfTrailer = 0;

            // workitem 6607 - don't read for directories
            // actually get the compressed size and CRC if necessary
            if (!ze._FileNameInArchive.EndsWith("/") && (ze._BitField & 0x0008) == 0x0008)
            {


                // workitem 8098: ok (restore)
                long posn = ze.ArchiveStream.Position;

                // Here, we're going to loop until we find a ZipEntryDataDescriptorSignature and
                // a consistent data record after that.   To be consistent, the data record must
                // indicate the length of the entry data.
                bool wantMore = true;
                long SizeOfDataRead = 0;
                int tries = 0;
                while (wantMore)
                {
                    tries++;
                    // We call the FindSignature shared routine to find the specified signature
                    // in the already-opened zip archive, starting from the current cursor
                    // position in that filestream.  If we cannot find the signature, then the
                    // routine returns -1, and the ReadHeader() method returns false,
                    // indicating we cannot read a legal entry header.  If we have found it,
                    // then the FindSignature() method returns the number of bytes in the
                    // stream we had to seek forward, to find the sig.  We need this to
                    // determine if the zip entry is valid, later.

                    if (ze._container.ZipFile != null)
                        ze._container.ZipFile.OnReadBytes(ze);

                    long d = Ionic.Zip.SharedUtilities.FindSignature(ze.ArchiveStream, ZipConstants.ZipEntryDataDescriptorSignature);
                    if (d == -1) return false;

                    // total size of data read (through all loops of this).
                    SizeOfDataRead += d;

                    if (ze._InputUsesZip64)
                    {
                        // read 1x 4-byte (CRC) and 2x 8-bytes (Compressed Size, Uncompressed Size)
                        block = new byte[20];
                        n = ze.ArchiveStream.Read(block, 0, block.Length);
                        if (n != 20) return false;

                        // do not increment bytesRead - it is for entry header only.
                        // the data we have just read is a footer (falls after the file data)
                        //bytesRead += n;

                        i = 0;
                        ze._Crc32 = (Int32)(block[i++] + block[i++] * 256 + block[i++] * 256 * 256 + block[i++] * 256 * 256 * 256);
                        ze._CompressedSize = BitConverter.ToInt64(block, i);
                        i += 8;
                        ze._UncompressedSize = BitConverter.ToInt64(block, i);
                        i += 8;
                    }
                    else
                    {
                        // read 3x 4-byte fields (CRC, Compressed Size, Uncompressed Size)
                        block = new byte[12];
                        n = ze.ArchiveStream.Read(block, 0, block.Length);
                        if (n != 12) return false;

                        // do not increment bytesRead - it is for entry header only.
                        // the data we have just read is a footer (falls after the file data)
                        //bytesRead += n;

                        i = 0;
                        ze._Crc32 = (Int32)(block[i++] + block[i++] * 256 + block[i++] * 256 * 256 + block[i++] * 256 * 256 * 256);
                        ze._CompressedSize = (uint)(block[i++] + block[i++] * 256 + block[i++] * 256 * 256 + block[i++] * 256 * 256 * 256);
                        ze._UncompressedSize = (uint)(block[i++] + block[i++] * 256 + block[i++] * 256 * 256 + block[i++] * 256 * 256 * 256);
                    }

                    wantMore = (SizeOfDataRead != ze._CompressedSize);

                    if (wantMore)
                    {
                        // Seek back to un-read the last 12 bytes  - maybe THEY contain
                        // the ZipEntryDataDescriptorSignature.
                        // (12 bytes for the CRC, Comp and Uncomp size.)
                        ze.ArchiveStream.Seek(-12, SeekOrigin.Current);

                        // Adjust the size to account for the false signature read in
                        // FindSignature().
                        SizeOfDataRead += 4;
                    }
                }

                // seek back to previous position, to prepare to read file data
                // workitem 8098: ok (restore)
                ze.ArchiveStream.Seek(posn, SeekOrigin.Begin);

                ze._LengthOfTrailer += ze._InputUsesZip64 ? 24 : 16;  // bytes including sig, CRC, Comp and Uncomp sizes
            }

            ze._CompressedFileDataSize = ze._CompressedSize;


            // bit 0 set indicates that some kind of encryption is in use
            if ((ze._BitField & 0x01) == 0x01)
            {
                {
                    // read in the header data for "weak" encryption
                    ze._WeakEncryptionHeader = new byte[12];
                    bytesRead += ZipEntry.ReadWeakEncryptionHeader(ze._archiveStream, ze._WeakEncryptionHeader);
                    // decrease the filedata size by 12 bytes
                    ze._CompressedFileDataSize -= 12;
                }
            }

            // Remember the size of the blob for this entry.
            // We also have the starting position in the stream for this entry.
            ze._LengthOfHeader = bytesRead;
            ze._TotalEntrySize = ze._LengthOfHeader + ze._CompressedFileDataSize + ze._LengthOfTrailer;


            // We've read in the regular entry header, the extra field, and any
            // encryption header.  The pointer in the file is now at the start of the
            // filedata, which is potentially compressed and encrypted.  Just ahead in
            // the file, there are _CompressedFileDataSize bytes of data, followed by
            // potentially a non-zero length trailer, consisting of optionally, some
            // encryption stuff (10 byte MAC for AES), and the bit-3 trailer (16 or 24
            // bytes).

            return true;
        }



        internal static int ReadWeakEncryptionHeader(Stream s, byte[] buffer)
        {

            // read the 12-byte encryption header
            int additionalBytesRead = s.Read(buffer, 0, 12);
            if (additionalBytesRead != 12)
                throw new ZipException(String.Format("Unexpected end of data at position 0x{0:X8}", s.Position));

            return additionalBytesRead;
        }



        private static bool IsNotValidSig(int signature)
        {
            return (signature != ZipConstants.ZipEntrySignature);
        }


        internal static ZipEntry ReadEntry(ZipContainer zc, bool first)
        {
            ZipFile zf = zc.ZipFile;
            Stream s = zc.ReadStream;
            System.Text.Encoding defaultEncoding = zc.AlternateEncoding;
            ZipEntry entry = new ZipEntry();
            entry._Source = ZipEntrySource.ZipFile;
            entry._container = zc;
            entry._archiveStream = s;
            if (zf != null)
                zf.OnReadEntry(true, null);

            if (first) HandlePK00Prefix(s);

            // Read entry header, including any encryption header
            if (!ReadHeader(entry, defaultEncoding)) return null;

            // Store the position in the stream for this entry
            // change for workitem 8098
            entry.__FileDataPosition = entry.ArchiveStream.Position;

            // seek past the data without reading it. We will read on Extract()
            s.Seek(entry._CompressedFileDataSize + entry._LengthOfTrailer, SeekOrigin.Current);

            HandleUnexpectedDataDescriptor(entry);

            if (zf != null)
            {
                zf.OnReadBytes(entry);
                zf.OnReadEntry(false, entry);
            }

            return entry;
        }


        internal static void HandlePK00Prefix(Stream s)
        {
            // in some cases, the zip file begins with "PK00".  This is a throwback and is rare,
            // but we handle it anyway. We do not change behavior based on it.
            uint datum = (uint)Ionic.Zip.SharedUtilities.ReadInt(s);
            if (datum != ZipConstants.PackedToRemovableMedia)
            {
                s.Seek(-4, SeekOrigin.Current); // unread the block
            }
        }



        private static void HandleUnexpectedDataDescriptor(ZipEntry entry)
        {
            Stream s = entry.ArchiveStream;

            // In some cases, the "data descriptor" is present, without a signature, even when
            // bit 3 of the BitField is NOT SET.  This is the CRC, followed
            //    by the compressed length and the uncompressed length (4 bytes for each
            //    of those three elements).  Need to check that here.
            //
            uint datum = (uint)Ionic.Zip.SharedUtilities.ReadInt(s);
            if (datum == entry._Crc32)
            {
                int sz = Ionic.Zip.SharedUtilities.ReadInt(s);
                if (sz == entry._CompressedSize)
                {
                    sz = Ionic.Zip.SharedUtilities.ReadInt(s);
                    if (sz == entry._UncompressedSize)
                    {
                        // ignore everything and discard it.
                    }
                    else
                    {
                        s.Seek(-12, SeekOrigin.Current); // unread the three blocks
                    }
                }
                else
                {
                    s.Seek(-8, SeekOrigin.Current); // unread the two blocks
                }
            }
            else
            {
                s.Seek(-4, SeekOrigin.Current); // unread the block
            }
        }




        internal int ProcessExtraField(Stream s, Int16 extraFieldLength)
        {
            int additionalBytesRead = 0;
            if (extraFieldLength > 0)
            {
                byte[] buffer = this._Extra = new byte[extraFieldLength];
                additionalBytesRead = s.Read(buffer, 0, buffer.Length);
                long posn = s.Position - additionalBytesRead;
                int j = 0;
                while (j + 3 < buffer.Length)
                {
                    int start = j;
                    UInt16 headerId = (UInt16)(buffer[j++] + buffer[j++] * 256);
                    UInt16 dataSize = (UInt16)(buffer[j++] + buffer[j++] * 256);

                    switch (headerId)
                    {
                        case 0x000a:  // NTFS ctime, atime, mtime
                            j = ProcessExtraFieldWindowsTimes(buffer, j, dataSize, posn);
                            break;

                        case 0x5455:  // Unix ctime, atime, mtime
                            j = ProcessExtraFieldUnixTimes(buffer, j, dataSize, posn);
                            break;

                        case 0x5855:  // Info-zip Extra field (outdated)
                            // This is outdated, so the field is supported on
                            // read only.
                            j = ProcessExtraFieldInfoZipTimes(buffer, j, dataSize, posn);
                            break;

                        case 0x7855:  // Unix uid/gid
                            // ignored. DotNetZip does not handle this field.
                            break;

                        case 0x7875:  // ??
                            // ignored.  I could not find documentation on this field,
                            // though it appears in some zip files.
                            break;

                        case 0x0001: // ZIP64
                            j = ProcessExtraFieldZip64(buffer, j, dataSize, posn);
                            break;

                        case 0x0017: // workitem 7968: handle PKWare Strong encryption header
                            j = ProcessExtraFieldPkwareStrongEncryption(buffer, j);
                            break;
                    }

                    // move to the next Header in the extra field
                    j = start + dataSize + 4;
                }
            }
            return additionalBytesRead;
        }

        private int ProcessExtraFieldPkwareStrongEncryption(byte[] Buffer, int j)
        {

            j += 2;
            _UnsupportedAlgorithmId = (UInt16)(Buffer[j++] + Buffer[j++] * 256);
            _Encryption_FromZipFile = _Encryption = EncryptionAlgorithm.Unsupported;

            // DotNetZip doesn't support this algorithm, but we don't need to throw
            // here.  we might just be reading the archive, which is fine.  We'll
            // need to throw if Extract() is called.

            return j;
        }




        private delegate T Func<T>();

        private int ProcessExtraFieldZip64(byte[] buffer, int j, UInt16 dataSize, long posn)
        {
         
            this._InputUsesZip64 = true;

            // workitem 7941: check datasize before reading.
            if (dataSize > 28)
                throw new BadReadException(String.Format("  Inconsistent size (0x{0:X4}) for ZIP64 extra field at position 0x{1:X16}",
                                                         dataSize, posn));
            int remainingData = dataSize;

            var slurp = new Func<Int64>(() => {
                if (remainingData < 8)
                    throw new BadReadException(String.Format("  Missing data for ZIP64 extra field, position 0x{0:X16}", posn));
                var x = BitConverter.ToInt64(buffer, j);
                j += 8;
                remainingData -= 8;
                return x;
            });

            if (this._UncompressedSize == 0xFFFFFFFF)
                this._UncompressedSize = slurp();

            if (this._CompressedSize == 0xFFFFFFFF)
                this._CompressedSize = slurp();

            if (this._RelativeOffsetOfLocalHeader == 0xFFFFFFFF)
                this._RelativeOffsetOfLocalHeader = slurp();

            if (this._diskNumber == 0xFFFF && remainingData >= 4)
            {
                this._diskNumber = BitConverter.ToUInt32(buffer, j);
                j += 4;
                remainingData -= 4;
            }

            return j;
        }


        private int ProcessExtraFieldInfoZipTimes(byte[] buffer, int j, UInt16 dataSize, long posn)
        {
            if (dataSize != 12 && dataSize != 8)
                throw new BadReadException(String.Format("  Unexpected size (0x{0:X4}) for InfoZip v1 extra field at position 0x{1:X16}", dataSize, posn));

            Int32 timet = BitConverter.ToInt32(buffer, j);
            this._Mtime = _unixEpoch.AddSeconds(timet);
            j += 4;

            timet = BitConverter.ToInt32(buffer, j);
            this._Atime = _unixEpoch.AddSeconds(timet);
            j += 4;

            this._Ctime = DateTime.UtcNow;

            _ntfsTimesAreSet = true;
            _timestamp |= ZipEntryTimestamp.InfoZip1; return j;
        }



        private int ProcessExtraFieldUnixTimes(byte[] buffer, int j, UInt16 dataSize, long posn)
        {
            // The Unix filetimes are 32-bit unsigned integers,
            // storing seconds since Unix epoch.

            if (dataSize != 13 && dataSize != 9 && dataSize != 5)
                throw new BadReadException(String.Format("  Unexpected size (0x{0:X4}) for Extended Timestamp extra field at position 0x{1:X16}", dataSize, posn));

            int remainingData = dataSize;

            var slurp = new Func<DateTime>( () => {
                    Int32 timet = BitConverter.ToInt32(buffer, j);
                    j += 4;
                    remainingData -= 4;
                    return _unixEpoch.AddSeconds(timet);
                });

            if (dataSize == 13 || _readExtraDepth > 0)
            {
                byte flag = buffer[j++];
                remainingData--;

                if ((flag & 0x0001) != 0 && remainingData >= 4)
                    this._Mtime = slurp();

                 this._Atime = ((flag & 0x0002) != 0 && remainingData >= 4)
                     ? slurp()
                     : DateTime.UtcNow;

                 this._Ctime =  ((flag & 0x0004) != 0 && remainingData >= 4)
                     ? slurp()
                     :DateTime.UtcNow;

                _timestamp |= ZipEntryTimestamp.Unix;
                _ntfsTimesAreSet = true;
                _emitUnixTimes = true;
            }
            else
                ReadExtraField(); // will recurse

            return j;
        }


        private int ProcessExtraFieldWindowsTimes(byte[] buffer, int j, UInt16 dataSize, long posn)
        {


            if (dataSize != 32)
                throw new BadReadException(String.Format("  Unexpected size (0x{0:X4}) for NTFS times extra field at position 0x{1:X16}", dataSize, posn));

            j += 4;  // reserved
            Int16 timetag = (Int16)(buffer[j] + buffer[j + 1] * 256);
            Int16 addlsize = (Int16)(buffer[j + 2] + buffer[j + 3] * 256);
            j += 4;  // tag and size

            if (timetag == 0x0001 && addlsize == 24)
            {
                Int64 z = BitConverter.ToInt64(buffer, j);
                this._Mtime = DateTime.FromFileTimeUtc(z);
                j += 8;



                z = BitConverter.ToInt64(buffer, j);
                this._Atime = DateTime.FromFileTimeUtc(z);
                j += 8;

                z = BitConverter.ToInt64(buffer, j);
                this._Ctime = DateTime.FromFileTimeUtc(z);
                j += 8;

                _ntfsTimesAreSet = true;
                _timestamp |= ZipEntryTimestamp.Windows;
                _emitNtfsTimes = true;
            }
            return j;
        }
    }
}
