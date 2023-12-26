using Nikon;

namespace seawatcher3000



{
    class Program
    {
        static void Main(string[] args)
        {
            NikonManager manager = new NikonManager("Type0020.md3");
            NikonDevice device = manager.Devices[0];
            device.Capture();
            manager.Shutdown();
        }
    }
}

NikonManager manager = new NikonManager("Type0020.md3"); 

void manager_DeviceAdded(NikonManager sender, NikonDevice device)
{
    Console.WriteLine("Device added: " + device.Name);
    NkMAIDCapInfo[] supportedCaps = device.GetCapabilityInfo();
    // Write list of supported capabilities to the console
    foreach (NkMAIDCapInfo supportedCap in supportedCaps)
    {
        Console.WriteLine(supportedCap.GetDescription());
    }

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

void device_ImageReady(NikonDevice device, NikonImage image)
{
    NkMAIDCapInfo[] supportedCaps = device.GetCapabilityInfo();
    // Write list of supported capabilities to the console
    foreach (NkMAIDCapInfo supportedCap in supportedCaps)
    {
        Console.WriteLine(supportedCap.GetDescription());
    }
}

manager.DeviceAdded += new DeviceAddedDelegate(manager_DeviceAdded);

// device.ImageReady += new ImageReadyDelegate(device_ImageReady);

Console.WriteLine("Press any key to exit...");
Console.ReadKey();

manager.Shutdown();