using System.Collections.Generic;
using UnityEngine;

namespace ScienceFoundry.FTL
{
    static class LogsManager
    {
        private static void DisplayMsg(params string msgparts)
        {
            string msg = "[" + KSPUtil.PrintTimeStamp(FlightLogger.met) + "] FTLDriveContinued: " + String.Join("", msgparts);
            string msgshort = String.Join("", msgparts);

            ScreenMessages.PostScreenMessage(msgshort, 4f, ScreenMessageStyle.UPPER_CENTER);
            Debug.Log(msg);
        }

        private static void FlightLog(params string msgparts)
        {
            string msg = "[" + KSPUtil.PrintTimeStamp(FlightLogger.met) + "] FTLDriveContinued: " + String.Join("", msgparts);

            FlightLogger.eventLog.Add(msg);
            Debug.Log(msg);
        }

        private static void ErrorLog(params string msgparts)
        {
            string msg = "[" + KSPUtil.PrintTimeStamp(FlightLogger.met) + "] FTLDriveContinued: " + String.Join("", msgparts);

            Debug.Log(msg);
            UnityEngine.Debug.Log(msg);
        }

    }
}
