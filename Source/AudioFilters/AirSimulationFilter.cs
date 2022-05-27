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
        public bool EnableWaveShaperFilter { get; set; }
        public AirSimulationUpdate SimulationUpdate { get; set; }
        public float MaxDistance { get; set; } = AudioMuffler.AirSimMaxDistance;
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

        int SampleRate;
        float lowpassFade;
        double combDelaySamples;
        AudioSource source;
        bool sourceActiveAndEnabled;

        float distanceLog, machVelocityClamped, angleAbsolute, anglePositive, machPass;

        void Awake()
        {
            SampleRate = AudioSettings.outputSampleRate;
            source = GetComponent<AudioSource>();
        }

        public void LateUpdate()
        {
            sourceActiveAndEnabled = source != null && source.enabled && source.isPlaying;
            if (!sourceActiveAndEnabled) return;

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
                        LowpassFrequency *= Mathf.Max(machPass, 0.05f);
                        HighPassFrequency = Mathf.Lerp(0, AngleHighPass * (1 + (machVelocityClamped * 2f)), anglePositive);
                    }
                    else
                    {

                        HighPassFrequency = AngleHighPass * anglePositive;
                    }
                }

                if (EnableWaveShaperFilter)
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
            if (EnableCombFilter) { combDelaySamples = CombDelay * SampleRate / 1000; }
            #endregion

            #region LowpassHighpassFilter Update
            if (EnableLowpassFilter)
            {
                freqLP = Mathf.Clamp(LowpassFrequency, 20, 22000) * 2 / SampleRate;
                freqHP = Mathf.Clamp(HighPassFrequency, 20, 22000) * 2 / SampleRate;
                fbLP = 0; // q + q / (1 - freqLP);
                fbHP = 0; // q + q / (1 - freqHP);

                lowpassFade = LowpassFrequency <= 50 ?
                    Mathf.Pow(2, Mathf.Lerp(-80, 0, LowpassFrequency / 50f) / 6) : 1;
            }
            #endregion

            #region Waveshaper Update
            if (EnableWaveShaperFilter)
            {
                float wsamount = Mathf.Min(Distortion, 0.999f);
                wsK = 2 * wsamount / (1 - wsamount);
            }
            #endregion
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            if (!sourceActiveAndEnabled) return;
            
            for(int i = 0; i < data.Length; i++) {
                if(EnableLowpassFilter) data[i] *= lowpassFade;
                if(EnableCombFilter) {
                    CombFilter(ref data[i]);
                }
                if(EnableLowpassFilter) {
                    LowpassHighpassFilter(ref data[i], i);
                }
                if(EnableWaveShaperFilter) {
                    Waveshaper(ref data[i]);
                }
            }
        }

        #region Time Variable Delay / Comb Filter
        //Flexible-time, non-sample quantized delay , can be used for stuff like waveguide synthesis or time-based(chorus/flanger) fx.
        //Source = https://www.musicdsp.org/en/latest/Effects/98-class-for-waveguide-delay-effects.html
        float[] buffer = new float[4096];
        int counter = 0;
        public void CombFilter(ref float input)
        {
            //float wetMix = Mathf.Min(CombMix, 0.8f);
            try {
                double back = (double)counter - combDelaySamples;

                // clip lookback buffer-bound
                if(back < 0.0)
                    back = buffer.Length + back;

                // compute interpolation left-floor
                int index0 = (int)Math.Floor(back);

                // compute interpolation right-floor
                int index_1 = index0 - 1;
                int index1 = index0 + 1;
                int index2 = index0 + 2;

                // clip interp. buffer-bound
                if(index_1 < 0) index_1 = buffer.Length - 1;
                if(index1 >= buffer.Length) index1 = 0;
                if(index2 >= buffer.Length) index2 = 0;

                // get neighbourgh samples
                float y_1 = buffer[index_1];
                float y0 = buffer[index0];
                float y1 = buffer[index1];
                float y2 = buffer[index2];

                // compute interpolation x
                float x = (float)back - index0;

                // calculate
                float c0 = y0;
                float c1 = 0.5f * (y1 - y_1);
                float c2 = y_1 - 2.5f * y0 + 2.0f * y1 - 0.5f * y2;
                float c3 = 0.5f * (y2 - y_1) + 1.5f * (y0 - y1);

                float combOutput = ((c3 * x + c2) * x + c1) * x + c0;

                // add to delay buffer
                //buffer[counter] = input + output * 0.12f;
                buffer[counter] = input;

                // increment delay counter
                counter++;

                // clip delay counter
                if(counter >= buffer.Length)
                    counter = 0;

                input += (combOutput * CombMix);
            } catch {
                ClearCombFilter();
            }

        }

        public void ClearCombFilter()
        {
            Array.Clear(buffer, 0, buffer.Length);
            counter = 0;
        }
        #endregion

        #region LowpassHighpass Filter
        // source: https://www.musicdsp.org/en/latest/Filters/29-resonant-filter.html
        float buf0L, buf1L, buf0R, buf1R;
        float buf2L, buf3L, buf2R, buf3R, hpL, hpR;
        float freqLP, freqHP, fbLP, fbHP;
        public void LowpassHighpassFilter(ref float input, int index)
        {
            float newOutput = input;
            if(index % 2 == 0) {
                buf0L += freqLP * (input - buf0L + fbLP * (buf0L - buf1L));
                buf1L += freqLP * (buf0L - buf1L);

                newOutput = buf1L;
                if(freqHP > 0) {
                    hpL = buf1L - buf2L;
                    buf2L += freqHP * (hpL + fbHP * (buf2L - buf3L));
                    buf3L += freqHP * (buf2L - buf3L);
                    newOutput = hpL;
                }
            } else {
                buf0R += freqLP * (input - buf0R + fbLP * (buf0R - buf1R));
                buf1R += freqLP * (buf0R - buf1R);

                newOutput = buf1R;
                if(freqHP > 0) {
                    hpR = buf1R - buf2R;
                    buf2R += freqHP * (hpR + fbHP * (buf2R - buf3R));
                    buf3R += freqHP * (buf2R - buf3R);
                    newOutput = hpR;
                }
            }
            input = newOutput;
        }
        #endregion

        #region Waveshaper
        // Source: https://www.musicdsp.org/en/latest/Effects/46-waveshaper.html
        float wsK;

        public void Waveshaper(ref float input)
        {
            input = Mathf.Min(Mathf.Max(input, -1), 1);
            input = (1 + wsK) * input / (1 + wsK * Mathf.Abs(input));
        }
        #endregion
    }
}
