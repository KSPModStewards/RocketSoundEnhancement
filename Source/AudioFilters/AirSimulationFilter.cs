using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace RocketSoundEnhancement
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
        public bool EnableCombFilter = false;
        public bool EnableLowpassFilter = false;
        public bool EnableWaveShaperFilter = false;
        public AirSimulationUpdate SimulationUpdate = AirSimulationUpdate.Basic;

        // Simulation Values
        public float Distance = 0;
        public float Mach = 0;
        public float Angle = 0;
        public float MachAngle = 90;
        public float MachPass = 1;

        // Simulation Settings
        public float MaxDistance = 2500;
        public float FarLowpass = 1000f;
        public float AngleHighPass = 500;
        public float MaxCombDelay = 20;
        public float MaxCombMix = 0.25f;
        public float MaxDistortion = 0.5f;
        public float MaxLowpassFrequency = 22200;

        // Filter Controls
        public float CombDelay = 0;
        public float CombMix = 0;
        public float LowpassFrequency = 22200;
        public float HighPassFrequency = 0;
        public float Distortion = 0;

        int SampleRate;
        float lowpassFade;
        double combDelaySamples;
        float combMixClamped;

        void Awake()
        {
            SampleRate = AudioSettings.outputSampleRate;
            InvokeRepeating("UpdateFilters", 0.0f, 0.05f);
        }

        public void UpdateFilters()
        {
            switch(SimulationUpdate) {
                case AirSimulationUpdate.Full:
                    UpdateFiltersFull();
                    break;
                case AirSimulationUpdate.Basic:
                    UpdateFiltersBasic();
                    break;
                case AirSimulationUpdate.None:
                    break;
            }

            lowpassFade = EnableLowpassFilter && LowpassFrequency <= 50 ?
                Mathf.Pow(2, Mathf.Lerp(-80, 0, Mathf.Clamp01(LowpassFrequency / 50f)) / 6) : 1;

            #region Combfilter Update
            combDelaySamples = CombDelay * SampleRate / 1000;
            combMixClamped = Mathf.Clamp01(CombMix);
            #endregion

            #region LowpassHighpassFilter Update
            freqLP = Mathf.Clamp(LowpassFrequency, 20, 22000) * 2 / SampleRate;
            freqHP = Mathf.Clamp(HighPassFrequency, 20, 22000) * 2 / SampleRate;
            fbLP = 0 / (1 - freqLP);
            fbHP = 0 / (1 - freqHP);
            #endregion

            #region Waveshaper Update
            float wsamount = Mathf.Min(Distortion, 0.999f);
            wsK = 2 * wsamount / (1 - wsamount);
            #endregion
        }

        public void UpdateFiltersFull()
        {
            float distanceInv = Mathf.Clamp01(Mathf.Pow(2, -(Distance / MaxDistance * 10)));                                    //  Inverse Distance
            float machVelocityClamped = Mathf.Clamp01(Mach);
            float angleAbs = 1 - Mathf.Clamp01(Angle / 180);

            if(EnableCombFilter) {
                CombDelay = MaxCombDelay * distanceInv;
                CombMix = Mathf.Lerp(MaxCombMix, 0, distanceInv);
            }

            if(EnableLowpassFilter) {
                float anglePos = Mathf.Clamp01((Angle - MachAngle) / MachAngle);                                                //  For Highpass when the camera is at front
                float machPass = Mathf.Clamp01(MachPass / Mathf.Lerp(0.1f, 1f, Mathf.Clamp01(Distance / 100)));                 //  Soften Mach Cone over Distance

                LowpassFrequency = Mathf.Lerp(Mathf.Min(FarLowpass, MaxLowpassFrequency), MaxLowpassFrequency, distanceInv);
                LowpassFrequency *= Mathf.Max(machPass, 0.05f) * RSE.Instance.WindModulation();                                 //  Only make it quieter outside the Cone, don't make it silent.
                HighPassFrequency = Mathf.Lerp(0, AngleHighPass * (1 + (machVelocityClamped * 2f)), anglePos);
            }

            if(EnableWaveShaperFilter) {
                Distortion = Mathf.Lerp(MaxDistortion, (MaxDistortion * 0.5f) * machVelocityClamped, distanceInv) * angleAbs;
            }
        }

        public void UpdateFiltersBasic()
        {
            float distanceInv = Mathf.Clamp01(Mathf.Pow(2, -(Distance / MaxDistance * 10)));

            if(EnableCombFilter) {
                CombDelay = MaxCombDelay * distanceInv;
                CombMix = Mathf.Lerp(MaxCombMix, 0, distanceInv);
            }

            if(EnableLowpassFilter) {
                float anglePos = Mathf.Clamp01((Angle - MachAngle) / MachAngle);

                LowpassFrequency = Mathf.Lerp(Mathf.Min(FarLowpass, MaxLowpassFrequency), MaxLowpassFrequency, distanceInv);
                LowpassFrequency *= RSE.Instance.WindModulation();
                HighPassFrequency = Mathf.Lerp(0, AngleHighPass, anglePos);
            }

            if(EnableWaveShaperFilter) {
                Distortion = Mathf.Lerp(MaxDistortion, 0, distanceInv);
            }
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            for(int i = 0; i < data.Length; i++) {
                data[i] *= lowpassFade;
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

                input += (combOutput * combMixClamped);
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
