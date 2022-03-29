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

        int childPartsCount = 0;

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

            var childParts = rotorModule.part.children;
            childPartsCount = childParts.Count;
            foreach(var childPart in childParts) {
                var configNode = GameDatabase.Instance.GetConfigs("PART").FirstOrDefault(x => x.name.Replace("_", ".") == childPart.partInfo.name);
                var propConfig = configNode.config.GetNode("RSE_Propellers");

                if(propConfig != null) {
                    if(!PropellerBlades.ContainsKey(childPart.partInfo.name)) {
                        var propData = new PropellerBladeData();
                        propData.soundLayers = AudioUtility.CreateSoundLayerGroup(propConfig.GetNodes("SOUNDLAYER"));

                        propData.volume = new FXCurve("volume", 1);
                        propData.volume.Load("volume", propConfig);
                        
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
                        PropellerBlades.Add(childPart.partInfo.name, propData);

                    } else {
                        var propUpdate = PropellerBlades[childPart.partInfo.name];
                        propUpdate.bladeCount += 1;
                        PropellerBlades[childPart.partInfo.name] = propUpdate;
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
                        string sourceLayerName = soundLayerGroup.Key + "_" + soundLayer.name;

                        float finalControl = rpmControl;

                        if(soundLayer.spool) {
                            float idle = rotorModule.servoMotorIsEngaged ? soundLayer.spoolIdle : 0;
                            finalControl = Mathf.Max(idle, rpmControl);
                        }

                        if(soundLayerGroup.Key == "MotorRPM") {
                            if(!rotorModule.servoMotorIsEngaged)
                                finalControl = 0;
                        }

                        AudioUtility.PlaySoundLayer(gameObject, sourceLayerName, soundLayer, finalControl, volume, Sources, spools, false);
                    }
                }
            }

            if(PropellerBlades.Count > 0) {
                float rotorRPM = (rotorModule.movingPartRB.angularVelocity.magnitude / 2 / Mathf.PI) * 60; //use the world space RPM instead of relative

                float atm = Mathf.Clamp((float)vessel.atmDensity, 0f, 1f); //only play prop sounds in an atmosphere

                if(childPartsCount != rotorModule.part.children.Count) {
                    SetupBlades();
                }

                foreach(var propBlade in PropellerBlades.Keys.ToList()) {
                    float propControl = rotorRPM / PropellerBlades[propBlade].baseRPM;
                    float propOverallVolume = PropellerBlades[propBlade].volume.Value(propControl) * atm;
                    float bladeMultiplier = Mathf.Clamp((float)PropellerBlades[propBlade].bladeCount / PropellerBlades[propBlade].maxBlades, 0, 1); //dont allow more than the max blade count. SoundEffects pitched up too much doesnt sound right

                    foreach(var soundLayer in PropellerBlades[propBlade].soundLayers) {
                        string sourceLayerName = propBlade + "_" + "_" + soundLayer.name;

                        AudioUtility.PlaySoundLayer(gameObject, sourceLayerName, soundLayer, propControl * bladeMultiplier, propOverallVolume, Sources, spools, false);
                    }
                }
            }

            base.OnUpdate();
        }
    }
}
