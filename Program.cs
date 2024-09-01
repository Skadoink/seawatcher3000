using Nikon;
using System.Diagnostics;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Compunet.YoloV8;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Compunet.YoloV8.Plotting;
using System.Windows.Data;
using System.ComponentModel;
using System.Collections.ObjectModel;

namespace seawatcher3000
{
    class Seawatcher : ViewModelBase
    {
        static NikonManager _manager = new("Type0020.md3");
        NikonDevice? _device;
        static DispatcherTimer _timer = new();
        YoloV8Predictor _predictor;
        BitmapSource _liveViewImage;
        static System.Timers.Timer focusTimer; 

        public Seawatcher()
        {
            _timer.Interval = TimeSpan.FromMilliseconds(100);
            _manager.DeviceAdded += new DeviceAddedDelegate(manager_DeviceAdded);
            _timer.Tick += new EventHandler(_timer_Tick);
            _predictor = YoloV8Predictor.Create("seaeyes_model_1.onnx"); // Load bird detection model 
            _liveViewImage = BitmapFrame.Create(new MemoryStream(File.ReadAllBytes("liveview.jpg"))); // Load the latest image from the Pictures folder
            setFocusTimer();
        }

        /// <summary>
        /// What to do when a device is added
        /// </summary>
        public void manager_DeviceAdded(NikonManager sender, NikonDevice device)
        {
            _device = device;
            Trace.WriteLine("Device added: " + device.Name);
            NkMAIDCapInfo[] supportedCaps = device.GetCapabilityInfo();
            // Write list of supported capabilities to the console
            foreach (NkMAIDCapInfo supportedCap in supportedCaps)
            {
                Trace.WriteLine(supportedCap.GetDescription());
            }

            // Write battery level to the console
            try
            {
                int batteryLevel = device.GetInteger(eNkMAIDCapability.kNkMAIDCapability_BatteryLevel);
                Trace.WriteLine("Battery level: " + batteryLevel);
            }
            catch (NikonException ex)
            {
                Trace.WriteLine("Error getting battery level: " + ex.Message);
            }

            // Change live view size to XGA (1024x768 pixels, 4:3 aspect ratio)
            try
            {
                NikonEnum liveViewSize = device.GetEnum(eNkMAIDCapability.kNkMAIDCapability_LiveViewImageSize); 
                for (int i = 0; i < liveViewSize.Length; i++)
                {
                    if ((uint)liveViewSize[i] == (uint)eNkMAIDLiveViewImageSize.kNkMAIDLiveViewImageSize_XGA)
                    {
                        liveViewSize.Index = i;
                        device.SetEnum(eNkMAIDCapability.kNkMAIDCapability_LiveViewImageSize, liveViewSize);
                        break;
                    }
                }
                Trace.WriteLine("Live view size: " + liveViewSize);
            }
            catch (NikonException ex)
            {
                Trace.WriteLine("Error setting live view image size: " + ex.Message);
            }
        }

        private void setFocusTimer()
        {
            focusTimer = new System.Timers.Timer(3000); // 300000ms = 5 minutes
            focusTimer.Elapsed += (sender, e) => LiveViewFocus();
            focusTimer.AutoReset = true;
            focusTimer.Enabled = true;
        }

        public void StartLiveView()
        {
            try
            {
                if (_device is NikonDevice device)
                {
                    // Start live view
                    device.LiveViewEnabled = true;
                    LiveViewFocus();
                    _timer.Start();
                }
                else { throw new Exception(); }
            }
            catch (NikonException ex)
            {
                Trace.WriteLine("Failed to start live view: " + ex.ToString());
            }
        }

        public void StopLiveView()
        {
            try
            {
                if (_device is NikonDevice device)
                {
                    _timer.Stop();
                    device.LiveViewEnabled = false;
                }
                else { throw new Exception(); }
            }
            catch (NikonException ex)
            {
                Trace.WriteLine("Failed to stop live view: " + ex.ToString());
            }
        }

        /*
         * Focus the camera using the live view AF. 
         */
        public void LiveViewFocus()
        {
            try
            {
                if (_device is NikonDevice device && _device.LiveViewEnabled)
                {
                    NikonEnum contrastAFActions = device.GetEnum(eNkMAIDCapability.kNkMAIDCapability_ContrastAF);
                    for (int i = 0; i < contrastAFActions.Length; i++)
                    {
                        if ((uint)contrastAFActions[i] == (uint)eNkMAIDContrastAF.kNkMAIDContrastAF_Start)
                        {
                            contrastAFActions.Index = i;
                            device.SetEnum(eNkMAIDCapability.kNkMAIDCapability_ContrastAF, contrastAFActions);
                            break;
                        }
                    }
                }
                else { throw new Exception(); }
            }
            catch (NikonException ex)
            {
                Trace.WriteLine("Failed to focus live view: " + ex.ToString());
            }
        }

        void _timer_Tick(object? sender, EventArgs e)
        {
            Trace.WriteLine("Tick");
            Debug.Assert(_device != null);

            if (_device.GetLiveViewImage() is not NikonLiveViewImage liveViewImage)
            {
                Trace.WriteLine("Failed to get live view image");
                _timer.Stop();
                return;
            }

            // Note: Decode the live view jpeg image on a seperate thread to keep the UI responsive

            ThreadPool.QueueUserWorkItem(new WaitCallback((o) =>
            {
                Debug.Assert(liveViewImage != null);

                JpegBitmapDecoder decoder = new JpegBitmapDecoder(
                    new MemoryStream(liveViewImage.JpegBuffer),
                    BitmapCreateOptions.None,
                    BitmapCacheOption.OnLoad);

                Debug.Assert(decoder.Frames.Count > 0);
                BitmapFrame frame = decoder.Frames[0];

                Dispatcher.CurrentDispatcher.Invoke((Action)(() =>
                {
                    SetLiveViewImage(frame);
                }));
            }));

            // Load the image from the byte array
            using var image = Image.Load<Rgb24>(liveViewImage.JpegBuffer);
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(640, 640), // Resize
                Mode = ResizeMode.Pad // This preserves aspect ratio and pads the image with a background color
            }));

            var watch = Stopwatch.StartNew(); //start stopwatch:
            watch.Start();
            var result = _predictor.Detect(image);
            watch.Stop();
            Trace.WriteLine("Detection time: " + watch.ElapsedMilliseconds);
            if (result.ToString() == "")
            {
                Trace.WriteLine("No birds detected");
                //uint afPt = _device.GetUnsigned(eNkMAIDCapability.kNkMAIDCapability_AutoFocusPt);
                //Trace.Wri                teLine("Auto focus point: " + afPt);
            }
            else
            {
                Trace.WriteLine("Detection results: " + result);
                // Get the target coordinates from the detection results, i.e., the biggest bird detected.
                RectangleF target = result.Boxes[0].Bounds;
                foreach (var box in result.Boxes.Skip(1))
                {
                    if (box.Bounds.Width * box.Bounds.Height > target.Width * target.Height)
                    {
                        target = box.Bounds;
                    }
                }
                PointF targetCenter = RectangleF.Center(target);

                // Focus the camera on the target coordinates, or as close as possible given the camera's focus points. 
                // This is going to be difficult due to the >100ms delay, so the focus could miss the bird and focus on the background instead.
                // This would be especially bad if the camera is at ground level because it could focus on the sky, making the bird very blurry.
                //uint afPt = _device.GetUnsigned(eNkMAIDCapability.kNkMAIDCapability_AutoFocusPt); //not a capability of the d500
                //Trace.WriteLine("Auto focus point: " + afPt);
                //_device.Start(eNkMAIDCapability.kNkMAIDCapability_AutoFocus);

                // Save the image with the detection results, but we don't want to save too often, especially if there's a persistant false positive! Use root factorials or similar to save decreasingly often.
                result.PlotImage(image);
            }

            image.Save("liveview.jpg"); // Just save the latest image to Pictures folder, useful for debugging
            image.Dispose(); // Dispose of the image to free up memory
        }

        void Save(byte[] buffer, string file)
        {
            string path = Path.Combine(
                System.Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                file);

            Trace.WriteLine("Saving: " + path);

            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write))
                {
                    stream.Write(buffer, 0, buffer.Length);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Failed to save file: " + path + ", " + ex.Message);
            }
        }

        void SetLiveViewImage(BitmapSource image)
        {
            _liveViewImage = image;
            OnPropertyChanged("LiveViewImage");
        }

        public BitmapSource LiveViewImage
        {
            get { return _liveViewImage; }
        }

        public void StopTimer()
        {
            _timer.Stop();
        }

        public void StopManager()
        {
            _manager.Shutdown();
        }

    }

    // View model base class
    abstract class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}


