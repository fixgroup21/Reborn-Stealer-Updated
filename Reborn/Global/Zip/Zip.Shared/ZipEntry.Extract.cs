
using System;
using System.IO;

namespace Ionic.Zip
{

    public partial class ZipEntry
    {

        internal Crc.CrcCalculatorStream InternalOpenReader(string password)
        {
            ValidateCompression(_CompressionMethod_FromZipFile, FileName, GetUnsupportedCompressionMethod(_CompressionMethod));
            ValidateEncryption(Encryption, FileName, _UnsupportedAlgorithmId);
            SetupCryptoForExtract(password);

            // workitem 7958
            if (this._Source != ZipEntrySource.ZipFile)
                throw new BadStateException("You must call ZipFile.Save before calling OpenReader");
            var leftToRead = (_CompressionMethod_FromZipFile == (short)CompressionMethod.None)
                ? _CompressedFileDataSize
                : UncompressedSize;

            this.ArchiveStream.Seek(this.FileDataPosition, SeekOrigin.Begin);

            _inputDecryptorStream = GetExtractDecryptor(ArchiveStream);
            var input3 = GetExtractDecompressor(_inputDecryptorStream);

            return new Crc.CrcCalculatorStream(input3, leftToRead);
        }


        void WriteStatus(string format, params Object[] args)
        {
            if (_container.ZipFile != null && _container.ZipFile.Verbose)
                _container.ZipFile.StatusMessageTextWriter.WriteLine(format, args);
        }


        internal void VerifyCrcAfterExtract(Int32 calculatedCrc32, EncryptionAlgorithm encryptionAlgorithm, int expectedCrc32, Stream archiveStream, long uncompressedSize)
        {

            if (calculatedCrc32 != expectedCrc32)
                throw new BadCrcException("CRC error: the file being extracted appears to be corrupted. " +
                                          String.Format("Expected 0x{0:X8}, Actual 0x{1:X8}", expectedCrc32, calculatedCrc32));
        }

   
        void _CheckRead(int nbytes)
        {
            if (nbytes == 0)
                throw new BadReadException(String.Format("bad read of entry {0} from compressed archive.",
                             FileName));
        }

        Stream _inputDecryptorStream;


        Stream GetExtractDecompressor(Stream input2)
        {
            if (input2 == null) throw new ArgumentNullException("input2");

            // get a stream that either decompresses or not.
            switch (_CompressionMethod_FromZipFile)
            {
                case (short)CompressionMethod.None:
                    return input2;
                case (short)CompressionMethod.Deflate:
                    return new Zlib.DeflateStream(input2, Zlib.CompressionMode.Decompress, true);

            }

            throw new Exception(string.Format("Failed to find decompressor matching {0}",
                _CompressionMethod_FromZipFile));
        }

        Stream GetExtractDecryptor(Stream input)
        {
            if (input == null) throw new ArgumentNullException("input");

            Stream input2;
            if (_Encryption_FromZipFile == EncryptionAlgorithm.PkzipWeak)
                input2 = new ZipCipherStream(input, _zipCrypto_forExtract, CryptoMode.Decrypt);



            else
                input2 = input;

            return input2;
        }






        #region Support methods

        // workitem 7968

        static string GetUnsupportedAlgorithm(uint unsupportedAlgorithmId)
        {
            string alg;
            switch (unsupportedAlgorithmId)
            {
                case 0:
                    alg = "--";
                    break;
                case 0x6601:
                    alg = "DES";
                    break;
                case 0x6602: // - RC2 (version needed to extract < 5.2)
                    alg = "RC2";
                    break;
                case 0x6603: // - 3DES 168
                    alg = "3DES-168";
                    break;
                case 0x6609: // - 3DES 112
                    alg = "3DES-112";
                    break;
                case 0x660E: // - AES 128
                    alg = "PKWare AES128";
                    break;
                case 0x660F: // - AES 192
                    alg = "PKWare AES192";
                    break;
                case 0x6610: // - AES 256
                    alg = "PKWare AES256";
                    break;
                case 0x6702: // - RC2 (version needed to extract >= 5.2)
                    alg = "RC2";
                    break;
                case 0x6720: // - Blowfish
                    alg = "Blowfish";
                    break;
                case 0x6721: // - Twofish
                    alg = "Twofish";
                    break;
                case 0x6801: // - RC4
                    alg = "RC4";
                    break;
                case 0xFFFF: // - Unknown algorithm
                default:
                    alg = String.Format("Unknown (0x{0:X4})", unsupportedAlgorithmId);
                    break;
            }
            return alg;
        }

        // workitem 7968

        static string GetUnsupportedCompressionMethod(short compressionMethod)
        {
            string meth;
            switch ((int) compressionMethod)
            {
                case 0:
                    meth = "Store";
                    break;
                case 1:
                    meth = "Shrink";
                    break;
                case 8:
                    meth = "DEFLATE";
                    break;
                case 9:
                    meth = "Deflate64";
                    break;
                case 12:
                    meth = "BZIP2"; // only if BZIP not compiled in
                    break;
                case 14:
                    meth = "LZMA";
                    break;
                case 19:
                    meth = "LZ77";
                    break;
                case 98:
                    meth = "PPMd";
                    break;
                default:
                    meth = String.Format("Unknown (0x{0:X4})", compressionMethod);
                    break;
            }
            return meth;
        }

        static void ValidateEncryption(EncryptionAlgorithm encryptionAlgorithm, string fileName, uint unsupportedAlgorithmId)
        {
            if (encryptionAlgorithm != EncryptionAlgorithm.PkzipWeak &&
                encryptionAlgorithm != EncryptionAlgorithm.None)
            {
                // workitem 7968
                if (unsupportedAlgorithmId != 0)
                    throw new ZipException(string.Format("Cannot extract: Entry {0} is encrypted with an algorithm not supported by DotNetZip: {1}",
                                                         fileName, GetUnsupportedAlgorithm(unsupportedAlgorithmId)));
                throw new ZipException(string.Format("Cannot extract: Entry {0} uses an unsupported encryption algorithm ({1:X2})",
                                                     fileName, (int)encryptionAlgorithm));
            }
        }

        static void ValidateCompression(short compressionMethod, string fileName, string compressionMethodName)
        {
            if ((compressionMethod != (short)CompressionMethod.None) &&
                (compressionMethod != (short)CompressionMethod.Deflate)

                )
                throw new ZipException(String.Format("Entry {0} uses an unsupported compression method (0x{1:X2}, {2})",
                                                          fileName, compressionMethod, compressionMethodName));
        }

        void SetupCryptoForExtract(string password)
        {
            //if (password == null) return;
            if (_Encryption_FromZipFile == EncryptionAlgorithm.None) return;

            if (_Encryption_FromZipFile == EncryptionAlgorithm.PkzipWeak)
            {
                if (password == null)
                    throw new ZipException("Missing password.");

                this.ArchiveStream.Seek(this.FileDataPosition - 12, SeekOrigin.Begin);
                _zipCrypto_forExtract = ZipCrypto.ForRead(password, this);
            }
        }
        #endregion

    }
}
