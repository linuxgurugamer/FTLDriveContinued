using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System.Text;

namespace ScienceFoundry.FTL
{
    [KSPModule("FTL Beacon")]
    public class FTLBeaconModule : PartModule
    {
        //------------------------------ FIELDS ---------------------------------------------------

        [KSPField(isPersistant = true)]
        public bool beaconActivated = false;


        //------------------------------ PARTMODULE OVERRIDES -------------------------------------

        public override void OnStart(PartModule.StartState state)
        {
            animationStages = animationNames.Split(',').Select(a => a.Trim()).ToArray();

            foreach (string animStage in animationStages)
                SetUpAnimation(animStage, this.part, WrapMode.Once);
            SetUpAnimation(animationStages.Last(), this.part, WrapMode.Loop);

            Events["ToggleBeacon"].guiName = beaconActivated ? "Deactivate Beacon" : "Activate Beacon";
        }

        public override void OnUpdate()
        {
            UpdateAnimations();
            base.OnUpdate();
        }


        //------------------------------ CORE FUNCTIONALITY ---------------------------------------

        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Activate Beacon")]
        public void ToggleBeacon()
        {
            beaconActivated = !beaconActivated;
            Events["ToggleBeacon"].guiName = beaconActivated ? "Deactivate Beacon" : "Activate Beacon";
            rampDirection = beaconActivated ? RampDirection.up : RampDirection.down;
        }

        public override string GetInfo()
        {
            var sb = new StringBuilder();
            sb.Append("Navigational computer \n");
            sb.Append("- Broadcasts position to vessels \n");
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


        void SetUpAnimation(string animationName, Part part, WrapMode wrapMode)
        {
            foreach (var animation in part.FindModelAnimators(animationName))
            {
                var animationState = animation[animationName];

                if (beaconActivated)
                {
                    if (wrapMode != WrapMode.Once)
                    {
                        animationState.speed = 1;
                    }
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

                if (beaconActivated)
                    animation.Play(animationName);
            };
        }

        public void UpdateAnimations()
        {
            if (!(HighLogic.LoadedSceneIsFlight || HighLogic.LoadedSceneIsEditor) || rampDirection == RampDirection.none)
                return;

            string activeAnim = animationStages[animStage];
            int curAnimStage = animStage;

            var mas = part.FindModelAnimators(activeAnim).ToList();
            for (int i = mas.Count - 1; i >= 0; i--)
            {
                var anim = mas[i];
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
