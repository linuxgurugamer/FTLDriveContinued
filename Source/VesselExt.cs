﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;

namespace ScienceFoundry.FTL
{
    public static class VesselExt
    {
        public static void Explode(this Vessel self)
        {
            //foreach (var p in self.Parts)
            for (int i = self.Parts.Count - 1; i >= 0; i--)
            {
                try
                {
                    self.Parts[i].explode();
                }
                catch
                {
                    Debug.Log("Explode, exception caught");
                }
            }
        }

        public static void Rendezvous(this Vessel self, Vessel destination, double leadTime = 5d)
        {
            var o = destination.orbit;
            var newOrbit = CreateOrbit(o.inclination, o.eccentricity, o.semiMajorAxis, o.LAN, o.argumentOfPeriapsis, o.meanAnomalyAtEpoch, 
                                       o.epoch - leadTime, o.referenceBody);

            self.SetOrbit(newOrbit);
        }

        private static void SetOrbit(this Vessel vessel, Orbit newOrbit)
        {
            if (newOrbit.getRelativePositionAtUT(Planetarium.GetUniversalTime()).magnitude > newOrbit.referenceBody.sphereOfInfluence)
            {
                LogsManager.ErrorLog("Destination position was above the sphere of influence");
                return;
            }

            try
            {
                OrbitPhysicsManager.HoldVesselUnpack(60);
            }
            catch (NullReferenceException)
            {
                LogsManager.ErrorLog("OrbitPhysicsManager.HoldVesselUnpack threw NullReferenceException");
            }

            vessel.GoOnRails();

            var oldBody = vessel.orbitDriver.orbit.referenceBody;

            vessel.orbitDriver.UpdateOrbit(newOrbit);

            vessel.orbitDriver.pos = vessel.orbit.pos.xzy;
            vessel.orbitDriver.vel = vessel.orbit.vel;

            var newBody = vessel.orbitDriver.orbit.referenceBody;

            if (newBody != oldBody)
            {
                var evnt = new GameEvents.HostedFromToAction<Vessel, CelestialBody>(vessel, oldBody, newBody);
                GameEvents.onVesselSOIChanged.Fire(evnt);
            }
        }

        private static void UpdateOrbit(this OrbitDriver orbitDriver, Orbit newOrbit)
        {
            var orbit = orbitDriver.orbit;

            orbit.inclination = newOrbit.inclination;
            orbit.eccentricity = newOrbit.eccentricity;
            orbit.semiMajorAxis = newOrbit.semiMajorAxis;
            orbit.LAN = newOrbit.LAN;
            orbit.argumentOfPeriapsis = newOrbit.argumentOfPeriapsis;
            orbit.meanAnomalyAtEpoch = newOrbit.meanAnomalyAtEpoch;
            orbit.epoch = newOrbit.epoch;
            orbit.referenceBody = newOrbit.referenceBody;
            orbit.Init();
            orbit.UpdateFromUT(Planetarium.GetUniversalTime());

            if (orbit.referenceBody != newOrbit.referenceBody)
            {
                if (orbitDriver.OnReferenceBodyChange != null)
                    orbitDriver.OnReferenceBodyChange(newOrbit.referenceBody);
            }
        }

        internal static Orbit CreateOrbit(double inc, double e, double sma, double lan, double w, double mEp, double epoch, CelestialBody body)
        {
            if (double.IsNaN(inc))
                inc = 0;
            if (double.IsNaN(e))
                e = 0;
            if (double.IsNaN(sma))
                sma = body.Radius * body.radiusAtmoFactor + 10000;
            if (double.IsNaN(lan))
                lan = 0;
            if (double.IsNaN(w))
                w = 0;
            if (double.IsNaN(mEp))
                mEp = 0;

            if (double.IsNaN(epoch))
                mEp = Planetarium.GetUniversalTime();

            if (Math.Sign(e - 1) == Math.Sign(sma))
                sma = -sma;

            if (Math.Sign(sma) >= 0)
            {
                //while (mEp < 0)
                //    mEp += Math.PI * 2;
                //while (mEp > Math.PI * 2)
                //    mEp -= Math.PI * 2;
                mEp = mEp % (Math.PI * 2);
            }

            return new Orbit(inc, e, sma, lan, w, mEp, epoch, body);
        }

    }
}
