

using System;
using System.IO;

namespace Ionic.Zip
{
    public partial class ZipFile
    {
        private string ArchiveNameForEvent
        {
            get
            {
                return (_name != null) ? _name : "(stream)";
            }
        }

        #region Save

 
        public event EventHandler<SaveProgressEventArgs> SaveProgress;


        internal bool OnSaveBlock(ZipEntry entry, Int64 bytesXferred, Int64 totalBytesToXfer)
        {
            EventHandler<SaveProgressEventArgs> sp = SaveProgress;
            if (sp != null)
            {
                var e = SaveProgressEventArgs.ByteUpdate(ArchiveNameForEvent, entry,
                                                         bytesXferred, totalBytesToXfer);
                sp(this, e);
                if (e.Cancel)
                    _saveOperationCanceled = true;
            }
            return _saveOperationCanceled;
        }

        private void OnSaveEntry(int current, ZipEntry entry, bool before)
        {
            EventHandler<SaveProgressEventArgs> sp = SaveProgress;
            if (sp != null)
            {
                var e = new SaveProgressEventArgs(ArchiveNameForEvent, before, _entries.Count, current, entry);
                sp(this, e);
                if (e.Cancel)
                    _saveOperationCanceled = true;
            }
        }

        private void OnSaveEvent(ZipProgressEventType eventFlavor)
        {
            EventHandler<SaveProgressEventArgs> sp = SaveProgress;
            if (sp != null)
            {
                var e = new SaveProgressEventArgs(ArchiveNameForEvent, eventFlavor);
                sp(this, e);
                if (e.Cancel)
                    _saveOperationCanceled = true;
            }
        }

        private void OnSaveStarted()
        {
            EventHandler<SaveProgressEventArgs> sp = SaveProgress;
            if (sp != null)
            {
                var e = SaveProgressEventArgs.Started(ArchiveNameForEvent);
                sp(this, e);
                if (e.Cancel)
                    _saveOperationCanceled = true;
            }
        }
        private void OnSaveCompleted()
        {
            EventHandler<SaveProgressEventArgs> sp = SaveProgress;
            if (sp != null)
            {
                var e = SaveProgressEventArgs.Completed(ArchiveNameForEvent);
                sp(this, e);
            }
        }
        #endregion


        #region Read

        public event EventHandler<ReadProgressEventArgs> ReadProgress;

        private void OnReadStarted()
        {
            EventHandler<ReadProgressEventArgs> rp = ReadProgress;
            if (rp != null)
            {
                    var e = ReadProgressEventArgs.Started(ArchiveNameForEvent);
                    rp(this, e);
            }
        }

        private void OnReadCompleted()
        {
            EventHandler<ReadProgressEventArgs> rp = ReadProgress;
            if (rp != null)
            {
                    var e = ReadProgressEventArgs.Completed(ArchiveNameForEvent);
                    rp(this, e);
            }
        }

        internal void OnReadBytes(ZipEntry entry)
        {
            EventHandler<ReadProgressEventArgs> rp = ReadProgress;
            if (rp != null)
            {
                    var e = ReadProgressEventArgs.ByteUpdate(ArchiveNameForEvent,
                                        entry,
                                        ReadStream.Position,
                                        LengthOfReadStream);
                    rp(this, e);
            }
        }

        internal void OnReadEntry(bool before, ZipEntry entry)
        {
            EventHandler<ReadProgressEventArgs> rp = ReadProgress;
            if (rp != null)
            {
                ReadProgressEventArgs e = (before)
                    ? ReadProgressEventArgs.Before(ArchiveNameForEvent, _entries.Count)
                    : ReadProgressEventArgs.After(ArchiveNameForEvent, entry, _entries.Count);
                rp(this, e);
            }
        }

        private Int64 _lengthOfReadStream = -99;
        private Int64 LengthOfReadStream
        {
            get
            {
                if (_lengthOfReadStream == -99)
                {
                    _lengthOfReadStream = (_ReadStreamIsOurs)
                        ? SharedUtilities.GetFileLength(_name)
                        : -1L;
                }
                return _lengthOfReadStream;
            }
        }
        #endregion





        #region Add
    
        public event EventHandler<AddProgressEventArgs> AddProgress;

        private void OnAddStarted()
        {
            EventHandler<AddProgressEventArgs> ap = AddProgress;
            if (ap != null)
            {
                var e = AddProgressEventArgs.Started(ArchiveNameForEvent);
                ap(this, e);
                if (e.Cancel) // workitem 13371
                    _addOperationCanceled = true;
            }
        }

        private void OnAddCompleted()
        {
            EventHandler<AddProgressEventArgs> ap = AddProgress;
            if (ap != null)
            {
                var e = AddProgressEventArgs.Completed(ArchiveNameForEvent);
                ap(this, e);
            }
        }

        internal void AfterAddEntry(ZipEntry entry)
        {
            EventHandler<AddProgressEventArgs> ap = AddProgress;
            if (ap != null)
            {
                var e = AddProgressEventArgs.AfterEntry(ArchiveNameForEvent, entry, _entries.Count);
                ap(this, e);
                if (e.Cancel) // workitem 13371
                    _addOperationCanceled = true;
            }
        }

        #endregion



        #region Error
      
        public event EventHandler<ZipErrorEventArgs> ZipError;

        internal bool OnZipErrorSaving(ZipEntry entry, Exception exc)
        {
            if (ZipError != null)
            {
                lock (LOCK)
                {
                    var e = ZipErrorEventArgs.Saving(this.Name, entry, exc);
                    ZipError(this, e);
                    if (e.Cancel)
                        _saveOperationCanceled = true;
                }
            }
            return _saveOperationCanceled;
        }
        #endregion

    }
}
