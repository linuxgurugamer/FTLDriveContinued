﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace ScienceFoundry.FTL
{
    [KSPAddon(KSPAddon.Startup.SpaceCentre, true)]
    public class SaveLoad : MonoBehaviour
    {
        //public SaveLoad Instance;
        public static String ROOT_PATH;
        private static String CONFIG_BASE_FOLDER;
        private static String FTL_BASE_FOLDER;
        private static String FTL_CFG_FOLDER;
        private static String FTL_CFG_FILE;
        private static String FTL_NODE;

        public static Vector2 editor;
        public static Vector2 flight;

        public static bool editorOK;
        public static bool flightOK;

        static string SafeLoad(string value, float oldvalue)
        {
            if (value == null)
                return oldvalue.ToString();
            return value;
        }

        static void Awake()
        {
            ROOT_PATH = KSPUtil.ApplicationRootPath;
            CONFIG_BASE_FOLDER = ROOT_PATH + "GameData/";
            FTL_BASE_FOLDER = CONFIG_BASE_FOLDER + "FTLDriveContinued/";
            FTL_CFG_FOLDER = FTL_BASE_FOLDER + "PluginData";
            FTL_CFG_FILE = FTL_CFG_FOLDER + "/FTL.cfg";
            FTL_NODE = "FTL";
        }

        void Start()
        {
            //Instance = this;
            Load();
            DontDestroyOnLoad(this);
            //GameEvents.onGameSceneLoadRequested
        }
        static void OnDestroy()
        {
            Save();
        }
        public static void SaveEditorPos(Rect winpos)
        {
            editor.x = winpos.x;
            editor.y = winpos.y;
            editorOK = true;
        }
        public static void SaveFlightPos(Rect winpos)
        {
            flight.x = winpos.x;
            flight.y = winpos.y;
            flightOK = true;
        }
        public static void Save()
        {

            ConfigNode configFile = new ConfigNode();
            ConfigNode node = new ConfigNode(FTL_NODE);
            if (editorOK)
                node.AddValue("editor", editor);
            if (flightOK)
                node.AddValue("flight", flight);
            configFile.AddNode(FTL_NODE, node);
            if (!Directory.Exists(FTL_CFG_FOLDER))
            {
                try
                {
                    Directory.CreateDirectory(FTL_CFG_FOLDER);
                }
                catch (Exception e)
                {
                    Debug.Log("Error creating folder: " + e.Message);
                }
            }
            if (Directory.Exists(FTL_CFG_FOLDER))
                configFile.Save(FTL_CFG_FILE);
        }

        public static void Load()
        {
            ConfigNode configFile = ConfigNode.Load(FTL_CFG_FILE);
            if (configFile != null)
            {
                ConfigNode node = configFile.GetNode(FTL_NODE);

                editorOK = node.TryGetValue("editor", ref editor);
                flightOK = node.TryGetValue("flight", ref flight);
            }
        }

    }
}
