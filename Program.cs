using Nikon;
using System;

NikonManager manager = new NikonManager("Type0020.md3");
manager.DeviceAdded += new DeviceAddedDelegate(manager_DeviceAdded);

/// <summary>
/// What to do when a device is added
/// </summary>
void manager_DeviceAdded(NikonManager sender, NikonDevice device)
{
    /*Console.WriteLine("Device added: " + device.Name);
    NkMAIDCapInfo[] supportedCaps = device.GetCapabilityInfo();
    // Write list of supported capabilities to the console
    foreach (NkMAIDCapInfo supportedCap in supportedCaps)
    {
        Console.WriteLine(supportedCap.GetDescription());
    }*/

    try
    {
        int batteryLevel = device.GetInteger(eNkMAIDCapability.kNkMAIDCapability_BatteryLevel);
        Console.WriteLine("Battery level: " + batteryLevel);
    }
    catch (NikonException ex)
    {
        Console.WriteLine("Error getting battery level: " + ex.Message);
    }

    device.ImageReady += new ImageReadyDelegate(device_ImageReady);
    device.CapabilityValueChanged += new CapabilityChangedDelegate(device_CapabilityValueChanged);

    device.Start(eNkMAIDCapability.kNkMAIDCapability_Capture);
//    device.Capture();

    sender.Shutdown();
}

/// <summary>
/// What to do when an image is ready
/// </summary>
void device_ImageReady(NikonDevice sender, NikonImage image)
{
    Console.WriteLine("Image ready: ");
}

/// <summary>
/// What to do when a capability value changes
/// </summary>
void device_CapabilityValueChanged(NikonDevice sender, eNkMAIDCapability capability)
{
    Console.WriteLine("Capability value changed: " );
}