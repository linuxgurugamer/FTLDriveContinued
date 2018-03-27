using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

using ClickThroughFix;

namespace ScienceFoundry.FTL
{
    [KSPModule("FTL Drive")]
    public class FTLDriveModule : DynamicDisplay
    {
        //------------------------------ PRECOMPUTATION (RUN ONCE) --------------------------------
        const int MaxStackableDrives = 25;

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

        public static bool windowVisible = false;
        public static Rect windowPosition;
        public static List<GuiElement> windowContent;

        static CelestialBody testBody = null;
        static int testBodyIdx = -1;
        static float testAltitude = 70;
        static float minTestAltitude = 70;
        static float maxTestAltitude = 50000;
        //------------------------------ PARTMODULE OVERRIDES -------------------------------------

        void CalculateValues(bool inFlight = true)
        {
            availableDrives = FindAllSourceDrives(inFlight ? FlightGlobals.ActiveVessel.Parts : EditorLogic.fetch.ship.Parts).ToArray();

            double exponent = HighLogic.CurrentGame.Parameters.CustomParams<FTLSettings>().multipleDriveExponent;
            totalGeneratedForce = availableDrives.Select(drv => drv.generatedForce).OrderByDescending(x => x).Take(MaxStackableDrives).Select((f, i) => f * Math.Pow(exponent, -i)).Sum();
            totalChargeRate = availableDrives.Select(drv => drv.chargeRate).Sum();
            totalChargeCapacity = availableDrives.Select(drv => drv.chargeRate * drv.chargeTime).Sum();
            // Total charge time is NOT the sum of individual charge rates, because different drives can have different charge times.
            totalChargeTime = totalChargeCapacity / totalChargeRate;
        }

        void CalcTestAltitudes(CelestialBody body)
        {
            testBody = body;
            testAltitude = (float)body.atmosphereDepth / 1000 + 1;
            minTestAltitude = (float)body.atmosphereDepth / 1000;
            maxTestAltitude = (float)(body.sphereOfInfluence - body.Radius) / 1000;

            // Suggestion: could use LINQ and foreach
            if (testBodyIdx == -1)
                for (int i = 0; i < bodiesList.Count; i++)
                {
                    if (bodiesList[i].body.name == body.name)
                    {
                        testBodyIdx = i;
                        break;
                    }
                }
        }

        // This is needed because FlightGlobals.CelestialBody.referencebody is always the Sun in the editor
        public class BodyRef
        {
            public CelestialBody parent;
            public CelestialBody body;
            public double semiMajorAxis;

            public BodyRef(CelestialBody parent, CelestialBody body)
            {
                this.parent = parent;
                this.body = body;
                foreach (OrbitDriver ob in Planetarium.Orbits)
                {
                    if (ob.celestialBody.name == body.name)
                    {
                        semiMajorAxis = ob.orbit.semiMajorAxis;
                    }
                }
            }
        }

        static List<BodyRef> bodiesList = null;

        void AddBodiesList(CelestialBody parent, CelestialBody body)
        {
            BodyRef br = new BodyRef(parent, body);

            bodiesList.Add(br);
            foreach (var b in body.orbitingBodies)
                AddBodiesList(body, b);
        }

        void InitBodiesList()
        {
            bodiesList = new List<BodyRef>();
            AddBodiesList(null, Planetarium.fetch.Sun);
        }

        void Start()
        {
            if (bodiesList == null)
                InitBodiesList();

            if (testBody == null)
                CalcTestAltitudes(FlightGlobals.GetHomeBody());

            SoundManager.LoadSound("FTLDriveContinued/Sounds/drive_sound", "DriveSound");
            driveSound = new FXGroup("DriveSound");
            SoundManager.CreateFXSound(part, driveSound, "DriveSound", true, 50f);

            animationStages = animationNames.Split(',').Select(a => a.Trim()).ToArray();
            SetUpAnimation(animationStages.First(), this.part, WrapMode.Loop);

            const int WIDTH = 250;
            const int HEIGHT = 250;
            windowPosition = new Rect((Screen.width - WIDTH) / 2, (Screen.height - HEIGHT) / 2, WIDTH, HEIGHT);
            windowContent = new List<GuiElement>();

            CalculateValues();
            GameEvents.onVesselWasModified.Add(onVesselWasModified);
            Events["ExecuteJump"].active = false;
            windowVisible = false;
        }

        void onVesselWasModified(Vessel v)
        {
            CalculateValues();
        }

        void Destory()
        {
            GameEvents.onVesselWasModified.Remove(onVesselWasModified);
        }

        public override void OnLoad(ConfigNode node)
        {
            if (!HighLogic.LoadedSceneIsFlight && !HighLogic.LoadedSceneIsEditor)
                return;
            // NOTE: stops spinning and hides GUI on reload/switch vessel/etc
            if (HighLogic.CurrentGame.Parameters.CustomParams<FTLSettings>().driveStopsUponVesselSwitch)
                isSpinning = false;

            windowVisible = false;

            base.OnLoad(node);
        }

        void Update()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                ClearLabels();
                AppendLabel("Generated force", String.Format("{0:N1} iN", generatedForce));
                AppendLabel("Required EC", String.Format("{0:N1} ec/sec over {1:N1} sec", chargeRate, chargeTime));

                if (windowVisible)
                {
                    windowContent.Clear();

                    CalculateValues(false);

                    StringBuilder sb = new StringBuilder();
                    sb.AppendEx("Total generated force", String.Format("{0:N1} iN", totalGeneratedForce));
                    sb.AppendEx("Total required EC", String.Format("{0:N1} ec/s over {1:N1} sec", totalChargeRate, totalChargeTime));

                    windowContent.Add(new GuiElement()
                    {
                        type = "leftright",
                        text = String.Format("Test body: {0}", testBody.name),
                        color = null,
                    });
                    windowContent.Add(new GuiElement()
                    {
                        type = "slider",
                        text = String.Format("Test altitude: {0:N1} km", testAltitude),
                        color = null,
                    });

                    sb = new StringBuilder();
                    sb.AppendEx("Atmospheric depth", String.Format("{0:N1} km", minTestAltitude));
                    //sb.AppendEx(testBody.name + " SOI: " + (testBody.sphereOfInfluence / 1000).ToString("n1") + " km");
                    sb.AppendEx(testBody.name + " max orbit", String.Format("{0:N1} km", maxTestAltitude));
                    sb.AppendEx("Ship mass", String.Format("{0:N1} kg", EditorLogic.fetch.ship.GetTotalMass() * 1000));
                    sb.AppendEx("Total generated force", String.Format("{0:N1} iN", totalGeneratedForce));

                    windowContent.Add(new GuiElement()
                    {
                        type = "text",
                        text = sb.ToString(),
                        color = null,
                    });

                    for (int i = 0; i < HighLogic.CurrentGame.flightState.protoVessels.Count(); i++)
                    {
                        ProtoVessel vessel = HighLogic.CurrentGame.flightState.protoVessels[i];
                    //}
                    //foreach (ProtoVessel vessel in HighLogic.CurrentGame.flightState.protoVessels)
                    //{
                        Vessel v = new Vessel();
                        v.loaded = false;
                        v.protoVessel = vessel;
                        if (VesselHasActiveBeacon(v))
                        {

                            Func<string, string> trimthe = s => s.StartsWith("The") ? s.Substring(3).Trim() : s;
                            var referenceBody = FlightGlobals.Bodies[vessel.orbitSnapShot.ReferenceBodyIndex];

                            string targetBodyName = trimthe(referenceBody.name);
                            double targetAltitude = vessel.orbitSnapShot.semiMajorAxis;

                            double sourceOrbit = testAltitude * 1000;
                            double gravsource = GravitationalForcesAll(testBody, sourceOrbit);
                            double gravsourceprim = gravsource - GravitationalForce(testBody, sourceOrbit);

                            double destOrbit = vessel.orbitSnapShot.semiMajorAxis;
                            double gravdest = GravitationalForcesAll(FlightGlobals.Bodies[vessel.orbitSnapShot.ReferenceBodyIndex], destOrbit);

                            double mass = EditorLogic.fetch.ship.GetTotalMass() * 1000;
                            double grav0 = testBody.gravParameter;
                            double radius0 = testBody.Radius;

                            double neededForce = (gravsource + gravdest) * mass;
                            successProbability = Math.Min(1, Math.Max(0, totalGeneratedForce / neededForce));

                            // Equations are shown on paper, image file in the repository.
                            double B = 2 * radius0;
                            double C = Square(radius0) - grav0 / ((totalGeneratedForce / mass) - gravdest - gravsourceprim);
                            double delta = B * B - 4 * C;
                            bool optimumExists = delta > 0;
                            double optimumAltitude = (-B + Math.Sqrt(delta)) / 2;
                            bool optimumBeyondSOI = optimumAltitude > testBody.sphereOfInfluence;

                            sb = new StringBuilder();
                            sb.AppendEx("A vessel orbiting", String.Format("{0} at {1:N1} km", targetBodyName, targetAltitude / 1000));
                            sb.AppendEx("Required force", String.Format("{0:N1} iN", neededForce));
                            //sb.AppendEx("Total required EC", String.Format("{0:N1}/s over {1:N1}s", totalChargeRate, totalChargeTime));
                            sb.AppendEx("Success probability", String.Format("{0:N0} %", successProbability * 100));
                            sb.AppendEx("Optimum altitude", String.Format((optimumExists ? ("{0:N1} km" + (optimumBeyondSOI ? " (beyond SOI)" : "")) : "none (insufficient drives?)"), optimumAltitude / 1000));

                            windowContent.Add(new GuiElement()
                            {
                                type = "text",
                                text = sb.ToString(),
                                color = null,
                            });
                        }
                    }

                }
            }
            else
            {
                if (doJump)
                {
                    ExecuteJump();
                    doJump = false;
                }
            }

        }

        bool doJump = false;

        public void FixedUpdate()
        {
            UpdateAnimations();

            // If no context menu is open, no point computing or displaying anything.
            if (!(UIPartActionController.Instance.ItemListContains(part, false) || windowVisible))
                return;

            try
            {
                // FixedUpdate not called when in Editor


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
                        AppendLabel("Currently generated force", String.Format("{0:N1} iN", currentForce));
                        AppendLabel("Currently drained EC", String.Format("{0:N1} ec/sec", currentDrain));
                        if (VesselInFlight(Source) && VesselInFlight(Destination) && Source != Destination && Destination != null && VesselHasActiveBeacon(Destination))
                            AppendLabel("Success probability", String.Format("{0:N0} %", successProbability * 100));
                        if (currentForce >= neededForce && successProbability >= 1.0f && HighLogic.CurrentGame.Parameters.CustomParams<FTLSettings>().autoJump)
                            doJump = true;
                    }
                    else if (JumpPossible(false))
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
                        AppendLabel("Total generated force", String.Format("{0:N1} iN", totalGeneratedForce));
                        AppendLabel("Total required EC", String.Format("{0:N1} ec/sec over {1:N1} sec", totalChargeRate, totalChargeTime));
                        AppendLabel("Target orbiting", String.Format("{0} at {1:N1} km", targetBodyName, targetAltitude / 1000));
                        AppendLabel("Required force", String.Format("{0:N1} iN", neededForce));
                        if (VesselInFlight(Source) && VesselInFlight(Destination) && Source != Destination && Destination != null && VesselHasActiveBeacon(Destination))
                            AppendLabel("Success probability", String.Format("{0:N0} %", successProbability * 100));
                        AppendLabel("Optimum altitude", String.Format((optimumExists ? ("{0:N1} km" + (optimumBeyondSOI ? " (beyond SOI)" : "")) : "none (insufficient drives?)"), optimumAltitude / 1000));
                    }
                    else
                    {
                        ClearLabels();
                        AppendLabel("Total generated force", String.Format("{0:N1} iN", totalGeneratedForce));
                        AppendLabel("Total required EC", String.Format("{0:N1} ec/sec over {1:N1} sec", totalChargeRate, totalChargeTime));
                        AppendLabel("Target vessel", "none or invalid");
                    }

                    if (windowVisible)
                    {
                        windowContent.Clear();

                        StringBuilder sb = new StringBuilder();
                        sb.AppendEx("Total generated force", String.Format("{0:N1} iN", totalGeneratedForce));
                        sb.AppendEx("Total required EC", String.Format("{0:N1} ec/sec over {1:N1} sec", totalChargeRate, totalChargeTime));
                        sb.AppendEx("Ship mass", String.Format("{0:N1} kg", Source.totalMass * 1000));

                        windowContent.Add(new GuiElement()
                        {
                            type = "button",
                            text = sb.ToString(),
                            color = null,
                            clicked = () => { FlightGlobals.fetch.SetVesselTarget(null); },
                        });

                        for (int i = 0; i < FlightGlobals.Vessels.Count; i++)
                        {
                            Vessel vessel = FlightGlobals.Vessels[i];
                        //}
                        //foreach (Vessel vessel in FlightGlobals.Vessels)
                        //{
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
                                sb.AppendEx("A vessel orbiting", String.Format("{0} at {1:N1} km", targetBodyName, targetAltitude / 1000));
                                sb.AppendEx("Required force", String.Format("{0:N1} iN", neededForce));
                                if (VesselInFlight(Source) && VesselInFlight(Destination) && Source != Destination && Destination != null && VesselHasActiveBeacon(Destination))
                                    sb.AppendEx("Success probability", String.Format("{0:N0} %", successProbability * 100));
                                sb.AppendEx("Optimum altitude", String.Format((optimumExists ? ("{0:N1} km" + (optimumBeyondSOI ? " (beyond SOI)" : "")) : "none (insufficient drives?)"), optimumAltitude / 1000));
                                sb.AppendEx(vessel == Destination ? @"     \ - - - - - - Selected target - - - - - - /     " : @"     \ - - - - - - Click to select - - - - - - /     ");

                                windowContent.Add(new GuiElement()
                                {
                                    type = "button",
                                    text = sb.ToString(),
                                    color = (vessel == Destination ? "green" : null),
                                    clicked = () => { FlightGlobals.fetch.SetVesselTarget(vessel); },
                                });
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

        private IEnumerable<FTLDriveModule> FindAllSourceDrives(List<Part> parts)
        {
            for (int i = 0; i < parts.Count(); i++)
            {
                Part p = parts[i];
           
                //foreach (Part p in parts)
                if (p.State != PartStates.DEAD)
                    for (int i2 = 0; i2 < p.Modules.Count; i2++)
                    {
                        PartModule pm = p.Modules[i2];

                        //foreach (PartModule pm in p.Modules)
                        if (pm.moduleName == "FTLDriveModule")
                            yield return (FTLDriveModule)pm;
                    }
            }
        }

        // following used in flight

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

        // Following used in Editor

        private double GravitationalForcesAll(CelestialBody body, double altitude)
        {
            if (body == null) return 0;
            double amt = 0;
            for (int i = 0; i < bodiesList.Count; i++)
            {
                FTLDriveModule.BodyRef br = bodiesList[i];
            //}
            //foreach (var br in bodiesList)
            //{
                if (body.name == br.body.name)
                {
                    amt = GravitationalForcesAll(br.parent, br.semiMajorAxis);
                    break;
                }
            }
            return GravitationalForce(body, altitude) + amt;
        }

        private double GravitationalForce(CelestialBody body, double altitude)
        {
            return (body.gravParameter / Square(altitude + body.Radius));
        }

        private static double Square(double x)
        {
            return x * x;
        }

        void DisplayJumpPossibleMsg(string str, bool display)
        {
            if (display)
                ScreenMessages.PostScreenMessage(str, 4f, ScreenMessageStyle.UPPER_CENTER);
            Debug.Log(str);
        }

        private bool VesselInFlight(Vessel vessel)
        {
            // This is the same thing as below, but is slightly faster, 3 instead of 5, also, if it		
            // fails one of the first, it doesn't check the following		
            return
                Source.situation != Vessel.Situations.LANDED &&
                Source.situation != Vessel.Situations.SPLASHED &&
                Source.situation != Vessel.Situations.PRELAUNCH;
#if false
            return
                vessel.situation == Vessel.Situations.FLYING ||
                vessel.situation == Vessel.Situations.SUB_ORBITAL ||
                vessel.situation == Vessel.Situations.ORBITING ||
                vessel.situation == Vessel.Situations.ESCAPING ||
                vessel.situation == Vessel.Situations.DOCKED;
#endif
        }

        private bool JumpPossible(bool display = true)
        {
            if (Source.situation == Vessel.Situations.LANDED ||
                 Source.situation == Vessel.Situations.SPLASHED ||
                 Source.situation == Vessel.Situations.PRELAUNCH)
            {
                DisplayJumpPossibleMsg("Jump not possible, landed vessel cannot jump", display);
                return false;
            }

            if (Destination == null)
            {
                DisplayJumpPossibleMsg("Jump not possible, no destination selected", display);
                return false;
            }
            if (Destination.situation == Vessel.Situations.LANDED ||
                Destination.situation == Vessel.Situations.SPLASHED ||
                Destination.situation == Vessel.Situations.PRELAUNCH)
            {
                DisplayJumpPossibleMsg("Jump not possible, destination cannot be landed", display);
                return false;
            }

            if (Destination == Source)
            {
                DisplayJumpPossibleMsg("Jump not possible, source and destination cannot be the same", display);
                return false;
            }
            if (!VesselHasActiveBeacon(Destination))
            {
                DisplayJumpPossibleMsg("Jump not possible, destination must have an active beacon", display);
                return false;
            }
            return true;
        }

        private bool VesselHasActiveBeacon(Vessel vessel)
        {
            if (vessel.loaded)
            {
                for (int i = 0; i < vessel.parts.Count(); i++)
                {
                    Part p = vessel.parts[i];

                    //foreach (Part p in vessel.parts)
                    if (p.State != PartStates.DEAD)
                        for (int i2 = 0; i2 < p.Modules.Count; i2++)
                        {
                            PartModule pm = p.Modules[i2];

                            //foreach (PartModule pm in p.Modules)
                                if (pm.moduleName == "FTLBeaconModule")
                                    if (((FTLBeaconModule)pm).beaconActivated)
                                        return true;
                        }
                }
                return false;
            }
            else
            {
                for (int i = 0; i < vessel.protoVessel.protoPartSnapshots.Count(); i++)
                {
                    ProtoPartSnapshot pps = vessel.protoVessel.protoPartSnapshots[i];

                    //foreach (ProtoPartSnapshot pps in vessel.protoVessel.protoPartSnapshots)
                    for (int i2 = 0; i2 < pps.modules.Count(); i2++)
                    {
                        ProtoPartModuleSnapshot m = pps.modules[i2];

                        //foreach (ProtoPartModuleSnapshot m in pps.modules)
                        if (m.moduleName == "FTLBeaconModule")
                            if (Convert.ToBoolean(m.moduleValues.GetValue("beaconActivated")))
                                return true;
                    }
                }
                return false;
            }
        }

        [KSPEvent(guiActive = true, guiName = "Spin/Abort")]
        public void ToggleSpinning()
        {
            if (isSpinning)
            {
                Events["ExecuteJump"].active = false;
                isSpinning = false;
                LogsManager.DisplayMsg("Aborting FTL drives");
                ToggleAnimations();
            }
            else
            {
                if (JumpPossible())
                {
                    Events["ExecuteJump"].active = true;
                    isSpinning = true;
                    totalChargeStored = 0d;
                    isReady = false;
                    LogsManager.DisplayMsg("Spinning up FTL drives");
                    ToggleAnimations();
                    doJump = false;
                }
            }
        }

        [KSPEvent(guiActive = true, guiName = "Execute Jump")]
        public void ExecuteJump()
        {
            if (isSpinning)
            {
                Events["ExecuteJump"].active = false;
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

        GameScenes lastSceneLoaded;

        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Show/Hide possible destinations")]
        public void ToggleGUI()
        {
            windowVisible = !windowVisible;
            if (windowVisible)
            {
                if (HighLogic.LoadedSceneIsEditor && SaveLoad.editorOK)
                {
                    lastSceneLoaded = GameScenes.EDITOR;
                    windowPosition.x = SaveLoad.editor.x;
                    windowPosition.y = SaveLoad.editor.y;
                }
                if (HighLogic.LoadedSceneIsFlight && SaveLoad.flightOK)
                {
                    lastSceneLoaded = GameScenes.FLIGHT;
                    windowPosition.x = SaveLoad.flight.x;
                    windowPosition.y = SaveLoad.flight.y;
                }
            }
        }

        void OnGUI()
        {
            if (windowVisible)
            {
                windowPosition = ClickThruBlocker.GUILayoutWindow(523429, windowPosition, Display, "FTL possible destinations");
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

            Vector2 buttonSize = new Vector2(25f, 20f);
            if (GUI.Button(new Rect(windowPosition.width - 23f, 2f, 18f, 13f), "x"))
            {
                windowVisible = false;
            }

            for (int i = 0; i < windowContent.Count(); i++)
            {
                GuiElement e = windowContent[i];
            //}
            //foreach (GuiElement e in windowContent)
            //{
                GUILayout.BeginHorizontal();
                if (e.type == "text")
                {
                    GUIStyle s = e.color == "yellow" ? yellow : e.color == "green" ? green : normal;
                    GUILayout.TextField(e.text, s);
                }
                if (e.type == "leftright")
                {
                    if (GUILayout.Button("<-", GUILayout.Width(30)))
                    {
                        testBodyIdx--;
                        if (testBodyIdx < 0)
                            testBodyIdx = bodiesList.Count() - 1;

                        CalcTestAltitudes(bodiesList[testBodyIdx].body);

                    }
                    GUIStyle s = e.color == "yellow" ? yellow : e.color == "green" ? green : normal;
                    GUILayout.Label(e.text, s);
                    if (GUILayout.Button("->", GUILayout.Width(30)))
                    {
                        testBodyIdx++;
                        if (testBodyIdx >= bodiesList.Count())
                            testBodyIdx = 0;

                        CalcTestAltitudes(bodiesList[testBodyIdx].body);
                    }

                }
                if (e.type == "slider")
                {
                    GUIStyle s = e.color == "yellow" ? yellow : e.color == "green" ? green : normal;
                    GUILayout.Label(e.text, s);
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    testAltitude = GUILayout.HorizontalSlider(testAltitude, minTestAltitude, maxTestAltitude);
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
            // Need to have 2 IF's here because leaving a scene while the window is visible leads to unpredictible results
            // Also, when leaving the editor to flight while the window is visible actually gets this called 2x in flight scene before i
            // it closes
            if (lastSceneLoaded == GameScenes.EDITOR)
                SaveLoad.SaveEditorPos(windowPosition);
            if (lastSceneLoaded == GameScenes.FLIGHT)
                SaveLoad.SaveFlightPos(windowPosition);
        }

        public override string GetInfo()
        {
            var sb = new StringBuilder();
            sb.AppendFormat("Generated force: {0:N1} iN \n", generatedForce);
            sb.AppendFormat("Required EC: {0:N1} ec/sec over {1:N1} sec \n", chargeRate, chargeTime);
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

            List<Animation> mas = part.FindModelAnimators(activeAnim).ToList();
            for (int i = 0; i < mas.Count(); i++)
            {
                Animation anim = mas[i];
            //}
            //foreach (var anim in part.FindModelAnimators(activeAnim))
            //{
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
