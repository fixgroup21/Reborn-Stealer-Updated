using System;
using System.Collections.Generic;
using System.Text;

namespace Ionic.Zip
{
    partial class ZipFile
    {
        private static System.Text.Encoding _defaultEncoding = null;
        private static bool _defaultEncodingInitialized = false;



        public static System.Text.Encoding DefaultEncoding
        {
            get
            {
                return _defaultEncoding;
            }
            set
            {
                if (_defaultEncodingInitialized)
                {
                    return;
                }
                _defaultEncoding = value;
                _defaultEncodingInitialized = true;
            }
        }
    }
}
