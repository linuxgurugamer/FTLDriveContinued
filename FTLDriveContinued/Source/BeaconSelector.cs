using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace ScienceFoundry.FTL
{
    public static class BeaconSelector
    {
        public const string NO_TARGET = "No Beacon Selected";

        public static Vessel Next(Vessel current, Vessel activeVessel)
        {
            Vessel retValue = null;

            if (activeVessel != null)
            {
                if((FlightGlobals.fetch != null) && (FlightGlobals.Vessels != null))
                {
                    var vessels = FlightGlobals.Vessels.FindAll((v) => v != null).FindAll((v) => v != activeVessel);
                    var beacons = vessels.FindAll((v) => VesselHasAnActiveBeacon(v));

                    if (beacons.Count > 0)
                    {
                        var index = beacons.FindIndex((v) => v == current);

                        if (index >= 0)
                            retValue = beacons[(index + 1) % beacons.Count];
                        else
                            retValue = beacons[0];
                    }
                    
                }
            }

            return retValue;
        }

        private static bool VesselHasAnActiveBeacon(Vessel v)
        {
            bool retValue = false;

            if (v.loaded)
                retValue = CheckLoadedVesselForABeacon(v);
            else
                retValue = CheckUnloadedVesselForABeacon(v);

            return retValue;
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
                            if (!retValue)
                                retValue = Convert.ToBoolean(m.moduleValues.GetValue("beaconActivated"));
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
                                if (!retValue)
                                    retValue = beacon.IsBeaconActive();
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
