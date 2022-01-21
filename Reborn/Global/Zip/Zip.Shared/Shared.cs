
using System;
using System.IO;

namespace Ionic.Zip
{

    internal static class SharedUtilities
    {

        public static Int64 GetFileLength(string fileName)
        {
            if (!File.Exists(fileName))
                throw new FileNotFoundException(String.Format("Could not find file '{0}'.", fileName), fileName);

            long fileLength;
            FileShare fs = FileShare.ReadWrite | FileShare.Delete;
            using (var s = File.Open(fileName, FileMode.Open, FileAccess.Read, fs))
            {
                fileLength = s.Length;
            }
            return fileLength;
        }


        private static System.Text.RegularExpressions.Regex doubleDotRegex1 =
            new System.Text.RegularExpressions.Regex(@"^(.*/)?([^/\\.]+/\\.\\./)(.+)$");

        private static string SimplifyFwdSlashPath(string path)
        {
            if (path.StartsWith("./")) path = path.Substring(2);
            path = path.Replace("/./", "/");

            // Replace foo/anything/../bar with foo/bar
            path = doubleDotRegex1.Replace(path, "$1$3");
            return path;
        }



        public static string NormalizePathForUseInZipFile(string pathName)
        {
            // boundary case
            if (String.IsNullOrEmpty(pathName)) return pathName;

            // trim volume if necessary
            if ((pathName.Length >= 2)  && ((pathName[1] == ':') && (pathName[2] == '\\')))
                pathName =  pathName.Substring(3);

            // swap slashes
            pathName = pathName.Replace('\\', '/');

            // trim all leading slashes
            while (pathName.StartsWith("/")) pathName = pathName.Substring(1);

            return SimplifyFwdSlashPath(pathName);
        }


        //static System.Text.Encoding ibm437 = System.Text.Encoding.GetEncoding("IBM437");
        static System.Text.Encoding utf8 = System.Text.Encoding.GetEncoding("UTF-8");

        internal static byte[] StringToByteArray(string value, System.Text.Encoding encoding)
        {
            byte[] a = encoding.GetBytes(value);
            return a;
        }
        internal static byte[] StringToByteArray(string value)
        {
            System.Text.Encoding ibm437 = null;
            try
            {
                ibm437 = System.Text.Encoding.GetEncoding("IBM437");
            }
            catch (Exception /*e*/)
            {

            }
            if (ibm437 == null)
            {
                try
                {
                    ibm437 = System.Text.Encoding.GetEncoding(1252);
                }
                catch (Exception /*e*/)
                {

                }
            }

            return StringToByteArray(value, ibm437);
        }

        internal static string Utf8StringFromBuffer(byte[] buf)
        {
            return StringFromBuffer(buf, utf8);
        }

        internal static string StringFromBuffer(byte[] buf, System.Text.Encoding encoding)
        {
            string s = encoding.GetString(buf);
            return s;
        }


        internal static int ReadSignature(System.IO.Stream s)
        {
            int x = 0;
            try { x = _ReadFourBytes(s, "n/a"); }
            catch (BadReadException) { }
            return x;
        }


        internal static int ReadEntrySignature(System.IO.Stream s)
        {
            // handle the case of ill-formatted zip archives - includes a data descriptor
            // when none is expected.
            int x = 0;
            try
            {
                x = _ReadFourBytes(s, "n/a");
                if (x == ZipConstants.ZipEntryDataDescriptorSignature)
                {
                    // advance past data descriptor - 12 bytes if not zip64
                    s.Seek(12, SeekOrigin.Current);
                    x = _ReadFourBytes(s, "n/a");
                    if (x != ZipConstants.ZipEntrySignature)
                    {
                        // Maybe zip64 was in use for the prior entry.
                        // Therefore, skip another 8 bytes.
                        s.Seek(8, SeekOrigin.Current);
                        x = _ReadFourBytes(s, "n/a");
                        if (x != ZipConstants.ZipEntrySignature)
                        {
                            // seek back to the first spot
                            s.Seek(-24, SeekOrigin.Current);
                            x = _ReadFourBytes(s, "n/a");
                        }
                    }
                }
            }
            catch (BadReadException) { }
            return x;
        }


        internal static int ReadInt(System.IO.Stream s)
        {
            return _ReadFourBytes(s, "Could not read block - no data!  (position 0x{0:X8})");
        }

        private static int _ReadFourBytes(System.IO.Stream s, string message)
        {
            int n = 0;
            byte[] block = new byte[4];
            n = s.Read(block, 0, block.Length);
            if (n != block.Length) throw new BadReadException(String.Format(message, s.Position));
            int data = unchecked((((block[3] * 256 + block[2]) * 256) + block[1]) * 256 + block[0]);
            return data;
        }



        internal static long FindSignature(System.IO.Stream stream, int SignatureToFind)
        {
            long startingPosition = stream.Position;

            int BATCH_SIZE = 65536; //  8192;
            byte[] targetBytes = new byte[4];
            targetBytes[0] = (byte)(SignatureToFind >> 24);
            targetBytes[1] = (byte)((SignatureToFind & 0x00FF0000) >> 16);
            targetBytes[2] = (byte)((SignatureToFind & 0x0000FF00) >> 8);
            targetBytes[3] = (byte)(SignatureToFind & 0x000000FF);
            byte[] batch = new byte[BATCH_SIZE];
            int n = 0;
            bool success = false;
            do
            {
                n = stream.Read(batch, 0, batch.Length);
                if (n != 0)
                {
                    for (int i = 0; i < n; i++)
                    {
                        if (batch[i] == targetBytes[3])
                        {
                            long curPosition = stream.Position;
                            stream.Seek(i - n, System.IO.SeekOrigin.Current);

                            // workitem 7711
                            int sig = ReadSignature(stream);

                            success = (sig == SignatureToFind);
                            if (!success)
                            {
                                stream.Seek(curPosition, System.IO.SeekOrigin.Begin);
                            }
                            else
                                break; // out of for loop
                        }
                    }
                }
                else break;
                if (success) break;

            } while (true);

            if (!success)
            {
                stream.Seek(startingPosition, System.IO.SeekOrigin.Begin);
                return -1;  // or throw?
            }

            // subtract 4 for the signature.
            long bytesRead = (stream.Position - startingPosition) - 4;

            return bytesRead;
        }


        // If I have a time in the .NET environment, and I want to use it for
        // SetWastWriteTime() etc, then I need to adjust it for Win32.
        internal static DateTime AdjustTime_Reverse(DateTime time)
        {
            if (time.Kind == DateTimeKind.Utc) return time;
            DateTime adjusted = time;
            if (DateTime.Now.IsDaylightSavingTime() && !time.IsDaylightSavingTime())
                adjusted = time - new System.TimeSpan(1, 0, 0);

            else if (!DateTime.Now.IsDaylightSavingTime() && time.IsDaylightSavingTime())
                adjusted = time + new System.TimeSpan(1, 0, 0);

            return adjusted;
        }



        internal static DateTime PackedToDateTime(Int32 packedDateTime)
        {
            // workitem 7074 & workitem 7170
            if (packedDateTime == 0xFFFF || packedDateTime == 0)
                return new System.DateTime(1995, 1, 1, 0, 0, 0, 0);  // return a fixed date when none is supplied.

            Int16 packedTime = unchecked((Int16)(packedDateTime & 0x0000ffff));
            Int16 packedDate = unchecked((Int16)((packedDateTime & 0xffff0000) >> 16));

            int year = 1980 + ((packedDate & 0xFE00) >> 9);
            int month = (packedDate & 0x01E0) >> 5;
            int day = packedDate & 0x001F;

            int hour = (packedTime & 0xF800) >> 11;
            int minute = (packedTime & 0x07E0) >> 5;
            //int second = packedTime & 0x001F;
            int second = (packedTime & 0x001F) * 2;

            // validation and error checking.
            // this is not foolproof but will catch most errors.
            if (second >= 60) { minute++; second = 0; }
            if (minute >= 60) { hour++; minute = 0; }
            if (hour >= 24) { day++; hour = 0; }

            DateTime d = System.DateTime.Now;
            bool success= false;
            try
            {
                d = new System.DateTime(year, month, day, hour, minute, second, 0);
                success= true;
            }
            catch (System.ArgumentOutOfRangeException)
            {
                if (year == 1980 && (month == 0 || day == 0))
                {
                    try
                    {
                        d = new System.DateTime(1980, 1, 1, hour, minute, second, 0);
                success= true;
                    }
                    catch (System.ArgumentOutOfRangeException)
                    {
                        try
                        {
                            d = new System.DateTime(1980, 1, 1, 0, 0, 0, 0);
                success= true;
                        }
                        catch (System.ArgumentOutOfRangeException) { }

                    }
                }
                // workitem 8814
                // my god, I can't believe how many different ways applications
                // can mess up a simple date format.
                else
                {
                    try
                    {
                        while (year < 1980) year++;
                        while (year > 2030) year--;
                        while (month < 1) month++;
                        while (month > 12) month--;
                        while (day < 1) day++;
                        while (day > 28) day--;
                        while (minute < 0) minute++;
                        while (minute > 59) minute--;
                        while (second < 0) second++;
                        while (second > 59) second--;
                        d = new System.DateTime(year, month, day, hour, minute, second, 0);
                        success= true;
                    }
                    catch (System.ArgumentOutOfRangeException) { }
                }
            }
            if (!success)
            {
                string msg = String.Format("y({0}) m({1}) d({2}) h({3}) m({4}) s({5})", year, month, day, hour, minute, second);
                throw new ZipException(String.Format("Bad date/time format in the zip file. ({0})", msg));

            }
            // workitem 6191
            //d = AdjustTime_Reverse(d);
            d = DateTime.SpecifyKind(d, DateTimeKind.Local);
            return d;
        }


        internal
         static Int32 DateTimeToPacked(DateTime time)
        {
            // The time is passed in here only for purposes of writing LastModified to the
            // zip archive. It should always be LocalTime, but we convert anyway.  And,
            // since the time is being written out, it needs to be adjusted.

            time = time.ToLocalTime();
            // workitem 7966
            //time = AdjustTime_Forward(time);

            // see http://www.vsft.com/hal/dostime.htm for the format
            UInt16 packedDate = (UInt16)((time.Day & 0x0000001F) | ((time.Month << 5) & 0x000001E0) | (((time.Year - 1980) << 9) & 0x0000FE00));
            UInt16 packedTime = (UInt16)((time.Second / 2 & 0x0000001F) | ((time.Minute << 5) & 0x000007E0) | ((time.Hour << 11) & 0x0000F800));

            Int32 result = (Int32)(((UInt32)(packedDate << 16)) | packedTime);
            return result;
        }



        public static void CreateAndOpenUniqueTempFile(string dir,
                                                       out Stream fs,
                                                       out string filename)
        {
            // workitem 9763
            // http://dotnet.org.za/markn/archive/2006/04/15/51594.aspx
            // try 3 times:
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    filename = Path.Combine(dir, InternalGetTempFileName());
                    fs = new FileStream(filename, FileMode.CreateNew);
                    return;
                }
                catch (IOException)
                {
                    if (i == 2) throw;
                }
            }
            throw new IOException();
        }

        public static string InternalGetTempFileName()
        {
            return "DotNetZip-" + Path.GetRandomFileName().Substring(0, 8) + ".tmp";
        }




        internal static int ReadWithRetry(System.IO.Stream s, byte[] buffer, int offset, int count, string FileName)
        {
            int n = 0;
            bool done = false;
            int retries = 0;

            do
            {
                try
                {
                    n = s.Read(buffer, offset, count);
                    done = true;
                }
                catch (System.IO.IOException ioexc1)
                {
                    // Check if we can call GetHRForException,
                    // which makes unmanaged code calls.
                    var p = new System.Security.Permissions.SecurityPermission(
                        System.Security.Permissions.SecurityPermissionFlag.UnmanagedCode);
                    if (p.IsUnrestricted())
                    {
                        uint hresult = _HRForException(ioexc1);
                        if (hresult != 0x80070021)  // ERROR_LOCK_VIOLATION
                            throw new System.IO.IOException(String.Format("Cannot read file {0}", FileName), ioexc1);
                        retries++;
                        if (retries > 10)
                            throw new System.IO.IOException(String.Format("Cannot read file {0}, at offset 0x{1:X8} after 10 retries", FileName, offset), ioexc1);

                        // max time waited on last retry = 250 + 10*550 = 5.75s
                        // aggregate time waited after 10 retries: 250 + 55*550 = 30.5s
                        System.Threading.Thread.Sleep(250 + retries * 550);
                    }
                    else
                    {
                        // The permission.Demand() failed. Therefore, we cannot call
                        // GetHRForException, and cannot do the subtle handling of
                        // ERROR_LOCK_VIOLATION.  Just bail.
                        throw;
                    }
                }
            }
            while (!done);

            return n;
        }


      
        private static uint _HRForException(System.Exception ex1)
        {
#if IOS
            return 0;
#else
            return unchecked((uint)System.Runtime.InteropServices.Marshal.GetHRForException(ex1));
#endif
        }

    }



    public class CountingStream : System.IO.Stream
    {
        // workitem 12374: this class is now public
        private System.IO.Stream _s;
        private Int64 _bytesWritten;
        private Int64 _bytesRead;
        private Int64 _initialOffset;


        public CountingStream(System.IO.Stream stream)
            : base()
        {
            _s = stream;
            try
            {
                _initialOffset = _s.Position;
            }
            catch
            {
                _initialOffset = 0L;
            }
        }


        public Stream WrappedStream
        {
            get
            {
                return _s;
            }
        }

        public Int64 BytesWritten
        {
            get { return _bytesWritten; }
        }

        public Int64 BytesRead
        {
            get { return _bytesRead; }
        }

        public void Adjust(Int64 delta)
        {
            _bytesWritten -= delta;
            if (_bytesWritten < 0)
                throw new InvalidOperationException();
            if (_s as CountingStream != null)
                ((CountingStream)_s).Adjust(delta);
        }


        public override int Read(byte[] buffer, int offset, int count)
        {
            int n = _s.Read(buffer, offset, count);
            _bytesRead += n;
            return n;
        }


        public override void Write(byte[] buffer, int offset, int count)
        {
            if (count == 0) return;
            _s.Write(buffer, offset, count);
            _bytesWritten += count;
        }

     
        public override bool CanRead
        {
            get { return _s.CanRead; }
        }

    
        public override bool CanSeek
        {
            get { return _s.CanSeek; }
        }

    
   
        public override bool CanWrite
        {
            get { return _s.CanWrite; }
        }

 
        public override void Flush()
        {
            _s.Flush();
        }


        public override long Length
        {
            get { return _s.Length; }   // bytesWritten??
        }


        public long ComputedPosition
        {
            get { return _initialOffset + _bytesWritten; }
        }



        public override long Position
        {
            get { return _s.Position; }
            set
            {
                _s.Seek(value, System.IO.SeekOrigin.Begin);
            }
        }


        public override long Seek(long offset, System.IO.SeekOrigin origin)
        {
            return _s.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            _s.SetLength(value);
        }
    }


}
