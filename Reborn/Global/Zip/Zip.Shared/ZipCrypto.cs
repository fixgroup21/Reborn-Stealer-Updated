
using System;

namespace Ionic.Zip
{

    internal class ZipCrypto
    {
        private ZipCrypto() { }

        public static ZipCrypto ForWrite(string password)
        {
            ZipCrypto z = new ZipCrypto();
            if (password == null)
                throw new BadPasswordException("This entry requires a password.");
            z.InitCipher(password);
            return z;
        }


        public static ZipCrypto ForRead(string password, ZipEntry e)
        {
            System.IO.Stream s = e._archiveStream;
            e._WeakEncryptionHeader = new byte[12];
            byte[] eh = e._WeakEncryptionHeader;
            ZipCrypto z = new ZipCrypto();

            if (password == null)
                throw new BadPasswordException("This entry requires a password.");

            z.InitCipher(password);

            ZipEntry.ReadWeakEncryptionHeader(s, eh);

            // Decrypt the header.  This has a side effect of "further initializing the
            // encryption keys" in the traditional zip encryption.
            byte[] DecryptedHeader = z.DecryptMessage(eh, eh.Length);

            // CRC check
            // According to the pkzip spec, the final byte in the decrypted header
            // is the highest-order byte in the CRC. We check it here.
            if (DecryptedHeader[11] != (byte)((e._Crc32 >> 24) & 0xff))
            {

                if ((e._BitField & 0x0008) != 0x0008)
                {
                    throw new BadPasswordException("The password did not match.");
                }
                else if (DecryptedHeader[11] != (byte)((e._TimeBlob >> 8) & 0xff))
                {
                    throw new BadPasswordException("The password did not match.");
                }

                // We have a good password.
            }
            else
            {
                // A-OK
            }
            return z;
        }


        private byte MagicByte
        {
            get
            {
                UInt16 t = (UInt16)((UInt16)(_Keys[2] & 0xFFFF) | 2);
                return (byte)((t * (t ^ 1)) >> 8);
            }
        }

        public byte[] DecryptMessage(byte[] cipherText, int length)
        {
            if (cipherText == null)
                throw new ArgumentNullException("cipherText");

            if (length > cipherText.Length)
                throw new ArgumentOutOfRangeException("length",
                                                      "Bad length during Decryption: the length parameter must be smaller than or equal to the size of the destination array.");

            byte[] plainText = new byte[length];
            for (int i = 0; i < length; i++)
            {
                byte C = (byte)(cipherText[i] ^ MagicByte);
                UpdateKeys(C);
                plainText[i] = C;
            }
            return plainText;
        }

        public byte[] EncryptMessage(byte[] plainText, int length)
        {
            if (plainText == null)
                throw new ArgumentNullException("plaintext");

            if (length > plainText.Length)
                throw new ArgumentOutOfRangeException("length",
                                                      "Bad length during Encryption: The length parameter must be smaller than or equal to the size of the destination array.");

            byte[] cipherText = new byte[length];
            for (int i = 0; i < length; i++)
            {
                byte C = plainText[i];
                cipherText[i] = (byte)(plainText[i] ^ MagicByte);
                UpdateKeys(C);
            }
            return cipherText;
        }


  
        public void InitCipher(string passphrase)
        {
            byte[] p = SharedUtilities.StringToByteArray(passphrase);
            for (int i = 0; i < passphrase.Length; i++)
                UpdateKeys(p[i]);
        }


        private void UpdateKeys(byte byteValue)
        {
            _Keys[0] = (UInt32)crc32.ComputeCrc32((int)_Keys[0], byteValue);
            _Keys[1] = _Keys[1] + (byte)_Keys[0];
            _Keys[1] = _Keys[1] * 0x08088405 + 1;
            _Keys[2] = (UInt32)crc32.ComputeCrc32((int)_Keys[2], (byte)(_Keys[1] >> 24));
        }



        // private fields for the crypto stuff:
        private UInt32[] _Keys = { 0x12345678, 0x23456789, 0x34567890 };
        private Ionic.Crc.CRC32 crc32 = new Ionic.Crc.CRC32();

    }

    internal enum CryptoMode
    {
        Encrypt,
        Decrypt
    }


    internal class ZipCipherStream : System.IO.Stream
    {
        private ZipCrypto _cipher;
        private System.IO.Stream _s;
        private CryptoMode _mode;

     
        public ZipCipherStream(System.IO.Stream s, ZipCrypto cipher, CryptoMode mode)
        {
            if (s == null) throw new ArgumentNullException("s");
            _cipher = cipher;
            _s = s;
            _mode = mode;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_mode == CryptoMode.Encrypt)
                throw new NotSupportedException("This stream does not encrypt via Read()");

            if (buffer == null)
                throw new ArgumentNullException("buffer");

            byte[] db = new byte[count];
            int n = _s.Read(db, 0, count);
            byte[] decrypted = _cipher.DecryptMessage(db, n);
            for (int i = 0; i < n; i++)
            {
                buffer[offset + i] = decrypted[i];
            }
            return n;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (_mode == CryptoMode.Decrypt)
                throw new NotSupportedException("This stream does not Decrypt via Write()");

            if (buffer == null)
                throw new ArgumentNullException("buffer");

            // workitem 7696
            if (count == 0) return;

            byte[] plaintext = null;
            if (offset != 0)
            {
                plaintext = new byte[count];
                for (int i = 0; i < count; i++)
                {
                    plaintext[i] = buffer[offset + i];
                }
            }
            else plaintext = buffer;

            byte[] encrypted = _cipher.EncryptMessage(plaintext, count);
            _s.Write(encrypted, 0, encrypted.Length);
        }


        public override bool CanRead
        {
            get { return (_mode == CryptoMode.Decrypt); }
        }
        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return (_mode == CryptoMode.Encrypt); }
        }

        public override void Flush()
        {
            //throw new NotSupportedException();
        }

        public override long Length
        {
            get { throw new NotSupportedException(); }
        }

        public override long Position
        {
            get { throw new NotSupportedException(); }
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
    }
}
