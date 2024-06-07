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

namespace seawatcher3000
{
    class Seawatcher
    {
        public static NikonManager manager = new("Type0020.md3");
        NikonDevice _device;
        public static DispatcherTimer _timer = new();
        private YoloV8Predictor predictor;

        public Seawatcher()
        {
            _timer.Interval = TimeSpan.FromMilliseconds(100);
            manager.DeviceAdded += new DeviceAddedDelegate(manager_DeviceAdded);
        }

        /// <summary>
        /// What to do when a device is added
        /// </summary>
        public void manager_DeviceAdded(NikonManager sender, NikonDevice device)
        {
            _device = device;
            _timer.Tick += new EventHandler(_timer_Tick);

            Trace.WriteLine("Device added: " + device.Name);
            NkMAIDCapInfo[] supportedCaps = device.GetCapabilityInfo();
            // Write list of supported capabilities to the console
            foreach (NkMAIDCapInfo supportedCap in supportedCaps)
            {
                Console.WriteLine(supportedCap.GetDescription());
            }

            // Write battery level to the console
            try
            {
                int batteryLevel = device.GetInteger(eNkMAIDCapability.kNkMAIDCapability_BatteryLevel);
                Console.WriteLine("Battery level: " + batteryLevel);
            }
            catch (NikonException ex)
            {
                Console.WriteLine("Error getting battery level: " + ex.Message);
            }
        }

        public void StartLiveView()
        {
            try
            {
                NikonDevice device = _device as NikonDevice;
                if (device != null)
                {
                    // Open bird detection model 
                    predictor = YoloV8Predictor.Create("seaeyes_model_1.onnx");

                    // Start live view
                    device.LiveViewEnabled = true;
                    _timer.Start();
                }
                else { throw new Exception(); }
            }
            catch (NikonException ex)
            {
                Console.WriteLine("Failed to start live view: " + ex.ToString());
            }
        }

        public void StopLiveView()
        {
            try
            {
                NikonDevice device = _device as NikonDevice;

                if (device != null)
                {
                    _timer.Stop();
                    device.LiveViewEnabled = false;
                }
                else { throw new Exception(); }
            }
            catch (NikonException ex)
            {
                Console.WriteLine("Failed to stop live view: " + ex.ToString());
            }
        }

        void _timer_Tick(object sender, EventArgs e)
        {
            Trace.WriteLine("Tick");
            Debug.Assert(_device != null);

            NikonLiveViewImage liveViewImage = null;

            try
            {
                liveViewImage = _device.GetLiveViewImage();
            }
            catch (NikonException ex)
            {
                Trace.WriteLine("Failed to get live view image: " + ex.ToString());
            }

            if (liveViewImage == null)
            {
                _timer.Stop();
                return;
            }

            // Load the image from the byte array
            using var image = Image.Load<Rgb24>(liveViewImage.JpegBuffer);

            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(640, 640), // Resize
                Mode = ResizeMode.Pad // This preserves aspect ratio and pads the image with a background color
            }));

            var watch = Stopwatch.StartNew(); //start stopwatch:
            watch.Start();
            var result = predictor.Detect(image);
            watch.Stop();
            Trace.WriteLine("Detection time: " + watch.ElapsedMilliseconds);
            if (result.ToString() == "")
            {
                Trace.WriteLine("No birds detected");
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

                // Save the image with the detection results, but we don't want to save too often, especially if there's a persistant false positive!
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
                Console.WriteLine("Failed to save file: " + path + ", " + ex.Message);
            }
        }
    }
}


