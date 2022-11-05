using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using PlexShareScreenshare.Client;
using System.IO;
using System.Runtime.InteropServices;


namespace PlexShareTests.ScreenshareTests
{
    internal class DisplayTools
    {
        [DllImport("gdi32.dll")]
        static extern int GetDeviceCaps(IntPtr hdc, int nIndex);
        private enum DeviceCap
        {
            Desktopvertres = 117,
            Desktophorzres = 118
        }
        public static (int, int) GetPhysicalDisplaySize()
        {
            Graphics g = Graphics.FromHwnd(IntPtr.Zero);
            IntPtr desktop = g.GetHdc();

            int physicalScreenHeight = GetDeviceCaps(desktop, (int)DeviceCap.Desktopvertres);
            int physicalScreenWidth = GetDeviceCaps(desktop, (int)DeviceCap.Desktophorzres);

            return (physicalScreenWidth, physicalScreenHeight);
        }
    }

    public class ScreenshotTests
    {
        /// <summary>
        /// Checks if the singleton instance of Screenshot works properly
        /// </summary>
        [Fact]
        public void Test1()
        {
            Screenshot screenshot1 = Screenshot.Instance();
            Screenshot screenshot2 = Screenshot.Instance();
            Assert.Equal(screenshot1, screenshot2);
        }

        /// <summary>
        /// Check if the captured screenshot is of the expected resolution
        /// </summary>
        [Fact]
        public void Test2()
        {
            Screenshot screenshot = Screenshot.Instance();
            Bitmap image = screenshot.MakeScreenshot();
            Thread.Sleep(1000);
            var screenSize = DisplayTools.GetPhysicalDisplaySize();
            var screenWidth = screenSize.Item1;
            var screenHeight = screenSize.Item2;
            Assert.Equal(1080, image.Height);
            Assert.Equal(1920, image.Width);
        }
    }
}
