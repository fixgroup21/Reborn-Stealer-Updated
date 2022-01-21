
using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.Serialization;

namespace Ionic.Zip
{
    [Serializable]
    [System.Runtime.InteropServices.GuidAttribute("ebc25cf6-9120-4283-b972-0e5520d0000B")]
    public class BadPasswordException : ZipException
    {

        public BadPasswordException() { }


        public BadPasswordException(String message)
            : base(message)
        { }

    }


    [Serializable]
    [System.Runtime.InteropServices.GuidAttribute("ebc25cf6-9120-4283-b972-0e5520d0000A")]
    public class BadReadException : ZipException
    {


        public BadReadException(String message)
            : base(message)
        { }

    }



    [Serializable]
    [System.Runtime.InteropServices.GuidAttribute("ebc25cf6-9120-4283-b972-0e5520d00009")]
    public class BadCrcException : ZipException
    {
        public BadCrcException(String message)
            : base(message)
        { }

    }




    [Serializable]
    [System.Runtime.InteropServices.GuidAttribute("ebc25cf6-9120-4283-b972-0e5520d00007")]
    public class BadStateException : ZipException
    {

        public BadStateException(String message)
            : base(message)
        { }

        public BadStateException(String message, Exception innerException)
            : base(message, innerException)
        {}
    }

    [Serializable]
    [System.Runtime.InteropServices.GuidAttribute("ebc25cf6-9120-4283-b972-0e5520d00006")]
    public class ZipException : Exception
    {

        public ZipException() { }


        public ZipException(String message) : base(message) { }

        public ZipException(String message, Exception innerException)
            : base(message, innerException)
        { }


    }

}
