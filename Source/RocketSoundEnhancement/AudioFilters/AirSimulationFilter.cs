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
        public float MaxDistance { get; set; } = 500;
        public float FarLowpass { get; set; }
        public float MaxCombDelay { get; set; }
        public float MaxCombMix { get; set; }
        public float MaxDistortion { get; set; }
        public float AngleHighpass { get; set; }


        // Simulation Inputs
        public float Distance = 0;
        public float Mach = 0;
        public float Angle = 0;
        public float MachAngle = 90;
        public float MachPass = 1;

        // Filter Controls
        public float CombDelay = 0;
        public float CombMix = 0;
        public float LowpassFrequency = 22000;
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
        }

        public void SetFilterProperties()
        {
            EnableLowpassFilter = true;
            SimulationUpdate = AirSimulationUpdate.Basic;
            MaxDistance = Settings.AirSimMaxDistance;
            FarLowpass = Settings.AirSimFarLowpass;

            Distance = Vector3.Distance(CameraManager.GetCurrentCamera().transform.position, transform.position);
        }

        private void LateUpdate()
        {
            SetFilterProperties();
            UpdateFilters();
        }

        private void UpdateFilters()
        {
            if (SimulationUpdate != AirSimulationUpdate.None)
            {
                distanceLog = (1 - Mathf.Log10(Mathf.Lerp(0.1f, 10, Mathf.Clamp01(Distance / MaxDistance)))) * 0.5f;
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
                    LowpassFrequency = Mathf.Lerp(FarLowpass, 22000, distanceLog);
                    HighPassFrequency = AngleHighpass * anglePositive;

                    if (SimulationUpdate == AirSimulationUpdate.Full)
                    {
                        LowpassFrequency *= machPass;
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
                float clp = 1.0f / Mathf.Tan(Mathf.PI * Mathf.Clamp(LowpassFrequency, 10, 22000) / sampleRate);

                float r = Mathf.Sqrt(2);
                alp[0] = 1.0f / (1.0f + r * clp + clp * clp);
                alp[1] = 2 * alp[0];
                alp[2] = alp[0];
                alp[3] = 2.0f * (1.0f - clp * clp) * alp[0];
                alp[4] = (1.0f - r * clp + clp * clp) * alp[0];

                if (AngleHighpass > 0)
                {
                    float chp = Mathf.Tan(Mathf.PI * Mathf.Clamp(HighPassFrequency, 0, 22000) / sampleRate);
                    chp = (chp + r) * chp;
                    ahp[0] = 1.0f / (1.0f + chp);
                    ahp[1] = (1.0f - chp);
                }
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
            float output = buffer[delay] * CombMix;

            counter++;
            if (counter >= buffer.Length)
            {
                counter = 0;
            }

            return input + output;
        }
        #endregion

        #region LowpassHighpass Filter
        // source: https://www.musicdsp.org/en/latest/Filters/38-lp-and-hp-filter.html
        float[] alp = new float[5];
        float[] inlpl = new float[2], inlpr = new float[2];
        float[] outlpl = new float[2], outlpr = new float[2];

        float[] ahp = new float[2];
        float[] inhpl = new float[2], inhpr = new float[2];
        float[] outhpl = new float[2], outhpr = new float[2];
        private float LowpassHighpassFilter(float input, int index)
        {
            float outputlp, outputhp;

            if (index % 2 == 0)
            {
                outputlp = alp[0] * input + alp[1] * inlpl[0] + alp[2] * inlpl[1] - alp[3] * outlpl[0] - alp[4] * outlpl[1];

                inlpl[1] = inlpl[0];
                inlpl[0] = input;

                outlpl[1] = outlpl[0];
                outlpl[0] = outputlp;

                if (AngleHighpass > 0)
                {
                    outputhp = (ahp[0] * outhpl[1] + outputlp - inhpl[1]) * ahp[1];

                    inhpl[1] = inhpl[0];
                    inhpl[0] = outputlp;

                    outhpl[1] = outhpl[0];
                    outhpl[0] = outputhp;

                    return outputhp;
                }

                return outputlp;
            }

            outputlp = alp[0] * input + alp[1] * inlpr[0] + alp[2] * inlpr[1] - alp[3] * outlpr[0] - alp[4] * outlpr[1];

            inlpr[1] = inlpr[0];
            inlpr[0] = input;

            outlpr[1] = outlpr[0];
            outlpr[0] = outputlp;

            if (AngleHighpass > 0)
            {
                outputhp = (ahp[0] * outhpr[1] + outputlp - inhpr[1]) * ahp[1];

                inhpr[1] = inhpr[0];
                inhpr[0] = outputlp;

                outhpr[1] = outhpr[0];
                outhpr[0] = outputhp;

                return outputhp;
            }

            return outputlp;
        }
        #endregion

        private void OnDisable()
        {
            if (distortionFilter != null) distortionFilter.enabled = false;

            Array.Clear(buffer, 0, buffer.Length);

            Array.Clear(inlpl, 0, inlpl.Length);
            Array.Clear(inlpr, 0, inlpr.Length);
            Array.Clear(outlpl, 0, outlpl.Length);
            Array.Clear(outlpl, 0, outlpl.Length);

            Array.Clear(inhpl, 0, inhpl.Length);
            Array.Clear(inhpr, 0, inhpr.Length);
            Array.Clear(outhpl, 0, outhpl.Length);
            Array.Clear(outhpr, 0, outhpr.Length);

            counter = 0;

        }

        private void OnEnable()
        {
            if (distortionFilter != null) distortionFilter.enabled = true;
        }
    }
}
