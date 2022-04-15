using System.Collections.Generic;
using UnityEngine;

namespace RocketSoundEnhancement
{
    public struct LowpassFilterPreset
    {
        public float InteriorMufflingAtm;
        public float InteriorMufflingVac;
        public float VacuumMuffling;
    }

    public static class AudioMuffler
    {
        public static Dictionary<string, LowpassFilterPreset> Presets = new Dictionary<string, LowpassFilterPreset>();
        public static LowpassFilterPreset DefaultLowpassFilterPreset
        {
            get {
                var defaultPreset = new LowpassFilterPreset {
                    InteriorMufflingAtm = 3000,
                    InteriorMufflingVac = 1500,
                    VacuumMuffling = 22200,
                };
                return defaultPreset;
            }
        }

        public static bool EnableMuffling = true;
        public static bool AirSimulation = false;
        public static bool AffectChatterer = false;
        public static string Preset;
        public static float InteriorMufflingAtm = 3000;
        public static float InteriorMufflingVac = 1500;
        public static float VacuumMuffling = 22200;

        public static void ApplyPreset()
        {
            if(Preset != string.Empty && Presets.ContainsKey(Preset)) {
                InteriorMufflingAtm = Presets[Preset].InteriorMufflingAtm;
                InteriorMufflingVac = Presets[Preset].InteriorMufflingVac;
                VacuumMuffling = Presets[Preset].VacuumMuffling;
                Debug.Log("[RSE]: Audio Muffler: " + Preset + " Preset Applied");
            } else {
                Default();
                Debug.Log("[RSE]: Audio Muffler: Preset Not Found = " + Preset + ". Using Default Settings");
            }
        }

        public static void Default()
        {
            InteriorMufflingAtm = DefaultLowpassFilterPreset.InteriorMufflingAtm;
            InteriorMufflingVac = DefaultLowpassFilterPreset.InteriorMufflingVac;
            VacuumMuffling = DefaultLowpassFilterPreset.VacuumMuffling;

            if(!Presets.ContainsKey("Custom")) {
                Presets.Add("Custom", DefaultLowpassFilterPreset);
            }
        }
    }
}
