using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

            if(AudioSettings.Reset(audioConfig)) {
                Debug.Log("[RSE]: Audio Settings Applied");
                Debug.Log("[RSE]: DSP Buffer Size : " + AudioSettings.GetConfiguration().dspBufferSize);
                Debug.Log("[RSE]: Real Voices : " +     AudioSettings.GetConfiguration().numRealVoices);
                Debug.Log("[RSE]: Virtual Voices : " +  AudioSettings.GetConfiguration().numVirtualVoices);
                Debug.Log("[RSE]: Samplerate : " +      AudioSettings.GetConfiguration().sampleRate);
                Debug.Log("[RSE]: Spearker Mode : " +   AudioSettings.GetConfiguration().speakerMode);
            }
        }
    }
}
