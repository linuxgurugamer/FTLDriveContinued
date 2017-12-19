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

        //------------------------------ FIELDS ---------------------------------------------------

        // data loaded from CFG, constants after loading
        [KSPField]
        public double generatedForce;
        [KSPField]
        public double chargeRate;
        [KSPField]
        public double chargeTime;

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
        public static bool isReady;

        // used for displaying GUI window
        public struct GuiElement
        {
            public string type;
            public string text;
            public string color;
            public Action clicked;
        }
        public static bool windowVisible;
        public static Rect windowPosition;
        public static List<GuiElement> windowContent;

        //------------------------------ PARTMODULE OVERRIDES -------------------------------------

        public override void OnStart(PartModule.StartState state)
        {
            SoundManager.LoadSound("FTLDriveContinued/Sounds/drive_sound", "DriveSound");
            driveSound = new FXGroup("DriveSound");
            SoundManager.CreateFXSound(part, driveSound, "DriveSound", true, 50f);

            animationStages = animationNames.Split(',').Select(a => a.Trim()).ToArray();
            SetUpAnimation(animationStages.First(), this.part, WrapMode.Loop);

            const int WIDTH = 250;
            const int HEIGHT = 250;
            windowPosition = new Rect((Screen.width - WIDTH) / 2, (Screen.height - HEIGHT) / 2, WIDTH, HEIGHT);
            windowContent = new List<GuiElement>();

            base.OnStart(state);
        }

        public override void OnLoad(ConfigNode node)
        {
            // NOTE: stops spinning and hides GUI on reload/switch vessel/etc
            isSpinning = false;
            windowVisible = false;

            availableDrives = FindAllSourceDrives.ToArray();

            totalGeneratedForce = availableDrives.Select(drv => drv.generatedForce).OrderByDescending(x => x).Take(25).Select((f,i) => f * Math.Pow(1.2, -i)).Sum();
            totalChargeRate = availableDrives.Select(drv => drv.chargeRate).Sum();
            totalChargeCapacity = availableDrives.Select(drv => drv.chargeRate * drv.chargeTime).Sum();
            // Total charge time is NOT the sum of individual charge rates, because different drives can have different charge times.
            totalChargeTime = totalChargeCapacity / totalChargeRate;

            base.OnLoad(node);
        }

        public override void OnUpdate()
        {
            UpdateAnimations();

            // If no context menu is open, no point computing or displaying anything.
            if (!(UIPartActionController.Instance.ItemListContains(part, false) || windowVisible))
                return;

            try
            {

                if (HighLogic.LoadedSceneIsEditor)
                {
                    // NOTE: ActiveVessel is not used in Editor mode, EditorLogic.ship doesnt work?
                }

                if (HighLogic.LoadedSceneIsFlight)
                {
                    Events["ToggleSpinning"].guiName = isSpinning ? "Abort" : "Spin";
                    Events["ToggleGUI"].guiName = windowVisible ? "Hide possible destinations" : "Show possible destinations";

                    if (isSpinning)
                    {
                        if (isReady)
                        {
                            part.RequestResource("ElectricCharge", 0.01 * totalChargeRate * (totalChargeStored / totalChargeCapacity) * Time.deltaTime);
                        }
                        else
                        {
                            totalChargeStored += part.RequestResource("ElectricCharge", totalChargeRate * Time.deltaTime);
                            totalChargeStored = Math.Min(totalChargeCapacity, Math.Max(0, totalChargeStored));
                        }

                        double currentForce = (totalChargeStored / totalChargeCapacity) * totalGeneratedForce;
                        double neededForce = (GravitationalForcesAll(Source.orbit) + GravitationalForcesAll(Destination.orbit)) * Source.totalMass * 1000;
                        // If source and destination are gravity-free like outside Sun SOI, success is 1.
                        successProbability = neededForce == 0 ? 1 : Math.Min(1, Math.Max(0, currentForce / neededForce));
                        double currentDrain = isReady ? 0.01 * totalChargeRate * (totalChargeStored / totalChargeCapacity) : totalChargeRate;

                        if (totalChargeStored >= totalChargeCapacity || currentForce > neededForce)
                        {
                            isReady = true;
                        }

                        ClearLabels();
                        AppendLabel("Currently generated force", String.Format("{0:N1}iN", currentForce));
                        AppendLabel("Currently drained EC", String.Format("{0:N1}/s", currentDrain));
                        AppendLabel("Success probability", String.Format("{0:N0}%", successProbability * 100));
                    }
                    else if (JumpPossible)
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
                        AppendLabel("Success probability", String.Format("{0:N0}%", successProbability * 100));
                        AppendLabel("Optimum altitude", String.Format((optimumExists ? ("{0:N1}km" + (optimumBeyondSOI ? " (beyond SOI)" : "")) : "none (insufficient drives?)"), optimumAltitude / 1000));
                    }
                    else
                    {
                        ClearLabels();
                        AppendLabel("Total generated force", String.Format("{0:N1}iN", totalGeneratedForce));
                        AppendLabel("Total required EC", String.Format("{0:N1}/s over {1:N1}s", totalChargeRate, totalChargeTime));
                        AppendLabel("Target vessel", "none or invalid");
                    }

                    if (windowVisible)
                    {
                        windowContent.Clear();

                        StringBuilder sb = new StringBuilder();
                        sb.AppendEx("Total generated force", String.Format("{0:N1}iN", totalGeneratedForce));
                        sb.AppendEx("Total required EC", String.Format("{0:N1}/s over {1:N1}s", totalChargeRate, totalChargeTime));
                        windowContent.Add(new GuiElement() { type = "button", text = sb.ToString(), color = null,
                            clicked = () => { FlightGlobals.fetch.SetVesselTarget(null); } });

                        foreach (Vessel vessel in FlightGlobals.Vessels)
                        {
                            if (vessel != Source && VesselHasActiveBeacon(vessel) && VesselInFlight(vessel))
                            {
                                Func<string, string> trimthe = s => s.StartsWith("The") ? s.Substring(3).Trim() : s;
                                string targetBodyName = trimthe(vessel.orbit.referenceBody.name);
                                double targetAltitude = vessel.orbit.altitude;

                                double gravsource = GravitationalForcesAll(Source.orbit);
                                double gravsourceprim = gravsource - GravitationalForce(Source.orbit);
                                double gravdest = GravitationalForcesAll(vessel.orbit);
                                double mass = Source.totalMass * 1000;
                                double grav0 = Source.orbit.referenceBody.gravParameter;
                                double radius0 = Source.orbit.referenceBody.Radius;

                                double neededForce = (gravsource + gravdest) * mass;
                                successProbability = Math.Min(1, Math.Max(0, totalGeneratedForce / neededForce));

                                // Equations are shown on paper, image file in the repository.
                                double B = 2 * radius0;
                                double C = (radius0 * radius0) - grav0 / ((totalGeneratedForce / mass) - gravdest - gravsourceprim);
                                double delta = B * B - 4 * C;
                                bool optimumExists = delta > 0;
                                double optimumAltitude = (-B + Math.Sqrt(delta)) / 2;
                                bool optimumBeyondSOI = optimumAltitude > Source.orbit.referenceBody.sphereOfInfluence;

                                sb = new StringBuilder();
                                sb.AppendEx("A vessel orbiting", String.Format("{0} at {1:N1}km", targetBodyName, targetAltitude / 1000));
                                sb.AppendEx("Required force", String.Format("{0:N1}iN", neededForce));
                                sb.AppendEx("Success probability", String.Format("{0:N0}%", successProbability * 100));
                                sb.AppendEx("Optimum altitude", String.Format((optimumExists ? ("{0:N1}km" + (optimumBeyondSOI ? " (beyond SOI)" : "")) : "none (insufficient drives?)"), optimumAltitude / 1000));
                                sb.AppendEx(vessel == Destination ? @"     \ - - - - - - Selected target - - - - - - /     " : @"     \ - - - - - - Click to select - - - - - - /     ");
                                windowContent.Add(new GuiElement() { type = "button", text = sb.ToString(), color = (vessel == Destination ? "green" : null),
                                    clicked = () => { FlightGlobals.fetch.SetVesselTarget(vessel); } });
                            }
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                LogsManager.ErrorLog(ex);
                ClearLabels();
                AppendLabel("ERROR IN COMPUTATION", "");
            }

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

        private bool JumpPossible
        {
            get => Destination != null && Destination != Source && VesselHasActiveBeacon(Destination) && VesselInFlight(Source) && VesselInFlight(Destination);
        }

        private bool VesselHasActiveBeacon(Vessel vessel)
        {
            if (vessel.loaded)
            {
                foreach (Part p in vessel.parts)
                    if (p.State != PartStates.DEAD)
                        foreach (PartModule pm in p.Modules)
                            if (pm.moduleName == "FTLBeaconModule")
                                if (((FTLBeaconModule)pm).beaconActivated)
                                    return true;
                return false;
            }
            else
            {
                foreach (ProtoPartSnapshot pps in vessel.protoVessel.protoPartSnapshots)
                    foreach (ProtoPartModuleSnapshot m in pps.modules)
                        if (m.moduleName == "FTLBeaconModule")
                            if (Convert.ToBoolean(m.moduleValues.GetValue("beaconActivated")))
                                return true;
                return false;
            }
        }

        private bool VesselInFlight(Vessel vessel)
        {
            return
                vessel.situation == Vessel.Situations.FLYING ||
                vessel.situation == Vessel.Situations.SUB_ORBITAL ||
                vessel.situation == Vessel.Situations.ORBITING ||
                vessel.situation == Vessel.Situations.ESCAPING ||
                vessel.situation == Vessel.Situations.DOCKED;
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
                if (JumpPossible)
                {
                    isSpinning = true;
                    totalChargeStored = 0d;
                    isReady = false;
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

        [KSPEvent(guiActive = true, guiName = "Show/Hide possible destinations")]
        public void ToggleGUI()
        {
            windowVisible = !windowVisible;
        }

        void OnGUI()
        {
            if (windowVisible)
            {
                windowPosition = GUILayout.Window(523429, windowPosition, Display, "FTL possible destinations");
            }
        }

        void Display(int windowId)
        {
            GUIStyle normal = new GUIStyle(GUI.skin.textField);
            normal.normal.textColor = normal.hover.textColor = GUI.skin.textField.normal.textColor;
            GUIStyle yellow = new GUIStyle(GUI.skin.textField);
            yellow.normal.textColor = yellow.hover.textColor = Color.yellow;
            GUIStyle green = new GUIStyle(GUI.skin.textField);
            green.normal.textColor = green.hover.textColor = Color.green;

            foreach (GuiElement e in windowContent)
            {
                GUILayout.BeginHorizontal();
                if (e.type == "text")
                {
                    GUIStyle s = e.color == "yellow" ? yellow : e.color == "green" ? green : normal;
                    GUILayout.TextField(e.text, s);
                }
                if (e.type == "space")
                {
                    GUILayout.FlexibleSpace();
                }
                if (e.type == "button")
                {
                    GUIStyle s = e.color == "yellow" ? yellow : e.color == "green" ? green : normal;
                    if (GUILayout.Button(e.text, s))
                        e.clicked.Invoke();
                }
                GUILayout.EndHorizontal();
            }
            GUI.DragWindow();
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
