using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
using UnityEngine;

namespace ScienceFoundry.FTL
{
    public class NavComputer
    {
        private System.Random rndGenerator = new System.Random();

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
                    //Debug.Log("JumpPossible, source not: docked, flying, sub_orbital, orbiting, escaping");
                    return false;
                }
                if (!((Destination.situation == Vessel.Situations.ORBITING) ||
                      (Destination.situation == Vessel.Situations.ESCAPING) ||
                      (Destination.situation == Vessel.Situations.FLYING)
                      ))
                {
                   // Debug.Log("JumpPossible, dest not: orbiting, escaping, flying");
                    return false;
                }
                if (Source.GetOrbitDriver() == null)
                {
                    //Debug.Log("JumpPossible, no source.GetOrbitDriver");
                    return false;
                }
                if (Destination.GetOrbitDriver() == null)
                {
                    //Debug.Log("JumpPossible, no dest.GetOrbitDriver");
                    return false;
                }
                if (Source.GetOrbitDriver().orbit.referenceBody == null)
                {
                    //Debug.Log("JumpPossible, no source orbit.referenceBody");
                    return false;
                }
                if (Destination.GetOrbitDriver().referenceBody == null)
                {
                   // Debug.Log("JumpPossible, no dest orbit.referenceBody");
                    return false;
                }

                return true;
            }
        }

        public bool Jump(double force)
        {
            bool retValue = false;

            if (JumpPossible)
            {
                if (rndGenerator.NextDouble() < GetSuccesProbability(force))
                {
                    Source.Rendezvous(Destination);
                    retValue = true;
                }
                else
                    Source.Kill();
            }

            return retValue;
        }

        public double GetRequiredForce()
        {
            return JumpPossible ? (Source.TunnelCreationRequirement() + Destination.TunnelCreationRequirement()) * Source.GetTotalMass() * 1e3 : Double.PositiveInfinity;
        }

        public double GetSuccesProbability(double generatedPunchForce)
        {
            double retValue = 0;

            if (JumpPossible)
            {
                var forceRequired = GetRequiredForce();

                if (forceRequired > generatedPunchForce)
                {
                    retValue = generatedPunchForce / forceRequired;
                }
                else
                {
                    retValue = 1;
                }
            }

            return retValue;
        }
    }
}
