using System;
using UnityEngine;
using UnityEngine.Profiling;

namespace RocketSoundEnhancement.AudioFilters
{
    public enum AirSimulationUpdate
    {
        Full,
        Basic,
        None
    }

    [RequireComponent(typeof(AudioBehaviour))]
    public class AirSimulationFilter : MonoBehaviour
    {
        // Simulation Settings
        public bool EnableCombFilter { get; set; }
        public bool EnableLowpassFilter { get; set; }
        public bool EnableDistortionFilter { get; set; }
        public AirSimulationUpdate SimulationUpdate { get; set; }
        public float MaxDistance { get; set; } = Settings.AirSimMaxDistance;
        public float FarLowpass { get; set; } = 2500;
        public float AngleHighPass { get; set; } = 0;
        public float MaxCombDelay { get; set; } = 20;
        public float MaxCombMix { get; set; } = 0.25f;
        public float MaxDistortion { get; set; } = 0.5f;

        // Simulation Inputs
        public float Distance = 0;
        public float Mach = 0;
        public float Angle = 0;
        public float MachAngle = 90;
        public float MachPass = 1;

        // Filter Controls
        public float CombDelay = 0;
        public float CombMix = 0;
        public float LowpassFrequency = 22200;
        public float HighPassFrequency = 0;
        public float Distortion = 0;

        private int sampleRate;
        private double delaySamples;

        private float distanceLog, machVelocityClamped, angleAbsolute, anglePositive, machPass;

        private AudioDistortionFilter distortionFilter;

        private void Awake()
        {
            sampleRate = AudioSettings.outputSampleRate;

            if (EnableDistortionFilter)
            {
                distortionFilter = gameObject.AddComponent<AudioDistortionFilter>();
                distortionFilter.enabled = enabled;
            }

            InvokeRepeating("UpdateFilters", 0, 0.02f);
        }

        private void UpdateFilters()
        {
            if (SimulationUpdate != AirSimulationUpdate.None)
            {
                distanceLog = Mathf.Pow(1 - Mathf.Clamp01(Distance / MaxDistance), 10);
                anglePositive = Mathf.Clamp01((Angle - MachAngle) / MachAngle);

                if (SimulationUpdate == AirSimulationUpdate.Full)
                {
                    machPass = Mathf.Clamp01(MachPass / Mathf.Lerp(0.1f, 1f, Distance / 100));
                    machVelocityClamped = Mathf.Clamp01(Mach);
                    angleAbsolute = 1 - Mathf.Clamp01(Angle / 180);
                }

                if (EnableCombFilter)
                {
                    CombDelay = MaxCombDelay * distanceLog;
                    CombMix = Mathf.Lerp(MaxCombMix, 0, distanceLog);
                }

                if (EnableLowpassFilter)
                {
                    LowpassFrequency = Mathf.Lerp(FarLowpass, 22500, distanceLog);
                    if (SimulationUpdate == AirSimulationUpdate.Full)
                    {
                        if (Settings.MachEffectsAmount > 0)
                        {
                            LowpassFrequency *= Mathf.Lerp(Settings.MachEffectLowerLimit, 1, machPass);
                        }
                        HighPassFrequency = Mathf.Lerp(0, AngleHighPass * (1 + (machVelocityClamped * 2f)), anglePositive);
                    }
                    else
                    {
                        HighPassFrequency = AngleHighPass * anglePositive;
                    }
                }

                if (EnableDistortionFilter)
                {
                    if (SimulationUpdate == AirSimulationUpdate.Full)
                    {
                        Distortion = Mathf.Lerp(MaxDistortion, (MaxDistortion * 0.5f) * machVelocityClamped, distanceLog) * angleAbsolute;
                    }
                    else
                    {
                        Distortion = Mathf.Lerp(MaxDistortion, 0, distanceLog);
                    }
                }
            }

            #region Combfilter Update
            if (EnableCombFilter)
            {
                delaySamples = Mathf.FloorToInt(Mathf.Min(CombDelay * sampleRate / 1000, 500000));
            }
            #endregion

            #region LowpassHighpassFilter Update
            if (EnableLowpassFilter)
            {
                float lpcutOff = Mathf.Exp(-2.0f * Mathf.Clamp(LowpassFrequency, 0, 22000) / sampleRate);
                a0 = 1.0f - lpcutOff;
                b0 = -lpcutOff;

                float hpcutOff = Mathf.Exp(-2.0f * Mathf.Clamp(HighPassFrequency, 0, 22000) / sampleRate);
                a1 = 1.0f - hpcutOff;
                b1 = -hpcutOff;
            }
            #endregion

            #region Waveshaper Update
            if (EnableDistortionFilter && distortionFilter != null)
            {
                distortionFilter.distortionLevel = Mathf.Clamp01(Distortion);
            }
            #endregion
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            for (int i = 0; i < data.Length; i++)
            {
                if (EnableCombFilter)
                {
                    data[i] = CombFilter(data[i]);
                }
                if (EnableLowpassFilter)
                {
                    data[i] = LowpassHighpassFilter(Quantize(data[i]), i);
                }
            }
        }

        //removes denormal numbers that causes very high cpu
        private float dn = 1e-18f;
        private float Quantize(float input)
        {
            dn = -dn;
            return (+input) + dn;
        }

        #region Comb Filter
        private float[] buffer = new float[500000];
        private int counter = 0;
        private float CombFilter(float input)
        {
            int delay = counter - (int)delaySamples;
            delay = delay > buffer.Length || delay < 0 ? 0 : delay;

            buffer[counter] = input;
            float output = input + buffer[delay] * CombMix;

            counter++;
            if (counter >= buffer.Length)
            {
                counter = 0;
            }

            return output;
        }
        #endregion

        #region LowpassHighpass Filter
        // source: https://www.musicdsp.org/en/latest/Filters/237-one-pole-filter-lp-and-hp.html
        float lpOutputL, lpOutputR, a0, b0;
        float hpOutputL, hpOutputR, a1, b1;
        private float LowpassHighpassFilter(float input, int index)
        {
            if (index % 2 == 0)
            {
                lpOutputL = a0 * input - b0 * lpOutputL;
                hpOutputL = a1 * lpOutputL - b1 * hpOutputL;

                return lpOutputL - hpOutputL;
            }

            lpOutputR = a0 * input - b0 * lpOutputR;
            hpOutputR = a1 * lpOutputR - b1 * hpOutputR;

            return lpOutputR - hpOutputR;
        }
        #endregion

        private void OnDisable()
        {
            if (distortionFilter != null) distortionFilter.enabled = false;

            Array.Clear(buffer, 0, buffer.Length);
            counter = 0;
            lpOutputL = 0;
            lpOutputR = 0;
            hpOutputL = 0;
            hpOutputR = 0;
        }

        private void OnEnable()
        {
            if (distortionFilter != null) distortionFilter.enabled = true;
        }
    }
}

