using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace SetBackground
{
    internal static class Wallpaper
    {
        private const int SPI_GETDESKWALLPAPER = 0x0073;
        private const int SPI_SETDESKWALLPAPER = 0x0014;
        private const int SPIF_UPDATEINIFILE = 0x01;
        private const int SPIF_SENDWININICHANGE = 0x02;

        [DllImport("user32.dll", EntryPoint = "SystemParametersInfo")]
        static extern int SystemParametersInfo(int uAction, int uParam, StringBuilder lpvParam, int fuWinIni);

        public enum Style
        {
            Tile,
            Center,
            Stretch,
            Fill,
            Fit,
            Span
        }

        public static void Set(string filePath, Style style)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                filePath = GetCurrentBackground();
            }

            StringBuilder s = new StringBuilder(filePath);
            RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true);

            if (key != null)
            {
                switch (style)
                {
                    case Style.Tile:
                        key.SetValue(@"WallpaperStyle", "0");
                        key.SetValue(@"TileWallpaper", "1");

                        break;
                    case Style.Center:
                        key.SetValue(@"WallpaperStyle", "0");
                        key.SetValue(@"TileWallpaper", "0");

                        break;
                    case Style.Stretch:
                        key.SetValue(@"WallpaperStyle", "2");
                        key.SetValue(@"TileWallpaper", "0");

                        break;
                    case Style.Fill:
                        key.SetValue(@"WallpaperStyle", "10");
                        key.SetValue(@"TileWallpaper", "0");

                        break;
                    case Style.Fit:
                        key.SetValue(@"WallpaperStyle", "6");
                        key.SetValue(@"TileWallpaper", "0");

                        break;
                    case Style.Span:
                        key.SetValue(@"WallpaperStyle", "22");
                        key.SetValue(@"TileWallpaper", "0");

                        break;
                    default:
                        break;
                }

                key.Close();
            }

            SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, s, SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);
        }


        private static string GetCurrentBackground()
        {
            StringBuilder s = new StringBuilder(300);
            SystemParametersInfo(SPI_GETDESKWALLPAPER, 300, s, 0);
            return s.ToString();
        }
    }
}
