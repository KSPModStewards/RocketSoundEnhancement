using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace RocketSoundEnhancement
{
    public class RSE_Module : PartModule
    {
        public Dictionary<string, AudioSource> Sources = new Dictionary<string, AudioSource>();

        public Dictionary<string, List<SoundLayer>> SoundLayerGroups;
        public List<SoundLayer> SoundLayers;
        public Dictionary<string, float> spools;

        public GameObject audioParent;

        public bool initialized;
        public bool gamePaused;

        public float volume = 1;
        public float pitchVariation = 1;

        public override void OnStart(StartState state)
        {
            GameEvents.onGamePause.Add(onGamePause);
            GameEvents.onGameUnpause.Add(onGameUnpause);
        }

        public override void OnUpdate()
        {
            if(Sources.Count > 0) {
                var sourceKeys = Sources.Keys.ToList();
                foreach(var source in sourceKeys) {
                    if(!Sources[source].isPlaying) {

                        // we dont want to accidentally delete the actual part
                        if(Sources[source].gameObject.name == source) {
                            UnityEngine.Object.Destroy(Sources[source].gameObject);
                        } else {
                            UnityEngine.Object.Destroy(Sources[source]);
                        }
                        
                        Sources.Remove(source);
                        spools.Remove(source);
                    }
                }
            }
        }

        public void onGamePause()
        {
            if(Sources.Count > 0) {
                foreach(var source in Sources.Values) {
                    source.Pause();
                }
            }
            gamePaused = true;
        }

        public void onGameUnpause()
        {
            if(Sources.Count > 0) {
                foreach(var source in Sources.Values) {
                    source.UnPause();
                }
            }
            gamePaused = false;
        }

        public void OnDestroy()
        {
            if(!initialized)
                return;

            if(Sources.Count > 0) {
                foreach(var source in Sources.Values) {
                    source.Stop();
                    UnityEngine.Object.Destroy(source);
                }
            }

            GameEvents.onGamePause.Remove(onGamePause);
            GameEvents.onGameUnpause.Remove(onGameUnpause);
        }
    }
}
