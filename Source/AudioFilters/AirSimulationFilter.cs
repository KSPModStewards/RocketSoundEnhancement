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
