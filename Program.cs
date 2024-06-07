﻿using Nikon;
using System.Diagnostics;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Compunet.YoloV8;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

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
            // Resize the image to the expected size
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(640, 640),
                Mode = ResizeMode.Pad // This preserves aspect ratio and pads the image with a background color
            }));

            var watch = Stopwatch.StartNew(); //start stopwatch:
            watch.Start();
            var result = predictor.Detect(image);
            watch.Stop();
            Trace.WriteLine("Detection time: " + watch.ElapsedMilliseconds);
            Trace.WriteLine("Detection results: " + result);

            Save(liveViewImage.JpegBuffer, "liveview.jpg");
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


