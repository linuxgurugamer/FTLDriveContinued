using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using System.Collections;
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
        [KSPField]
        public string jumpResourceDef = "";

        // used for displaying current status, get recomputed on occasion, static because only one active vessel
        public static FTLDriveModule[] availableDrives;
        public static double totalGeneratedForce;
        public static double totalChargeRate;
        public static double totalChargeTime;
        public static double totalChargeCapacity;
        // public static double successProbability;

        public static double totalResourceAmtNeeded;

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
        public static List<GuiElement> windowContent = new List<GuiElement>();

        static CelestialBody testBody = null;
        static int testBodyIdx = -1;
        static float testAltitude = 70;
        static float minTestAltitude = 70;
        static float maxTestAltitude = 50000;

        // For resource usage
        double resourceAmtAvailable = 0;
        double maxAmount = 0;
        public string jumpResource = "";
        public double jumpResourceECmultiplier = 0;

        int resourceID = -1;



        const int WIDTH = 250;
        const int HEIGHT = 250;
        const int KSP_SKIN_WIDTH = 320;

        bool hidden = false;

        //------------------------------ PARTMODULE OVERRIDES -------------------------------------
        void CalculateValues(bool inFlight = true)
        {
            LogsManager.Info("CalculateValues, inFlight: " + inFlight);
            if (!inFlight && (EditorLogic.fetch == null || EditorLogic.fetch.ship == null || EditorLogic.fetch.ship.Parts == null))
                return;
            if (inFlight && FlightGlobals.ActiveVessel == null)
                return;

            availableDrives = FindAllSourceDrives(inFlight ? FlightGlobals.ActiveVessel.Parts : EditorLogic.fetch.ship.Parts).ToArray();

            double exponent = HighLogic.CurrentGame.Parameters.CustomParams<FTLSettings>().multipleDriveExponent;
            totalGeneratedForce = availableDrives.Select(drv => drv.generatedForce).OrderByDescending(x => x).Take(MaxStackableDrives).Select((f, i) => f * Math.Pow(exponent, -i)).Sum();
            totalChargeRate = availableDrives.Select(drv => drv.chargeRate).Sum();
            totalChargeCapacity = availableDrives.Select(drv => drv.chargeRate * drv.chargeTime).Sum();

            // Total charge time is NOT the sum of individual charge rates, because different drives can have different charge times.
            totalChargeTime = totalChargeCapacity / totalChargeRate;

            if (resourceID != -1)
                totalResourceAmtNeeded = jumpResourceECmultiplier * totalChargeRate * totalChargeTime;
        }

        double _neededResourceAmt = 0;
        Guid targetVesselID;

        double neededResourceAmt(Guid targetedVesselID, double neededForce = 0)
        {
            LogsManager.Info("neededResourceAmt, targetVesselID: " + targetVesselID);
            if (neededForce == 0 && targetVesselID.Equals(targetedVesselID))
            {
                LogsManager.Info("target id: " + FlightGlobals.fetch.VesselTarget.GetVessel().protoVessel.vesselID + ",  _neededResourceAmt: " + _neededResourceAmt);
                return _neededResourceAmt;
            }
            double forceChargeRate = totalChargeRate * Time.deltaTime / totalChargeCapacity * totalGeneratedForce;
            double neededChargeTime = neededForce / forceChargeRate * Time.deltaTime;
            var a_neededResourceAmt = jumpResourceECmultiplier * Math.Min(neededChargeTime, chargeTime) * totalChargeRate;
            if (targetVesselID.Equals(targetedVesselID) && neededForce > 0)
            {
                LogsManager.Info("Setting _neededResourceAmt for: " + targetVesselID + ",  amt: " + a_neededResourceAmt);
                _neededResourceAmt = a_neededResourceAmt;
            }
            LogsManager.Info("targetedVesselID: " + targetedVesselID + ",   a_neededResourceAmt: " + a_neededResourceAmt);
            return a_neededResourceAmt;
        }

        void CalcTestAltitudes(CelestialBody body)
        {
            testBody = body;
            testAltitude = (float)body.atmosphereDepth / 1000 + 1;
            minTestAltitude = (float)body.atmosphereDepth / 1000;
            maxTestAltitude = (float)(body.sphereOfInfluence - body.Radius) / 1000;

            // Suggestion: could use LINQ and foreach
            if (testBodyIdx == -1)
                for (int i = bodiesList.Count - 1; i >= 0; i--)
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
                    if (ob.celestialBody == null)
                        continue;

                    if (ob.celestialBody.name == body.name)
                    {
                        semiMajorAxis = ob.orbit.semiMajorAxis;
                        break;
                    }
                }
            }
        }

        static List<BodyRef> bodiesList = null;

        void AddBodiesList(CelestialBody parent, CelestialBody body)
        {
            if (body == null)
                return;

            BodyRef br = new BodyRef(parent, body);

            bodiesList.Add(br);
            foreach (var b in body.orbitingBodies)
            {
                AddBodiesList(body, b);
            }
        }

        void InitBodiesList()
        {
            bodiesList = new List<BodyRef>();
            AddBodiesList(null, Planetarium.fetch.Sun);
        }

        static Dictionary<string, TargetSuccess> targetSuccessList = new Dictionary<string, TargetSuccess>();
        /// <summary>
        /// To be used in a CoRoutine to calculate the values periodically
        /// </summary>
        public class TargetSuccess
        {
            public string vesselName;
            public string targetBodyName;
            public double targetAltitude;
            public double gravsource;
            public double gravsourceprim;
            public double gravdest;
            public double mass;
            public double grav0;
            public double radius0;
            public double neededForce;
            public double successProbability;
            public bool optimumBeyondSOI;
            public double optimumAltitude;
            public bool optimumExists;

            double chargeTime;

            double jumpResourceECmultiplier;

            public double neededResourceAmt;

            Func<string, string> trimthe = s => s.StartsWith("The") ? s.Substring(3).Trim() : s;

            double NeededResourceAmt(Guid targetedVesselID, double neededForce)
            {
                double forceChargeRate = totalChargeRate * Time.deltaTime / totalChargeCapacity * totalGeneratedForce;
                double neededChargeTime = neededForce / forceChargeRate * Time.deltaTime;
                var neededResourceAmt = jumpResourceECmultiplier * Math.Min(neededChargeTime, chargeTime) * totalChargeRate;

                LogsManager.Info("targetedVesselID: " + targetedVesselID + ",   a_neededResourceAmt: " + neededResourceAmt);
                return neededResourceAmt;
            }

            void Init(double jumpResourceECmultiplier, double chargeTime, Vessel Source, Vessel targetVessel, Orbit sourceOrbit, Orbit destOrbit)
            {
                this.jumpResourceECmultiplier = jumpResourceECmultiplier;
                this.chargeTime = chargeTime;
                Calc(Source, targetVessel, sourceOrbit, destOrbit);
            }

            void Calc(Vessel Source, Vessel targetVessel, Orbit sourceOrbit, Orbit destOrbit)
            {
                targetBodyName = trimthe(destOrbit.referenceBody.name);
                targetAltitude = destOrbit.altitude;

                gravsource = GravitationalForcesAll(sourceOrbit.referenceBody, sourceOrbit.altitude);
                gravsourceprim = gravsource - GravitationalForce(sourceOrbit.referenceBody, sourceOrbit.altitude);
                gravdest = GravitationalForcesAll(destOrbit);
                mass = Source.totalMass * 1000;
                grav0 = sourceOrbit.referenceBody.gravParameter;
                radius0 = sourceOrbit.referenceBody.Radius;

                neededForce = (gravsource + gravdest) * mass;

                successProbability = Math.Min(1, Math.Max(0, totalGeneratedForce / neededForce));
                  optimumBeyondSOI = false;
                optimumAltitude = 0;

                if (jumpResourceECmultiplier > 0)
                    neededResourceAmt = NeededResourceAmt(targetVessel.protoVessel.vesselID, neededForce);
                else
                    neededResourceAmt = 0;

                optimumExists = CalculateOptimumAltitude(Source, mass, totalGeneratedForce, gravsourceprim, gravdest, sourceOrbit.referenceBody, out optimumBeyondSOI, out optimumAltitude);

            }

            public void ReCalc(Vessel Source, Vessel targetVessel, Orbit sourceOrbit, Orbit targetOrbit)
            {
                Calc(Source, targetVessel, sourceOrbit, targetOrbit);
                LogsManager.Info("Target vessel: " + vesselName + ",  neededForce: " + neededForce + ",  successProbability: " + successProbability + ",  optimumAltitude: " + optimumAltitude);

            }
            public TargetSuccess(double jumpResourceECmultiplier, double chargeTime, Vessel Source, Vessel targetVessel, Orbit sourceOrbit, Vessel Destination)
            {
                Init(jumpResourceECmultiplier, chargeTime, Source, targetVessel, sourceOrbit, Destination.orbit);
                vesselName = Destination.vesselName;

                LogsManager.Info("Target vessel: " + vesselName + ",  neededForce: " + neededForce + ",  successProbability: " + successProbability + ",  optimumAltitude: " + optimumAltitude);
            }
            public TargetSuccess(double jumpResourceECmultiplier, double chargeTime, Vessel Source, Vessel targetVessel, Orbit sourceOrbit, Orbit targetOrbit)
            {
                Init(jumpResourceECmultiplier, chargeTime, Source, targetVessel, sourceOrbit, targetOrbit);
                LogsManager.Info("Target vessel: " + vesselName + ",  neededForce: " + neededForce + ",  successProbability: " + successProbability + ",  optimumAltitude: " + optimumAltitude);
            }
        }

        IEnumerator UpdateTargetList()
        {
            if (HighLogic.LoadedSceneIsEditor && EditorLogic.fetch.ship != null)
            {
                Vessel sourceVessel = new Vessel();
                sourceVessel.loaded = false;
                sourceVessel.totalMass = EditorLogic.fetch.ship.GetTotalMass();

                double sourceOrbit = testAltitude * 1000;
                Orbit testOrigOrbit = VesselExt.CreateOrbit(Double.NaN, Double.NaN, sourceOrbit, Double.NaN, Double.NaN, Double.NaN, Double.NaN, FlightGlobals.GetHomeBody());

                testOrigOrbit.referenceBody = FlightGlobals.GetHomeBody();
                testOrigOrbit.altitude = sourceOrbit;

                for (int i = HighLogic.CurrentGame.flightState.protoVessels.Count() - 1; i >= 0; i--)
                {
                    ProtoVessel protoVessel = HighLogic.CurrentGame.flightState.protoVessels[i];

                    Vessel targetVessel = new Vessel();
                    targetVessel.loaded = false;
                    targetVessel.protoVessel = protoVessel;
                    if (VesselHasActiveBeacon(targetVessel))
                    {

                        double targetAltitude = protoVessel.orbitSnapShot.semiMajorAxis;

                        Orbit destOrbit = new Orbit(protoVessel.orbitSnapShot.inclination, protoVessel.orbitSnapShot.eccentricity, protoVessel.orbitSnapShot.semiMajorAxis,
                            protoVessel.orbitSnapShot.LAN, protoVessel.orbitSnapShot.argOfPeriapsis, protoVessel.orbitSnapShot.meanAnomalyAtEpoch, protoVessel.orbitSnapShot.epoch, FlightGlobals.Bodies[protoVessel.orbitSnapShot.ReferenceBodyIndex]);
                        destOrbit.semiMajorAxis = targetAltitude;
                        destOrbit.altitude = targetAltitude;

                        TargetSuccess tc = null;
                        if (!targetSuccessList.TryGetValue(targetVessel.protoVessel.vesselID.ToString(), out tc))
                        {
                            tc = new TargetSuccess(jumpResourceECmultiplier, chargeTime, sourceVessel, targetVessel, testOrigOrbit, destOrbit);
                            tc.vesselName = targetVessel.protoVessel.vesselName;
                            targetSuccessList.Add(targetVessel.protoVessel.vesselID.ToString(), tc);
                        }
                        else
                            tc.ReCalc(sourceVessel, targetVessel, testOrigOrbit, destOrbit);
                        //yield return new WaitForSeconds(1f);

                        yield return null;
                    }
                }
            }
            if (HighLogic.LoadedSceneIsFlight)
            {
                Vessel sourceVessel = FlightGlobals.ActiveVessel;
                Orbit testOrigOrbit = sourceVessel.orbit;
                for (int i = FlightGlobals.Vessels.Count - 1; i >= 0; i--)
                {
                    Vessel targetVessel = FlightGlobals.Vessels[i];
                    if (targetVessel != FlightGlobals.ActiveVessel && VesselHasActiveBeacon(targetVessel) && VesselInFlight(targetVessel))
                    {
                        double targetAltitude = targetVessel.orbit.altitude;
                        Orbit destOrbit = new Orbit(targetVessel.orbit.inclination, targetVessel.orbit.eccentricity, targetVessel.orbit.semiMajorAxis,
                            targetVessel.orbit.LAN, targetVessel.orbit.argumentOfPeriapsis, targetVessel.orbit.meanAnomalyAtEpoch, targetVessel.orbit.epoch, targetVessel.orbit.referenceBody);
                        destOrbit.semiMajorAxis = targetAltitude;
                        destOrbit.altitude = targetAltitude;

                        TargetSuccess tc = null;
                        if (!targetSuccessList.TryGetValue(targetVessel.protoVessel.vesselID.ToString(), out tc))
                        {
                            tc = new TargetSuccess(jumpResourceECmultiplier, chargeTime, sourceVessel, targetVessel, testOrigOrbit, destOrbit);
                            tc.vesselName = targetVessel.protoVessel.vesselName;
                            targetSuccessList.Add(targetVessel.protoVessel.vesselID.ToString(), tc);
                        }
                        else
                            tc.ReCalc(sourceVessel, targetVessel, testOrigOrbit, destOrbit);

                        yield return null;
                    }
                }
            }
            yield return null;
        }



        void Start()
        {
            LogsManager.Info("FTLDriveModule.Start");
            if (bodiesList == null)
                InitBodiesList();

            if (testBody == null)
                CalcTestAltitudes(FlightGlobals.GetHomeBody());

            SoundManager.LoadSound("FTLDriveContinued/Sounds/drive_sound", "DriveSound");
            driveSound = new FXGroup("DriveSound");
            SoundManager.CreateFXSound(part, driveSound, "DriveSound", true, 50f);

            animationStages = animationNames.Split(',').Select(a => a.Trim()).ToArray();
            SetUpAnimation(animationStages.First(), this.part, WrapMode.Loop);

            float leftPos = (HighLogic.CurrentGame.Parameters.CustomParams<FTLSettings>().initialWinPos) / 100 * (Screen.width - WIDTH);

            windowPosition = new Rect(leftPos , (Screen.height - HEIGHT) / 2, WIDTH, HEIGHT);
            
            if (jumpResourceDef != "")
            {
                string[] st = jumpResourceDef.Split(',');
                jumpResource = st[0].Trim();
                try
                {
                    jumpResourceECmultiplier = Double.Parse(st[1].Trim());
                }
                catch
                {
                    jumpResource = "";
                }
                if (jumpResource != "")
                {
                    PartResourceDefinition prd = PartResourceLibrary.Instance.GetDefinition(jumpResource);
                    if (prd != null)
                        resourceID = prd.id;
                    LogsManager.Info("jumpResourceECmultiplier: " + jumpResourceECmultiplier);
                }
            }

            CalculateValues(HighLogic.LoadedSceneIsFlight);

            GameEvents.onVesselWasModified.Add(onVesselWasModified);

            Events["ExecuteJump"].active = false;

            windowVisible = false;

            if (!periodicTargetUpdatesActive)
                StartCoroutine("PeriodicTargetUpdates");

            GameEvents.onHideUI.Add(OnHideUI);
            GameEvents.onShowUI.Add(OnShowUI);
        }

        static bool periodicTargetUpdatesActive = false;


        IEnumerator PeriodicTargetUpdates()
        {
            periodicTargetUpdatesActive = true;
            while (true)
            {
                StartCoroutine(UpdateTargetList());
                yield return new WaitForSeconds(10f);
            }
        }

        void SingleTargetUpdate()
        {
            StartCoroutine(UpdateTargetList());
        }

        void onVesselWasModified(Vessel v)
        {
            CalculateValues();
            // Need to update the target list since values will have changed
            SingleTargetUpdate();
        }

        void OnDestroy()
        {
            LogsManager.Info("FTLDriveModule.OnDestroy");
            GameEvents.onHideUI.Remove(OnHideUI);
            GameEvents.onShowUI.Remove(OnShowUI);
            Destroy();
        }
        void Destroy()
        {
            LogsManager.Info("Destroy");
            GameEvents.onVesselWasModified.Remove(onVesselWasModified);
            periodicTargetUpdatesActive = false;
        }
 
        private void OnHideUI()
        {
            hidden = true;
        }

        private void OnShowUI()
        {
            hidden = false;
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

        static bool CalculateOptimumAltitude(Vessel Source, double shipMass, double totalGeneratedForce, double gravsourceprim, double gravdest, CelestialBody referenceBody, out bool optimumBeyondSOI, out double optimumAltitude)
        {
            // Equations are shown on paper, image file in the repository.
            double B = 2 * referenceBody.Radius;
            double C = Square(referenceBody.Radius) - referenceBody.gravParameter / ((totalGeneratedForce / shipMass) - gravdest - gravsourceprim);
            double delta = B * B - 4 * C;
            bool optimumExists = delta > 0;
            optimumAltitude = (-B + Math.Sqrt(delta)) / 2;


            if (Source != null && Source.orbit != null)
                optimumBeyondSOI = optimumAltitude > Source.orbit.referenceBody.sphereOfInfluence;
            else
                optimumBeyondSOI = optimumAltitude > FlightGlobals.GetHomeBody().sphereOfInfluence;
            return optimumExists;
        }

        Func<string, string> trimthe = s => s.StartsWith("The") ? s.Substring(3).Trim() : s;

        void Update()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                LogsManager.Info("FTLDriveModule.Update in editor");
                ClearLabels();
                AppendLabel("Generated force", String.Format("{0:N1} iN", generatedForce));
                AppendLabel("Required EC", String.Format("{0:N1} ec/sec over {1:N1} sec", chargeRate, chargeTime));


                if (windowVisible)
                {
                    windowContent.Clear();
                    
                    CalculateValues(false);
#if false
                    StringBuilder sb = new StringBuilder();
                    sb.AppendEx("Estimated Values in Editor");
                    sb.AppendEx("Total generated force", String.Format("{0:N1} iN", totalGeneratedForce));
                    sb.AppendEx("Total required EC", String.Format("{0:N1} ec/s over {1:N1} sec", totalChargeRate, totalChargeTime));
                    if (totalResourceAmtNeeded > 0)
                        sb.AppendEx("Max " + jumpResource + " needed", String.Format("{0:N1}", totalResourceAmtNeeded));
#endif
                    windowContent.Add(new GuiElement()
                    {
                        type = "leftright",
                        text = String.Format("Test body: {0}", testBody.name),
                        color = null,
                    });
                    windowContent.Add(new GuiElement()
                    {
                        type = "slider",
                        text = String.Format("Test altitude: {0:N1} Km", testAltitude),
                        color = null,
                    });

                    StringBuilder sb = new StringBuilder();
                    sb.AppendEx("Estimated Values");
                    sb.AppendEx(" ");
                    sb.AppendEx("Atmospheric depth", String.Format("{0:N1} Km", minTestAltitude));
                    //sb.AppendEx(testBody.name + " SOI: " + (testBody.sphereOfInfluence / 1000).ToString("n1") + " Km");
                    sb.AppendEx(testBody.name + " max orbit", String.Format("{0:N1} Km", maxTestAltitude));
                    sb.AppendEx("Ship mass", String.Format("{0:N1} kg", EditorLogic.fetch.ship.GetTotalMass() * 1000));
                    sb.AppendEx("Total generated force", String.Format("{0:N1} iN", totalGeneratedForce));
                    sb.AppendEx("Total required EC", String.Format("{0:N1} ec/s over {1:N1} sec", totalChargeRate, totalChargeTime));
                    if (totalResourceAmtNeeded > 0)
                        sb.AppendEx("Max " + jumpResource + " needed", String.Format("{0:N1}", totalResourceAmtNeeded));

                    windowContent.Add(new GuiElement()
                    {
                        type = "header",
                        text = sb.ToString(),
                        color = "yellow",
                    });

                    for (int i = HighLogic.CurrentGame.flightState.protoVessels.Count() - 1; i >= 0; i--)
                    {
                        ProtoVessel vessel = HighLogic.CurrentGame.flightState.protoVessels[i];

                        Vessel v = new Vessel();
                        v.loaded = false;
                        v.protoVessel = vessel;


                        if (VesselHasActiveBeacon(v))
                        {
                            TargetSuccess tc = null;
                            if (targetSuccessList.TryGetValue(vessel.vesselID.ToString(), out tc))
                            {

                                sb = new StringBuilder();
                                sb.AppendEx("A beacon orbiting", String.Format("{0} at {1:N1} Km", tc.targetBodyName, tc.targetAltitude / 1000));
                                sb.AppendEx("Required force", String.Format("{0:N1} iN", tc.neededForce));
                                if (neededResourceAmt(vessel.vesselID, tc.neededResourceAmt) > 0)
                                    sb.AppendEx(jumpResource + " needed", String.Format("{0:N1}", tc.neededResourceAmt));
                                //sb.AppendEx("Total required EC", String.Format("{0:N1}/s over {1:N1}s", totalChargeRate, totalChargeTime));
                                sb.AppendEx("Success probability", String.Format("{0:N0} %", tc.successProbability * 100));
                                sb.AppendEx("Optimum altitude", String.Format((tc.optimumExists ? ("{0:N1} Km" + (tc.optimumBeyondSOI ? " (beyond SOI)" : "")) : "none (insufficient drives?)"), tc.optimumAltitude / 1000));

                                windowContent.Add(new GuiElement()
                                {
                                    type = "editortext",
                                    text = sb.ToString(),
                                    color = null,
                                });
                            }
                        }
                    }

                }
            }
            else
            {
                if (doJump)
                {
                    TargetSuccess tc = null;
                    if (targetSuccessList.TryGetValue(vessel.protoVessel.vesselID.ToString(), out tc))
                    {
                        ExecuteJump();
                        doJump = false;
                    }
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
            // FixedUpdate not called when in Editor


            if (HighLogic.LoadedSceneIsFlight)
            {
                try
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
                        double successProbability = neededForce == 0 ? 1 : Math.Min(1, Math.Max(0, currentForce / neededForce));
                        double currentDrain = isReady ? 0.01 * totalChargeRate * (totalChargeStored / totalChargeCapacity) : totalChargeRate;

                        if (totalChargeStored >= totalChargeCapacity || currentForce > neededForce)
                        {
                            isReady = true;
                        }

                        ClearLabels();
                        AppendLabel("Currently generated force", String.Format("{0:N1} iN", currentForce));
                        AppendLabel("Currently drained EC", String.Format("{0:N1} ec/sec", currentDrain));
                        if (neededResourceAmt(Destination.protoVessel.vesselID, neededForce) > 0)
                            AppendLabel(jumpResource + " needed", String.Format("{0:N1}", neededResourceAmt(Destination.protoVessel.vesselID, neededForce)));
                        if (VesselInFlight(Source) && VesselInFlight(Destination) && Source != Destination && Destination != null && VesselHasActiveBeacon(Destination))
                            AppendLabel("Success probability", String.Format("{0:N0} %", successProbability * 100));
                        if (currentForce >= neededForce && successProbability >= 1.0f && HighLogic.CurrentGame.Parameters.CustomParams<FTLSettings>().autoJump)
                            doJump = true;
                    }
                    else
                        if (JumpPossible(false))
                    {
                        TargetSuccess tc = null;
                        if (targetSuccessList.TryGetValue(Destination.protoVessel.vesselID.ToString(), out tc))
                        {
                            ClearLabels();
                            AppendLabel("Total generated force", String.Format("{0:N1} iN", totalGeneratedForce));
                            AppendLabel("Total required EC", String.Format("{0:N1} ec/sec over {1:N1} sec", totalChargeRate, totalChargeTime));
                            if (neededResourceAmt(Destination.protoVessel.vesselID, tc.neededResourceAmt) > 0)
                                AppendLabel(jumpResource + " needed", String.Format("{0:N1}", tc.neededResourceAmt));
                            AppendLabel("Target orbiting", String.Format("{0} at {1:N1} Km", tc.targetBodyName, tc.targetAltitude / 1000));
                            AppendLabel("Required force", String.Format("{0:N1} iN", tc.neededForce));
                            if (VesselInFlight(Source) && VesselInFlight(Destination) && Source != Destination && Destination != null && VesselHasActiveBeacon(Destination))
                                AppendLabel("Success probability", String.Format("{0:N0} %", tc.successProbability * 100));
                            AppendLabel("Optimum altitude", String.Format((tc.optimumExists ? ("{0:N1} Km" + (tc.optimumBeyondSOI ? " (beyond SOI)" : "")) : "none (insufficient drives?)"), tc.optimumAltitude / 1000));
                        }
                    }
                    else
                    {
                        ClearLabels();
                        AppendLabel("Total generated force", String.Format("{0:N1} iN", totalGeneratedForce));
                        AppendLabel("Total required EC", String.Format("{0:N1} ec/sec over {1:N1} sec", totalChargeRate, totalChargeTime));
                        if (totalResourceAmtNeeded > 0)
                            AppendLabel(jumpResource + " needed", String.Format("{0:N1}", totalResourceAmtNeeded));
                        AppendLabel("Target vessel", "none or invalid");
                    }

                    if (windowVisible)
                    {
                        windowContent.Clear();

                        StringBuilder sb = new StringBuilder();
                        sb.AppendEx("Current Vessel: " + FlightGlobals.ActiveVessel.name);
                        sb.AppendEx("Total generated force", String.Format("{0:N1} iN", totalGeneratedForce));
                        sb.AppendEx("Total required EC", String.Format("{0:N1} ec/sec over {1:N1} sec", totalChargeRate, totalChargeTime));
                        if (totalResourceAmtNeeded > 0)
                            sb.AppendEx(jumpResource + " needed", String.Format("{0:N1}", totalResourceAmtNeeded));
                        sb.AppendEx("Ship mass", String.Format("{0:N1} kg", Source.totalMass * 1000));

                        windowContent.Add(new GuiElement()
                        {
                            type = "header",
                            text = sb.ToString(),
                            color = "yellow",
                            clicked = () => { targetVesselID = Guid.Empty; FlightGlobals.fetch.SetVesselTarget(null); },
                        });

                        for (int i = FlightGlobals.Vessels.Count - 1; i >= 0; i--)
                        {
                            Vessel vessel = FlightGlobals.Vessels[i];
                            //}
                            //foreach (Vessel vessel in FlightGlobals.Vessels)
                            //{

                            if (vessel != Source && VesselHasActiveBeacon(vessel) && VesselInFlight(vessel))
                            {
                                TargetSuccess tc = null;
                                if (targetSuccessList.TryGetValue(vessel.protoVessel.vesselID.ToString(), out tc))
                                {
                                    sb = new StringBuilder();
                                    sb.AppendEx("A beacon orbiting", String.Format("{0} at {1:N1} Km", tc.targetBodyName, tc.targetAltitude / 1000));
                                    sb.AppendEx("Required force", String.Format("{0:N1} iN", tc.neededForce));
                                    if (Destination != null && tc.neededResourceAmt > 0)
                                        sb.AppendEx(jumpResource + " needed", String.Format("{0:N1}", tc.neededResourceAmt));
                                    if (VesselInFlight(Source) && VesselInFlight(Destination) && Source != Destination && Destination != null && VesselHasActiveBeacon(Destination))
                                        sb.AppendEx("Success probability", String.Format("{0:N0} %", tc.successProbability * 100));
                                    sb.AppendEx("Optimum altitude", String.Format((tc.optimumExists ? ("{0:N1} Km" + (tc.optimumBeyondSOI ? " (beyond SOI)" : "")) : "none (insufficient drives?)"), tc.optimumAltitude / 1000));
                                    sb.AppendEx(vessel == Destination ? @"     \ - - - - - - Selected target - - - - - - /     " : @"     \ - - - - - - Click to select - - - - - - /     ");

                                    windowContent.Add(new GuiElement()
                                    {
                                        type = "button",
                                        text = sb.ToString(),
                                        color = (vessel == Destination ? "green" : null),
                                        clicked = () => { targetVesselID = vessel.protoVessel.vesselID; FlightGlobals.fetch.SetVesselTarget(vessel); },
                                    });
                                }
                                else
                                {
                                    LogsManager.Info("FixedUpdate 1, vessel: " + vessel.vesselName + ", vesselID: " + vessel.protoVessel.vesselID.ToString() + ",  targetSuccessList.Count: " + targetSuccessList.Count);
                                    foreach (var tc1 in targetSuccessList.Keys)
                                    {
                                        LogsManager.Info("tc1.key: " + tc1);
                                    }
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
            for (int i = parts.Count() - 1; i >= 0; i--)
            {
                Part p = parts[i];

                //foreach (Part p in parts)
                if (p.State != PartStates.DEAD)
                    for (int i2 = p.Modules.Count - 1; i2 >= 0; i2--)
                    {
                        PartModule pm = p.Modules[i2];

                        //foreach (PartModule pm in p.Modules)
                        if (pm.moduleName == "FTLDriveModule")
                            yield return (FTLDriveModule)pm;
                    }
            }
        }

        // following used in flight

        private static double GravitationalForcesAll(Orbit orbit)
        {
            if (orbit == null) return 0d;
            return GravitationalForce(orbit) + GravitationalForcesAll(orbit.referenceBody.orbit);
        }

        private static double GravitationalForce(Orbit orbit)
        {
            if (orbit == null) return 0d;
            return (orbit.referenceBody.gravParameter / Square(orbit.altitude + orbit.referenceBody.Radius));
        }

        // Following used in Editor

        static private double GravitationalForcesAll(CelestialBody body, double altitude)
        {
            if (body == null) return 0;
            double amt = 0;
            for (int i = bodiesList.Count - 1; i >= 0; i--)
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

        static private double GravitationalForce(CelestialBody body, double altitude)
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
            //Debug.Log(str);
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
            if (jumpResource != "" && jumpResourceECmultiplier > 0 && resourceID >= 0)
            {
                this.part.GetConnectedResourceTotals(resourceID, out resourceAmtAvailable, out maxAmount);

                if (neededResourceAmt(targetVesselID) > resourceAmtAvailable)
                {
                    LogsManager.Info("Not enough resource available for jump: " + neededResourceAmt(targetVesselID));
                    return false;
                }

            }
            return true;
        }

        private bool VesselHasActiveBeacon(Vessel vessel)
        {
            if (vessel.loaded)
            {
                for (int i = vessel.parts.Count() - 1; i >= 0; i--)
                {
                    Part p = vessel.parts[i];

                    //foreach (Part p in vessel.parts)
                    if (p.State != PartStates.DEAD)
                        for (int i2 = p.Modules.Count - 1; i2 >= 0; i2--)
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
                for (int i = vessel.protoVessel.protoPartSnapshots.Count() - 1; i >= 0; i--)
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
                TargetSuccess tc = null;
                if (targetSuccessList.TryGetValue(Destination.protoVessel.vesselID.ToString(), out tc))
                {
                    double successProbability = tc.successProbability;

                    Events["ExecuteJump"].active = false;
                    isSpinning = false;
                    ToggleAnimations();
                    System.Random rng = new System.Random();

                    if (rng.NextDouble() < successProbability)
                    {
                        if (totalResourceAmtNeeded > 0)
                            part.RequestResource(resourceID, totalResourceAmtNeeded);
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
        }

        GameScenes lastSceneLoaded;

        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Show/Hide possible destinations")]
        public void ToggleGUI()
        {
            windowVisible = !windowVisible;
            if (windowVisible)
            {
                // Do an immediate update of the target list, in case it is in the middle of the delay
                SingleTargetUpdate();
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

        GUIStyle normal;
        GUIStyle yellow;
        GUIStyle green;
        bool initted = false;
        GUIStyle label;
        bool lastKSPSkin = false;
        void OnGUI()
        {
            if (!initted)
            {
                initted = true;
                normal = new GUIStyle(GUI.skin.textField);
                normal.normal.textColor = normal.hover.textColor = GUI.skin.textField.normal.textColor;
                yellow = new GUIStyle(GUI.skin.textField);
                yellow.normal.textColor = yellow.hover.textColor = Color.yellow;
                green = new GUIStyle(GUI.skin.textField);
                green.normal.textColor = green.hover.textColor = Color.green;
                label = new GUIStyle(GUI.skin.label);
                label.normal.textColor = yellow.hover.textColor = Color.yellow;
            }
            if (HighLogic.CurrentGame.Parameters.CustomParams<FTLSettings>().KSPSkin)
            {
                GUI.skin = HighLogic.Skin;
            }
            if (lastKSPSkin != HighLogic.CurrentGame.Parameters.CustomParams<FTLSettings>().KSPSkin)
            {
                if (HighLogic.CurrentGame.Parameters.CustomParams<FTLSettings>().KSPSkin)
                    windowPosition.width = KSP_SKIN_WIDTH;
                else
                    windowPosition.width = WIDTH;
                lastKSPSkin = HighLogic.CurrentGame.Parameters.CustomParams<FTLSettings>().KSPSkin;
            }
            if (windowVisible && ! hidden)
                windowPosition = ClickThruBlocker.GUILayoutWindow(523429, windowPosition, DisplayDestinations, "FTL Possible Destinations");

        }


        void DisplayDestinations(int windowId)
        {
            Vector2 buttonSize = new Vector2(25f, 20f);
            if (GUI.Button(new Rect(windowPosition.width - 23f, 2f, 18f, 13f), "x"))
                windowVisible = false;

            int i0 = windowContent.Count();
            for (int i = 0; i < i0; i++)
            {
                GuiElement e = windowContent[i];

                GUILayout.BeginHorizontal();
                if (e.type == "header")
                {
                    GUILayout.TextField(e.text, label);
                }
                if (e.type == "text")
                {
                    GUIStyle s = e.color == "yellow" ? yellow : e.color == "green" ? green : normal;
                    GUILayout.TextField(e.text, s);
                }
                if (e.type == "editortext")
                {
                    GUIStyle s = e.color == "yellow" ? yellow : e.color == "green" ? green : normal;
                    GUILayout.TextField(e.text, s);
#if false
                    // this is for a future feature, where the destination craft can have it's test orbit variable in the editor for testing purposes
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    float newtestAltitude = GUILayout.HorizontalSlider(testAltitude, minTestAltitude, maxTestAltitude);
#endif
                }
                if (e.type == "leftright")
                {
                    if (GUILayout.Button("<-", GUILayout.Width(30)))
                    {
                        testBodyIdx--;
                        if (testBodyIdx < 0)
                        {
                            testBodyIdx = bodiesList.Count() - 1;
                        }

                        CalcTestAltitudes(bodiesList[testBodyIdx].body);
                        SingleTargetUpdate();

                    }
                    GUIStyle s = e.color == "yellow" ? yellow : e.color == "green" ? green : normal;
                    GUILayout.Label(e.text, s);
                    if (GUILayout.Button("->", GUILayout.Width(30)))
                    {
                        testBodyIdx++;
                        if (testBodyIdx >= bodiesList.Count())
                            testBodyIdx = 0;

                        CalcTestAltitudes(bodiesList[testBodyIdx].body);
                        SingleTargetUpdate();
                    }

                }
                if (e.type == "slider")
                {
                    GUIStyle s = e.color == "yellow" ? yellow : e.color == "green" ? green : normal;
                    GUILayout.Label(e.text, s);
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    float newtestAltitude = GUILayout.HorizontalSlider(testAltitude, minTestAltitude, maxTestAltitude);
                    if (newtestAltitude != testAltitude)
                    {
                        testAltitude = newtestAltitude;
                        SingleTargetUpdate();
                    }
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
            for (int i = mas.Count() - 1; i >= 0; i--)
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
