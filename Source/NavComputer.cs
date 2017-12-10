using System;
using UnityEngine;

namespace ScienceFoundry.FTL
{
    public class NavComputer
    {
        private Random rndGenerator = new Random();

        public Vessel Destination { get; set; }
        public Vessel Source { get; set; }

        public bool JumpPossible
        {
            get
            {
                if (Destination == null)
                    return false;
                if (Source == null)
                    return false;
                
                if (!((Source.situation == Vessel.Situations.DOCKED) ||
                      (Source.situation == Vessel.Situations.FLYING) ||
                      (Source.situation == Vessel.Situations.SUB_ORBITAL) ||
                      (Source.situation == Vessel.Situations.ORBITING) ||
                      (Source.situation == Vessel.Situations.ESCAPING)))
                {
                    LogsManager.DisplayMsg("JumpPossible, source not: docked, flying, sub_orbital, orbiting, escaping");
                    return false;
                }
                if (!((Destination.situation == Vessel.Situations.ORBITING) ||
                      (Destination.situation == Vessel.Situations.ESCAPING) ||
                      (Destination.situation == Vessel.Situations.FLYING)
                      ))
                {
                    LogsManager.DisplayMsg("JumpPossible, destination not: orbiting, escaping, flying");
                    return false;
                }
                if (Source.GetOrbitDriver() == null)
                {
                    LogsManager.DisplayMsg("JumpPossible, no source.GetOrbitDriver");
                    return false;
                }
                if (Destination.GetOrbitDriver() == null)
                {
                    LogsManager.DisplayMsg("JumpPossible, no dest.GetOrbitDriver");
                    return false;
                }
                if (Source.GetOrbitDriver().orbit.referenceBody == null)
                {
                    LogsManager.DisplayMsg("JumpPossible, no source orbit.referenceBody");
                    return false;
                }
                if (Destination.GetOrbitDriver().referenceBody == null)
                {
                    LogsManager.DisplayMsg("JumpPossible, no dest orbit.referenceBody");
                    return false;
                }

                return true;
            }
        }

        public bool Jump(double force)
        {
            if (JumpPossible)
            {
                double r = rndGenerator.NextDouble();
                double s = GetSuccessProbability(force);
                LogsManager.FlightLog("before jump, rolled ", r, ", needed ", s);

                if (r < s)
                {
                    LogsManager.FlightLog("jump successful");
                    Source.Rendezvous(Destination);
                    return true;
                }
                else
                {
                    LogsManager.FlightLog("jump failed, vessel destroyed");
                    Source.Explode();
                    return false;
                }
            }

            return false;
        }

        public double GetRequiredForce()
        {
            return JumpPossible ? (Source.TunnelCreationRequirement() + Destination.TunnelCreationRequirement()) * Source.GetTotalMass() * 1000d : Double.PositiveInfinity;
        }

        public double GetSuccessProbability(double generatedPunchForce)
        {
            return Math.Min(1, Math.Max(0, generatedPunchForce / GetRequiredForce()));
        }

    }
}
