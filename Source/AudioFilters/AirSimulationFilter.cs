using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace RocketSoundEnhancement
{
    [RequireComponent(typeof(AudioBehaviour))]
    public class AirSimulationFilter : MonoBehaviour
    {
        public bool EnableCombFilter = false;
        public bool EnableLowpassFilter = false;
        public bool EnableWaveShaperFilter = false;
        public bool EnableSimulationUpdating = true;

        public float Distance = 0;
        public float Velocity = 0;
        public float Angle = 0;
        public float VesselSize = 1;
        public float SpeedOfSound = 340.29f;
        public float AtmosphericPressurePa = 101325;

        public float MaxAirSimDistance = 2000;
        public float FarLowpass = 1000f;
        public float AngleHighPass = 250;
        public float MaxCombDelay = 20;
        public float MaxCombMix = 0.5f;
        public float FarDistortion = 0.5f;
        public float NearDistortion = 0.7f;

        public float CombDelay = 0;
        public float CombMix = 0;
        public float LowpassFrequency = 22200;
        public float HighPassFrequency = 0;
        public float ResonanceQ = 0.1f;
        public float Distortion = 0;

        int SampleRate;
        void Awake()
        {
            SampleRate = AudioSettings.outputSampleRate;

            if(EnableSimulationUpdating) {
                InvokeRepeating("UpdateFilters", 0, 0.02f);
            }
        }

        public void UpdateFilters()
        {
            float distanceInv = Mathf.Clamp01(Mathf.Pow(2, -(Distance / MaxAirSimDistance * 10)));                          //Inverse Distance
            float machVelocity = (Velocity / SpeedOfSound) * Mathf.Clamp01(AtmosphericPressurePa / 404.1f);                 //Current Mach Tapered by Pressure on Vacuum Approach.
            float machVelocityClamped = Mathf.Clamp01(machVelocity);
            float angleDegrees = (1 + Angle) * 90f;                                                                         //Camera Angle
            float machAngle = Mathf.Asin(1 / Mathf.Max(machVelocity, 1)) * Mathf.Rad2Deg;                                   //Mach Angle
            float anglePos = Mathf.Clamp01((angleDegrees - machAngle) / machAngle) * Mathf.Clamp01(Distance / VesselSize);  //For Highpass when the camera is at front
            float angleAbs = (1 - Angle) * 0.5f;
            float machPass = 1f - Mathf.Clamp01((angleDegrees - 12.5f) / machAngle) * machVelocityClamped;                  //The Mach Cone

            machPass = Mathf.Clamp01(machPass / Mathf.Lerp(0.1f, 1f, Mathf.Clamp01(Distance / 100)));                       //Soften Mach Cone by Distance
            machPass = Mathf.Lerp(1, machPass, Mathf.Clamp01(Distance / VesselSize));                                       //Taper Mach Effects if Near the Vessel.

            LowpassFrequency = Mathf.Lerp(FarLowpass, 22000f, distanceInv) * Mathf.Max(machPass, 0.05f);                    //Only make it quieter outside the Cone, don't make it silent.
            HighPassFrequency = Mathf.Lerp(0, AngleHighPass * (1 + (machVelocityClamped * 2f)), anglePos);
            CombDelay = MaxCombDelay * distanceInv;
            CombMix = Mathf.Lerp(MaxCombMix, MaxCombMix * 0.5f * angleAbs, distanceInv);
            Distortion = Mathf.Lerp(FarDistortion, NearDistortion * machVelocityClamped, distanceInv);
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            double delay = CombDelay * SampleRate / 1000;
            float dryMix = Mathf.Lerp(1, 0.5f, CombMix);
            float wetMix = Mathf.Lerp(0, 0.5f, CombMix);

            for(int i = 0; i < data.Length; i++) {
                if(LowpassFrequency < 20) {
                    data[i] *= (LowpassFrequency / 20);
                }
                if(EnableCombFilter) {
                    CombFilter(ref data[i], delay, dryMix, wetMix);
                }
                if(EnableLowpassFilter) {
                    lowpassHighpassFilter(ref data[i], i, LowpassFrequency, HighPassFrequency, ResonanceQ);
                }
                if(EnableWaveShaperFilter) {
                    Waveshaper(ref data[i], Distortion);
                }
            }
        }

        #region Time Variable Delay / Comb Filter
        //Flexible-time, non-sample quantized delay , can be used for stuff like waveguide synthesis or time-based(chorus/flanger) fx.
        //Source = https://www.musicdsp.org/en/latest/Effects/98-class-for-waveguide-delay-effects.html
        float[] buffer = new float[64000];
        int counter = 0;
        public void CombFilter(ref float input, double delay, float dryMix = 0.5f, float wetMix = 0.5f)
        {
            try {
                if(delay > buffer.Length) {
                    delay = buffer.Length;
                }

                double back = (double)counter - delay;

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
                float x = (float)back - (float)index0;

                // calculate
                float c0 = y0;
                float c1 = 0.5f * (y1 - y_1);
                float c2 = y_1 - 2.5f * y0 + 2.0f * y1 - 0.5f * y2;
                float c3 = 0.5f * (y2 - y_1) + 1.5f * (y0 - y1);

                float output = ((c3 * x + c2) * x + c1) * x + c0;

                // add to delay buffer
                //buffer[counter] = input + output * 0.5f;
                buffer[counter] = input;

                // increment delay counter
                counter++;

                // clip delay counter
                if(counter >= buffer.Length)
                    counter = 0;

                input = (input * dryMix) + (output * wetMix);
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

        #region Lowpass Filter
        // source: https://www.musicdsp.org/en/latest/Filters/29-resonant-filter.html
        float buf0L, buf1L, buf0R, buf1R;
        float buf2L, buf3L, buf2R, buf3R, hpL, hpR;
        public void lowpassHighpassFilter(ref float input, int index, float lowpass = 22000, float highpass = 0, float resQ = 0.8f)
        {
            float freqLP = Mathf.Clamp(lowpass, 20, 22000) * 2 / SampleRate;
            float freqHP = Mathf.Clamp(highpass, 20, 22000) * 2 / SampleRate;
            float res = Mathf.Clamp01(resQ);
            float fbLP = res + res / (1 - freqLP);
            float fbHP = res + res / (1 - freqHP);

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
        public void Waveshaper(ref float input, float amount = 0)
        {
            float amnt = Mathf.Min(amount, 0.999f);
            float k = 2 * amnt / (1 - amnt);

            input = Mathf.Min(Mathf.Max(input, -1), 1);
            input = (1 + k) * input / (1 + k * Mathf.Abs(input));
        }
        #endregion
    }
}
