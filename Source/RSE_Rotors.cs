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
        Dictionary<string, List<SoundLayer>> SoundLayerGroups = new Dictionary<string, List<SoundLayer>>();
        Dictionary<string, AudioSource> Sources = new Dictionary<string, AudioSource>();
        Dictionary<string, float> spools = new Dictionary<string, float>();

        bool initialized;
        bool gamePaused;
        ModuleRoboticServoRotor rotorModule;

        float volume = 1;

        int numbOfChildren = 0;

        public override void OnStart(StartState state)
        {
            if(state == StartState.Editor || state == StartState.None)
                return;

            SoundLayerGroups.Clear();
            spools.Clear();

            rotorModule = part.GetComponent<ModuleRoboticServoRotor>();

            var configNode = AudioUtility.GetConfigNode(part.partInfo.name, this.moduleName);
            if(!float.TryParse(configNode.GetValue("volume"), out volume))
                volume = 1;

            foreach(var node in configNode.GetNodes()) {
                string soundLayerGroupName = node.name;
                var soundLayers = AudioUtility.CreateSoundLayerGroup(node.GetNodes("SOUNDLAYER"));
                if(soundLayers.Count > 0) {
                    if(SoundLayerGroups.ContainsKey(soundLayerGroupName)) {
                        SoundLayerGroups[soundLayerGroupName].AddRange(soundLayers);
                    } else {
                        SoundLayerGroups.Add(soundLayerGroupName, soundLayers);
                    }
                }
            }

            SetupBlades();

            GameEvents.onGamePause.Add(onGamePause);
            GameEvents.onGameUnpause.Add(onGameUnpause);
        }

        void SetupBlades()
        {
            if(PropellerBlades.Count > 0) {
                foreach(var data in PropellerBlades.Keys.ToList()) {
                    var cleanData = PropellerBlades[data];
                    cleanData.bladeCount = 0;
                    PropellerBlades[data] = cleanData;
                }
            }

            var blades = rotorModule.part.children;
            numbOfChildren = blades.Count ;
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

            initialized = true;
        }

        public override void OnUpdate()
        {
            if(!HighLogic.LoadedSceneIsFlight || gamePaused || !initialized)
                return;

            if(SoundLayerGroups.Count > 0) {
                foreach(var soundLayerGroup in SoundLayerGroups) {
                    float rpmControl = rotorModule.transformRateOfMotion / rotorModule.traverseVelocityLimits.y; //* (rotorModule.servoMotorSize / 100);
                    if(soundLayerGroup.Key == "MotorRPM") {
                        if(!rotorModule.servoMotorIsEngaged)
                            rpmControl = 0;
                    }

                    foreach(var soundLayer in soundLayerGroup.Value) {
                        AudioUtility.PlaySoundLayer(gameObject, soundLayer.name, soundLayer, rpmControl, volume, Sources, spools, false);
                    }
                }
            }

            if(PropellerBlades.Count > 0) {
                float atm = Mathf.Clamp((float)vessel.atmDensity,0f,1f); //only play prop sounds in an atmosphere
                numbOfChildren = PropellerBlades.First().Value.bladeCount;
                if(numbOfChildren != rotorModule.part.children.Count) {
                    SetupBlades();
                }
                foreach(var propValues in PropellerBlades.Values.ToList()) {
                    float propControl = rotorModule.transformRateOfMotion / propValues.baseRPM;
                    float bladeMultiplier = Mathf.Clamp((float)propValues.bladeCount / propValues.maxBlades,0,1); //dont allow more than the max blade count. SoundEffects pitched up too much doesnt sound right
                    propControl *= bladeMultiplier;

                    foreach(var soundLayer in propValues.soundLayers) {
                        AudioUtility.PlaySoundLayer(gameObject, soundLayer.name, soundLayer, propControl, volume * atm, Sources, spools, false);
                    }
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
