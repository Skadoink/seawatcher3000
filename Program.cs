﻿using Nikon;
using System.Diagnostics;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.IO;


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
    _timer.Tick += new EventHandler(timerTick);

    Console.WriteLine("Device added: " + device.Name);
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

    sender.Shutdown();
}

void StartLiveView()
{
    try
    {
        if (_device != null)
        {
            _device.LiveViewEnabled = true;
            _timer.Start();
        }
    }
    catch (NikonException ex)
    {
        Console.WriteLine("Failed to start live view: " + ex.ToString());
    }
}

void timerTick(object sender, EventArgs e)
{
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
}

void Save(byte[] buffer, string file)
{
    string path = Path.Combine(
        System.Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
        file);

    Console.WriteLine("Saving: " + path);

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