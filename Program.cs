using Nikon;

NikonManager manager = new NikonManager("Type0020.md3");

manager.DeviceAdded += new DeviceAddedDelegate(manager_DeviceAdded);

NkMAIDCapInfo[] capInfo = device.GetCapabilityInfo();
print("Device name: " + capInfo[0].szDescription);

try
{
    int batteryLevel = device.GetInteger(eNkMAIDCapability.kNkMAIDCapability_BatteryLevel);
    print("Battery level: " + batteryLevel);
}
catch (NikonException ex)
{
}


manager.Shutdown();