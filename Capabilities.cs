using Nikon;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace seawatcher3000
{
    class Capabilities
    {
        public static NikonManager manager = new("Type0020.md3");
        NikonDevice _device;

        public Capabilities()
        {
            manager.DeviceAdded += new DeviceAddedDelegate(manager_DeviceAdded);
        }

        /// <summary>
        /// What to do when a device is added
        /// </summary>
        public void manager_DeviceAdded(NikonManager sender, NikonDevice device)
        {
            // Write list of supported capabilities to the console
            NkMAIDCapInfo[] supportedCaps = device.GetCapabilityInfo();
            foreach (NkMAIDCapInfo supportedCap in supportedCaps)
            {
                Console.WriteLine(supportedCap.GetDescription());
            }
        }
    }
}