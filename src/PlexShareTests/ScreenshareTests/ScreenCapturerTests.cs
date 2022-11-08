using PlexShareScreenshare.Client;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;

namespace PlexShareTests.ScreenshareTests
{
    [Collection("Sequential")]
    public class ScreenCapturerTests
    {
        /// <summary>
        /// Capture for some time and see if the elements of queues are not null
        /// </summary>
        [Fact]
        public void Test1()
        {
            ScreenCapturer screenCapturer = new ScreenCapturer();
            screenCapturer.StartCapture();
            Thread.Sleep(1000);
            int count = 0;
            for (int i = 0; i < 50; i++)
            {
                Bitmap frame = screenCapturer.GetImage();
                if (frame != null)
                    count++;
            }

            screenCapturer.StopCapture();
            Assert.Equal(50, count);
        }

        /// <summary>
        /// Queue length should always be between 0 and MaxQueueLength
        /// </summary>
        [Fact]
        public void Test2()
        {
            ScreenCapturer screenCapturer = new ScreenCapturer();
            screenCapturer.StartCapture();
            Thread.Sleep(1000);
            int framesCaptured = screenCapturer.GetCapturedFrameLength();

            screenCapturer.StopCapture();
            Thread.Sleep(1);
            Assert.True(framesCaptured is > 0 and <= ScreenCapturer.MaxQueueLength);
        }

        /// <summary>
        /// Runs only capturing for a longer time and see if queue reaches its limit
        /// </summary>
        [Fact]
        public void Test3()
        {
            ScreenCapturer screenCapturer = new ScreenCapturer();
            screenCapturer.StartCapture();
            Thread.Sleep(10000);
            int framesCaptured = screenCapturer.GetCapturedFrameLength();
            screenCapturer.StopCapture();
            Assert.True(condition: framesCaptured == ScreenCapturer.MaxQueueLength);
        }
    }
}
