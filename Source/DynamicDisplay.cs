using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ScienceFoundry.FTL
{
    public class DynamicDisplay : PartModule
    {
        public int statuscount = 0;

        [KSPField(guiActive = false, guiActiveEditor = false)]
        public string status01;
        [KSPField(guiActive = false, guiActiveEditor = false)]
        public string status02;
        [KSPField(guiActive = false, guiActiveEditor = false)]
        public string status03;
        [KSPField(guiActive = false, guiActiveEditor = false)]
        public string status04;
        [KSPField(guiActive = false, guiActiveEditor = false)]
        public string status05;
        [KSPField(guiActive = false, guiActiveEditor = false)]
        public string status06;
        [KSPField(guiActive = false, guiActiveEditor = false)]
        public string status07;
        [KSPField(guiActive = false, guiActiveEditor = false)]
        public string status08;
        [KSPField(guiActive = false, guiActiveEditor = false)]
        public string status09;
        [KSPField(guiActive = false, guiActiveEditor = false)]
        public string status10;

        public void AppendLabel(string name, string value)
        {
            switch (statuscount)
            {
                case 0:
                    {
                        Fields["status01"].guiActive = true;
                        Fields["status01"].guiActiveEditor = true;
                        Fields["status01"].guiName = name;
                        Fields.SetValue("status01", value);
                        statuscount++;
                        break;
                    }
                case 1:
                    {
                        Fields["status02"].guiActive = true;
                        Fields["status02"].guiActiveEditor = true;
                        Fields["status02"].guiName = name;
                        Fields.SetValue("status02", value);
                        statuscount++;
                        break;
                    }
                case 2:
                    {
                        Fields["status03"].guiActive = true;
                        Fields["status03"].guiActiveEditor = true;
                        Fields["status03"].guiName = name;
                        Fields.SetValue("status03", value);
                        statuscount++;
                        break;
                    }
                case 3:
                    {
                        Fields["status04"].guiActive = true;
                        Fields["status04"].guiActiveEditor = true;
                        Fields["status04"].guiName = name;
                        Fields.SetValue("status04", value);
                        statuscount++;
                        break;
                    }
                case 4:
                    {
                        Fields["status05"].guiActive = true;
                        Fields["status05"].guiActiveEditor = true;
                        Fields["status05"].guiName = name;
                        Fields.SetValue("status05", value);
                        statuscount++;
                        break;
                    }
                case 5:
                    {
                        Fields["status06"].guiActive = true;
                        Fields["status06"].guiActiveEditor = true;
                        Fields["status06"].guiName = name;
                        Fields.SetValue("status06", value);
                        statuscount++;
                        break;
                    }
                case 6:
                    {
                        Fields["status07"].guiActive = true;
                        Fields["status07"].guiActiveEditor = true;
                        Fields["status07"].guiName = name;
                        Fields.SetValue("status07", value);
                        statuscount++;
                        break;
                    }
                case 7:
                    {
                        Fields["status08"].guiActive = true;
                        Fields["status08"].guiActiveEditor = true;
                        Fields["status08"].guiName = name;
                        Fields.SetValue("status08", value);
                        statuscount++;
                        break;
                    }
                case 8:
                    {
                        Fields["status09"].guiActive = true;
                        Fields["status09"].guiActiveEditor = true;
                        Fields["status09"].guiName = name;
                        Fields.SetValue("status09", value);
                        statuscount++;
                        break;
                    }
                case 9:
                    {
                        Fields["status10"].guiActive = true;
                        Fields["status10"].guiActiveEditor = true;
                        Fields["status10"].guiName = name;
                        Fields.SetValue("status10", value);
                        statuscount++;
                        break;
                    }
            }
        }

        public void ClearLabels()
        {
            Fields["status01"].guiActive = false;
            Fields["status01"].guiActiveEditor = false;
            Fields["status02"].guiActive = false;
            Fields["status02"].guiActiveEditor = false;
            Fields["status03"].guiActive = false;
            Fields["status03"].guiActiveEditor = false;
            Fields["status04"].guiActive = false;
            Fields["status04"].guiActiveEditor = false;
            Fields["status05"].guiActive = false;
            Fields["status05"].guiActiveEditor = false;
            Fields["status06"].guiActive = false;
            Fields["status06"].guiActiveEditor = false;
            Fields["status07"].guiActive = false;
            Fields["status07"].guiActiveEditor = false;
            Fields["status08"].guiActive = false;
            Fields["status08"].guiActiveEditor = false;
            Fields["status09"].guiActive = false;
            Fields["status09"].guiActiveEditor = false;
            Fields["status10"].guiActive = false;
            Fields["status10"].guiActiveEditor = false;
            statuscount = 0;
        }

    }
}
