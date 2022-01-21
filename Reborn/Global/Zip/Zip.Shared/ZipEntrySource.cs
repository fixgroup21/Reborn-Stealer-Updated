// ZipEntrySource.cs

namespace Ionic.Zip
{

    public enum ZipEntrySource
    {

        None = 0,


        FileSystem,

        Stream,


        ZipFile,
        

        WriteDelegate,
        

        JitStream,
        

        ZipOutputStream,
    }
    
}