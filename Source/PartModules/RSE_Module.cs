using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RocketSoundEnhancement
{
    public class RSE_Module : PartModule
    {
        public Dictionary<string, AudioSource> Sources = new Dictionary<string, AudioSource>();
        public Dictionary<string, float> spools = new Dictionary<string, float>();

        public Dictionary<string, List<SoundLayer>> SoundLayerGroups;
        public List<SoundLayer> SoundLayers;

        public GameObject audioParent;

        public bool initialized;
        public bool gamePaused;

        public float volume = 1;

        public override void OnStart(StartState state)
        {
            GameEvents.onGamePause.Add(onGamePause);
            GameEvents.onGameUnpause.Add(onGameUnpause);
        }

        Vector3 lastSourcePosition = Vector3.zero;
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

        float pitchVariation = 1;
        public void PlaySoundLayer(GameObject audioGameObject, string sourceLayerName, SoundLayer soundLayer, float rawControl, float vol, bool spoolProccess = true)
        {
            float control = rawControl;

            if(spoolProccess) {
                if(!spools.ContainsKey(sourceLayerName)) {
                    spools.Add(sourceLayerName, 0);
                }

                if(soundLayer.spool) {
                    spools[sourceLayerName] = Mathf.MoveTowards(spools[sourceLayerName], control, soundLayer.spoolSpeed * TimeWarp.deltaTime);
                    control = spools[sourceLayerName];
                } else {
                    //fix for audiosource clicks
                    spools[sourceLayerName] = Mathf.MoveTowards(spools[sourceLayerName], control, AudioUtility.SmoothControl.Evaluate(control) * (60 * Time.deltaTime));
                    control = spools[sourceLayerName];
                }
            }

            control = Mathf.Round(control * 1000.0f) * 0.001f;

            //For Looped sounds cleanup
            if(control < float.Epsilon) {
                if(Sources.ContainsKey(sourceLayerName)) {
                    Sources[sourceLayerName].Stop();
                }
                return;
            }

            AudioSource source;

            if(!Sources.ContainsKey(sourceLayerName)) {
                var go = new GameObject(sourceLayerName);
                go.transform.parent = audioGameObject.transform;
                go.transform.position = audioGameObject.transform.position;
                go.transform.rotation = audioGameObject.transform.rotation;

                source = AudioUtility.CreateSource(go, soundLayer);
                Sources.Add(sourceLayerName, source);

                if(soundLayer.pitchVariation) {
                    pitchVariation = UnityEngine.Random.Range(0.95f, 1.05f);
                }

            } else {
                source = Sources[sourceLayerName];
            }

            if(soundLayer.useFloatCurve) {
                source.volume = soundLayer.volumeFC.Evaluate(control) * GameSettings.SHIP_VOLUME * vol;
                source.pitch = soundLayer.pitchFC.Evaluate(control);
            } else {

                source.volume = soundLayer.volume.Value(control) * GameSettings.SHIP_VOLUME * vol;
                source.pitch = soundLayer.pitch.Value(control);
            }


            if(soundLayer.pitchVariation && !soundLayer.loopAtRandom) {
                source.pitch *= pitchVariation;
            }

            AudioUtility.PlayAtChannel(source, soundLayer.channel, soundLayer.loop, soundLayer.loopAtRandom);
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
