
using System;
using System.IO;
using System.Collections.Generic;

namespace Ionic.Zip
{

    partial class ZipFile
    {
    

        public void AddSelectedFiles(String selectionCriteria,
                                     String directoryOnDisk,
                                     String directoryPathInArchive,
                                     bool recurseDirectories)
        {
            _AddOrUpdateSelectedFiles(selectionCriteria,
                                      directoryOnDisk,
                                      directoryPathInArchive,
                                      recurseDirectories,
                                      false);
        }



        private void _AddOrUpdateSelectedFiles(String selectionCriteria,
                                               String directoryOnDisk,
                                               String directoryPathInArchive,
                                               bool recurseDirectories,
                                               bool wantUpdate)
        {
            if (directoryOnDisk == null && (Directory.Exists(selectionCriteria)))
            {
                directoryOnDisk = selectionCriteria;
                selectionCriteria = "*.*";
            }
            else if (String.IsNullOrEmpty(directoryOnDisk))
            {
                directoryOnDisk = ".";
            }

            // workitem 9176
            while (directoryOnDisk.EndsWith("\\")) directoryOnDisk = directoryOnDisk.Substring(0, directoryOnDisk.Length - 1);
            if (Verbose) StatusMessageTextWriter.WriteLine("adding selection '{0}' from dir '{1}'...",
                                                               selectionCriteria, directoryOnDisk);
            Ionic.FileSelector ff = new Ionic.FileSelector(selectionCriteria,
                                                           AddDirectoryWillTraverseReparsePoints);
            var itemsToAdd = ff.SelectFiles(directoryOnDisk, recurseDirectories);

            if (Verbose) StatusMessageTextWriter.WriteLine("found {0} files...", itemsToAdd.Count);

            OnAddStarted();

            AddOrUpdateAction action = (wantUpdate) ? AddOrUpdateAction.AddOrUpdate : AddOrUpdateAction.AddOnly;
            foreach (var item in itemsToAdd)
            {
                // workitem 10153
                string dirInArchive = (directoryPathInArchive == null)
                    ? null
                    // workitem 12260
                    : ReplaceLeadingDirectory(Path.GetDirectoryName(item),
                                              directoryOnDisk,
                                              directoryPathInArchive);

                if (File.Exists(item))
                {
                    if (wantUpdate)
                        this.UpdateFile(item, dirInArchive);
                    else
                        this.AddFile(item, dirInArchive);
                }
                else
                {
                    // this adds "just" the directory, without recursing to the contained files
                    AddOrUpdateDirectoryImpl(item, dirInArchive, action, false, 0);
                }
            }

            OnAddCompleted();
        }


        // workitem 12260
        private static string ReplaceLeadingDirectory(string original,
                                                      string pattern,
                                                      string replacement)
        {
            string upperString = original.ToUpper();
            string upperPattern = pattern.ToUpper();
            int p1 = upperString.IndexOf(upperPattern);
            if (p1 != 0) return original;
            return replacement + original.Substring(upperPattern.Length);
        }


        public ICollection<ZipEntry> SelectEntries(String selectionCriteria)
        {
            Ionic.FileSelector ff = new Ionic.FileSelector(selectionCriteria,
                                                           AddDirectoryWillTraverseReparsePoints);
            return ff.SelectEntries(this);
        }




    }
}



namespace Ionic
{
    internal abstract partial class SelectionCriterion
    {
        internal abstract bool Evaluate(Ionic.Zip.ZipEntry entry);
    }


    internal partial class NameCriterion : SelectionCriterion
    {
        internal override bool Evaluate(Ionic.Zip.ZipEntry entry)
        {
            // swap slashes in reference to local configuration
            string transformedFileName = entry.FileName.Replace(Path.DirectorySeparatorChar == '/' ? '\\' : '/', Path.DirectorySeparatorChar);

            return _Evaluate(transformedFileName);
        }
    }


    internal partial class SizeCriterion : SelectionCriterion
    {
        internal override bool Evaluate(Ionic.Zip.ZipEntry entry)
        {
            return _Evaluate(entry.UncompressedSize);
        }
    }

    internal partial class TimeCriterion : SelectionCriterion
    {
        internal override bool Evaluate(Ionic.Zip.ZipEntry entry)
        {
            DateTime x;
            switch (Which)
            {
                case WhichTime.atime:
                    x = entry.AccessedTime;
                    break;
                case WhichTime.mtime:
                    x = entry.ModifiedTime;
                    break;
                case WhichTime.ctime:
                    x = entry.CreationTime;
                    break;
                default: throw new ArgumentException("??time");
            }
            return _Evaluate(x);
        }
    }


    internal partial class TypeCriterion : SelectionCriterion
    {
        internal override bool Evaluate(Ionic.Zip.ZipEntry entry)
        {
            bool result = (ObjectType == 'D')
                ? entry.IsDirectory
                : !entry.IsDirectory;

            if (Operator != ComparisonOperator.EqualTo)
                result = !result;
            return result;
        }
    }

    internal partial class AttributesCriterion : SelectionCriterion
    {
        internal override bool Evaluate(Ionic.Zip.ZipEntry entry)
        {
            FileAttributes fileAttrs = entry.Attributes;
            return _Evaluate(fileAttrs);
        }
    }

    internal partial class CompoundCriterion : SelectionCriterion
    {
        internal override bool Evaluate(Ionic.Zip.ZipEntry entry)
        {
            bool result = Left.Evaluate(entry);
            switch (Conjunction)
            {
                case LogicalConjunction.AND:
                    if (result)
                        result = Right.Evaluate(entry);
                    break;
                case LogicalConjunction.OR:
                    if (!result)
                        result = Right.Evaluate(entry);
                    break;
                case LogicalConjunction.XOR:
                    result ^= Right.Evaluate(entry);
                    break;
            }
            return result;
        }
    }



    public partial class FileSelector
    {
        private bool Evaluate(Ionic.Zip.ZipEntry entry)
        {
            bool result = _Criterion.Evaluate(entry);
            return result;
        }

        public ICollection<Ionic.Zip.ZipEntry> SelectEntries(Ionic.Zip.ZipFile zip)
        {
            if (zip == null)
                throw new ArgumentNullException("zip");

            var list = new List<Ionic.Zip.ZipEntry>();

            foreach (Ionic.Zip.ZipEntry e in zip)
            {
                if (this.Evaluate(e))
                    list.Add(e);
            }

            return list;
        }


    

    }
}
