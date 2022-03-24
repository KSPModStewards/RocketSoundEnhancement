using Expansions.Serenity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace RocketSoundEnhancement
{
    public struct PropellerBladeData
    {
        public int bladeCount;
        public float baseRPM;
        public int maxBlades;
        public List<SoundLayer> soundLayers;
    }

    class RSE_Rotors : PartModule
    {
        Dictionary<string, PropellerBladeData> PropellerBlades = new Dictionary<string, PropellerBladeData>();
        Dictionary<string, AudioSource> Sources = new Dictionary<string, AudioSource>();
        Dictionary<string, float> spools = new Dictionary<string, float>();

        bool initialized;
        bool gamePaused;
        ModuleRoboticServoRotor rotorModule;

        float volume = 1;

        [KSPField(isPersistant = false, guiActive = true)]
        int numberOfChildren = 0;
        [KSPField(isPersistant = false, guiActive = true)]
        int audioSourceCount = 0;
        [KSPField(isPersistant = false, guiActive = true)]
        float currentControl;

        public override void OnStart(StartState state)
        {
            if(state == StartState.Editor || state == StartState.None)
                return;

            rotorModule = part.GetComponent<ModuleRoboticServoRotor>();

            initialized = rotorModule != null;

            SetupBlades();

            if(!initialized) 
                return;

            GameEvents.onGamePause.Add(onGamePause);
            GameEvents.onGameUnpause.Add(onGameUnpause);
        }

        void SetupBlades()
        {
            if(PropellerBlades.Count() > 0) {
                foreach(var data in PropellerBlades.Keys.ToList()) {
                    var cleanData = PropellerBlades[data];
                    cleanData.bladeCount = 0;
                    PropellerBlades[data] = cleanData;
                }
            }

            var blades = rotorModule.part.children;
            numberOfChildren = blades.Count() ;
            foreach(var blade in blades) {
                var configNode = GameDatabase.Instance.GetConfigs("PART").FirstOrDefault(x => x.name.Replace("_", ".") == blade.partInfo.name);
                var propConfig = configNode.config.GetNode("RSE_Propellers");

                if(propConfig != null) {
                    if(!PropellerBlades.ContainsKey(blade.partInfo.name)) {
                        var propData = new PropellerBladeData();
                        propData.soundLayers = AudioUtility.CreateSoundLayerGroup(propConfig.GetNodes("SOUNDLAYER"));
                        
                        if(!float.TryParse(propConfig.GetValue("baseRPM"), out propData.baseRPM)) {
                            Debug.Log("[RSE]: [RSE_Propellers] baseRPM cannot be empty");
                            initialized = false;
                            return;
                        }

                        if(!int.TryParse(propConfig.GetValue("maxBlades"), out propData.maxBlades)) {
                            Debug.Log("[RSE]: [RSE_Propellers] maxBlades cannot be empty");
                            initialized = false;
                            return;
                        }

                        propData.bladeCount = 1;
                        PropellerBlades.Add(blade.partInfo.name, propData);

                    } else {
                        var propUpdate = PropellerBlades[blade.partInfo.name];
                        propUpdate.bladeCount += 1;
                        PropellerBlades[blade.partInfo.name] = propUpdate;
                    }
                }
            }
        }

        public override void OnUpdate()
        {
            if(!HighLogic.LoadedSceneIsFlight || gamePaused || !initialized)
                return;

            audioSourceCount = Sources.Count();
            numberOfChildren = PropellerBlades.First().Value.bladeCount;

            if(numberOfChildren != rotorModule.part.children.Count()) {
                SetupBlades();
            }

            foreach(var propValues in PropellerBlades.Values.ToList()) {

                float rawControl = rotorModule.normalizedOutput * rotorModule.rpmLimit / propValues.baseRPM;
                float bladeMultiplier = (float)propValues.bladeCount / propValues.maxBlades;
                rawControl *= bladeMultiplier;
                currentControl = rawControl;

                foreach(var soundLayer in propValues.soundLayers) {
                    string sourceLayerName = rotorModule.part.partInfo.name + "_" + soundLayer.name;

                    if(!spools.ContainsKey(sourceLayerName)) {
                        spools.Add(sourceLayerName, 0);
                    }

                    spools[sourceLayerName] = Mathf.MoveTowards(spools[sourceLayerName], rawControl, AudioUtility.SmoothControl.Evaluate(rawControl) * (60 * Time.deltaTime));
                    float control = spools[sourceLayerName];

                    //For Looped sounds cleanup
                    if(control < float.Epsilon) {
                        if(Sources.ContainsKey(sourceLayerName)) {
                            Sources[sourceLayerName].Stop();
                        }
                        continue;
                    }

                    AudioSource source;

                    if(!Sources.ContainsKey(sourceLayerName)) {
                        source = AudioUtility.CreateSource(gameObject, soundLayer);
                        Sources.Add(sourceLayerName, source);

                    } else {
                        source = Sources[sourceLayerName];
                    }

                    source.volume = soundLayer.volume.Value(control) * GameSettings.SHIP_VOLUME * volume;
                    source.pitch = soundLayer.pitch.Value(control);

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
