using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;


namespace ScienceFoundry.FTL
{
    // HighLogic.CurrentGame.Parameters.CustomParams<FTLSettings>()

    public class FTLSettings : GameParameters.CustomParameterNode
    {
        public override string Title { get { return ""; } }
        public override string DisplaySection { get { return ""; } }
        public override GameParameters.GameMode GameMode { get { return GameParameters.GameMode.ANY; } }
        public override string Section { get { return "FTL Drive"; } }
        public override int SectionOrder { get { return 1; } }
        public override bool HasPresets { get { return true; } }


        [GameParameters.CustomParameterUI("Drive stops when switching vessels")]
        public bool driveStopsUponVesselSwitch = true;

        [GameParameters.CustomParameterUI("Jump when success reaches 100%")]
        public bool autoJump;

        [GameParameters.CustomFloatParameterUI("Multiple Drive Exponent", minValue = 1.0f, maxValue = 2.0f, stepCount = 101, displayFormat = "F2", asPercentage = false)]
        public float multipleDriveExponent = 1.4f;

        [GameParameters.CustomFloatParameterUI("Initial Window Position", minValue = 0.0f, maxValue = 100.0f, stepCount = 101, displayFormat = "F0", asPercentage = false)]
        public float initialWinPos = 50f;

        [GameParameters.CustomParameterUI("Use KSP skin")]
        public bool KSPSkin;



        public override void SetDifficultyPreset(GameParameters.Preset preset)
        {
            driveStopsUponVesselSwitch = true;
            autoJump = false;
            multipleDriveExponent = 1.4f;
        }

        public override bool Enabled(MemberInfo member, GameParameters parameters)
        {
            return true;
        }

        public override bool Interactible(MemberInfo member, GameParameters parameters)
        {
            return true;
        }

        public override IList ValidValues(MemberInfo member)
        {
            return null;
        }
    }

}
