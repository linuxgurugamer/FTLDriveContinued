using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ScienceFoundry.FTL
{
    public static class BeaconSelector
    {
        public const string NO_TARGET = "No Beacon Selected";
        public const string MODULE_NAME = "FTLBeaconModule";

        public static Vessel Next(Vessel current, Vessel activeVessel)
        {
            if (activeVessel != null)
            {
                if(FlightGlobals.fetch != null && FlightGlobals.Vessels != null)
                {
                    var vessels = FlightGlobals.Vessels.FindAll(v => v != null && v != activeVessel);
                    var beacons = vessels.FindAll(v => VesselHasAnActiveBeacon(v));

                    if (beacons.Count > 0)
                    {
                        int index = beacons.FindIndex(v => v == current);
                        return index >= 0 ? beacons[(index + 1) % beacons.Count] : beacons[0];
                    }
                }
            }

            return null;
        }

        private static bool VesselHasAnActiveBeacon(Vessel v)
        {
            try
            {
                if (v.loaded)
                {
                    foreach (Part p in v.parts)
                        if (p.State != PartStates.DEAD)
                            foreach (PartModule pm in p.Modules)
                                if (pm.moduleName == MODULE_NAME)
                                    if (((FTLBeaconModule)pm).IsBeaconActive())
                                        return true;
                    return false;
                }
                else
                {
                    foreach (ProtoPartSnapshot pps in v.protoVessel.protoPartSnapshots)
                        foreach (ProtoPartModuleSnapshot m in pps.modules)
                            if (m.moduleName == MODULE_NAME)
                                if (Convert.ToBoolean(m.moduleValues.GetValue("beaconActivated")))
                                    return true;
                    return false;
                }
            }
            catch
            {
                LogsManager.ErrorLog("Enumerating modules caused unexpected error");
                return false;
            }
        }

    }
}
