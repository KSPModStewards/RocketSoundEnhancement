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
            AudioConfiguration audioConfig = UnityEngine.AudioSettings.GetConfiguration();
            audioConfig.numRealVoices = 64;

            if(UnityEngine.AudioSettings.Reset(audioConfig)) {
                Debug.Log("[RSE]: Audio Settings Applied");
                Debug.Log("[RSE]: DSP Buffer Size : " +  UnityEngine.AudioSettings.GetConfiguration().dspBufferSize);
                Debug.Log("[RSE]: Real Voices : " +      UnityEngine.AudioSettings.GetConfiguration().numRealVoices);
                Debug.Log("[RSE]: Virtual Voices : " +   UnityEngine.AudioSettings.GetConfiguration().numVirtualVoices);
                Debug.Log("[RSE]: Samplerate : " +       UnityEngine.AudioSettings.GetConfiguration().sampleRate);
                Debug.Log("[RSE]: Spearker Mode : " +    UnityEngine.AudioSettings.GetConfiguration().speakerMode);
            }

        }
    }
}
