using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ScienceFoundry.FTL
{
    static class LogsManager
    {
        public static void DisplayMsg(params object[] msgparts)
        {
            string msg = "[" + KSPUtil.PrintTimeStamp(FlightLogger.met) + "] FTLDriveContinued: " + string.Join("", msgparts.Select(o => o.ToString()).ToArray());
            string msgshort = string.Join("", msgparts.Select(o => o.ToString()).ToArray());

            ScreenMessages.PostScreenMessage(msgshort, 4f, ScreenMessageStyle.UPPER_CENTER);
            Debug.Log(msg);
        }

        public static void FlightLog(params object[] msgparts)
        {
            string msg = "[" + KSPUtil.PrintTimeStamp(FlightLogger.met) + "] FTLDriveContinued: " + string.Join("", msgparts.Select(o => o.ToString()).ToArray());

            FlightLogger.eventLog.Add(msg);
            Debug.Log(msg);
        }

        public static void ErrorLog(params object[] msgparts)
        {
            string msg = "[" + KSPUtil.PrintTimeStamp(FlightLogger.met) + "] FTLDriveContinued: " + string.Join("", msgparts.Select(o => o.ToString()).ToArray());

            Debug.Log(msg);
        }
    }
}
