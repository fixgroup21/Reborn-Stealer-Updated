
using System;
using System.Collections.Generic;
using System.Text;

namespace Ionic.Zip
{
    public delegate void WriteDelegate(string entryName, System.IO.Stream stream);



    public delegate System.IO.Stream OpenDelegate(string entryName);

  
    public delegate void CloseDelegate(string entryName, System.IO.Stream stream);


    public delegate Ionic.Zlib.CompressionLevel SetCompressionCallback(string localFileName, string fileNameInArchive);


    public enum ZipProgressEventType
    {
     
        Adding_Started,

   
        Adding_AfterAddEntry,

    
        Adding_Completed,

        Reading_Started,

     
        Reading_BeforeReadEntry,

     
        Reading_AfterReadEntry,

      
        Reading_Completed,

   
        Reading_ArchiveBytesRead,

        Saving_Started,

       
        Saving_BeforeWriteEntry,

     
        Saving_AfterWriteEntry,

       
        Saving_Completed,

      
        Saving_AfterSaveTempArchive,

   
        Saving_BeforeRenameTempArchive,

   
        Saving_AfterRenameTempArchive,

        
     
        Saving_AfterCompileSelfExtractor,


        Saving_EntryBytesRead,


        Extracting_BeforeExtractEntry,



        Extracting_AfterExtractEntry,

        Extracting_ExtractEntryWouldOverwrite,


        Extracting_EntryBytesWritten,


        Extracting_BeforeExtractAll,


        Extracting_AfterExtractAll,


        Error_Saving,
    }


    public class ZipProgressEventArgs : EventArgs
    {
        private int _entriesTotal;
        private bool _cancel;
        private ZipEntry _latestEntry;
        private ZipProgressEventType _flavor;
        private String _archiveName;
        private Int64 _bytesTransferred;
        private Int64 _totalBytesToTransfer;


        internal ZipProgressEventArgs() { }

        internal ZipProgressEventArgs(string archiveName, ZipProgressEventType flavor)
        {
            this._archiveName = archiveName;
            this._flavor = flavor;
        }

 
        public int EntriesTotal
        {
            get { return _entriesTotal; }
            set { _entriesTotal = value; }
        }


        public ZipEntry CurrentEntry
        {
            get { return _latestEntry; }
            set { _latestEntry = value; }
        }

        public bool Cancel
        {
            get { return _cancel; }
            set { _cancel = _cancel || value; }
        }

        public ZipProgressEventType EventType
        {
            get { return _flavor; }
            set { _flavor = value; }
        }

   
        public String ArchiveName
        {
            get { return _archiveName; }
            set { _archiveName = value; }
        }


 
        public Int64 BytesTransferred
        {
            get { return _bytesTransferred; }
            set { _bytesTransferred = value; }
        }



 
        public Int64 TotalBytesToTransfer
        {
            get { return _totalBytesToTransfer; }
            set { _totalBytesToTransfer = value; }
        }
    }




    public class ReadProgressEventArgs : ZipProgressEventArgs
    {


        private ReadProgressEventArgs(string archiveName, ZipProgressEventType flavor)
            : base(archiveName, flavor)
        { }

        internal static ReadProgressEventArgs Before(string archiveName, int entriesTotal)
        {
            var x = new ReadProgressEventArgs(archiveName, ZipProgressEventType.Reading_BeforeReadEntry);
            x.EntriesTotal = entriesTotal;
            return x;
        }

        internal static ReadProgressEventArgs After(string archiveName, ZipEntry entry, int entriesTotal)
        {
            var x = new ReadProgressEventArgs(archiveName, ZipProgressEventType.Reading_AfterReadEntry);
            x.EntriesTotal = entriesTotal;
            x.CurrentEntry = entry;
            return x;
        }

        internal static ReadProgressEventArgs Started(string archiveName)
        {
            var x = new ReadProgressEventArgs(archiveName, ZipProgressEventType.Reading_Started);
            return x;
        }

        internal static ReadProgressEventArgs ByteUpdate(string archiveName, ZipEntry entry, Int64 bytesXferred, Int64 totalBytes)
        {
            var x = new ReadProgressEventArgs(archiveName, ZipProgressEventType.Reading_ArchiveBytesRead);
            x.CurrentEntry = entry;
            x.BytesTransferred = bytesXferred;
            x.TotalBytesToTransfer = totalBytes;
            return x;
        }

        internal static ReadProgressEventArgs Completed(string archiveName)
        {
            var x = new ReadProgressEventArgs(archiveName, ZipProgressEventType.Reading_Completed);
            return x;
        }

    }



    public class AddProgressEventArgs : ZipProgressEventArgs
    {
        internal AddProgressEventArgs() { }

        private AddProgressEventArgs(string archiveName, ZipProgressEventType flavor)
            : base(archiveName, flavor)
        { }

        internal static AddProgressEventArgs AfterEntry(string archiveName, ZipEntry entry, int entriesTotal)
        {
            var x = new AddProgressEventArgs(archiveName, ZipProgressEventType.Adding_AfterAddEntry);
            x.EntriesTotal = entriesTotal;
            x.CurrentEntry = entry;
            return x;
        }

        internal static AddProgressEventArgs Started(string archiveName)
        {
            var x = new AddProgressEventArgs(archiveName, ZipProgressEventType.Adding_Started);
            return x;
        }

        internal static AddProgressEventArgs Completed(string archiveName)
        {
            var x = new AddProgressEventArgs(archiveName, ZipProgressEventType.Adding_Completed);
            return x;
        }

    }

    public class SaveProgressEventArgs : ZipProgressEventArgs
    {
        private int _entriesSaved;

        internal SaveProgressEventArgs(string archiveName, bool before, int entriesTotal, int entriesSaved, ZipEntry entry)
            : base(archiveName, (before) ? ZipProgressEventType.Saving_BeforeWriteEntry : ZipProgressEventType.Saving_AfterWriteEntry)
        {
            this.EntriesTotal = entriesTotal;
            this.CurrentEntry = entry;
            this._entriesSaved = entriesSaved;
        }


        internal SaveProgressEventArgs(string archiveName, ZipProgressEventType flavor)
            : base(archiveName, flavor)
        { }


        internal static SaveProgressEventArgs ByteUpdate(string archiveName, ZipEntry entry, Int64 bytesXferred, Int64 totalBytes)
        {
            var x = new SaveProgressEventArgs(archiveName, ZipProgressEventType.Saving_EntryBytesRead);
            x.ArchiveName = archiveName;
            x.CurrentEntry = entry;
            x.BytesTransferred = bytesXferred;
            x.TotalBytesToTransfer = totalBytes;
            return x;
        }

        internal static SaveProgressEventArgs Started(string archiveName)
        {
            var x = new SaveProgressEventArgs(archiveName, ZipProgressEventType.Saving_Started);
            return x;
        }

        internal static SaveProgressEventArgs Completed(string archiveName)
        {
            var x = new SaveProgressEventArgs(archiveName, ZipProgressEventType.Saving_Completed);
            return x;
        }

    }

   



    public class ZipErrorEventArgs : ZipProgressEventArgs
    {
        private Exception _exc;
        private ZipErrorEventArgs() { }
        internal static ZipErrorEventArgs Saving(string archiveName, ZipEntry entry, Exception exception)
        {
            var x = new ZipErrorEventArgs
                {
                    EventType = ZipProgressEventType.Error_Saving,
                    ArchiveName = archiveName,
                    CurrentEntry = entry,
                    _exc = exception
                };
            return x;
        }
    }


}
