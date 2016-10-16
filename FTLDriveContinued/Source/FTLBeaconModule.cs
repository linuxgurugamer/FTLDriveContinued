//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
using System.Diagnostics;

namespace ScienceFoundry.FTL
{
    [KSPModule("FTL Beacon")]
    public class FTLBeaconModule : PartModule
    {
        [KSPField(guiActive = false, guiActiveEditor = false, isPersistant = true)]
        private bool beaconActivated = false;

        [KSPEvent(active = true, guiActive = true, guiActiveEditor = true, guiName = "Turn Beacon Off")]
        public void ToggleBeacon()
        {
            beaconActivated = !beaconActivated;            
            UpdateEvents();
        }

        private void UpdateEvents()
        {
            Events["ToggleBeacon"].guiName = beaconActivated ? "Turn Beacon Off" : "Turn Beacon On";
        }

        public bool IsBeaconActive()
        {
            return beaconActivated;
        }

        public override void OnStart(PartModule.StartState state)
        {
            UpdateEvents();
            //base.OnStart(state);
        }
    }
}
