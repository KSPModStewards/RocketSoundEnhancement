using Expansions.Serenity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace RocketSoundEnhancement.PartModules
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
        public Dictionary<string, PropellerBladeData> PropellerBlades = new Dictionary<string, PropellerBladeData>();
        private ModuleRoboticServoRotor rotorModule;
        private ModuleResourceIntake resourceIntake;
        private int childPartsCount = 0;

        public override void OnStart(StartState state)
        {
            if(state == StartState.Editor || state == StartState.None)
                return;

            EnableLowpassFilter = true;
            EnableCombFilter = true;
            EnableDistortionFilter = true;
            base.OnStart(state);

            rotorModule = part.GetComponent<ModuleRoboticServoRotor>();
            resourceIntake = part.GetComponent<ModuleResourceIntake>();

            SetupBlades();

            Initialized = true;
        }

        public void SetupBlades()
        {
            if (PropellerBlades.Count > 0)
            {
                foreach (var propellerBlade in PropellerBlades.Keys.ToList())
                {
                    var cleanData = PropellerBlades[propellerBlade];
                    cleanData.bladeCount = 0;
                    PropellerBlades[propellerBlade] = cleanData;
                }
            }

            var childParts = rotorModule.part.children;
            childPartsCount = childParts.Count;
            foreach (var childPart in childParts)
            {
                var configNode = GameDatabase.Instance.GetConfigs("PART").FirstOrDefault(x => x.name.Replace("_", ".") == childPart.partInfo.name);
                var propConfig = configNode.config.GetNode("RSE_Propellers");

                if (propConfig != null)
                {
                    if (!PropellerBlades.ContainsKey(childPart.partInfo.name))
                    {
                        var propData = new PropellerBladeData();
                        propData.soundLayers = AudioUtility.CreateSoundLayerGroup(propConfig.GetNodes("SOUNDLAYER"));

                        propData.volume = new FXCurve("volume", 1);
                        propData.volume.Load("volume", propConfig);

                        if (!float.TryParse(propConfig.GetValue("baseRPM"), out propData.baseRPM))
                        {
                            Debug.Log("[RSE]: [RSE_Propellers] baseRPM cannot be empty");
                            Initialized = false;
                            return;
                        }

                        if (!int.TryParse(propConfig.GetValue("maxBlades"), out propData.maxBlades))
                        {
                            Debug.Log("[RSE]: [RSE_Propellers] maxBlades cannot be empty");
                            Initialized = false;
                            return;
                        }

                        propData.bladeCount = 1;
                        PropellerBlades.Add(childPart.partInfo.name, propData);

                    }
                    else
                    {
                        var propUpdate = PropellerBlades[childPart.partInfo.name];
                        propUpdate.bladeCount += 1;
                        PropellerBlades[childPart.partInfo.name] = propUpdate;
                    }
                }
            }

            foreach (var propSoundLayers in PropellerBlades.Values)
            {
                StartCoroutine(SetupAudioSources(propSoundLayers.soundLayers));
            }

            Initialized = true;
        }

        public override void LateUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || !Initialized || !vessel.loaded || GamePaused)
                return;

            if (SoundLayerGroups.Count > 0)
            {
                float intakeMultiplier = 1;
                bool motorEngaged = rotorModule.servoMotorIsEngaged && !rotorModule.servoIsLocked;

                if (resourceIntake != null)
                {
                    motorEngaged = rotorModule.servoMotorIsEngaged && !rotorModule.servoIsLocked && resourceIntake.intakeEnabled;
                    intakeMultiplier = resourceIntake.intakeEnabled ? Mathf.Min(resourceIntake.airFlow, 1) : 0;
                }

                float rpm = rotorModule.transformRateOfMotion / rotorModule.traverseVelocityLimits.y;
                float torque = rotorModule.totalTorque / rotorModule.maxTorque;

                foreach (var soundLayerGroup in SoundLayerGroups)
                {
                    float control = 0;

                    switch (soundLayerGroup.Key)
                    {
                        case "RPM":
                            control = rpm;
                            break;
                        case "Motor":
                            control = motorEngaged ? Mathf.Min(torque, rpm) * intakeMultiplier : 0;
                            if (rotorModule.servoIsBraking)
                            {
                                control *= 0.25f;
                            }
                            break;
                    }

                    foreach (var soundLayer in soundLayerGroup.Value)
                    {
                        string soundLayerName = soundLayerGroup.Key + "_" + soundLayer.name;

                        if (!Controls.ContainsKey(soundLayerName))
                        {
                            Controls.Add(soundLayerName, 0);
                        }

                        if (soundLayer.spool)
                        {
                            float spoolSpeed = Mathf.Max(soundLayer.spoolSpeed, control) * TimeWarp.deltaTime;
                            float spoolControl = Mathf.Lerp(motorEngaged ? soundLayer.spoolIdle : 0, 1, control);

                            Controls[soundLayerName] = Mathf.MoveTowards(Controls[soundLayerName], spoolControl, spoolSpeed);
                        }
                        else
                        {
                            float smoothControl = AudioUtility.SmoothControl.Evaluate(control) * (60 * Time.deltaTime);
                            Controls[soundLayerName] = Mathf.MoveTowards(Controls[soundLayerName], control, smoothControl);
                        }

                        PlaySoundLayer(soundLayer, Controls[soundLayerName], Volume);
                    }
                }
            }

            if (PropellerBlades.Count > 0)
            {
                float rotorRPM = (rotorModule.movingPartRB.angularVelocity.magnitude / 2 / Mathf.PI) * 60; //use the world space RPM instead of relative

                float atm = Mathf.Clamp((float)vessel.atmDensity, 0f, 1f); //only play prop sounds in an atmosphere

                if (childPartsCount != rotorModule.part.children.Count)
                {
                    SetupBlades();
                }

                foreach (var propellerBlade in PropellerBlades.Values)
                {
                    float propControl = rotorRPM / propellerBlade.baseRPM;
                    float propOverallVolume = propellerBlade.volume.Value(propControl) * atm;
                    float bladeMultiplier = Mathf.Clamp((float)propellerBlade.bladeCount / propellerBlade.maxBlades, 0, 2);
                    float control = propControl * bladeMultiplier;

                    foreach (var soundLayer in propellerBlade.soundLayers)
                    {
                        string soundLayerName = propellerBlade + "_" + "_" + soundLayer.name;

                        if (!Controls.ContainsKey(soundLayerName))
                        {
                            Controls.Add(soundLayerName, 0);
                        }

                        Controls[soundLayerName] = Mathf.MoveTowards(Controls[soundLayerName], control, AudioUtility.SmoothControl.Evaluate(control) * (60 * Time.deltaTime));

                        PlaySoundLayer(soundLayer, Controls[soundLayerName], propOverallVolume);
                    }
                }
            }

            base.LateUpdate();
        }
    }
}
