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
        public FXCurve volume;
        public List<SoundLayer> soundLayers;
    }

    public class RSE_RotorEngines : RSE_Module
    {
        Dictionary<string, PropellerBladeData> PropellerBlades = new Dictionary<string, PropellerBladeData>();
        ModuleRoboticServoRotor rotorModule;

        float maxRPM = 250;

        int numbOfChildren = 0;

        public override void OnStart(StartState state)
        {
            if(state == StartState.Editor || state == StartState.None)
                return;

            rotorModule = part.GetComponent<ModuleRoboticServoRotor>();

            var configNode = AudioUtility.GetConfigNode(part.partInfo.name, this.moduleName);
            if(!float.TryParse(configNode.GetValue("volume"), out volume))
                volume = 1;

            if(!float.TryParse(configNode.GetValue("maxRPM"), out maxRPM))
                maxRPM = rotorModule.traverseVelocityLimits.y;

            SoundLayerGroups = new Dictionary<string, List<SoundLayer>>();
            spools = new Dictionary<string, float>();
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

            base.OnStart(state);
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

                        propData.volume = new FXCurve("volume", 1);
                        propData.volume.Load("volume", propConfig);

                        //if(!float.TryParse(propConfig.GetValue("volume"), out propData.volume)) {
                        //    propData.volume = 1;
                        //}
                        
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
                    float rpmControl = rotorModule.transformRateOfMotion / maxRPM; //* (rotorModule.servoMotorSize / 100);
                    
                    foreach(var soundLayer in soundLayerGroup.Value) {
                        float finalControl = rpmControl;

                        if(soundLayer.spool) {
                            float idle = rotorModule.servoMotorIsEngaged ? soundLayer.spoolIdle : 0;
                            finalControl = Mathf.Max(idle, rpmControl);
                        }

                        if(soundLayerGroup.Key == "MotorRPM") {
                            if(!rotorModule.servoMotorIsEngaged)
                                finalControl = 0;
                        }

                        AudioUtility.PlaySoundLayer(gameObject, soundLayer.name, soundLayer, finalControl, volume, Sources, spools, false);
                    }
                }
            }

            if(PropellerBlades.Count > 0) {

                //take into account rotors on rotors if possible
                float realRPM = 0;
                if(part.Rigidbody != null) {
                    realRPM = (part.Rigidbody.angularVelocity.magnitude / 2 / Mathf.PI) * 60;
                }

                float atm = Mathf.Clamp((float)vessel.atmDensity, 0f, 1f); //only play prop sounds in an atmosphere
                numbOfChildren = PropellerBlades.First().Value.bladeCount;
                if(numbOfChildren != rotorModule.part.children.Count) {
                    SetupBlades();
                }
                foreach(var propValues in PropellerBlades.Values.ToList()) {
                    float propControl = Mathf.Abs(rotorModule.transformRateOfMotion - realRPM) / propValues.baseRPM;
                    float propOverallVolume = propValues.volume.Value(propControl) * atm;
                    float bladeMultiplier = Mathf.Clamp((float)propValues.bladeCount / propValues.maxBlades,0,1); //dont allow more than the max blade count. SoundEffects pitched up too much doesnt sound right

                    foreach(var soundLayer in propValues.soundLayers) {
                        AudioUtility.PlaySoundLayer(gameObject, soundLayer.name, soundLayer, propControl * bladeMultiplier, propOverallVolume, Sources, spools, false);
                    }
                }
            }

            base.OnUpdate();
        }
    }
}
