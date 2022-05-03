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
        ModuleResourceIntake resourceIntake;

        int childPartsCount = 0;

        public override void OnStart(StartState state)
        {
            if(state == StartState.Editor || state == StartState.None)
                return;

            base.OnStart(state);

            rotorModule = part.GetComponent<ModuleRoboticServoRotor>();
            resourceIntake = part.GetComponent<ModuleResourceIntake>();

            SetupBlades();

            initialized = true;
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
                float intakeMultiplier = 1;
                bool motorEngaged = rotorModule.servoMotorIsEngaged && !rotorModule.servoIsLocked;
                
                if(resourceIntake!= null){
                    motorEngaged = rotorModule.servoMotorIsEngaged && !rotorModule.servoIsLocked && resourceIntake.intakeEnabled;
                    intakeMultiplier = resourceIntake.intakeEnabled ? Mathf.Min(resourceIntake.airFlow, 1) : 0;
                }

                float rpmControl = (rotorModule.transformRateOfMotion / rotorModule.traverseVelocityLimits.y);

                foreach(var soundLayerGroup in SoundLayerGroups) {
                    float control = 0;

                    switch(soundLayerGroup.Key){
                        case "RPM":
                            control = rpmControl;
                            break;
                        case "Motor":
                            control = motorEngaged ? rpmControl * intakeMultiplier: 0;
                            if(rotorModule.servoIsBraking){
                                control *= 0.25f;
                            }
                            break;
                    }

                    foreach(var soundLayer in soundLayerGroup.Value) {
                        string sourceLayerName = soundLayerGroup.Key + "_" + soundLayer.name;

                        if(!Controls.ContainsKey(sourceLayerName)) {
                            Controls.Add(sourceLayerName, 0);
                        }
                        
                        if(soundLayer.spool) {
                            float spoolSpeed = Mathf.Max(soundLayer.spoolSpeed, control) * TimeWarp.deltaTime;
                            float spoolControl = Mathf.Lerp(motorEngaged ? soundLayer.spoolIdle : 0, 1, control); 

                            Controls[sourceLayerName] = Mathf.MoveTowards(Controls[sourceLayerName], spoolControl, spoolSpeed);
                        } else {
                            float smoothControl = AudioUtility.SmoothControl.Evaluate(Mathf.Max(Controls[sourceLayerName], control)) * (60 * Time.deltaTime);
                            Controls[sourceLayerName] = Mathf.MoveTowards(Controls[sourceLayerName], control, smoothControl);
                        }
                        
                        PlaySoundLayer(sourceLayerName, soundLayer, Controls[sourceLayerName], Volume);
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
                    float control = propControl * bladeMultiplier;

                    foreach(var soundLayer in PropellerBlades[propBlade].soundLayers) {
                        string sourceLayerName = propBlade + "_" + "_" + soundLayer.name;

                        if(!Controls.ContainsKey(sourceLayerName)) {
                            Controls.Add(sourceLayerName, 0);
                        }

                        Controls[sourceLayerName] = Mathf.MoveTowards(Controls[sourceLayerName], control, AudioUtility.SmoothControl.Evaluate(control) * (60 * Time.deltaTime));

                        PlaySoundLayer(sourceLayerName, soundLayer, Controls[sourceLayerName], propOverallVolume);
                    }
                }
            }

            base.OnUpdate();
        }
    }
}
