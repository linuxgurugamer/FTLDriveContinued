﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using UnityEngine;

//
// Class used to test the speed of the various methods used to concatenate strings together
// IMPORTANT: file not included in project, so it does not run
//
namespace ScienceFoundry.FTL
{
    [KSPAddon(KSPAddon.Startup.SpaceCentre, true)]
    public class StringTest : MonoBehaviour
    {
        void TestConcatenation(int multiplier)
        {
            for (int i = 0; i < multiplier; i++)
            {
                var result = "This is test #" + i.ToString("N1") + " with a result of " + i.ToString("N1");
            }
        }

        void TestFormat(int multiplier)
        {
            for (int i = 0; i < multiplier; i++)
            {
                var result = string.Format("This is test #{0:N1} with a result of {1:N1}", i, i);
            }
        }

        void TestSTringBuilder(int multiplier)
        {
            for (int i = 0; i < multiplier; i++)
            {
                var builder = new StringBuilder();
                builder.Append("This is test #");
                builder.Append(i.ToString("N1"));
                builder.Append(" with a result of ");
                builder.Append(i.ToString("N1"));
                var result = builder.ToString();
            }
        }

        static List<double> data = Enumerable.Range(0,1000000).Cast<double>().ToList();

        void TestFor(int multiplier)
        {
            double sum = 0;
            for (int i = 0; i < data.Count; i++)
            {
                sum += data[i]; 
            }
        }

        void TestForeach(int multiplier)
        {
            double sum = 0;
            foreach (double item in data)
            {
                sum += item;
            }
        }

        void TimeTest(string name, int multiplier, Action<int> func)
        {
            // get the total amount of memory used, true tells it to run GC first.
            long start = GC.GetTotalMemory(true);

            // restart the timer
            Stopwatch timer = Stopwatch.StartNew();
            func(multiplier);

            // get the current amount of memory, stop the timer, then get memory after GC.
            long stop = GC.GetTotalMemory(false);
            timer.Stop();
            long other = GC.GetTotalMemory(true);

            string result = String.Format("{0}  Time: {1} ms/1_000_000calls , Memory: {2}/{3}/{4}",
                name, timer.ElapsedMilliseconds, start, stop, other);
            UnityEngine.Debug.Log("Timing results: " + result);
        }

        public void DoTests(int multiplier)
        {
            TimeTest("Operator +", multiplier, TestConcatenation);
            TimeTest("String.Format", multiplier, TestFormat);
            TimeTest("StringBuilder", multiplier, TestSTringBuilder);

            TimeTest("For loop", multiplier, TestFor);
            TimeTest("Foreach loop", multiplier, TestForeach);
        }

        void Start()
        {
            UnityEngine.Debug.Log("Beginning timing tests");
            DoTests(1000000);
            UnityEngine.Debug.Log("Completed timing tests");
        }
    }
}
