//using System;
using System.Collections.Generic;
//using System.Linq;
//using System.Text;
using UnityEngine;

namespace ScienceFoundry.FTL
{
    class SoundManager
    {
        static SoundManager instance;

        Dictionary<string, AudioClip> Sounds;

        public static bool IsInitialized
        {
            get
            {
                return (instance != null);
            }
        }

        SoundManager()
        {
            Sounds = new Dictionary<string, AudioClip>();
        }

        public static void Initialize()
        {
            if (instance == null)
            {
                instance = new SoundManager();
                //Logger.DebugLog("Sound Manager STARTED.");
            }
        }

        public static void LoadSound(string filePath, string soundName)
        {
            if (instance != null)
            {
                foreach (KeyValuePair<string, AudioClip> pair in instance.Sounds)
                {
                    if (pair.Key == soundName)
                    {
                        return;
                    }
                }

                if (GameDatabase.Instance.ExistsAudioClip(filePath))
                {
                    instance.Sounds.Add(soundName, GameDatabase.Instance.GetAudioClip(filePath));
                    //Logger.DebugLog("Loaded: " + soundName);
                }
                else
                {
                    //Logger.DebugError("ERROR: Sound \"" + soundName + "\" not found in the database!");
                }
            }
            else
            {
                Initialize();
                LoadSound(filePath, soundName);
            }
        }

        public static AudioClip GetSound(string soundName)
        {
            try
            {
                return instance.Sounds[soundName];
            }
            catch
            {
                //Logger.DebugError("ERROR: AudioClip \"" + soundName + "\" not found! Ensure it is being properly loaded.");
                return null;
            }
        }

        public static void CreateFXSound(Part part, FXGroup group, string defaultSound, bool loop, float maxDistance = 30f)
        {
            group.audio = (part == null ? Camera.main : part).gameObject.AddComponent<AudioSource>();

            group.audio.volume = GameSettings.SHIP_VOLUME;
            group.audio.rolloffMode = AudioRolloffMode.Linear;
            group.audio.dopplerLevel = 0f;
            group.audio.spatialBlend = 1f;
            group.audio.maxDistance = maxDistance;
            group.audio.loop = loop;
            group.audio.playOnAwake = false;
            group.audio.clip = GetSound(defaultSound);
        }

    }
}
