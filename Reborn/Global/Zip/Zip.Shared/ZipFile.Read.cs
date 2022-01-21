

using System;
using System.IO;
using System.Collections.Generic;

namespace Ionic.Zip
{

    public class ReadOptions
    {

        public EventHandler<ReadProgressEventArgs> ReadProgress { get; set; }

        public TextWriter StatusMessageWriter { get; set; }

        public System.Text.Encoding @Encoding { get; set; }
    }


    public partial class ZipFile
    {



        private static void ReadIntoInstance(ZipFile zf)
        {
            Stream s = zf.ReadStream;
            try
            {
                zf._readName = zf._name; // workitem 13915
                if (!s.CanSeek)
                {
                    ReadIntoInstance_Orig(zf);
                    return;
                }

                zf.OnReadStarted();

                // change for workitem 8098
                //zf._originPosition = s.Position;

                // Try reading the central directory, rather than scanning the file.

                uint datum = ReadFirstFourBytes(s);

                if (datum == ZipConstants.EndOfCentralDirectorySignature)
                    return;


                int nTries = 0;
                bool success = false;


                long posn = s.Length - 64;
                long maxSeekback = Math.Max(s.Length - 0x4000, 10);
                do
                {
                    if (posn < 0) posn = 0;  // BOF
                    s.Seek(posn, SeekOrigin.Begin);
                    long bytesRead = SharedUtilities.FindSignature(s, (int)ZipConstants.EndOfCentralDirectorySignature);
                    if (bytesRead != -1)
                        success = true;
                    else
                    {
                        if (posn==0) break; // started at the BOF and found nothing
                        nTries++;
                        // Weird: with NETCF, negative offsets from SeekOrigin.End DO
                        // NOT WORK. So rather than seek a negative offset, we seek
                        // from SeekOrigin.Begin using a smaller number.
                        //
                        // We no longer target NETCF, so somebody feeling brave could
                        // restore the (very) old code.
                        posn -= (32 * (nTries + 1) * nTries);
                    }
                }
                while (!success && posn > maxSeekback);

                if (success)
                {
                    // workitem 8299
                    zf._locEndOfCDS = s.Position - 4;

                    byte[] block = new byte[16];
                    s.Read(block, 0, block.Length);

                    zf._diskNumberWithCd = BitConverter.ToUInt16(block, 2);

                    if (zf._diskNumberWithCd == 0xFFFF)
                        throw new ZipException("Spanned archives with more than 65534 segments are not supported at this time.");

                    zf._diskNumberWithCd++; // I think the number in the file differs from reality by 1

                    int i = 12;

                    uint offset32 = (uint) BitConverter.ToUInt32(block, i);
                    if (offset32 == 0xFFFFFFFF)
                    {
                        Zip64SeekToCentralDirectory(zf);
                    }
                    else
                    {
                        zf._OffsetOfCentralDirectory = offset32;
                        // change for workitem 8098
                        s.Seek(offset32, SeekOrigin.Begin);
                    }

                    ReadCentralDirectory(zf);
                }
                else
                {
                    // Could not find the central directory.
                    // Fallback to the old method.
                    // workitem 8098: ok
                    //s.Seek(zf._originPosition, SeekOrigin.Begin);
                    s.Seek(0L, SeekOrigin.Begin);
                    ReadIntoInstance_Orig(zf);
                }
            }
            catch (Exception ex1)
            {
                if (zf._ReadStreamIsOurs && zf._readstream != null)
                {
                    try
                    {
                        zf._readstream.Dispose();
                        zf._readstream = null;
                    }
                    finally { }
                }

                throw new ZipException("Cannot read that as a ZipFile", ex1);
            }

            // the instance has been read in
            zf._contentsChanged = false;
        }



        private static void Zip64SeekToCentralDirectory(ZipFile zf)
        {
            Stream s = zf.ReadStream;
            byte[] block = new byte[16];

            // seek back to find the ZIP64 EoCD.
            s.Seek(-40, SeekOrigin.Current);
            s.Read(block, 0, 16);

            Int64 offset64 = BitConverter.ToInt64(block, 8);
            zf._OffsetOfCentralDirectory = 0xFFFFFFFF;
            zf._OffsetOfCentralDirectory64 = offset64;
            // change for workitem 8098
            s.Seek(offset64, SeekOrigin.Begin);
            //zf.SeekFromOrigin(Offset64);

            uint datum = (uint)Ionic.Zip.SharedUtilities.ReadInt(s);
            if (datum != ZipConstants.Zip64EndOfCentralDirectoryRecordSignature)
                throw new BadReadException(String.Format("  Bad signature (0x{0:X8}) looking for ZIP64 EoCD Record at position 0x{1:X8}", datum, s.Position));

            s.Read(block, 0, 8);
            Int64 Size = BitConverter.ToInt64(block, 0);

            block = new byte[Size];
            s.Read(block, 0, block.Length);

            offset64 = BitConverter.ToInt64(block, 36);
            // change for workitem 8098
            s.Seek(offset64, SeekOrigin.Begin);
            //zf.SeekFromOrigin(Offset64);
        }


        private static uint ReadFirstFourBytes(Stream s)
        {
            uint datum = (uint)Ionic.Zip.SharedUtilities.ReadInt(s);
            return datum;
        }



        private static void ReadCentralDirectory(ZipFile zf)
        {
            // We must have the central directory footer record, in order to properly
            // read zip dir entries from the central directory.  This because the logic
            // knows when to open a spanned file when the volume number for the central
            // directory differs from the volume number for the zip entry.  The
            // _diskNumberWithCd was set when originally finding the offset for the
            // start of the Central Directory.

            // workitem 9214
            bool inputUsesZip64 = false;
            ZipEntry de;
            // in lieu of hashset, use a dictionary
            var previouslySeen = new Dictionary<String, object>(StringComparer.Ordinal);
            while ((de = ZipEntry.ReadDirEntry(zf, previouslySeen)) != null)
            {
                de.ResetDirEntry();
                zf.OnReadEntry(true, null);

                if (zf.Verbose)
                    zf.StatusMessageTextWriter.WriteLine("entry {0}", de.FileName);

                zf._entries.Add(de.FileName,de);
                if (!zf._entriesInsensitive.ContainsKey(de.FileName))
                    zf._entriesInsensitive.Add(de.FileName,de);

                // workitem 9214
                if (de._InputUsesZip64) inputUsesZip64 = true;
                previouslySeen.Add(de.FileName, null); // to prevent dupes
            }

            // workitem 9214; auto-set the zip64 flag
            if (inputUsesZip64) zf.UseZip64WhenSaving = Zip64Option.Always;

            // workitem 8299
            if (zf._locEndOfCDS > 0)
                zf.ReadStream.Seek(zf._locEndOfCDS, SeekOrigin.Begin);

            ReadCentralDirectoryFooter(zf);

            if (zf.Verbose && !String.IsNullOrEmpty(zf.Comment))
                zf.StatusMessageTextWriter.WriteLine("Zip file Comment: {0}", zf.Comment);

            // We keep the read stream open after reading.

            if (zf.Verbose)
                zf.StatusMessageTextWriter.WriteLine("read in {0} entries.", zf._entries.Count);

            zf.OnReadCompleted();
        }




        // build the TOC by reading each entry in the file.
        private static void ReadIntoInstance_Orig(ZipFile zf)
        {
            zf.OnReadStarted();
            zf._entries.Clear();
            zf._entriesInsensitive.Clear();

            ZipEntry e;
            if (zf.Verbose)
                if (zf.Name == null)
                    zf.StatusMessageTextWriter.WriteLine("Reading zip from stream...");
                else
                    zf.StatusMessageTextWriter.WriteLine("Reading zip {0}...", zf.Name);

            // work item 6647:  PK00 (packed to removable disk)
            bool firstEntry = true;
            ZipContainer zc = new ZipContainer(zf);
            while ((e = ZipEntry.ReadEntry(zc, firstEntry)) != null)
            {
                if (zf.Verbose)
                    zf.StatusMessageTextWriter.WriteLine("  {0}", e.FileName);

                zf._entries.Add(e.FileName,e);
                if (!zf._entriesInsensitive.ContainsKey(e.FileName))
                    zf._entriesInsensitive.Add(e.FileName,e);
                firstEntry = false;
            }

            // read the zipfile's central directory structure here.
            // workitem 9912
            // But, because it may be corrupted, ignore errors.
            try
            {
                ZipEntry de;
                // in lieu of hashset, use a dictionary
                var previouslySeen = new Dictionary<String,Object>(StringComparer.Ordinal);
                while ((de = ZipEntry.ReadDirEntry(zf, previouslySeen)) != null)
                {
                    // Housekeeping: Since ZipFile exposes ZipEntry elements in the enumerator,
                    // we need to copy the comment that we grab from the ZipDirEntry
                    // into the ZipEntry, so the application can access the comment.
                    // Also since ZipEntry is used to Write zip files, we need to copy the
                    // file attributes to the ZipEntry as appropriate.
                    ZipEntry e1 = zf._entries[de.FileName];
                    if (e1 != null)
                    {
                        e1._Comment = de.Comment;
                        if (de.IsDirectory) e1.MarkAsDirectory();
                    }
                    previouslySeen.Add(de.FileName,null); // to prevent dupes
                }

                // workitem 8299
                if (zf._locEndOfCDS > 0)
                    zf.ReadStream.Seek(zf._locEndOfCDS, SeekOrigin.Begin);

                ReadCentralDirectoryFooter(zf);

                if (zf.Verbose && !String.IsNullOrEmpty(zf.Comment))
                    zf.StatusMessageTextWriter.WriteLine("Zip file Comment: {0}", zf.Comment);
            }
            catch (ZipException) { }
            catch (IOException) { }

            zf.OnReadCompleted();
        }




        private static void ReadCentralDirectoryFooter(ZipFile zf)
        {
            Stream s = zf.ReadStream;
            int signature = Ionic.Zip.SharedUtilities.ReadSignature(s);

            byte[] block = null;
            int j = 0;
            if (signature == ZipConstants.Zip64EndOfCentralDirectoryRecordSignature)
            {
                // We have a ZIP64 EOCD
                // This data block is 4 bytes sig, 8 bytes size, 44 bytes fixed data,
                // followed by a variable-sized extension block.  We have read the sig already.
                // 8 - datasize (64 bits)
                // 2 - version made by
                // 2 - version needed to extract
                // 4 - number of this disk
                // 4 - number of the disk with the start of the CD
                // 8 - total number of entries in the CD on this disk
                // 8 - total number of entries in the CD
                // 8 - size of the CD
                // 8 - offset of the CD
                // -----------------------
                // 52 bytes

                block = new byte[8 + 44];
                s.Read(block, 0, block.Length);

                Int64 DataSize = BitConverter.ToInt64(block, 0);  // == 44 + the variable length

                if (DataSize < 44)
                    throw new ZipException("Bad size in the ZIP64 Central Directory.");

                zf._versionMadeBy = BitConverter.ToUInt16(block, j);
                j += 2;
                zf._versionNeededToExtract = BitConverter.ToUInt16(block, j);
                j += 2;
                zf._diskNumberWithCd = BitConverter.ToUInt32(block, j);
                j += 2;

                //zf._diskNumberWithCd++; // hack!!

                // read the extended block
                block = new byte[DataSize - 44];
                s.Read(block, 0, block.Length);
                // discard the result

                signature = Ionic.Zip.SharedUtilities.ReadSignature(s);
                if (signature != ZipConstants.Zip64EndOfCentralDirectoryLocatorSignature)
                    throw new ZipException("Inconsistent metadata in the ZIP64 Central Directory.");

                block = new byte[16];
                s.Read(block, 0, block.Length);
                // discard the result

                signature = Ionic.Zip.SharedUtilities.ReadSignature(s);
            }

            // Throw if this is not a signature for "end of central directory record"
            // This is a sanity check.
            if (signature != ZipConstants.EndOfCentralDirectorySignature)
            {
                s.Seek(-4, SeekOrigin.Current);
                throw new BadReadException(String.Format("Bad signature ({0:X8}) at position 0x{1:X8}",
                                                         signature, s.Position));
            }

            // read the End-of-Central-Directory-Record
            block = new byte[16];
            zf.ReadStream.Read(block, 0, block.Length);

            // off sz  data
            // -------------------------------------------------------
            //  0   4  end of central dir signature (0x06054b50)
            //  4   2  number of this disk
            //  6   2  number of the disk with start of the central directory
            //  8   2  total number of entries in the  central directory on this disk
            // 10   2  total number of entries in  the central directory
            // 12   4  size of the central directory
            // 16   4  offset of start of central directory with respect to the starting disk number
            // 20   2  ZIP file comment length
            // 22  ??  ZIP file comment

            if (zf._diskNumberWithCd == 0)
            {
                zf._diskNumberWithCd = BitConverter.ToUInt16(block, 2);
                //zf._diskNumberWithCd++; // hack!!
            }

            // read the comment here
            ReadZipFileComment(zf);
        }



        private static void ReadZipFileComment(ZipFile zf)
        {
            // read the comment here
            byte[] block = new byte[2];
            zf.ReadStream.Read(block, 0, block.Length);

            Int16 commentLength = (short)(block[0] + block[1] * 256);
            if (commentLength > 0)
            {
                block = new byte[commentLength];
                zf.ReadStream.Read(block, 0, block.Length);

                // workitem 10392 - prefer ProvisionalAlternateEncoding,
                // first.  The fix for workitem 6513 tried to use UTF8
                // only as necessary, but that is impossible to test
                // for, in this direction. There's no way to know what
                // characters the already-encoded bytes refer
                // to. Therefore, must do what the user tells us.

                string s1 = zf.AlternateEncoding.GetString(block, 0, block.Length);
                zf.Comment = s1;
            }
        }
    }
}