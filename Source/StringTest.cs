using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using UnityEngine;

//
// Class used to test the speed of the various methods used to concatenate strings together
//
#if false
namespace ScienceFoundry.FTL
{
    [KSPAddon(KSPAddon.Startup.SpaceCentre, true)]
    public class StringTest : MonoBehaviour
    {
        string[] strings;
        string[] results = new string[100];

        void TestConcatenation(int num)
        {
            for (int i = 0; i < num; i++)
            {
                results[i] = "This is test #" + i + " with a result of " + strings[i];
            }
        }
        
        void testSTringBuilder(int num)
        {
            // build strings using StringBuilder
            for (int i = 0; i < num; i++)
            {
                var builder = new StringBuilder();
                builder.Append("This is test #");
                builder.Append(i);
                builder.Append(" with a result of ");
                builder.Append(strings[i]);
                results[i] = builder.ToString();
            }
        }

        void testFormat(int num)
        {
            // build strings using string.Format()
            for (int i = 0; i < num; i++)
            {
                results[i] = string.Format("This is test #{0} with a result of {1}", i, strings[i]);
            }
        }

        void createStringList(int num)
        {
            strings = new string[num];
            for (int i = 0; i < num; i++)
            {
                string s = "";
                for (int x = 0; x < i; x++)
                    s += x.ToString();
                strings[i] = s;
            }
        }


        void TimeTest(string tstName,int multiplier,  Action<int> f1)
        {
            long start, stop, other;
            Stopwatch timer = new Stopwatch();
            for (int tstCnt = 1; tstCnt < 10; tstCnt++)
            {
                createStringList(tstCnt);

                // get the total amount of memory used, true tells it to run GC first.
                start = System.GC.GetTotalMemory(true);

                // restart the timer
                timer.Reset();
                timer.Start();
                for (int i = 0; i < multiplier; i++)
                    f1(tstCnt);

                // get the current amount of memory, stop the timer, then get memory after GC.
                stop = System.GC.GetTotalMemory(false);
                timer.Stop();
                other = System.GC.GetTotalMemory(true);
                WriteResults(tstName, tstCnt, start, stop, other, timer);
                strings = null;
            }
        }

        public void DoTests(int multiplier)
        {
            Action<int> f;

            f = TestConcatenation;
            TimeTest("Operator +", multiplier, f);

            f = testFormat;
            TimeTest("Format", multiplier, f);

            f = testSTringBuilder;
            TimeTest("StringBuilder", multiplier, f);
        }

        void WriteResults(string s, int cnt, long start, long stop, long other, Stopwatch timer)
        {
            string result = String.Format("{0}, #: {1}, Memory: {2}/{3} - {4}  Time: {5} ", s, cnt.ToString(), start.ToString(), stop.ToString(), other.ToString(), timer.ElapsedMilliseconds.ToString());
            UnityEngine.Debug.Log("Timing results: " + result);
        }

        void Start()
        {
            UnityEngine.Debug.Log("Beginning timing tests");
            DoTests(100000);
            UnityEngine.Debug.Log("Completed timing tests");
        }
    }
}

#endif