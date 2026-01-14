using Nikon;
using System.Diagnostics;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Compunet.YoloSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Compunet.YoloSharp.Plotting;
using System.Windows.Data;
using System.ComponentModel;
using System.Collections.ObjectModel;
using Compunet.YoloSharp.Data;

namespace seawatcher3000
{
    class Seawatcher : ViewModelBase
    {
        static NikonManager _manager = new("Type0020.md3");
        NikonDevice? _device;
        static DispatcherTimer _timer = new();
        YoloPredictor _predictor;
        BitmapSource _liveViewImage;
        static System.Timers.Timer focusTimer;
        List<int> save_indices = [3, 5, 8, 12, 18, 26, 37]; // Save at these consecutive detection counts (decreasing frequency)
        int consecutive_detections = 0;
        Track currentTrack = new Track();

        public Seawatcher()
        {
            _timer.Interval = TimeSpan.FromMilliseconds(100);
            _manager.DeviceAdded += new DeviceAddedDelegate(manager_DeviceAdded);
            _timer.Tick += new EventHandler(_timer_Tick);
            _predictor = new YoloPredictor("seaeyes_model_1_obb.onnx"); // OBB model
            _liveViewImage = BitmapFrame.Create(new MemoryStream(File.ReadAllBytes("liveview.jpg"))); // Load the latest image from the Pictures folder
            setFocusTimer();

            if (!Directory.Exists("detections"))
            {
                Directory.CreateDirectory("detections");
            }
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
                else { throw new Exception("No device connected"); }
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

            // Decode the live view jpeg image on a seperate thread to keep the UI responsive
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
            var result = _predictor.DetectObb(image);
            watch.Stop();
            Trace.WriteLine("Detection time: " + watch.ElapsedMilliseconds);
            if (result.ToString() == "")
            {
                Trace.WriteLine("No birds detected");
                consecutive_detections = 0;
                //uint afPt = _device.GetUnsigned(eNkMAIDCapability.kNkMAIDCapability_AutoFocusPt);
                //Trace.WriteLine("Auto focus point: " + afPt);
            }
            else // Bird(s) detected
            {
                Trace.WriteLine("Detection results: " + result);
                consecutive_detections += 1;
                // Get the target coordinates from the detection results, i.e., the biggest bird detected.
                // This is bad logic because diagonal birds will have bigger bounding boxes than frontal birds
                // TODO: Use oriented bounding boxes instead
                ObbDetection target = result.First();
                foreach (var detection in result.Skip(1))
                {
                    // Calculate actual area considering rotation
                    float area = detection.Bounds.Width * detection.Bounds.Height;
                    float targetArea = target.Bounds.Width * target.Bounds.Height;
                    
                    if (area > targetArea)
                    {
                        target = detection;
                    }
                }
                PointF targetCenter = RectangleF.Center(target.Bounds);

                // Focus the camera on the target coordinates, or as close as possible given the camera's focus points. 
                // This is going to be difficult due to the >100ms delay, so the focus could miss the bird and focus on the background instead.
                // This would be especially bad if the camera is at ground level because it could focus on the sky, making the bird very blurry.
                //uint afPt = _device.GetUnsigned(eNkMAIDCapability.kNkMAIDCapability_AutoFocusPt); //not a capability of the d500
                //Trace.WriteLine("Auto focus point: " + afPt);
                //_device.Start(eNkMAIDCapability.kNkMAIDCapability_AutoFocus);

                // Save the image with the detection results, but we don't want to save too often, especially if there's a persistant false positive! Save decreasingly often.
                result.PlotImage(image);
                if (consecutive_detections == 1) // First detection in sequence
                {
                    // Reset track
                    // because previous frame had no detections
                    currentTrack.Reset();
                }
                else if (consecutive_detections >= 3 && isNewTrack(target, image)) // Detect if this is a new bird/track based on spatial heuristics
                {
                    consecutive_detections = 1;
                    // Reset track
                    // because bird is different to previous detections in the sequence
                    currentTrack.Reset();
                }
                else if (save_indices.Contains(consecutive_detections)) // Save at decreasing frequency
                {
                    string filename = Path.Combine("detections", "detection_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".jpg");
                    image.SaveAsJpegAsync(filename);
                    //TODO: Log detection to CSV file with timestamp, bounding box, confidence, etc.
                    //TODO: Use Track's DateID to group detections of the same bird together
                    //TODO: Trigger shutter
                }
                // Update track with new detection
                currentTrack.Positions.Add((targetCenter.X, targetCenter.Y, DateTime.Now));
                currentTrack.BoundingSizes.Add((target.Bounds.Width, target.Bounds.Height));
            }
            image.Dispose(); // Dispose of the image to free up memory
        }

        /// <summary>
        /// Determines whether the specified image represents a new track.
        /// Use spatial heuristics to determine if the detected bird is different to previous detections in the 
        /// consecutive sequence. Compares to the last two detections in the current track.
        /// </summary>
        /// <param name="target">The detection to evaluate.</param>
        /// <param name="image">The current frame image.</param>
        /// <returns>true if the bird is identified as a new track; otherwise, false.</returns>
        private bool isNewTrack(ObbDetection target, Image<Rgb24> image)
        {
            // ----- Is the direction of flight significantly different? -----
            PointF currentCenter = RectangleF.Center(target.Bounds);

            // Get last two positions to calculate previous direction
            var prevPos2 = currentTrack.Positions[^2];
            var prevPos1 = currentTrack.Positions[^1];
            
            // Calculate previous direction vector
            float prevDx = prevPos1.X - prevPos2.X;
            float prevDy = prevPos1.Y - prevPos2.Y;

            // Calculate current direction vector
            float currentDx = currentCenter.X - prevPos1.X;
            float currentDy = currentCenter.Y - prevPos1.Y;
            
            // Calculate dot product
            double dotProduct = prevDx * currentDx + prevDy * currentDy;

            // If dot product is zero or negative, angle >= 90° (direction changed significantly)
            if (dotProduct <= 0)
            {
                return true;
            }
            
            // ----- Is the size of the bounding box significantly different (+-30%)? -----
            if (currentTrack.BoundingSizes.Count > 0)
            {
                var prevSize = currentTrack.BoundingSizes[^1];
                float prevArea = prevSize.Width * prevSize.Height;
                float currentArea = target.Bounds.Width * target.Bounds.Height;
                
                float sizeRatio = currentArea / prevArea;
                
                if (sizeRatio < 0.7f || sizeRatio > 1.3f)
                {
                    return true;
                }
            }

            // ----- Is the position of the bird significantly different (>30% image span apart)
            // ----- AND is the bird sufficiently distant (occupying <20% image width)? -----
            float imageWidth = image.Width;
            float imageHeight = image.Height;
            
            float distanceFromPrev = (float)Math.Sqrt(
                Math.Pow(currentCenter.X - prevPos1.X, 2) + 
                Math.Pow(currentCenter.Y - prevPos1.Y, 2));
            
            float maxImageSpan = (float)Math.Sqrt(imageWidth * imageWidth + imageHeight * imageHeight);
            float distanceRatio = distanceFromPrev / maxImageSpan;
            
            bool birdIsDistant = (target.Bounds.Width / imageWidth) < 0.2f;
            bool positionChangedSignificantly = distanceRatio > 0.3f;
            
            if (positionChangedSignificantly && birdIsDistant)
            {
                return true;
            }
            
            return false;
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


