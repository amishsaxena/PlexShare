///<author>Satyam Mishra</author>
///<summary>
/// This file has ScreenProcessor class. It is responsible for 
/// processing the image from ScreenCapturer class and calculating
/// the image bits that are different from the previous image
///</summary>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace PlexShareScreenshare.Client
{
    /// <summary>
    /// Class contains implementation of the screen processing using threads (tasks)
    /// </summary>
    public class ScreenProcessor
    {
        // The queue in which the image will be enqueued after
        // processing it
        private readonly Queue<string> _processedFrame;

        // Processing task
        private Task? _processorTask;

        // Limits the number of frames in the queue
        public const short MaxQueueLength = 20;

        // The screen capturer object
        private readonly ScreenCapturer _capturer;

        // Current and the new resolutions 
        private Resolution _currentRes;
        private Resolution _newRes;
        public readonly object ResolutionLock;

        // Height and Width of the images captured by the capturer
        private int _capturedImageHeight;
        private int _capturedImageWidth;

        // Tokens added to be able to stop the thread execution
        private bool _cancellationToken;

        // Storing the previous frame
        Bitmap? prevImage;

        // Stores whether diff image is being sent for the first time or not
        private int _first_xor = 0;

        /// <summary>
        /// Called by ScreenshareClient.
        /// Initializes processed frame queue.
        /// </summary>
        public ScreenProcessor(ScreenCapturer Capturer)
        {
            _capturer = Capturer;
            _processedFrame = new Queue<string>();
            ResolutionLock = new();

            Trace.WriteLine(Utils.GetDebugMessage("Successfully created an instance of ScreenProcessor", withTimeStamp: true));
        }

        /// <summary>
        /// Pops and return the image from the queue. If there is no image in the queue then it waits for 
        /// the queue to become not empty
        /// </summary>
        public string GetFrame(ref bool cancellationToken)
        {
            while (true)
            {
                lock (_processedFrame)
                {
                    if (_processedFrame.Count != 0)
                    {
                        break;
                    }
                }

                if (cancellationToken)
                    return "";
                Thread.Sleep(100);
            }
            lock (_processedFrame)
            {
                Trace.WriteLine(Utils.GetDebugMessage("Successfully sent frame", withTimeStamp: true));
                return _processedFrame.Dequeue();
            }
        }

        /// <summary>
        /// Returns the length of the processed image queue 
        /// </summary>
        public int GetProcessedFrameLength()
        {
            lock (_processedFrame)
            {
                Trace.WriteLine(Utils.GetDebugMessage("Successfully sent frame length", withTimeStamp: true));
                return _processedFrame.Count;
            }
        }

        /// <summary>
        /// In this function we go through every pixel of both the images and
        /// returns a bitmap image which has xor of all the coorosponding pixels
        /// </summary>
        public static unsafe Bitmap? Process(Bitmap curr, Bitmap prev)
        {
            // taking lock on the images and extracting bitmap data
            BitmapData currData = curr.LockBits(new Rectangle(0, 0, curr.Width, curr.Height), ImageLockMode.ReadWrite, curr.PixelFormat);
            BitmapData prevData = prev.LockBits(new Rectangle(0, 0, prev.Width, prev.Height), ImageLockMode.ReadWrite, prev.PixelFormat);

            int bytesPerPixel = Bitmap.GetPixelFormatSize(curr.PixelFormat) / 8;
            int heightInPixels = currData.Height;
            int widthInBytes = currData.Width * bytesPerPixel;

            // taking pointer to both the image bytes
            byte* currptr = (byte*)currData.Scan0;
            byte* prevptr = (byte*)prevData.Scan0;

            // initializing the resultant bitmap image
            Bitmap newb = new Bitmap(curr.Width, curr.Height);
            BitmapData bmd = newb.LockBits(new Rectangle(0, 0, 10, 10), System.Drawing.Imaging.ImageLockMode.ReadOnly, newb.PixelFormat);
            byte* ptr = (byte*)bmd.Scan0;

            int diff = 0;

            // iterating over both the images
            for (int y = 0; y < heightInPixels; y++)
            {
                int currentLine = y * currData.Stride;

                for (int x = 0; x < widthInBytes; x += bytesPerPixel)
                {
                    int oldBlue = currptr[currentLine + x];
                    int oldGreen = currptr[currentLine + x + 1];
                    int oldRed = currptr[currentLine + x + 2];
                    int oldAlpha = currptr[currentLine + x + 3];

                    int newBlue = prevptr[currentLine + x];
                    int newGreen = prevptr[currentLine + x + 1];
                    int newRed = prevptr[currentLine + x + 2];
                    int newAlpha = prevptr[currentLine + x + 3];

                    // setting xor of coorosponding pixels in the resultant image
                    ptr[currentLine + x] = (byte)(oldBlue ^ newBlue);
                    ptr[currentLine + x + 1] = (byte)(oldGreen ^ newGreen);
                    ptr[currentLine + x + 2] = (byte)(oldRed ^ newRed);
                    ptr[currentLine + x + 3] = (byte)(oldAlpha ^ newAlpha);

                    // if the pixels diff count is more than a certain value then return as this is not the 
                    // optimized way of sending image
                    if ((oldBlue != newBlue) || (oldGreen != newGreen) || (oldRed != newRed) || (oldAlpha != newAlpha))
                    {
                        diff++;
                        if (diff > 500)
                        {
                            curr.UnlockBits(currData);
                            prev.UnlockBits(prevData);
                            newb.UnlockBits(bmd);
                            return null;
                        }
                    }
                }
            }

            // unlocking the images
            curr.UnlockBits(currData);
            prev.UnlockBits(prevData);
            newb.UnlockBits(bmd);

            return newb;
        }

        /// <summary>
        /// Main function which will run in loop and capture the image
        /// calculate the image bits differences and append it in the array
        /// </summary>
        private void Processing()
        {
            while (!_cancellationToken)
            {
                Bitmap? img = _capturer.GetImage(ref _cancellationToken);
                if (_cancellationToken)
                    break;

                Debug.Assert(img != null, Utils.GetDebugMessage("img is null"));
                string serialized_buffer = Compress(img);

                lock (_processedFrame)
                {
                    if (_processedFrame.Count < MaxQueueLength)
                    {
                        _processedFrame.Enqueue(serialized_buffer);
                    }
                    else
                    {
                        // Sleep for some time, if queue is filled 
                        while (_processedFrame.Count > MaxQueueLength / 2)
                            _processedFrame.Dequeue();
                    }
                }
                prevImage = img;
            }
        }

        /// <summary>
        /// Called by ScreenshareClient when the client starts screen sharing.
        /// Creates a task for the Processing function.
        /// </summary>
        public void StartProcessing()
        {
            // dropping one frame to set the previous image value
            _cancellationToken = false;
            _first_xor = 0;
            Bitmap? img = null;
            try
            {
                img = _capturer.GetImage(ref _cancellationToken);
                Debug.Assert(!_cancellationToken);
            }
            catch (Exception e)
            {
                Trace.WriteLine(Utils.GetDebugMessage($"Failed to cancel processor task: {e.Message}", withTimeStamp: true));
            }

            Debug.Assert(img != null, Utils.GetDebugMessage("img is null"));
            _capturedImageHeight = img.Height;
            _capturedImageWidth = img.Width;

            // also set the current and new resolution
            _newRes = new() { Height = _capturedImageHeight, Width = _capturedImageWidth };
            _currentRes = _newRes;
            prevImage = new Bitmap(_newRes.Width, _newRes.Height);

            Trace.WriteLine(Utils.GetDebugMessage("Previous image set and" +
                "going to start image processing", withTimeStamp: true));

            // start the processing thread
            try
            {
                _processorTask = new Task(Processing);
                _processorTask.Start();
            }
            catch (OperationCanceledException e)
            {
                Trace.WriteLine(Utils.GetDebugMessage($"Processor task cancelled: {e.Message}", withTimeStamp: true));
            }
            catch (Exception e)
            {
                Trace.WriteLine(Utils.GetDebugMessage($"Failed to cancel processor task: {e.Message}", withTimeStamp: true));
            }
        }

        /// <summary>
        /// Called by ScreenshareClient when the client stops screen sharing
        /// kill the processor task and make the processor task variable null
        /// Then empty the Queue.
        /// </summary>
        public void StopProcessing()
        {
            Debug.Assert(_processorTask != null, Utils.GetDebugMessage("_processorTask was null, cannot call cancel."));

            try
            {
                _cancellationToken = true;
                _processorTask.Wait();
            }
            catch (Exception e)
            {
                Trace.WriteLine(Utils.GetDebugMessage($"Failed to cancel processor task: {e.Message}", withTimeStamp: true));
            }

            Debug.Assert(_processedFrame != null, Utils.GetDebugMessage("_processedTask is found null"));
            _processedFrame.Clear();

            Trace.WriteLine(Utils.GetDebugMessage("Successfully stopped image processing", withTimeStamp: true));
        }

        /// <summary>
        /// Setting new resolution for sending the image. 
        /// </summary>
        /// <param name="res"> New resolution values </param>
        public void SetNewResolution(int windowCount)
        {
            Debug.Assert(windowCount != 0, Utils.GetDebugMessage("windowCount is found 0"));
            Resolution res = new()
            {
                Height = _capturedImageHeight / windowCount,
                Width = _capturedImageWidth / windowCount
            };
            // taking lock since newres is shared variable as it is
            // used even in Compress function
            lock (ResolutionLock)
            {
                _newRes = res;
            }
            Trace.WriteLine(Utils.GetDebugMessage("Successfully changed the rew resolution" +
                " variable", withTimeStamp: true));
        }

        /// <summary>
        /// Compressing the image byte array data using Deflated stream. It provides
        /// a lossless compression.
        /// </summary>
        /// <param name="data">Image data to be compressed</param>
        /// <returns>Compressed data</returns>
        public static byte[] CompressByteArray(byte[] data)
        {
            MemoryStream output = new();
            using (DeflateStream dstream = new(output, CompressionLevel.Fastest))
            {
                dstream.Write(data, 0, data.Length);
            }
            return output.ToArray();
        }

        /// <summary>
        /// Called by StartProcessing, if the image resolution has changed then set
        /// the new image resolution
        /// </summary>
        public string Compress(Bitmap img)
        {
            Bitmap? new_img = null;

            lock (ResolutionLock)
            {
                // if the new resolution and the current resolution are the same and the previous image is not
                // not null then process the image using the previous image
                if (prevImage != null && _newRes == _currentRes)
                {
                    new_img = Process(img, prevImage);
                }
                // else we need to update the current res with the new res and change the resolution
                // of captured image to the new resolution
                else if (_newRes != _currentRes)
                {
                    _currentRes = _newRes;
                }
            }
            // compressing image to the current  resolution values
            img = new Bitmap(img, _currentRes.Width, _currentRes.Height);
            new_img = null;

            // if no processing happened then send the whole image
            if (new_img == null)
            {
                MemoryStream ms = new();
                img.Save(ms, ImageFormat.Jpeg);
                var data = CompressByteArray(ms.ToArray());
                _first_xor = 0;
                return Convert.ToBase64String(data) + "1";
            }
            // else if processing was done then compress the processed image
            else
            {
                if (_first_xor == 0)
                {
                    MemoryStream ms = new();
                    img.Save(ms, ImageFormat.Bmp);
                    var data = CompressByteArray(ms.ToArray());
                    _first_xor = 1;
                    return Convert.ToBase64String(data) + "1";
                }

                else
                {
                    MemoryStream ms = new();
                    new_img.Save(ms, ImageFormat.Bmp);
                    var data = CompressByteArray(ms.ToArray());
                    return Convert.ToBase64String(data) + "0";
                }
            }
        }
    }
}
