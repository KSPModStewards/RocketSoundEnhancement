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
        private static AssetBundle rse_Bundle;

        public static AssetBundle RSE_Bundle
        {
            get {
                if(rse_Bundle == null) {
                    string path = KSPUtil.ApplicationRootPath + Settings.ModPath + "Plugins/";
                    rse_Bundle = AssetBundle.LoadFromFile(path + "rse_bundle");
                    Debug.Log("[RSE]: AssetBundle loaded");
                }
                return rse_Bundle;
            }
        }

        void Awake()
        {
            AudioConfiguration audioConfig = UnityEngine.AudioSettings.GetConfiguration();
            audioConfig.numRealVoices = Settings.VoiceCount;

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
