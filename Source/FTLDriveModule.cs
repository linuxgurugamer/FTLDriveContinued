using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ScienceFoundry.FTL
{
    [KSPModule("FTL Drive")]
    public class FTLDriveModule : DynamicDisplay
    {
        //------------------------------ PRECOMPUTATION (RUN ONCE) --------------------------------

        // precomputed, but Math.Pow() is only 50 nanosec/call
        // at -10 its small but significant 0.03, at -35 its negligible 0.000007
        private static double[] MathPow = Enumerable.Range(0, 35).Select(i => Math.Pow(1.4, -i)).ToArray();

        //------------------------------ FIELDS ---------------------------------------------------

        // data loaded from CFG, constants after loading
        [KSPField]
        public double generatedForce;
        [KSPField]
        public double chargeRate;
        [KSPField]
        public double chargeTime;
        [KSPField]
        public double chargeCapacity;

        // used for displaying current status, get recomputed on occasion, static because only one active vessel
        public static FTLDriveModule[] availableDrives;
        public static double totalGeneratedForce;
        public static double totalChargeRate;
        public static double totalChargeTime;
        public static double totalChargeCapacity;
        public static double successProbability;

        // stateful variables, cannot be recomputed from other variables
        public static bool isSpinning;
        public static double totalChargeStored;


        //------------------------------ PARTMODULE OVERRIDES -------------------------------------

        public override void OnStart(PartModule.StartState state)
        {
            SoundManager.LoadSound("FTLDriveContinued/Sounds/drive_sound", "DriveSound");
            driveSound = new FXGroup("DriveSound");
            SoundManager.CreateFXSound(part, driveSound, "DriveSound", true, 50f);

            animationStages = animationNames.Split(',').Select(a => a.Trim()).ToArray();
            SetUpAnimation(animationStages.First(), this.part, WrapMode.Loop);

            chargeCapacity = chargeRate * chargeTime;

            // NOTE: sufficient for restart while spinning?
            isSpinning = false;

            base.OnStart(state);
        }

        public override void OnUpdate()
        {
            // If no context menu is open, no point computing or displaying anything.
            if (!UIPartActionController.Instance.ItemListContains(part, false))
                return;

            try
            {
                availableDrives = FindAllSourceDrives.ToArray();

                totalGeneratedForce = availableDrives.Select(drv => drv.generatedForce).OrderByDescending(x => x).Take(35).Select((f, i) => f * MathPow[i]).Sum();
                totalChargeRate = availableDrives.Select(drv => drv.chargeRate).Sum();
                totalChargeCapacity = availableDrives.Select(drv => drv.chargeCapacity).Sum();
                // Total charge time is NOT the sum of individual charge rates, because different drives can have different charge times.
                totalChargeTime = totalChargeCapacity / totalChargeRate;

                if (HighLogic.LoadedSceneIsEditor)
                {
                    // NOTE: ActiveVessel is not used in Editor mode, making total force/ec uncomputable.
                }

                if (HighLogic.LoadedSceneIsFlight)
                {
                    if (Destination == null)
                    {
                        ClearLabels();
                        AppendLabel("Total generated force", String.Format("{0:N1}iN", totalGeneratedForce));
                        AppendLabel("Total required EC", String.Format("{0:N1}/s over {1:N1}s", totalChargeRate, totalChargeTime));
                        AppendLabel("Target vessel", "none selected");
                    }
                    // NOTE: Maybe code should check if vessel is dead (aka exploded) after being selected?
                    //else if (!DestinationExists)
                    // NOTE: Mode restrictions are OFF. I advise to NOT use these restrictions, there surely are people doing crazy FTL jumps for fun.
                    //else if (!DestinationHasMode)
                    //{
                    //    ClearLabels();
                    //    AppendLabel("Target vessel", "not flying in space");
                    //}
                    else if (!DestinationHasActiveBeacon)
                    {
                        ClearLabels();
                        AppendLabel("Total generated force", String.Format("{0:N1}iN", totalGeneratedForce));
                        AppendLabel("Total required EC", String.Format("{0:N1}/s over {1:N1}s", totalChargeRate, totalChargeTime));
                        AppendLabel("Target vessel", "has no active beacon");
                    }
                    else if (isSpinning)
                    {
                        // If subtraction is after addition, it prevents drives from reaching 100%.
                        // This causes a mild bug that effective charge time is about 110% of advertised. 
                        totalChargeStored -= totalChargeRate * Time.deltaTime * 0.1d;
                        totalChargeStored += part.RequestResource("ElectricCharge", totalChargeRate * Time.deltaTime);
                        totalChargeStored = Math.Min(totalChargeCapacity, Math.Max(0, totalChargeStored));
                        double currentForce = (totalChargeStored / totalChargeCapacity) * totalGeneratedForce;
                        double neededForce = (GravitationalForcesAll(Source.orbit) + GravitationalForcesAll(Destination.orbit)) * Source.totalMass * 1000;
                        // If source and destination are gravity-free like outside Sun SOI, success should be 1. Mild bug.
                        successProbability = Math.Min(1, Math.Max(0, currentForce / neededForce));

                        ClearLabels();
                        AppendLabel("Currently generated force", String.Format("{0:N1}iN", currentForce));
                        AppendLabel("Currently drained EC", String.Format("{0:N1}/s", totalChargeRate));
                        AppendLabel("Success probability", String.Format("{0:P0}", successProbability));
                    }
                    else
                    {
                        Func<string, string> trimthe = s => s.StartsWith("The") ? s.Substring(3).Trim() : s;
                        string targetBodyName = trimthe(Destination.orbit.referenceBody.name);
                        double targetAltitude = Destination.orbit.altitude;

                        double gravsource = GravitationalForcesAll(Source.orbit);
                        double gravsourceprim = gravsource - GravitationalForce(Source.orbit);
                        double gravdest = GravitationalForcesAll(Destination.orbit);
                        double mass = Source.totalMass * 1000;
                        double grav0 = Source.orbit.referenceBody.gravParameter;
                        double radius0 = Source.orbit.referenceBody.Radius;

                        double neededForce = (gravsource + gravdest) * mass;
                        successProbability = Math.Min(1, Math.Max(0, totalGeneratedForce / neededForce));

                        // Equations are shown on paper, image file in the repository.
                        double B = 2 * radius0;
                        double C = Square(radius0) - grav0 / ((totalGeneratedForce / mass) - gravdest - gravsourceprim);
                        double delta = B * B - 4 * C;
                        bool optimumExists = delta > 0;
                        double optimumAltitude = (-B + Math.Sqrt(delta)) / 2;
                        bool optimumBeyondSOI = optimumAltitude > Source.orbit.referenceBody.sphereOfInfluence;

                        ClearLabels();
                        AppendLabel("Total generated force", String.Format("{0:N1}iN", totalGeneratedForce));
                        AppendLabel("Total required EC", String.Format("{0:N1}/s over {1:N1}s", totalChargeRate, totalChargeTime));
                        AppendLabel("Target orbiting", String.Format("{0} at {1:N1}km", targetBodyName, targetAltitude / 1000));
                        AppendLabel("Required force", String.Format("{0:N1}iN", neededForce));
                        AppendLabel("Success probability", String.Format("{0:P0}", successProbability));
                        AppendLabel("Optimum altitude", String.Format((optimumExists ? ("{0:N1}km" + (optimumBeyondSOI ? " (beyond SOI)" : "")) : "none (insufficient drives?)"), optimumAltitude / 1000));
                    }
                }
            }
            catch (Exception ex)
            {
                LogsManager.ErrorLog(ex);
                ClearLabels();
                AppendLabel("ERROR IN COMPUTATION", "");
            }

            UpdateAnimations();
            base.OnUpdate();
        }

        //------------------------------ CORE METHODS ---------------------------------------------

        private Vessel Source
        {
            get => FlightGlobals.ActiveVessel;
        }
        private Vessel Destination
        {
            get => Source?.targetObject?.GetVessel();
        }

        private IEnumerable<FTLDriveModule> FindAllSourceDrives
        {
            get
            {
                foreach (Part p in Source.parts)
                    if (p.State != PartStates.DEAD)
                        foreach (PartModule pm in p.Modules)
                            if (pm.moduleName == "FTLDriveModule")
                                yield return (FTLDriveModule)pm;
            }
        }

        private double GravitationalForcesAll(Orbit orbit)
        {
            if (orbit == null) return 0d;
            return GravitationalForce(orbit) + GravitationalForcesAll(orbit.referenceBody.orbit);
        }

        private double GravitationalForce(Orbit orbit)
        {
            if (orbit == null) return 0d;
            return (orbit.referenceBody.gravParameter / Square(orbit.altitude + orbit.referenceBody.Radius));
        }

        private static double Square(double x)
        {
            return x * x;
        }

        private bool DestinationHasActiveBeacon
        {
            get
            {
                if (Destination == null)
                {
                    return false;
                }
                if (Destination.loaded)
                {
                    foreach (Part p in Destination.parts)
                        if (p.State != PartStates.DEAD)
                            foreach (PartModule pm in p.Modules)
                                if (pm.moduleName == "FTLBeaconModule")
                                    if (((FTLBeaconModule)pm).beaconActivated)
                                        return true;
                    return false;
                }
                else
                {
                    foreach (ProtoPartSnapshot pps in Destination.protoVessel.protoPartSnapshots)
                        foreach (ProtoPartModuleSnapshot m in pps.modules)
                            if (m.moduleName == "FTLBeaconModule")
                                if (Convert.ToBoolean(m.moduleValues.GetValue("beaconActivated")))
                                    return true;
                    return false;
                }
            }
        }

        private bool DestinationHasMode
        {
            get
            {
                return 
                    (Source.situation == Vessel.Situations.DOCKED ||
                    Source.situation == Vessel.Situations.SUB_ORBITAL ||
                    Source.situation == Vessel.Situations.ORBITING ||
                    Source.situation == Vessel.Situations.ESCAPING) &&
                    (Destination.situation == Vessel.Situations.DOCKED ||
                    Destination.situation == Vessel.Situations.SUB_ORBITAL ||
                    Destination.situation == Vessel.Situations.ORBITING ||
                    Destination.situation == Vessel.Situations.ESCAPING);
            }
        }

        [KSPEvent(guiActive = true, guiName = "Spin/Abort")]
        public void ToggleSpinning()
        {
            if (isSpinning)
            {
                isSpinning = false;
                LogsManager.DisplayMsg("Aborting FTL drives");
                ToggleAnimations();
            }
            else
            {
                if (DestinationHasActiveBeacon) //  && DestinationHasMode
                {
                    isSpinning = true;
                    totalChargeStored = 0d;
                    LogsManager.DisplayMsg("Spinning up FTL drives");
                    ToggleAnimations();
                }
            }
        }

        [KSPEvent(guiActive = true, guiName = "Execute Jump")]
        public void ExecuteJump()
        {
            if (isSpinning)
            {
                isSpinning = false;
                ToggleAnimations();
                System.Random rng = new System.Random();

                if (rng.NextDouble() < successProbability)
                {
                    Source.Rendezvous(Destination);
                    LogsManager.DisplayMsg("Jump successful!");
                }
                else
                {
                    Source.Explode();
                    LogsManager.DisplayMsg("Jump failed!");
                }
            }
        }

        public override string GetInfo()
        {
            var sb = new StringBuilder();
            sb.AppendFormat("Generated force: {0:N1}iN \n", generatedForce);
            sb.AppendFormat("Required EC: {0:N1}/s over {1:N1}s \n", chargeRate, chargeTime);
            sb.AppendFormat("\n");
            sb.Append("Navigational computer \n");
            sb.Append("- Required force \n");
            sb.Append("- Required electricity \n");
            sb.Append("- Success probability \n");
            sb.Append("- Optimum altitude \n");
            return sb.ToString();
        }


        //------------------------------ ANIMATION ------------------------------------------------

        [KSPField]
        public string animationNames;
        // animationRampspeed is how quickly it gets up to speed.  1 meaning it gets to full speed (as defined by 
        // the animSpeed and customAnimationSpeed) immediately, less than that will ramp up over time
        [KSPField]
        public float animationRampSpeed = 0.001f;
        [KSPField]
        public float customAnimationSpeed = 1f;

        private enum RampDirection { none, up, down };

        private RampDirection rampDirection = RampDirection.none;
        private float ramp = 0;
        private int animStage = 0;
        private string[] animationStages = { };

        private FXGroup driveSound;

        public void SetUpAnimation(string animationName, Part part, WrapMode wrapMode)
        {
            foreach (var animation in part.FindModelAnimators(animationName))
            {
                var animationState = animation[animationName];

                animationState.speed = 0;
                animationState.normalizedTime = 0f;
                animationState.enabled = true;
                animationState.wrapMode = wrapMode;
            };
        }

        public void ToggleAnimations()
        {
            foreach (FTLDriveModule dm in availableDrives)
            {
                if (isSpinning)
                {
                    dm.rampDirection = RampDirection.up;
                    dm.driveSound.audio.Play();
                }
                else
                {
                    dm.rampDirection = RampDirection.down;
                    dm.driveSound.audio.Stop();
                }
            }
        }

        public void UpdateAnimations()
        {
            if (!(HighLogic.LoadedSceneIsFlight || HighLogic.LoadedSceneIsEditor) || rampDirection == RampDirection.none)
                return;

            string activeAnim = animationStages[animStage];
            int curAnimStage = animStage;

            foreach (var anim in part.FindModelAnimators(activeAnim))
            {
                if (anim != null)
                {
                    float origSpd = anim[activeAnim].speed;
                    if (rampDirection == RampDirection.up)
                    {
                        if (ramp < 1f)
                        {
                            ramp += animationRampSpeed;
                        }
                        if (ramp > 1f)
                        {
                            ramp = 1f;
                            animStage++;
                        }
                    }
                    else if (rampDirection == RampDirection.down)
                    {
                        if (ramp > 0)
                        {
                            ramp -= animationRampSpeed;
                        }
                        if (ramp < 0)
                        {
                            ramp = 0f;
                            animStage--;
                        }
                    }

                    if (curAnimStage < animationStages.Length - 1)
                    {
                        anim[activeAnim].normalizedTime = ramp;
                    }
                    else
                    {
                        anim[activeAnim].speed = customAnimationSpeed * ramp;
                    }

                    if (ramp == 0)
                    {
                        if (animStage < 0)
                        {
                            animStage = 0;
                            rampDirection = RampDirection.none;
                        }
                        else
                        {
                            ramp = 1;
                        }
                    }
                    else if (ramp == 1)
                    {
                        if (animStage >= animationStages.Length)
                        {
                            animStage = animationStages.Length - 1;
                            rampDirection = RampDirection.none;
                        }
                        else
                        {
                            ramp = 0;
                        }
                    }

                    anim.Play(activeAnim);
                }
            }
        }



    }
}
