

using System;
using Interop = System.Runtime.InteropServices;

namespace Ionic.Crc
{


#if !PCL
    [Interop.GuidAttribute("ebc25cf6-9120-4283-b972-0e5520d0000C")]
    [Interop.ComVisible(true)]
    [Interop.ClassInterface(Interop.ClassInterfaceType.AutoDispatch)]
#endif
    public class CRC32
    {


        public Int64 TotalBytesRead
        {
            get
            {
                return _TotalBytesRead;
            }
        }

   
        public Int32 Crc32Result
        {
            get
            {
                return unchecked((Int32)(~_register));
            }
        }

        public Int32 GetCrc32(System.IO.Stream input)
        {
            return GetCrc32AndCopy(input, null);
        }


        public Int32 GetCrc32AndCopy(System.IO.Stream input, System.IO.Stream output)
        {
            if (input == null)
                throw new Exception("The input stream must not be null.");

            unchecked
            {
                byte[] buffer = new byte[BUFFER_SIZE];
                int readSize = BUFFER_SIZE;

                _TotalBytesRead = 0;
                int count = input.Read(buffer, 0, readSize);
                if (output != null) output.Write(buffer, 0, count);
                _TotalBytesRead += count;
                while (count > 0)
                {
                    SlurpBlock(buffer, 0, count);
                    count = input.Read(buffer, 0, readSize);
                    if (output != null) output.Write(buffer, 0, count);
                    _TotalBytesRead += count;
                }

                return (Int32)(~_register);
            }
        }



        public Int32 ComputeCrc32(Int32 W, byte B)
        {
            return _InternalComputeCrc32((UInt32)W, B);
        }

        internal Int32 _InternalComputeCrc32(UInt32 W, byte B)
        {
            return (Int32)(crc32Table[(W ^ B) & 0xFF] ^ (W >> 8));
        }


        public void SlurpBlock(byte[] block, int offset, int count)
        {
            if (block == null)
                throw new Exception("The data buffer must not be null.");

            // bzip algorithm
            for (int i = 0; i < count; i++)
            {
                int x = offset + i;
                byte b = block[x];
                if (this.reverseBits)
                {
                    UInt32 temp = (_register >> 24) ^ b;
                    _register = (_register << 8) ^ crc32Table[temp];
                }
                else
                {
                    UInt32 temp = (_register & 0x000000FF) ^ b;
                    _register = (_register >> 8) ^ crc32Table[temp];
                }
            }
            _TotalBytesRead += count;
        }





        private static uint ReverseBits(uint data)
        {
            unchecked
            {
                uint ret = data;
                ret = (ret & 0x55555555) << 1 | (ret >> 1) & 0x55555555;
                ret = (ret & 0x33333333) << 2 | (ret >> 2) & 0x33333333;
                ret = (ret & 0x0F0F0F0F) << 4 | (ret >> 4) & 0x0F0F0F0F;
                ret = (ret << 24) | ((ret & 0xFF00) << 8) | ((ret >> 8) & 0xFF00) | (ret >> 24);
                return ret;
            }
        }

        private static byte ReverseBits(byte data)
        {
            unchecked
            {
                uint u = (uint)data * 0x00020202;
                uint m = 0x01044010;
                uint s = u & m;
                uint t = (u << 2) & (m << 1);
                return (byte)((0x01001001 * (s + t)) >> 24);
            }
        }



        private void GenerateLookupTable()
        {
            crc32Table = new UInt32[256];
            unchecked
            {
                UInt32 dwCrc;
                byte i = 0;
                do
                {
                    dwCrc = i;
                    for (byte j = 8; j > 0; j--)
                    {
                        if ((dwCrc & 1) == 1)
                        {
                            dwCrc = (dwCrc >> 1) ^ dwPolynomial;
                        }
                        else
                        {
                            dwCrc >>= 1;
                        }
                    }
                    if (reverseBits)
                    {
                        crc32Table[ReverseBits(i)] = ReverseBits(dwCrc);
                    }
                    else
                    {
                        crc32Table[i] = dwCrc;
                    }
                    i++;
                } while (i!=0);
            }


        }


        private uint gf2_matrix_times(uint[] matrix, uint vec)
        {
            uint sum = 0;
            int i=0;
            while (vec != 0)
            {
                if ((vec & 0x01)== 0x01)
                    sum ^= matrix[i];
                vec >>= 1;
                i++;
            }
            return sum;
        }

        private void gf2_matrix_square(uint[] square, uint[] mat)
        {
            for (int i = 0; i < 32; i++)
                square[i] = gf2_matrix_times(mat, mat[i]);
        }


        public void Combine(int crc, int length)
        {
            uint[] even = new uint[32];     // even-power-of-two zeros operator
            uint[] odd = new uint[32];      // odd-power-of-two zeros operator

            if (length == 0)
                return;

            uint crc1= ~_register;
            uint crc2= (uint) crc;

            // put operator for one zero bit in odd
            odd[0] = this.dwPolynomial;  // the CRC-32 polynomial
            uint row = 1;
            for (int i = 1; i < 32; i++)
            {
                odd[i] = row;
                row <<= 1;
            }

            // put operator for two zero bits in even
            gf2_matrix_square(even, odd);

            // put operator for four zero bits in odd
            gf2_matrix_square(odd, even);

            uint len2 = (uint) length;

            // apply len2 zeros to crc1 (first square will put the operator for one
            // zero byte, eight zero bits, in even)
            do {
                // apply zeros operator for this bit of len2
                gf2_matrix_square(even, odd);

                if ((len2 & 1)== 1)
                    crc1 = gf2_matrix_times(even, crc1);
                len2 >>= 1;

                if (len2 == 0)
                    break;

                // another iteration of the loop with odd and even swapped
                gf2_matrix_square(odd, even);
                if ((len2 & 1)==1)
                    crc1 = gf2_matrix_times(odd, crc1);
                len2 >>= 1;


            } while (len2 != 0);

            crc1 ^= crc2;

            _register= ~crc1;

            //return (int) crc1;
            return;
        }


 
        public CRC32() : this(false)
        {
        }


        public CRC32(bool reverseBits) :
            this( unchecked((int)0xEDB88320), reverseBits)
        {
        }


        public CRC32(int polynomial, bool reverseBits)
        {
            this.reverseBits = reverseBits;
            this.dwPolynomial = (uint) polynomial;
            this.GenerateLookupTable();
        }



        // private member vars
        private UInt32 dwPolynomial;
        private Int64 _TotalBytesRead;
        private bool reverseBits;
        private UInt32[] crc32Table;
        private const int BUFFER_SIZE = 8192;
        private UInt32 _register = 0xFFFFFFFFU;
    }



    public class CrcCalculatorStream : System.IO.Stream, IDisposable
    {
        static readonly Int64 UnsetLengthLimit = -99;

        readonly System.IO.Stream _innerStream;
        readonly CRC32 _crc32;
        readonly Int64 _lengthLimit = -99;
        bool _leaveOpen;


        public CrcCalculatorStream(System.IO.Stream stream)
            : this(true, UnsetLengthLimit, stream, null)
        {
        }


        public CrcCalculatorStream(System.IO.Stream stream, bool leaveOpen)
            : this(leaveOpen, UnsetLengthLimit, stream, null)
        {
        }

        public CrcCalculatorStream(System.IO.Stream stream, Int64 length)
            : this(true, length, stream, null)
        {
            if (length < 0)
                throw new ArgumentException("length");
        }



        // This ctor is private - no validation except null is done here.
        // This is to allow the use
        // of a (specific) negative value for the _lengthLimit, to indicate that there
        // is no length set.  So we validate the length limit in those ctors that use an
        // explicit param, otherwise we don't validate, because it could be our special
        // value.
        CrcCalculatorStream(bool leaveOpen, Int64 length, System.IO.Stream stream, CRC32 crc32)
        {
            if (stream == null) throw new ArgumentNullException("stream");
            _innerStream = stream;
            _crc32 = crc32 ?? new CRC32();
            _lengthLimit = length;
            _leaveOpen = leaveOpen;
        }



        public Int64 TotalBytesSlurped
        {
            get { return _crc32.TotalBytesRead; }
        }

        public Int32 Crc
        {
            get { return _crc32.Crc32Result; }
        }


        public override int Read(byte[] buffer, int offset, int count)
        {
            int bytesToRead = count;

            // Need to limit the # of bytes returned, if the stream is intended to have
            // a definite length.  This is especially useful when returning a stream for
            // the uncompressed data directly to the application.  The app won't
            // necessarily read only the UncompressedSize number of bytes.  For example
            // wrapping the stream returned from OpenReader() into a StreadReader() and
            // calling ReadToEnd() on it, We can "over-read" the zip data and get a
            // corrupt string.  The length limits that, prevents that problem.

            if (_lengthLimit != CrcCalculatorStream.UnsetLengthLimit)
            {
                if (_crc32.TotalBytesRead >= _lengthLimit) return 0; // EOF
                Int64 bytesRemaining = _lengthLimit - _crc32.TotalBytesRead;
                if (bytesRemaining < count) bytesToRead = (int)bytesRemaining;
            }
            int n = _innerStream.Read(buffer, offset, bytesToRead);
            if (n > 0) _crc32.SlurpBlock(buffer, offset, n);
            return n;
        }


        public override void Write(byte[] buffer, int offset, int count)
        {
            if (count > 0) _crc32.SlurpBlock(buffer, offset, count);
            _innerStream.Write(buffer, offset, count);
        }

   
        public override bool CanRead
        {
            get { return _innerStream.CanRead; }
        }

 
        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return _innerStream.CanWrite; }
        }

    
        public override void Flush()
        {
            _innerStream.Flush();
        }


        public override long Length
        {
            get
            {
                if (_lengthLimit == CrcCalculatorStream.UnsetLengthLimit)
                    return _innerStream.Length;
                else return _lengthLimit;
            }
        }


        public override long Position
        {
            get { return _crc32.TotalBytesRead; }
            set { throw new NotSupportedException(); }
        }


        public override long Seek(long offset, System.IO.SeekOrigin origin)
        {
            throw new NotSupportedException();
        }


        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }


        void IDisposable.Dispose()
        {
            InnerClose();
        }

        private void InnerClose()
        {
            if (!_leaveOpen)
            {

                _innerStream.Close();

            }
        }

        public override void Close()
        {
            base.Close();
            InnerClose();
        }


    }

}
