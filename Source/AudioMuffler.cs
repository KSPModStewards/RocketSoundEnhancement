using System.Collections.Generic;
using UnityEngine;

namespace RocketSoundEnhancement
{
    public struct LowpassFilterPreset
    {
        public float InteriorMuffling;
        public float ExteriorMuffling;
    }

    public enum AudioMufflerQuality
    {
        Lite = 0,
        Full = 1,
        AirSim = 2
    }

    public static class AudioMuffler
    {
        public static Dictionary<string, LowpassFilterPreset> Presets = new Dictionary<string, LowpassFilterPreset>();
        public static LowpassFilterPreset DefaultLowpassFilterPreset
        {
            get {
                var defaultPreset = new LowpassFilterPreset {
                    InteriorMuffling = 1500,
                    ExteriorMuffling = 22200,
                };
                return defaultPreset;
            }
        }

        public static bool EnableMuffling = true;
        public static AudioMufflerQuality MufflerQuality = AudioMufflerQuality.Lite;
        public static bool AffectChatterer = false;
        public static string Preset;
        public static float InteriorMuffling = 1500;
        public static float ExteriorMuffling = 22200;

        public static void ApplyPreset()
        {
            if(Preset != string.Empty && Presets.ContainsKey(Preset)) {
                InteriorMuffling = Presets[Preset].InteriorMuffling;
                ExteriorMuffling = Presets[Preset].ExteriorMuffling;
                Debug.Log("[RSE]: Audio Muffler: " + Preset + " Preset Applied");
            } else {
                Default();
                Debug.Log("[RSE]: Audio Muffler: Preset Not Found = " + Preset + ". Using Default Settings");
            }
            Debug.Log("[RSE]: Audio Muffler: Quality = [" + MufflerQuality.ToString() + "]");
        }

        public static void Default()
        {
            InteriorMuffling = DefaultLowpassFilterPreset.InteriorMuffling;
            ExteriorMuffling = DefaultLowpassFilterPreset.ExteriorMuffling;

            if(!Presets.ContainsKey("Custom")) {
                Presets.Add("Custom", DefaultLowpassFilterPreset);
            }
        }
    }
}
