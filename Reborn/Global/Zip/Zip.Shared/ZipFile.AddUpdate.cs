
using System;
using System.IO;
using System.Collections.Generic;

namespace Ionic.Zip
{
    public partial class ZipFile
    {
        
        public ZipEntry AddFile(string fileName, String directoryPathInArchive)
        {
            string nameInArchive = ZipEntry.NameInArchive(fileName, directoryPathInArchive);
            ZipEntry ze = ZipEntry.CreateFromFile(fileName, nameInArchive);
            if (Verbose) StatusMessageTextWriter.WriteLine("adding {0}...", fileName);
            return _InternalAddEntry(ze);
        }

        public ZipEntry UpdateFile(string fileName, String directoryPathInArchive)
        {
            // ideally this would all be transactional!
            var key = ZipEntry.NameInArchive(fileName, directoryPathInArchive);
            if (this[key] != null)
                this.RemoveEntry(key);
            return this.AddFile(fileName, directoryPathInArchive);
        }
   
        public ZipEntry AddEntry(string entryName, Stream stream)
        {
            ZipEntry ze = ZipEntry.CreateForStream(entryName, stream);
            ze.SetEntryTimes(DateTime.Now,DateTime.Now,DateTime.Now);
            if (Verbose) StatusMessageTextWriter.WriteLine("adding {0}...", entryName);
            return _InternalAddEntry(ze);
        }
        private ZipEntry _InternalAddEntry(ZipEntry ze)
        {
            // stamp all the props onto the entry
            ze._container = new ZipContainer(this);
            ze.CompressionMethod = this.CompressionMethod;
            ze.CompressionLevel = this.CompressionLevel;
            ze.ZipErrorAction = this.ZipErrorAction;
            ze.SetCompression = this.SetCompression;
            ze.AlternateEncoding = this.AlternateEncoding;
            ze.AlternateEncodingUsage = this.AlternateEncodingUsage;
            ze.Password = this._Password;
            ze.Encryption = this.Encryption;
            ze.EmitTimesInWindowsFormatWhenSaving = this._emitNtfsTimes;
            ze.EmitTimesInUnixFormatWhenSaving = this._emitUnixTimes;
            //string key = DictionaryKeyForEntry(ze);
            InternalAddEntry(ze.FileName,ze);
            AfterAddEntry(ze);
            return ze;
        }

        public ZipEntry AddDirectory(string directoryName)
        {
            return AddDirectory(directoryName, null);
        }


        public ZipEntry AddDirectory(string directoryName, string directoryPathInArchive)
        {
            return AddOrUpdateDirectoryImpl(directoryName, directoryPathInArchive, AddOrUpdateAction.AddOnly);
        }

        private ZipEntry AddOrUpdateDirectoryImpl(string directoryName,
                                                  string rootDirectoryPathInArchive,
                                                  AddOrUpdateAction action)
        {
            if (rootDirectoryPathInArchive == null)
            {
                rootDirectoryPathInArchive = "";
            }

            return AddOrUpdateDirectoryImpl(directoryName, rootDirectoryPathInArchive, action, true, 0);
        }


        internal void InternalAddEntry(String name, ZipEntry entry)
        {
            _entries.Add(name, entry);
            if (!_entriesInsensitive.ContainsKey(name))
                _entriesInsensitive.Add(name, entry);
            _contentsChanged = true;
        }
        private ZipEntry AddOrUpdateDirectoryImpl(string directoryName,
                                                  string rootDirectoryPathInArchive,
                                                  AddOrUpdateAction action,
                                                  bool recurse,
                                                  int level)
        {
            if (Verbose)
                StatusMessageTextWriter.WriteLine("{0} {1}...",
                                                  (action == AddOrUpdateAction.AddOnly) ? "adding" : "Adding or updating",
                                                  directoryName);

            if (level == 0)
            {
                _addOperationCanceled = false;
                OnAddStarted();
            }

            // workitem 13371
            if (_addOperationCanceled)
                return null;

            string dirForEntries = rootDirectoryPathInArchive;
            ZipEntry baseDir = null;

            if (level > 0)
            {
                int f = directoryName.Length;
                for (int i = level; i > 0; i--)
                    f = directoryName.LastIndexOfAny("/\\".ToCharArray(), f - 1, f - 1);

                dirForEntries = directoryName.Substring(f + 1);
                dirForEntries = Path.Combine(rootDirectoryPathInArchive, dirForEntries);
            }

            // if not top level, or if the root is non-empty, then explicitly add the directory
            if (level > 0 || rootDirectoryPathInArchive != "")
            {
                baseDir = ZipEntry.CreateFromFile(directoryName, dirForEntries);
                baseDir._container = new ZipContainer(this);
                baseDir.AlternateEncoding = this.AlternateEncoding;  // workitem 6410
                baseDir.AlternateEncodingUsage = this.AlternateEncodingUsage;
                baseDir.MarkAsDirectory();
                baseDir.EmitTimesInWindowsFormatWhenSaving = _emitNtfsTimes;
                baseDir.EmitTimesInUnixFormatWhenSaving = _emitUnixTimes;

                // add the directory only if it does not exist.
                // It's not an error if it already exists.
                if (!_entries.ContainsKey(baseDir.FileName))
                {
                    InternalAddEntry(baseDir.FileName,baseDir);
                    AfterAddEntry(baseDir);
                }
                dirForEntries = baseDir.FileName;
            }

            if (!_addOperationCanceled)
            {

                String[] filenames = Directory.GetFiles(directoryName);

                if (recurse)
                {
                    // add the files:
                    foreach (String filename in filenames)
                    {
                        if (_addOperationCanceled) break;
                        if (action == AddOrUpdateAction.AddOnly)
                            AddFile(filename, dirForEntries);
                        else
                            UpdateFile(filename, dirForEntries);
                    }

                    if (!_addOperationCanceled)
                    {
                        // add the subdirectories:
                        String[] dirnames = Directory.GetDirectories(directoryName);
                        foreach (String dir in dirnames)
                        {
                            // workitem 8617: Optionally traverse reparse points
                            FileAttributes fileAttrs = System.IO.File.GetAttributes(dir);
                            if (this.AddDirectoryWillTraverseReparsePoints
                                || ((fileAttrs & FileAttributes.ReparsePoint) == 0)
                                )
                                AddOrUpdateDirectoryImpl(dir, rootDirectoryPathInArchive, action, recurse, level + 1);

                        }

                    }
                }
            }

            if (level == 0)
                OnAddCompleted();

            return baseDir;
        }

    }

}