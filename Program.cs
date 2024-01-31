using Nikon;
using System.Diagnostics;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Threading;
using System.IO;
using System.Windows.Controls;
using System.Windows.Input;

namespace seawatcher3000
{

    NikonManager manager = new("Type0020.md3");
    NikonDevice _device;
    DispatcherTimer _timer = new();
    _timer.Interval = TimeSpan.FromMilliseconds(100);
manager.DeviceAdded += new DeviceAddedDelegate(manager_DeviceAdded);


    /// <summary>
    /// What to do when a device is added
    /// </summary>
    void manager_DeviceAdded(NikonManager sender, NikonDevice device)
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


        device.Capture();
        StartLiveView();
        //let live view run for 5 seconds
        //Thread.Sleep(5000);
        //StopLiveView();
        //sender.Shutdown();
    }

    void StartLiveView()
    {
        try
        {
            NikonDevice device = _device as NikonDevice;
            if (device != null)
            {
                device.LiveViewEnabled = true;
                _timer.Start();
            }
        }
        catch (NikonException ex)
        {
            Console.WriteLine("Failed to start live view: " + ex.ToString());
        }
    }

    void StopLiveView()
    {
        try
        {
            NikonDevice device = _device as NikonDevice;

            if (device != null)
            {
                _timer.Stop();
                device.LiveViewEnabled = false;
            }
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
            Console.WriteLine("Failed to get live view image: " + ex.ToString());
        }

        if (liveViewImage == null)
        {
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
                //SetLiveViewImage(frame);
            }));
        }));

        Save(liveViewImage.JpegBuffer, "liveview.jpg");
        using (FileStream stream = new FileStream("liveview.jpg", FileMode.Create, FileAccess.Write))
        {
            stream.Write(liveViewImage.JpegBuffer, 0, liveViewImage.JpegBuffer.Length);
        }
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