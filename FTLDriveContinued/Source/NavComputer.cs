using System;
using UnityEngine;

namespace ScienceFoundry.FTL
{
    public class NavComputer
    {
        private System.Random rndGenerator = new System.Random();

        public Vessel Destination { get; set; }
        public Vessel Source { get; set; }

        void DisplayJumpPossibleMsg(string str)
        {
            ScreenMessages.PostScreenMessage(str, 4f, ScreenMessageStyle.UPPER_CENTER);
            Debug.Log(str);
        }

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
                    DisplayJumpPossibleMsg("JumpPossible, source not: docked, flying, sub_orbital, orbiting, escaping");
                    return false;
                }
                if (!((Destination.situation == Vessel.Situations.ORBITING) ||
                      (Destination.situation == Vessel.Situations.ESCAPING) ||
                      (Destination.situation == Vessel.Situations.FLYING)
                      ))
                {
                    DisplayJumpPossibleMsg("JumpPossible, destination not: orbiting, escaping, flying");
                    return false;
                }
                if (Source.GetOrbitDriver() == null)
                {
                    DisplayJumpPossibleMsg("JumpPossible, no source.GetOrbitDriver");
                    return false;
                }
                if (Destination.GetOrbitDriver() == null)
                {
                    DisplayJumpPossibleMsg("JumpPossible, no dest.GetOrbitDriver");
                    return false;
                }
                if (Source.GetOrbitDriver().orbit.referenceBody == null)
                {
                    DisplayJumpPossibleMsg("JumpPossible, no source orbit.referenceBody");
                    return false;
                }
                if (Destination.GetOrbitDriver().referenceBody == null)
                {
                    DisplayJumpPossibleMsg("JumpPossible, no dest orbit.referenceBody");
                    return false;
                }

                return true;
            }
        }

        /// <summary>
        /// Adds a new entry to the flight events log.
        /// Automatically adds the MET at the beginning of the log
        /// </summary>
        public static void FlightLog(string msg)
        {
            FlightLogger.eventLog.Add("[" + KSPUtil.PrintTimeStamp(FlightLogger.met) + "]: " + msg);
        }

        public bool Jump(double force)
        {
            if (JumpPossible)
            {
                double r = rndGenerator.NextDouble();
                Debug.Log("Jump, r: " + r.ToString() + "   SuccessProbability: " + GetSuccessProbability(force).ToString());

                if (r < GetSuccessProbability(force))
                {
                    Source.Rendezvous(Destination);
                    return true;
                }
                else
                {
                    FlightLog("FTL Jump to " + Destination.ToString() + " failed, vessel destroyed");
                    Source.Explode();
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
