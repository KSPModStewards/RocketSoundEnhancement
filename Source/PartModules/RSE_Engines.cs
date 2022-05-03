using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RocketSoundEnhancement
{
    public class RSE_Engines : RSE_Module
    {
        Dictionary<string, bool> ignites = new Dictionary<string, bool>();
        Dictionary<string, bool> flameouts = new Dictionary<string, bool>();
        Dictionary<string, bool> bursts = new Dictionary<string, bool>();

        List<ModuleEngines> engineModules = new List<ModuleEngines>();

        public override void OnStart(StartState state)
        {
            if(state == StartState.Editor || state == StartState.None)
                return;

            base.OnStart(state);

            engineModules = part.Modules.GetModules<ModuleEngines>();
            foreach(var engineModule in engineModules) {
                ignites.Add(engineModule.engineID, engineModule.EngineIgnited);
                flameouts.Add(engineModule.engineID, engineModule.flameout);
                bursts.Add(engineModule.engineID, false);
            }

            initialized = true;
        }

        public override void OnUpdate()
        {
            if(!HighLogic.LoadedSceneIsFlight || gamePaused || !initialized)
                return;

            foreach(var engineModule in engineModules) {
                string engineID = engineModule.engineID;
                bool engineIgnited = engineModule.EngineIgnited;
                bool engineFlameout = engineModule.flameout;
                float rawControl = engineModule.GetCurrentThrust() / engineModule.maxThrust;

                if(SoundLayerGroups.ContainsKey(engineID)) {
                    foreach(var soundLayer in SoundLayerGroups[engineID]) {
                        string sourceLayerName = engineID + "_" + soundLayer.name;
                        float control = soundLayer.data.Contains("Turbine") ? Mathf.Min(engineModule.requestedThrottle, rawControl) : rawControl;

                        if(!Controls.ContainsKey(sourceLayerName)) {
                            Controls.Add(sourceLayerName, 0);
                        }

                       if(soundLayer.spool) {
                            float spoolSpeed = Mathf.Max(soundLayer.spoolSpeed, control) * TimeWarp.deltaTime;
                            float spoolControl = Mathf.Lerp(engineIgnited ? soundLayer.spoolIdle : 0, 1, control); 
                            spoolControl = engineFlameout ? 0 : spoolControl;

                            if(!soundLayer.data.Contains("Turbine") && (!engineIgnited || engineFlameout)) {
                                spoolSpeed = AudioUtility.SmoothControl.Evaluate(rawControl) * (60 * Time.deltaTime);
                            }
                            
                            Controls[sourceLayerName] = Mathf.MoveTowards(Controls[sourceLayerName], spoolControl, spoolSpeed);

                        } else {
                            float smoothControl = AudioUtility.SmoothControl.Evaluate(Mathf.Max(Controls[sourceLayerName], control)) * (60 * Time.deltaTime);
                            Controls[sourceLayerName] = Mathf.MoveTowards(Controls[sourceLayerName], control, smoothControl);
                        }

                        PlaySoundLayer(sourceLayerName, soundLayer, Controls[sourceLayerName], Volume);
                    }
                }

                foreach(var soundLayer in SoundLayerGroups) {
                    float control = 1;
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
                        case "Burst":
                            control = rawControl;
                            if(engineIgnited && rawControl > 0) {
                                if(!bursts[engineID]) {
                                    bursts[engineID] = true;
                                } else {
                                    continue;
                                }
                            } else {
                                bursts[engineID] = false;
                                continue;
                            }
                            break;
                        default:
                            continue;
                    }

                    var oneShotLayers = soundLayer.Value;
                    foreach(var oneShotLayer in oneShotLayers) {
                        string oneShotLayerName = soundLayer.Key + "_" + oneShotLayer.name;
                        PlaySoundLayer(oneShotLayerName, oneShotLayer, control, Volume, true);
                    }
                }
            }

            base.OnUpdate();
        }

    }
}
