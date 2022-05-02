using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace RocketSoundEnhancement
{
    /// <summary>
    /// Thanks to Iron-Warrior for source: https://forum.unity.com/threads/custom-low-pass-filter-using-onaudiofilterread.976326/
    /// </summary>
    [RequireComponent(typeof(AudioBehaviour))]
    public class LowpassFilter : MonoBehaviour
    {

        private float[] inputHistoryLeft = new float[2];
        private float[] inputHistoryRight = new float[2];

        private float[] outputHistoryLeft = new float[3];
        private float[] outputHistoryRight = new float[3];

        private float c, a1, a2, a3, b1, b2, lowpassFade;

        public float CutoffFrequency = 22200;

        int SampleRate;
        private void Awake()
        {
            SampleRate = AudioSettings.outputSampleRate;

            InvokeRepeating("UpdateFilter", 0, 0.05f);

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

        public void UpdateFilter()
        {
            float finalCutOff = Mathf.Clamp(CutoffFrequency, 10, 22200);
            float finalResonance = Mathf.Sqrt(2);

            c = 1.0f / (float)Mathf.Tan(Mathf.PI * finalCutOff / SampleRate);
            a1 = 1.0f / (1.0f + finalResonance * c + c * c);
            a2 = 2f * a1;
            a3 = a1;
            b1 = 2.0f * (1.0f - c * c) * a1;
            b2 = (1.0f - finalResonance * c + c * c) * a1;

            lowpassFade = CutoffFrequency <= 50 ?
                Mathf.Pow(2, Mathf.Lerp(-80, 0, CutoffFrequency / 50f) / 6) : 1;
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            for(int i = 0; i < data.Length; i++) {
                data[i] *= lowpassFade;
                data[i] = AddInput(data[i], i);
            }
        }

        float AddInput(float newInput, int index)
        {
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