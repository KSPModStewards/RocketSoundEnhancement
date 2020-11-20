using System;
using System.Reflection;
using UnityEngine;

namespace RocketSoundEnhancement
{
    public class AudioLimiterSettings : GameParameters.CustomParameterNode
    {
        public override string Title { get { return "Audio Limiter"; } }
        public override string Section { get { return "Rocket Sound Enhancement"; } }
        public override string DisplaySection { get { return "Rocket Sound Enhancement"; } }
        public override int SectionOrder { get { return 3; } }
        public override GameParameters.GameMode GameMode { get { return GameParameters.GameMode.ANY; } }
        public override bool HasPresets { get { return false; } }

        [GameParameters.CustomParameterUI("Enable Audio Limiter")]
        public bool EnableLimiter = true;

        [GameParameters.CustomFloatParameterUI("Input Gain", minValue = 0f, maxValue = 5f, displayFormat = "N1")]
        public float Gain = 0.5f;
        [GameParameters.CustomFloatParameterUI("Makeup Gain", minValue = 0f, maxValue = 5f, displayFormat = "N1")]
        public float GainMakeup = 1.5f;
        [GameParameters.CustomIntParameterUI("Window Size (ms)", minValue = 50, maxValue = 500)]
        public int WindowSize = 300;
        [GameParameters.CustomIntParameterUI("Lookahead (ms)", minValue = 20, maxValue = 500)]
        public int LookAhead = 150;
        [GameParameters.CustomFloatParameterUI("RMS Threshold", minValue = -20f, maxValue = 0f, displayFormat = "N1")]
        public float Threshold = -8;
        [GameParameters.CustomFloatParameterUI("Ratio", minValue = 1f, maxValue = 30f, displayFormat = "N1")]
        public float Ratio = 4;
        [GameParameters.CustomIntParameterUI("Attack (ms)", minValue = 1, maxValue = 500)]
        public int Attack = 10;
        [GameParameters.CustomIntParameterUI("Release (ms)", minValue = 1, maxValue = 5000)]
        public int Release = 250;

        public override bool Enabled(MemberInfo member, GameParameters parameters)
        {
            return true;
        }
        public override bool Interactible(MemberInfo member, GameParameters parameters)
        {
            return true;
        }
    }

    //RMS Meter from Lubomir's Tutorial:    http://neolit123.blogspot.com/2009/03/designing-analog-vu-meter-in-dsp.html
    //Lookahead / Delay:                    https://www.musicdsp.org/en/latest/Effects/153-most-simple-static-delay.html?highlight=delay
    [RequireComponent(typeof(AudioBehaviour))]
    public class AudioLimiter : MonoBehaviour
    {
        public float Gain => HighLogic.CurrentGame.Parameters.CustomParams<AudioLimiterSettings>().Gain;
        public float MakeUp => HighLogic.CurrentGame.Parameters.CustomParams<AudioLimiterSettings>().GainMakeup;
        public int WindowSize => HighLogic.CurrentGame.Parameters.CustomParams<AudioLimiterSettings>().WindowSize;
        public float LookAhead => HighLogic.CurrentGame.Parameters.CustomParams<AudioLimiterSettings>().LookAhead;
        public float Threshold => HighLogic.CurrentGame.Parameters.CustomParams<AudioLimiterSettings>().Threshold;
        public float Ratio => HighLogic.CurrentGame.Parameters.CustomParams<AudioLimiterSettings>().Ratio;
        public int Attack => HighLogic.CurrentGame.Parameters.CustomParams<AudioLimiterSettings>().Attack;
        public int Release => HighLogic.CurrentGame.Parameters.CustomParams<AudioLimiterSettings>().Release;

        public float RMS = float.NegativeInfinity;
        public float Reduction = 1;
        int cs = 0;
        float sum = 0;

        int sampleRate;
        float[] buffer;
        int numSamplesDelay;
        int read = 0;
        int write = 0;

        public bool initalized = false;
        public void Initialize()
        {
            numSamplesDelay = (int)(0.001 * LookAhead * sampleRate);
            buffer = new float[numSamplesDelay * 2];
            for(int i = 0; i < numSamplesDelay; i++) {
                buffer[i] = 0;
            }
            read = 0;
            write = numSamplesDelay;

            Reduction = 1;
            RMS = float.NegativeInfinity;
            cs = 0;
            sum = 0;

            initalized = true;
        }
        void Awake()
        {
            sampleRate = AudioSettings.outputSampleRate;
            Initialize();
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            if(!initalized)
                return;

            float processReduction = 1 / Mathf.Lerp(Ratio, 1, Mathf.Abs(RMS / Threshold));
            float atkrls = RMS > Threshold ? Attack : Release;
            Reduction = Mathf.MoveTowards(Reduction, Mathf.Clamp(processReduction, 0, 1), (100 / atkrls) * 0.02f); //OnAudioFilterRead is called every 20ms or so.

            for(int i = 0; i < data.Length; i++) {
                //LookAhead Buffer
                buffer[write] = ProcessSamples(data[i]);
                data[i] = buffer[read] * Reduction * MakeUp;

                ++write;
                if(write >= numSamplesDelay * 2)
                    write = 0;
                
                ++read;
                if(read >= numSamplesDelay * 2)
                    read = 0;
            }
        }

        //RMS METER
        float ProcessSamples(float input)
        {
            float output = input * Gain;

            if(cs >= (0.001 * WindowSize * sampleRate)) {
                cs = 0;
                sum = 0;
            } else {
                cs += 1;
                sum += Mathf.Sqrt(Mathf.Abs(output));
            }

            RMS = Mathf.Floor(6 / Mathf.Log(2) * Mathf.Log(Mathf.Sqrt(sum / cs)) * 100) / 100;
            return output;
        }
    }
}
