// FAIRLY CHILDISH LIMITER
//
// Copyright 2006, Thomas Scott Stillwell
// All rights reserved.
//
//Redistribution and use in source and binary forms, with or without modification, are permitted 
//provided that the following conditions are met:
//
//Redistributions of source code must retain the above copyright notice, this list of conditions 
//and the following disclaimer. 
//
//Redistributions in binary form must reproduce the above copyright notice, this list of conditions 
//and the following disclaimer in the documentation and/or other materials provided with the distribution. 
//
//The name of Thomas Scott Stillwell may not be used to endorse or 
//promote products derived from this software without specific prior written permission. 
//
//THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR 
//IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND 
//FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS 
//BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES 
//(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR 
//PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, 
//STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF 
//THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
//Ported to .NET by Mark Heath

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace RocketSoundEnhancement
{
    [RequireComponent(typeof(AudioBehaviour))]
    public class AudioLimiter : MonoBehaviour
    {
        public static bool EnableLimiter;
        public static float Threshold;                     // 0f
        public static float Bias;                          // 70f
        public static float Ratio;                         // 20f
        public static float Gain;                          // 0f
        public static int TimeConstant;                    // 1
        public static int LevelDetectorRMSWindow = 100;    // 100
        public static float CurrentCompressionRatio;
        public static float GainReduction;

        float log2db;
        float db2log;
        float attime;
        float reltime;
        float rmstime;
        float cratio;
        float rundb;
        float overdb;
        float atcoef;
        float relcoef;
        float rmscoef;

        float thresh;
        float threshv;
        float bias;
        //float cthresh;
        //float cthreshv;
        float makeup;
        float makeupv;
        float timeconstant;

        int SampleRate;

        private void Awake()
        {
            SampleRate = AudioSettings.outputSampleRate;

            log2db = 8.6858896380650365530225783783321f; // 20 / ln(10)
            db2log = 0.11512925464970228420089957273422f; // ln(10) / 20 
            attime = 0.0002f; //200us
            reltime = 0.300f; //300ms
            rmstime = 0.000050f; //50us
            cratio = 0;
            rundb = 0;
            overdb = 0;
            atcoef = Mathf.Exp(-1 / (attime * SampleRate));
            relcoef = Mathf.Exp(-1 / (reltime * SampleRate));
            rmscoef = Mathf.Exp(-1 / (rmstime * SampleRate));
        }

        float aspl0;
        float runave;
        float dcoffset = 0; // never assigned to

        void OnAudioFilterRead(float[] data, int channels)
        {
            thresh = Threshold;
            threshv = Mathf.Exp(thresh * db2log);
            bias = 80 * Bias / 100;
            //cthresh = thresh - bias;
            //cthreshv = Mathf.Exp(cthresh * db2log);
            makeup = Gain;
            makeupv = Mathf.Exp(makeup * db2log);

            timeconstant = TimeConstant;
            if(timeconstant == 1) {
                attime = 0.0002f;
                reltime = 0.300f;
            }
            if(timeconstant == 2) {
                attime = 0.0002f;
                reltime = 0.800f;
            }
            if(timeconstant == 3) {
                attime = 0.0004f;
                reltime = 2.000f;
            }
            if(timeconstant == 4) {
                attime = 0.0008f;
                reltime = 5.000f;
            }
            if(timeconstant == 5) {
                attime = 0.0002f;
                reltime = 10.000f;
            }
            if(timeconstant == 6) {
                attime = 0.0004f;
                reltime = 25.000f;
            }

            atcoef = Mathf.Exp(-1 / (attime * SampleRate));
            relcoef = Mathf.Exp(-1 / (reltime * SampleRate));

            rmstime = LevelDetectorRMSWindow / 1000000;
            rmscoef = Mathf.Exp(-1 / (rmstime * SampleRate));


            for(int i = 0; i < data.Length; i++) {
                aspl0 = Mathf.Abs(data[i]);

                float maxspl = aspl0 * aspl0;

                runave = maxspl + rmscoef * (runave - maxspl);
                float det = Mathf.Sqrt(Mathf.Max(0, runave));

                overdb = (log2db * 2.08136898f) * Mathf.Log(det / threshv);
                overdb = Mathf.Max(0, overdb);

                if(overdb > rundb) {
                    rundb = overdb + atcoef * (rundb - overdb);
                } else {
                    rundb = overdb + relcoef * (rundb - overdb);
                }
                overdb = Mathf.Max(rundb, 0);

                if(bias == 0) {
                    cratio = Ratio;
                } else {
                    cratio = 1 + (Ratio - 1) * Mathf.Sqrt((overdb + dcoffset) / (bias + dcoffset));
                }
                CurrentCompressionRatio = cratio;

                float gr = -overdb * (cratio - 1) / cratio;
                GainReduction = gr;
                float grv = Mathf.Exp(gr * db2log);

                data[i] *= grv * makeupv;
            }

        }
    }
}
