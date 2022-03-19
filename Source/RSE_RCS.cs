using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RocketSoundEnhancement
{
    class RSE_RCS : PartModule
    {
        List<SoundLayer> SoundLayers = new List<SoundLayer>();
        Dictionary<string, AudioSource> Sources = new Dictionary<string, AudioSource>();

        bool initialized;
        bool gamePaused;

        ModuleRCSFX moduleRCSFX;
        GameObject audioParent;

        float[] lastThrustControl;

        float volume = 1;
        float pitchVariation = 1;

        public override void OnStart(StartState state)
        {
            if(state == StartState.Editor || state == StartState.None)
                return;

            string partParentName = part.name + "_" + this.moduleName;
            audioParent = part.gameObject.GetChild(partParentName);
            if(audioParent == null) {
                audioParent = new GameObject(partParentName);
                audioParent.transform.rotation = part.transform.rotation;
                audioParent.transform.position = part.transform.position;
                audioParent.transform.parent = part.transform;
            }

            moduleRCSFX = part.Modules.GetModule<ModuleRCSFX>();
            lastThrustControl = new float[moduleRCSFX.thrustForces.Length];

            var configNode = AudioUtility.GetConfigNode(part.partInfo.name, this.moduleName);
            if(!float.TryParse(configNode.GetValue("volume"), out volume))
                volume = 1;

            SoundLayers = AudioUtility.CreateSoundLayerGroup(configNode.GetNodes("SOUNDLAYER"));

            initialized = true;

            GameEvents.onGamePause.Add(onGamePause);
            GameEvents.onGameUnpause.Add(onGameUnpause);
        }

        public override void OnUpdate()
        {
            if(audioParent == null || !HighLogic.LoadedSceneIsFlight || gamePaused || !initialized)
                return;

            var thrustTransforms = moduleRCSFX.thrusterTransforms;
            var thrustForces = moduleRCSFX.thrustForces;

            for(int i = 0; i < thrustTransforms.Count; i++) {

                float rawControl = thrustForces[i] / moduleRCSFX.thrusterPower;
                //smooth control to prevent clicking
                //Doesn't work, still clicking even at slowest of rates
                float control = Mathf.MoveTowards(lastThrustControl[i], rawControl, AudioUtility.SmoothControl.Evaluate(rawControl));
                lastThrustControl[i] = control;

                foreach(var soundLayer in SoundLayers) {
                    string sourceLayerName = moduleRCSFX.thrusterTransformName + "_" + i + "_" + soundLayer.name;

                    //For Looped sounds cleanup
                    if(control < float.Epsilon) {
                        if(Sources.ContainsKey(sourceLayerName)) {
                            Sources[sourceLayerName].Stop();
                        }
                        continue;
                    }

                    AudioSource source;
                    GameObject thrustTransform = thrustTransforms[i].gameObject;

                    if(!Sources.ContainsKey(sourceLayerName)) {
                        source = AudioUtility.CreateSource(thrustTransform, soundLayer);
                        source.time = Random.Range(0, 0.05f);
                        Sources.Add(sourceLayerName, source);

                        pitchVariation = Random.Range(0.9f, 1.1f);
                    } else {
                        source = Sources[sourceLayerName];
                    }

                    source.volume = soundLayer.volume.Value(control) * GameSettings.SHIP_VOLUME * volume;
                    source.pitch = soundLayer.pitch.Value(control) * pitchVariation;

                    AudioUtility.PlayAtChannel(source, soundLayer.channel, soundLayer.loop, soundLayer.loopAtRandom);

                }
            }

            if(Sources.Count > 0) {
                var sourceKeys = Sources.Keys.ToList();
                foreach(var source in sourceKeys) {
                    if(!Sources[source].isPlaying) {
                        UnityEngine.Object.Destroy(Sources[source]);
                        Sources.Remove(source);
                    }
                }
            }
        }

        void onGamePause()
        {
            if(Sources.Count > 0) {
                foreach(var source in Sources.Values) {
                    source.Pause();
                }
            }
            gamePaused = true;
        }
        void onGameUnpause()
        {
            if(Sources.Count > 0) {
                foreach(var source in Sources.Values) {
                    source.UnPause();
                }
            }
            gamePaused = false;
        }

        void OnDestroy()
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
