using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace RocketSoundEnhancement
{
    public struct LowpassFilterPreset
    {
        public float InteriorMufflingAtm;
        public float InteriorMufflingVac;
        public float VacuumMuffling;
    }

    /// <summary>
    /// Thanks to Iron-Warrior for source: https://forum.unity.com/threads/custom-low-pass-filter-using-onaudiofilterread.976326/
    /// </summary>
    [RequireComponent(typeof(AudioBehaviour))]
    public class LowpassFilter : MonoBehaviour
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

        private float[] inputHistoryLeft = new float[2];
        private float[] inputHistoryRight = new float[2];

        private float[] outputHistoryLeft = new float[3];
        private float[] outputHistoryRight = new float[3];

        private float c, a1, a2, a3, b1, b2;

        public float cutoffFrequency = 22200;
        public float lowpassResonanceQ = 3;

        int SampleRate;

        private void Awake()
        {
            SampleRate = AudioSettings.outputSampleRate;

            inputHistoryLeft[1] = 0;
            inputHistoryLeft[0] = 0;

            outputHistoryLeft[2] = 0;
            outputHistoryLeft[1] = 0;
            outputHistoryLeft[0] = 0;

            inputHistoryRight[1] = 0;
            inputHistoryRight[0] = 0;

            outputHistoryRight[2] = 0;
            outputHistoryRight[1] = 0;
            outputHistoryRight[0] = 0;
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            for(int i = 0; i < data.Length; i++) {
                data[i] = AddInput(data[i], i);
            }
        }

        float AddInput(float newInput, int index)
        {
            float finalCutOff = Mathf.Clamp(cutoffFrequency, 0, 22200);
            float finalResonance = Mathf.Clamp(lowpassResonanceQ, 0.5f, 10);

            //fadeout below 10hz;
            if(finalCutOff <= 10) {
                newInput *= (finalCutOff / 10f);
                finalCutOff = 10;
            }

            c = 1.0f / (float)Mathf.Tan(Mathf.PI * finalCutOff / SampleRate);
            a1 = 1.0f / (1.0f + finalResonance * c + c * c);
            a2 = 2f * a1;
            a3 = a1;
            b1 = 2.0f * (1.0f - c * c) * a1;
            b2 = (1.0f - finalResonance * c + c * c) * a1;

            float newOutput = 0;
            if(index % 2 == 0) {
                newOutput = a1 * newInput + a2 * inputHistoryLeft[0] + a3 * inputHistoryLeft[1] - b1 * outputHistoryLeft[0] - b2 * outputHistoryLeft[1];

                inputHistoryLeft[1] = inputHistoryLeft[0];
                inputHistoryLeft[0] = newInput;

                outputHistoryLeft[2] = outputHistoryLeft[1];
                outputHistoryLeft[1] = outputHistoryLeft[0];
                outputHistoryLeft[0] = newOutput;
            } else {
                newOutput = a1 * newInput + a2 * inputHistoryRight[0] + a3 * inputHistoryRight[1] - b1 * outputHistoryRight[0] - b2 * outputHistoryRight[1];

                inputHistoryRight[1] = inputHistoryRight[0];
                inputHistoryRight[0] = newInput;

                outputHistoryRight[2] = outputHistoryRight[1];
                outputHistoryRight[1] = outputHistoryRight[0];
                outputHistoryRight[0] = newOutput;
            }

            return newOutput;
        }
    }
}
