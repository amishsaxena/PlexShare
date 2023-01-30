﻿///<author>Amish Ashish Saxena</author>
///<summary> 
///This file contains the ScreenCapturer Class that implements the
///screen capturing functionality. It is used by ScreenshareClient. 
///</summary>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;

namespace PlexShareScreenshare.Client
{
    /// <summary>
    /// Class contains implementation of the screen capturing using threads (tasks)
    /// </summary>
    public class ScreenCapturer
    {
        readonly Queue<Bitmap> _capturedFrame;

        // Limits the number of frames in the queue
        public const short MaxQueueLength = 20;

        // Token and its source for killing the task
        private bool _cancellationToken;

        private Task? _captureTask;

        private readonly Screenshot _screenshot;

        public ScreenCapturer()
        {
            _capturedFrame = new Queue<Bitmap>();
            _screenshot = Screenshot.Instance();
            Trace.WriteLine(Utils.GetDebugMessage("[Screenshare] Successfully created an instance of ScreenCapturer.", withTimeStamp: true));
        }

        /// <summary>
        /// Returns the bitmap image at the front of _capturedFrame queue. 
        /// </summary>
        /// <returns>Bitmap image of 720p dimension</returns>
        public Bitmap? GetImage(ref bool cancellationToken)
        {
            while (true)
            {
                lock (_capturedFrame)
                {
                    if (_capturedFrame.Count != 0)
                    {
                        break;
                    }
                }

                if (cancellationToken)
                    return null;
                Thread.Sleep(100);
            }

            lock (_capturedFrame)
            {
                return _capturedFrame.Dequeue();
            }
        }

        /// <summary>
        /// Returns the length of the _capturedFrame queue
        /// </summary>
        /// <returns>Integer value containing the length of queue.</returns>
        public int GetCapturedFrameLength()
        {
            lock (_capturedFrame)
            {
                Trace.WriteLine(Utils.GetDebugMessage($"[Screenshare] capturedFrame Queue size : {_capturedFrame.Count}.", withTimeStamp: true));
                return _capturedFrame.Count;
            }
        }

        /// <summary>
        /// Creates the task for capturing screenshots and starts capturing
        /// </summary>
        public void StartCapture()
        {
            Trace.WriteLine(Utils.GetDebugMessage($"[Screenshare] Starting Screen Capture.", withTimeStamp: true));
            _cancellationToken = false;
            _captureTask = new Task(() =>
            {
                while (!_cancellationToken)
                {
                    lock (_capturedFrame)
                    {
                        if (_capturedFrame.Count < MaxQueueLength)
                        {
                            try
                            {
                                Bitmap img = _screenshot.MakeScreenshot();
                                if (img != null)
                                {
                                    Thread.Sleep(150);
                                    _capturedFrame.Enqueue(img);
                                }
                            }
                            catch (Exception e)
                            {
                                Trace.WriteLine($"[Screenshare] Could not capture screenshot: {e.Message}");
                            }
                        }
                        else
                        {
                            // Sleep for some time, if queue is filled 
                            while (_capturedFrame.Count > MaxQueueLength / 2)
                                _capturedFrame.Dequeue();

                        }
                    }
                }
            });

            _captureTask.Start();
            Trace.WriteLine(Utils.GetDebugMessage($"[Screenshare] Screen Capture started successfully.", withTimeStamp: true));
        }

        /// <summary>
        /// Stops the capturing by Cancelling the task and clears the _capturedFrame queue.
        /// </summary>
        public void StopCapture()
        {
            Trace.WriteLine(Utils.GetDebugMessage($"[Screenshare] Stopping Screen Capture.", withTimeStamp: true));

            Debug.Assert(_captureTask != null,
                Utils.GetDebugMessage("_cancellationTask is null, cannot stop image capture"));
            try
            {
                _cancellationToken = true;
                _captureTask.Wait();
            }
            catch (Exception e)
            {
                Trace.WriteLine(Utils.GetDebugMessage($"Unable to stop capture: {e.Message}", withTimeStamp: true));
            }

            _capturedFrame.Clear();
            Trace.WriteLine(Utils.GetDebugMessage($"[Screenshare] __capturedFrame Queue has been emptied.", withTimeStamp: true));
            Trace.WriteLine(Utils.GetDebugMessage($"[Screenshare] Screen Capture stopped successfully.", withTimeStamp: true));
        }
    }
}
