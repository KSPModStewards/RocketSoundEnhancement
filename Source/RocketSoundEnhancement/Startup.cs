using HarmonyLib;
using KSPCommunityLib.Logging;
using System;
using UnityEngine;

namespace RocketSoundEnhancement
{
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    class Startup : MonoBehaviour
    {
        void Awake()
        {
            AudioConfiguration audioConfig = AudioSettings.GetConfiguration();
            audioConfig.numRealVoices = Settings.VoiceCount;

            if (AudioSettings.Reset(audioConfig)) {
                Log.Debug("[RSE]: Audio Settings Applied");
                Log.Debug("[RSE]: DSP Buffer Size : " + AudioSettings.GetConfiguration().dspBufferSize);
                Log.Debug("[RSE]: Real Voices : " +     AudioSettings.GetConfiguration().numRealVoices);
                Log.Debug("[RSE]: Virtual Voices : " +  AudioSettings.GetConfiguration().numVirtualVoices);
                Log.Debug("[RSE]: Samplerate : " +      AudioSettings.GetConfiguration().sampleRate);
                Log.Debug("[RSE]: Spearker Mode : " +   AudioSettings.GetConfiguration().speakerMode);
            }

            try
            {
                Harmony harmony = new Harmony("RocketSoundEnhancement");
                harmony.PatchAll(typeof(Startup).Assembly);
            }
            catch (Exception ex)
            {
                Log.Error("Harmony patching failed!");
                Log.Exception(ex);
            }
        }
    }
}
