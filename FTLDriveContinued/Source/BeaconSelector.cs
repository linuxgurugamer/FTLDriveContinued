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
            return v.loaded ? CheckLoadedVesselForABeacon(v) : CheckUnloadedVesselForABeacon(v);
        }

        private static bool CheckUnloadedVesselForABeacon(Vessel v)
        {
            bool retValue = false;
            const string module_name = "FTLBeaconModule";

            foreach (ProtoPartSnapshot pps in v.protoVessel.protoPartSnapshots)
            {
                foreach (ProtoPartModuleSnapshot m in pps.modules)
                {
                    if (m.moduleName == module_name)
                    {
                        try
                        {
                            retValue |= Convert.ToBoolean(m.moduleValues.GetValue("beaconActivated"));
                        }
                        catch (Exception e)
                        {
                            UnityEngine.Debug.Log("Proto Beacon activated: Exception: " + e.Message);
                        }
                        break;
                    }
                }
            }
            return retValue;
        }

        private static bool CheckLoadedVesselForABeacon(Vessel v)
        {
            bool retValue = false;
            const string module_name = "FTLBeaconModule";

            foreach (Part p in v.parts)
            {
                if (p.State != PartStates.DEAD)
                {
                    foreach (PartModule pm in p.Modules)
                    {
                        if (pm.moduleName == module_name)
                        {
                            var beacon = pm as FTLBeaconModule;

                            if (beacon != null)
                            {
                                retValue |= beacon.IsBeaconActive();
                            }
                            else
                                UnityEngine.Debug.Log("Not as expected a FTLBeaconModule");
                        }
                    }
                }
            }
            return retValue;
        }

    }
}
