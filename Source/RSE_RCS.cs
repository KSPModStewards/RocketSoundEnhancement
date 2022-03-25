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

        float[] lastThrustControl;

        float volume = 1;

        public override void OnStart(StartState state)
        {
            if(state == StartState.Editor || state == StartState.None)
                return;

            moduleRCSFX = part.Modules.GetModule<ModuleRCSFX>();
            lastThrustControl = new float[moduleRCSFX.thrustForces.Length];

            var configNode = AudioUtility.GetConfigNode(part.partInfo.name, this.moduleName);
            if(!float.TryParse(configNode.GetValue("volume"), out volume))
                volume = 1;

            SoundLayers = AudioUtility.CreateSoundLayerGroup(configNode.GetNodes("SOUNDLAYER"));

            GameEvents.onGamePause.Add(onGamePause);
            GameEvents.onGameUnpause.Add(onGameUnpause);

            initialized = true;
        }

        public override void OnUpdate()
        {
            if(!HighLogic.LoadedSceneIsFlight || gamePaused || !initialized)
                return;

            var thrustTransforms = moduleRCSFX.thrusterTransforms;
            var thrustForces = moduleRCSFX.thrustForces;

            for(int i = 0; i < thrustTransforms.Count; i++) {

                float rawControl = thrustForces[i] / moduleRCSFX.thrusterPower;
                //smooth control to prevent clicking
                //Doesn't work, still clicking even at slowest of rates
                float control = Mathf.MoveTowards(lastThrustControl[i], rawControl, AudioUtility.SmoothControl.Evaluate(rawControl) * (60 * Time.deltaTime));
                lastThrustControl[i] = control;
                GameObject thrustTransform = thrustTransforms[i].gameObject;

                foreach(var soundLayer in SoundLayers) {
                    string sourceLayerName = moduleRCSFX.thrusterTransformName + "_" + i + "_" + soundLayer.name;

                    AudioUtility.PlaySoundLayer(thrustTransform, sourceLayerName, soundLayer, control, volume, Sources, null, true);
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
