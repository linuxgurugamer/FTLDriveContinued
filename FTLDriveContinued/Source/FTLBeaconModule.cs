using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace ScienceFoundry.FTL
{
    [KSPModule("FTL Beacon")]
    public class FTLBeaconModule : PartModule
    {
        //------------------------------ SETUP (RUN ONCE) -----------------------------------------


        //------------------------------ CORE FUNCTIONALITY ---------------------------------------

        [KSPField(guiActive = false, guiActiveEditor = false, isPersistant = true)]
        private bool beaconActivated = false;

        [KSPEvent(active = true, guiActive = true, guiActiveEditor = true, guiName = "Turn Beacon On")]
        public void ToggleBeacon()
        {
            //changed = true;
            for (int i = 0; i < animStages.Length; i++)
            {
                if (beaconActivated)
                {
                    LogsManager.ErrorLog("Deactivating Beacon");
                    foreach (var animation in part.FindModelAnimators(animStages[i]))
                    {
                        //animation[animationName].speed = 0;
                        // animation[animationName].enabled = false;
                        rampDirection = RampDirection.down;
                        // animation.Play(animationName);
                    }
                }
                else
                {
                    LogsManager.ErrorLog("Activating Beacon");
                    foreach (var animation in part.FindModelAnimators(animStages[i]))
                    {
                        //animation[animationName].speed = 1;
                        //animation[animationName].enabled = true;
                        rampDirection = RampDirection.up;
                        //animation.Play(animationName);
                    }
                }
            }
            beaconActivated = !beaconActivated;
            UpdateEvents();
        }

        private void UpdateEvents()
        {
            Events["ToggleBeacon"].guiName = beaconActivated ? "Turn Beacon Off" : "Turn Beacon On";
        }

        public bool IsBeaconActive()
        {
            return beaconActivated;
        }


        //------------------------------ ANIMATION ------------------------------------------------

        [KSPField]
        public string animationNames;
        // animationRampspeed is how quickly it gets up to speed.  1 meaning it gets to full speed (as defined by 
        // the animSpeed and customAnimationSpeed) immediately, less than that will ramp up over time
        [KSPField]
        public float animationRampSpeed = 0.001f;
        // When the mod starts, what speed to set the initial animSpeed to
        [KSPField]
        public float startAnimSpeed = 0f;
        [KSPField]
        public float customAnimationSpeed = 1f;

        private enum RampDirection { none, up, down };
        private float ramp = 0;
        private RampDirection rampDirection = RampDirection.none;
        private AnimationState[] containerStates;
        private List<AnimationState> states = new List<AnimationState>();
        private int animStage = 0;
        private string[] animStages = { };

        public override void OnStart(PartModule.StartState state)
        {
            animStages = animationNames.Split(',').Select(a => a.Trim()).ToArray();

            UpdateEvents();
            for (int i = 0; i < animStages.Length - 1; i++)
                SetUpAnimation(animStages[i], this.part, WrapMode.Once);

            SetUpAnimation(animStages[animStages.Length - 1], this.part, WrapMode.Loop);
            containerStates = states.ToArray();
        }

        public void SetUpAnimation(string animationName, Part part, WrapMode wrapMode)  //Thanks Majiir!
        {
            foreach (var animation in part.FindModelAnimators(animationName))
            {
                var animationState = animation[animationName];
                if (beaconActivated)
                {
                    if (wrapMode != WrapMode.Once)
                        animationState.speed = 1;
                    else
                    {
                        animationState.speed = 0;
                        animationState.normalizedTime = 1;
                    }
                }
                else
                {
                    animationState.speed = 0;
                    animationState.normalizedTime = 0f;
                }
                animationState.enabled = true;
                animationState.wrapMode = wrapMode;
                // animation.Blend(animationName);
                states.Add(animationState);

                if (beaconActivated)
                    animation.Play(animationName);
            };
            // return states.ToArray();
        }

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
                    
                    if (curAnimStage < animStages.Length - 1)
                        anim[activeAnim].normalizedTime = ramp;
                    else
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

    }
}
