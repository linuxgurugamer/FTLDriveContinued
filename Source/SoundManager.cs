using System.Collections.Generic;
using UnityEngine;

namespace ScienceFoundry.FTL
{
    static class SoundManager
    {
        private static readonly Dictionary<string, AudioClip> Sounds = new Dictionary<string, AudioClip>();

        public static void LoadSound(string filePath, string soundName)
        {
            foreach (var pair in Sounds)
            {
                if (pair.Key == soundName)
                    return;
            }

            if (GameDatabase.Instance.ExistsAudioClip(filePath))
            {
                Sounds.Add(soundName, GameDatabase.Instance.GetAudioClip(filePath));
            }
        }

        public static AudioClip GetSound(string soundName)
        {
            try
            {
                return Sounds[soundName];
            }
            catch
            {
                return null;
            }
        }

        public static void CreateFXSound(Part part, FXGroup group, string defaultSound, bool loop, float maxDistance = 30f)
        {
            group.audio = (part as UnityEngine.Component ? (UnityEngine.Component)part : Camera.main).gameObject.AddComponent<AudioSource>();
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
