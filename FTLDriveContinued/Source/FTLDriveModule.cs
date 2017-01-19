using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ScienceFoundry.FTL
{
    [KSPModule("FTL Drive")]
    public class FTLDriveModule : PartModule
    {
        static List<FTLDriveModule> availableFtlDrives = new List<FTLDriveModule>();
        static double[] MathPow = null;

        [KSPField]
        public string animationNames;

        private string[] animStages = { };

        // animationRampspeed is how quickly it gets up to speed.  1 meaning it gets to full speed (as defined by 
        // the animSpeed and customAnimationSpeed) immediately, less than that will ramp up over time
        [KSPField]
        public float animationRampSpeed = 0.001f;
        // When the mod starts, what speed to set the initial animSpeed to
        [KSPField]
        public float startAnimSpeed = 0f;
        [KSPField]
        public float customAnimationSpeed = 1f;

        protected enum RampDirection { none, up, down };
        protected RampDirection rampDirection = RampDirection.none;
        private int animStage = 0;
        private AnimationState[] containerStates;
        private List<AnimationState> states = new List<AnimationState>();

        private void Start()
        {
            if (MathPow == null)
            {
                MathPow = new double[10];
                for (double cnt = 0; cnt < 10f; cnt++)
                {
                    MathPow[(int)cnt] = Math.Pow(1.4, -1 * cnt);
                }
            }
        }


        protected enum DriveState
        {
            IDLE,
            STARTING,
            SPINNING,
            JUMPING,
            JUMPING_SECONDARY
        }

        protected DriveState state = DriveState.IDLE;
        private double activationTime = 0;
        protected FXGroup driveSound;

        /**
         * \brief Jump beacon name (displayed in the GUI)
         * This is the name of the currently selected jump beacon. It is updated from the Next function,
         * which will go to the next active beacon on the list.
         * \note this variable is not actually used by the mod, it is only for the GUI
         */
        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "Beacon", isPersistant = false)]
        private string beaconName = BeaconSelector.NO_TARGET;

        private double Force
        {
            get
            {
                return generatedForce;
            }
            set
            {
                generatedForceStr = String.Format("{0:0.0}iN", value);
                generatedForce = value;
            }
        }
        private double totalGeneratedForce = 0f;
        private double TotalGeneratedForce
        {
            get
            {
                return totalGeneratedForce;
            }
            set
            {
                generatedForceStr = String.Format("{0:0.0}iN", value);
                totalGeneratedForce = value;
            }
        }

        private double generatedForce = 0;

        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "Generated force", isPersistant = false)]
        private string generatedForceStr = "0.0iN";

        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "Force required", isPersistant = false)]
        private string requiredForce = "Inf";

        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "Success probability", isPersistant = false)]
        private string successProb = "?";

        [KSPField(guiActive = false, guiActiveEditor = false, isPersistant = true)]
        public double maxGeneratorForce = 2000;

        [KSPField(guiActive = false, guiActiveEditor = false, isPersistant = true)]
        public double maxChargeTime = 10;

        [KSPField(guiActive = false, guiActiveEditor = false, isPersistant = true)]
        public double requiredElectricalCharge = 100;


        double MaxCombinedGeneratorForce()
        {
            double d = 0;
            
            foreach (var dm in availableFtlDrives)
                d += dm.maxGeneratorForce;
            return d;
        }
        /**
         * \brief Currently selected beacon.
         */
        private NavComputer navCom = new NavComputer();

        public override string GetInfo()
        {
            var str = new StringBuilder();

            str.AppendFormat("Maximal force: {0:0.0}iN\n", maxGeneratorForce);
            str.AppendFormat("Maximal charge time: {0:0.0}s\n\n", maxChargeTime);
            str.AppendFormat("Requires\n");
            str.AppendFormat("- Electric charge: {0:0.00}/s\n\n", requiredElectricalCharge);
            str.Append("Navigational computer\n");
            str.Append("- Required force\n");
            str.Append("- Success probability\n");

            return str.ToString();
        }

        bool driveActive = true;
        [KSPEvent(active = true, guiActive = true, guiActiveEditor = true, guiName = "Active (deactivate)")]
        public void DriveActive()
        {
            driveActive = !driveActive;
            if (driveActive)
                availableFtlDrives.Add(this);
            else
                availableFtlDrives.Remove(this);
            UpdateEvents();
        }
        [KSPEvent(active = true, guiActive = true, guiActiveEditor = true, guiName = "Jump")]
        public void Jump()
        {
            DriveState curState = state;
            foreach (var dm in availableFtlDrives)
            {
                if (navCom.JumpPossible || HighLogic.LoadedSceneIsEditor)
                {
                    if (curState == DriveState.IDLE)
                    {
                        if (dm == this)
                            ScreenMessages.PostScreenMessage("Spinning up FTL drive...", (float)AbsoluteMaxChargeTime(), ScreenMessageStyle.UPPER_CENTER);
                        dm.state = DriveState.STARTING;
                        dm.rampDirection = RampDirection.up;
                        dm.driveSound.audio.Play();
                    }
                    else
                    {
                        dm.rampDirection = RampDirection.down;
                        dm.driveSound.audio.Stop();
                        dm.Force = 0;
                        dm.state = DriveState.IDLE;
                    }
                    dm.UpdateEvents();
                }
            }
        }

        protected void UpdateEvents()
        {
            Events["Jump"].guiName = (state == DriveState.IDLE) ? "Jump" : "Emergency Stop Jump";

            Events["DriveActive"].guiName = driveActive ? "Active (deactivate)" : "Not Active (activate)";

        }

        [KSPAction("Activate drive")]
        public void JumpAction(KSPActionParam p)
        {
            Jump();
        }

        [KSPEvent(active = true, guiActive = true, guiActiveEditor = true, guiName = "Next Beacon")]
        public void NextBeacon()
        {
            print("NextBeacon");
            if (state == DriveState.IDLE)
            {
                print("DriveState.Idle");
                if (navCom != null && navCom.Destination != null)
                    print("navCom.Destination: " + navCom.Destination.ToString());
                navCom.Destination = BeaconSelector.Next(navCom.Destination, FlightGlobals.ActiveVessel);
                UpdateJumpStatistics();
            }
        }

        [KSPAction("Next beacon")]
        public void NextAction(KSPActionParam p)
        {
            if (state == DriveState.IDLE)
            {
                NextBeacon();

                if (navCom.JumpPossible)
                {
                    ScreenMessages.PostScreenMessage(String.Format("Beacon {0} selected", beaconName),
                                                     4f,
                                                     ScreenMessageStyle.UPPER_CENTER);
                }
                else
                {
                    ScreenMessages.PostScreenMessage("NAVCOM Unavailable", 4f, ScreenMessageStyle.UPPER_CENTER);
                }
            }
        }

        string ftlAnalysisReport = "";
        bool display = false;
        const int WIDTH = 250;
        const int HEIGHT = 250;
        Rect position = new Rect((Screen.width - WIDTH) / 2, (Screen.height - HEIGHT) / 2, WIDTH, HEIGHT);

        [KSPEvent(active = true, guiActive = true, guiActiveEditor = false, guiName = "FTL Analysis")]
        public void FTLAnalysis()
        {
            ftlAnalysisReport = "Beacon: " + beaconName + "\n";
            ftlAnalysisReport += "Force required: " + requiredForce + "\n";
            ftlAnalysisReport += "Success probability: " + successProb + "\n\n";

            var orbit = navCom.Source.GetOrbitDriver().orbit;
            ftlAnalysisReport += "Vessel orbiting around: " + orbit.referenceBody.name + "\n";
            ftlAnalysisReport += "Vessel altitude: " + String.Format("{0:0.0}m", orbit.altitude) + "\n";
            ftlAnalysisReport += orbit.referenceBody.name + "Radius: " + String.Format("{0:0.0}m", orbit.referenceBody.Radius) + "\n\n";

            if (navCom.Destination != null)
            {
                orbit = navCom.Destination.GetOrbitDriver().orbit;
                ftlAnalysisReport += "Beacon orbiting around " + navCom.Destination.GetOrbitDriver().orbit.referenceBody.name + "\n";
                ftlAnalysisReport += "Beacon altitude: " + String.Format("{0:0.0}m", orbit.altitude) + "\n";
                ftlAnalysisReport += orbit.referenceBody.name + "Radius: " + String.Format("{0:0.0}m", orbit.referenceBody.Radius) + "\n\n";
            }
            Debug.Log("FTL Analysis\n" + ftlAnalysisReport);
            display = true;
        }



        void OnGUI()
        {
            if (!display)
                return;

            position = GUILayout.Window(523429, position, Display, "FTL Analysis Report");
        }
        void Display(int windowId)
        {
            GUILayout.BeginHorizontal();
            GUILayout.TextField(ftlAnalysisReport);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Ok", GUILayout.Height(30)))
            {
                display = false;
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUI.DragWindow();
        }

        private void UpdateJumpStatistics()
        {
            if (navCom.JumpPossible)
            {
                beaconName = BeaconDescriptor(navCom.Destination);
                requiredForce = String.Format("{0:0.0}iN", navCom.GetRequiredForce());
                successProb = String.Format("{0:0.0}%", navCom.GetSuccessProbability(MaxCombinedGeneratorForce()) * 100);
            }
            else
            {
                beaconName = BeaconSelector.NO_TARGET;
                requiredForce = "Inf";
                successProb = "?";
            }
            if (display)
                FTLAnalysis();
        }

        private string BeaconDescriptor(Vessel beacon)
        {
            string retValue = beacon.vesselName;
            var orbit = beacon.orbitDriver;

            if ((orbit.referenceBody != null) && (beacon.orbit != null))
            {
                var body = orbit.referenceBody;
                var altitude = beacon.orbit.altitude;

                if (altitude < 1000000)
                    retValue = String.Format("{0} ({1:0.0km})", body.name, altitude / 1000);
                else
                    retValue = String.Format("{0} ({1:0.0Mm})", body.name, altitude / 1000000);


            }

            return retValue;
        }

        public override void OnStart(PartModule.StartState state)
        {
            SoundManager.LoadSound("FTLDriveContinued/Sounds/drive_sound", "DriveSound");
            driveSound = new FXGroup("DriveSound");
            SoundManager.CreateFXSound(part, driveSound, "DriveSound", true, 50f);
            animStages = animationNames.Split(',').Select(a => a.Trim()).ToArray();

            SetUpAnimation(animStages[0], this.part, WrapMode.Loop);
            UpdateEvents();

            containerStates = states.ToArray();
            if (driveActive)
                availableFtlDrives.Add(this);

            try
            {
                this.state = DriveState.IDLE;

                if (state != StartState.Editor)
                    navCom.Source = FlightGlobals.ActiveVessel;
            }
            catch (Exception ex)
            {
                print(String.Format("[FTLDriveContinued] Error in OnStart - {0}", ex.ToString()));
            }

            base.OnStart(state);
        }


        void OnDestroy()
        {
            availableFtlDrives.Remove(this);
        }

        public void SetUpAnimation(string animationName, Part part, WrapMode wrapMode)  //Thanks Majiir!
        {
            foreach (var animation in part.FindModelAnimators(animationName))
            {
                var animationState = animation[animationName];

                animationState.speed = 0;
                animationState.normalizedTime = 0f;
                
                animationState.enabled = true;
                animationState.wrapMode = wrapMode;

                states.Add(animationState);


               //     animation.Play(animationName);
            };
            // return states.ToArray();
        }

        public override void OnLoad(ConfigNode node)
        {
            try
            {
                navCom = new NavComputer();
                maxGeneratorForce = Convert.ToDouble(node.GetValue("maxGeneratorForce"));
                maxChargeTime = Convert.ToDouble(node.GetValue("maxChargeTime"));
                requiredElectricalCharge = Convert.ToDouble(node.GetValue("requiredElectricalCharge"));
            }
            catch (Exception ex)
            {
                print(String.Format("[FTLDriveContinued] Error in OnLoad - {0}", ex.ToString()));
            }

            base.OnLoad(node);
        }

        public override void OnAwake()
        {
            base.OnAwake();
        }

        private double lastUpdateTime = -1.0f;

        private double LastUpdateTime
        {
            get
            {
                if (lastUpdateTime < 0)
                {
                    lastUpdateTime = Planetarium.GetUniversalTime();
                }

                return lastUpdateTime;
            }
            set
            {
                lastUpdateTime = value;
            }
        }
        double lastActivationTime;
        public void FixedUpdate()
        {
            if (IsVesselReady())
            {
                var deltaT = GetElapsedTime();
                LastUpdateTime += deltaT;
                lastActivationTime = activationTime;
                activationTime += deltaT;

                switch (state)
                {
                    case DriveState.IDLE:
                        break;
                    case DriveState.STARTING:
                        state = DriveState.SPINNING;
                        activationTime = 0;
                        break;
                    case DriveState.SPINNING:
                        SpinningUpDrive(deltaT);
                        break;
                    case DriveState.JUMPING:
                        ExecuteJump();
                        break;
                }
            }

            base.OnFixedUpdate();
        }

        private float ramp = 0;

        public void LateUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight && !HighLogic.LoadedSceneIsEditor || rampDirection == RampDirection.none) return;

            string activeAnim = animStages[animStage];
            int curAnimStage = animStage;

            foreach (var anim in part.FindModelAnimators(activeAnim))
            {
                if (anim != null)
                {
                    float origSpd = anim[activeAnim].speed;
                    switch (rampDirection)
                    {
                        case RampDirection.up:
                            if (ramp < 1f)
                            {
                                ramp += animationRampSpeed;
                            }
                            if (ramp > 1f)
                            {
                                ramp = 1f;
                                animStage++;
                                // rampDirection = RampDirection.none;
                            }
                            break;
                        case RampDirection.down:
                            if (ramp > 0)
                            {
                                ramp -= animationRampSpeed;
                            }
                            if (ramp < 0)
                            {
                                ramp = 0f;
                                animStage--;
                            }
                            break;
                    }
                    
                    anim[activeAnim].speed = customAnimationSpeed * ramp;
                    
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
                        if (animStage >= animStages.Length)
                        {
                            animStage = animStages.Length - 1;
                            rampDirection = RampDirection.none;
                        }
                        else
                        {
                            ramp = 0;
                        }
                    }

                    anim.Play(activeAnim);

                }
                else
                    Debug.Log("anim is null");
            }

        }
        /**
         * \brief Check if the vessel is ready
         * \return true if the vessel is ready, otherwise false.
         */
        private static bool IsVesselReady()
        {
            return (Time.timeSinceLevelLoad > 1.0f) && FlightGlobals.ready;
        }

        /**
         * \brief Return the elapsed time since last update.
         * \return elapsed time
         */
        private double GetElapsedTime()
        {
            return Planetarium.GetUniversalTime() - LastUpdateTime;
        }

        public override void OnUpdate()
        {
            UpdateJumpStatistics();
            base.OnUpdate();
        }
        
        private double TotalForce()
        {
            double totalForce = 0;
            List<double> forceList = new List<double>();
            foreach (var dm in availableFtlDrives)
                forceList.Add(dm.Force);
            forceList = forceList.OrderByDescending(x => x).ToList();
            int cnt = 0;
            foreach (double d in forceList)
            {
                totalForce += d * MathPow[cnt];
                cnt++;
                if (cnt >= 10)
                    break;
           }
            
            return totalForce;
        }

        /// <summary>
        /// Activates jumping on only one drive, to avoid multiple drives jumping at the same time
        /// </summary>
        void ActivateJumping()
        {
            foreach (var dm in availableFtlDrives)
                dm.state = DriveState.JUMPING_SECONDARY;

            state = DriveState.JUMPING;
        }

        /// <summary>
        /// Return the largest charge time in the list
        /// </summary>
        /// <returns></returns>
        double AbsoluteMaxChargeTime()
        {
            double d = 0;
            foreach (var dm in availableFtlDrives)
                if (dm.maxChargeTime > d)
                    d = dm.maxChargeTime;
            return d;
        }

        private void SpinningUpDrive(double deltaT)
        {
            double d = AbsoluteMaxChargeTime();
            double spinRate = maxGeneratorForce / maxChargeTime;
            
            if (lastActivationTime < AbsoluteMaxChargeTime())
            {
                double delta = deltaT;
                if (activationTime > maxChargeTime)
                    delta = maxChargeTime - lastActivationTime;
                
                Force += PowerDrive(delta * spinRate, delta);
                TotalGeneratedForce = TotalForce();
            }

            if (activationTime >= AbsoluteMaxChargeTime())
            {
                if (state != DriveState.JUMPING_SECONDARY)
                    ActivateJumping();
            }
            else
            {
                TotalGeneratedForce = TotalForce();
                if (TotalForce() >= navCom.GetRequiredForce())
                {
                    if (state != DriveState.JUMPING_SECONDARY)
                        ActivateJumping();
                }
            }
        }

        private double PowerDrive(double deltaF, double deltaT)
        {
            var demand = deltaT * requiredElectricalCharge;
            var delivered = part.RequestResource("ElectricCharge", demand);
            //Debug.Log("PowerDrive:  deltaT: " + deltaT.ToString() + "  demand: " + demand.ToString() + "   delivered: " + delivered.ToString() + "  deltaF: " + deltaF.ToString());
            return deltaF * (delivered / demand);
        }

        private void ExecuteJump()
        {
            //Debug.Log("ExecuteJump");
            if (navCom.Jump(TotalForce()))
            {
                ScreenMessages.PostScreenMessage("Jump Completed!", 5f, ScreenMessageStyle.UPPER_CENTER);
            }
            else
            {
                ScreenMessages.PostScreenMessage("Jump failed!", 2f, ScreenMessageStyle.UPPER_CENTER);
            }
            foreach (var dm in availableFtlDrives)
            {
                dm.rampDirection = RampDirection.down;
                dm.driveSound.audio.Stop();
                dm.Force = 0;
                dm.state = DriveState.IDLE;
                dm.UpdateEvents();
            }
        }
    }
}
