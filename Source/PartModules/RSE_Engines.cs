using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RocketSoundEnhancement
{
    public class RSE_Engines : RSE_Module
    {
        Dictionary<string, bool> ignites = new Dictionary<string, bool>();
        Dictionary<string, bool> flameouts = new Dictionary<string, bool>();

        List<ModuleEngines> engineModules = new List<ModuleEngines>();

        public override void OnStart(StartState state)
        {
            if(state == StartState.Editor || state == StartState.None)
                return;

            string partParentName = part.name + "_" + this.moduleName;
            audioParent = AudioUtility.CreateAudioParent(part, partParentName);

            var configNode = AudioUtility.GetConfigNode(part.partInfo.name, this.moduleName);
            engineModules = part.Modules.GetModules<ModuleEngines>();

            if(!float.TryParse(configNode.GetValue("volume"), out volume))
                volume = 1;

            SoundLayerGroups = new Dictionary<string, List<SoundLayer>>();
            Controls = new Dictionary<string, float>();
            foreach(var node in configNode.GetNodes()) {
                string _engineState = node.name;

                var soundLayers = AudioUtility.CreateSoundLayerGroup(node.GetNodes("SOUNDLAYER"));
                if(soundLayers.Count > 0) {
                    if(SoundLayerGroups.ContainsKey(_engineState)) {
                        SoundLayerGroups[_engineState].AddRange(soundLayers);
                    } else {
                        SoundLayerGroups.Add(_engineState, soundLayers);
                    }
                }
            }

            foreach(var engineModule in engineModules) {
                ignites.Add(engineModule.engineID, engineModule.EngineIgnited);
                flameouts.Add(engineModule.engineID, engineModule.flameout);
            }

            UseAirSimFilters = true;
            EnableCombFilter = true;
            EnableLowpassFilter = true;
            EnableWaveShaperFilter = true;

            base.OnStart(state);
        }

        public override void OnUpdate()
        {
            if(audioParent == null || !HighLogic.LoadedSceneIsFlight || gamePaused || !initialized)
                return;

            foreach(var engineModule in engineModules) {
                string engineID = engineModule.engineID;
                bool engineIgnited = engineModule.EngineIgnited;
                bool engineFlameout = engineModule.flameout;
                float rawControl = engineModule.GetCurrentThrust() / engineModule.maxThrust;

                if(SoundLayerGroups.ContainsKey(engineID)) {
                    foreach(var soundLayer in SoundLayerGroups[engineID]) {
                        string sourceLayerName = engineID + "_" + soundLayer.name;
                        if(!Controls.ContainsKey(sourceLayerName)) {
                            Controls.Add(sourceLayerName, 0);
                        }

                        if(soundLayer.spool) {
                            float spoolIdle = engineModule.EngineIgnited ? soundLayer.spoolIdle : 0;
                            float spoolControl = Mathf.Lerp(spoolIdle, 1.0f, soundLayer.data.Contains("Turbine") ? engineModule.requestedThrottle : rawControl);
                            float spoolSpeed = engineModule.EngineIgnited ? soundLayer.spoolSpeed : Mathf.Max(soundLayer.spoolSpeed, spoolControl);
                            spoolSpeed *= TimeWarp.deltaTime;

                            if(engineModule.flameout) {
                                Controls[sourceLayerName] = Mathf.MoveTowards(Controls[sourceLayerName], 0, spoolSpeed);
                            } else {
                                Controls[sourceLayerName] = Mathf.MoveTowards(Controls[sourceLayerName], spoolControl, spoolSpeed);
                            }

                            if(!soundLayer.data.Contains("Turbine") && (!engineIgnited || engineFlameout)) {
                                Controls[sourceLayerName] = Mathf.MoveTowards(Controls[sourceLayerName], 0, AudioUtility.SmoothControl.Evaluate(rawControl) * (60 * Time.deltaTime));
                            }

                        } else {
                            //fix for audiosource clicks
                            Controls[sourceLayerName] = Mathf.MoveTowards(Controls[sourceLayerName], rawControl, AudioUtility.SmoothControl.Evaluate(rawControl) * (60 * Time.deltaTime));
                        }

                        PlaySoundLayer(audioParent, sourceLayerName, soundLayer, Controls[sourceLayerName], volume, false);
                    }
                }

                foreach(var soundLayer in SoundLayerGroups) {
                    switch(soundLayer.Key) {
                        case "Engage":
                            if(engineIgnited && !ignites[engineID]) {
                                ignites[engineID] = true;
                            } else {
                                if(!SoundLayerGroups.ContainsKey("Disengage"))
                                    ignites[engineID] = engineIgnited;
                                continue;
                            }
                            break;
                        case "Disengage":
                            if(!engineIgnited && ignites[engineID]) {
                                ignites[engineID] = false;
                            } else {
                                if(!SoundLayerGroups.ContainsKey("Engage"))
                                    ignites[engineID] = engineIgnited;
                                continue;
                            }
                            break;
                        case "Flameout":
                            if(engineFlameout && !flameouts[engineID]) {
                                flameouts[engineID] = true;
                            } else {
                                flameouts[engineID] = engineFlameout;
                                continue;
                            }
                            break;
                        default:
                            continue;
                    }

                    var oneShotLayers = soundLayer.Value;
                    foreach(var oneShotLayer in oneShotLayers) {
                        string oneShotLayerName = soundLayer.Key + "_" + oneShotLayer.name;
                        PlaySoundLayer(audioParent, oneShotLayerName, oneShotLayer, 1, volume, false, true);
                    }
                }
            }

            base.OnUpdate();
        }

    }
}
