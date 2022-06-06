using System.Collections.Generic;
using UnityEngine;

namespace RocketSoundEnhancement.PartModules
{
    public class RSE_Engines : RSE_Module
    {
        public Dictionary<string, bool> ignites = new Dictionary<string, bool>();
        public Dictionary<string, bool> flameouts = new Dictionary<string, bool>();
        public Dictionary<string, bool> bursts = new Dictionary<string, bool>();

        public Dictionary<string, int> sharedSoundLayers = new Dictionary<string, int>();
        private List<ModuleEngines> engineModules = new List<ModuleEngines>();

        public override void OnStart(StartState state)
        {
            if(state == StartState.Editor || state == StartState.None)
                return;

            EnableLowpassFilter = true;
            EnableCombFilter = true;
            EnableWaveShaperFilter = true;
            base.OnStart(state);

            var soundLayersCache = new HashSet<string>();
            engineModules = part.Modules.GetModules<ModuleEngines>();
            foreach (var engineModule in engineModules)
            {
                if (engineModules.Count > 1)
                {
                    foreach (var soundLayer in SoundLayerGroups[engineModule.engineID])
                    {
                        if (sharedSoundLayers.ContainsKey(soundLayer.name))
                        {
                            sharedSoundLayers[soundLayer.name] += 1;
                            continue;
                        }

                        if (soundLayersCache.Contains(soundLayer.name))
                        {
                            sharedSoundLayers.Add(soundLayer.name, 1);
                            continue;
                        }
                        soundLayersCache.Add(soundLayer.name);
                    }
                }

                ignites.Add(engineModule.engineID, engineModule.EngineIgnited);
                flameouts.Add(engineModule.engineID, engineModule.flameout);
                bursts.Add(engineModule.engineID, false);
            }

            Initialized = true;
        }
        
        public override void LateUpdate()
        {
            if(!HighLogic.LoadedSceneIsFlight || !Initialized || !vessel.loaded || GamePaused)
                return;

            foreach (var engineModule in engineModules)
            {
                var engineID = engineModule.engineID;
                var engineIgnited = engineModule.EngineIgnited;
                var engineFlameout = engineModule.flameout;
                var currentThrust = engineModule.GetCurrentThrust() / engineModule.maxThrust;

                if (SoundLayerGroups.ContainsKey(engineID))
                {
                    foreach (var soundLayer in SoundLayerGroups[engineID])
                    {
                        if (sharedSoundLayers.ContainsKey(soundLayer.name) && !engineModule.isEnabled)
                            continue;

                        float control = currentThrust;

                        if (!Controls.ContainsKey(soundLayer.name))
                        {
                            Controls.Add(soundLayer.name, 0);
                        }

                        if (soundLayer.spool)
                        {
                            float spoolSpeed = Mathf.Max(soundLayer.spoolSpeed, control) * TimeWarp.deltaTime;
                            float spoolControl = Mathf.Lerp(engineIgnited ? soundLayer.spoolIdle : 0, 1, control);
                            spoolControl = engineFlameout ? 0 : spoolControl;

                            if (!soundLayer.data.Contains("Turbine") && (!engineIgnited || engineFlameout))
                            {
                                spoolSpeed = AudioUtility.SmoothControl.Evaluate(control) * (60 * Time.deltaTime);
                            }

                            Controls[soundLayer.name] = Mathf.MoveTowards(Controls[soundLayer.name], spoolControl, spoolSpeed);
                        }
                        else
                        {
                            float smoothControl = AudioUtility.SmoothControl.Evaluate(control) * (60 * Time.deltaTime);
                            Controls[soundLayer.name] = Mathf.MoveTowards(Controls[soundLayer.name], control, smoothControl);
                        }

                        PlaySoundLayer(soundLayer, Controls[soundLayer.name], Volume);
                    }
                }

                foreach (var soundLayerGroup in SoundLayerGroups) {
                    float control = currentThrust;
                    switch(soundLayerGroup.Key) {
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
                            if (flameouts[engineID] == engineFlameout)
                                continue;

                            flameouts[engineID] = engineFlameout;
                            break;
                        case "Burst":
                            if(engineIgnited && currentThrust > 0) {
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

                    foreach (var soundLayer in soundLayerGroup.Value)
                    {
                        PlaySoundLayer(soundLayer, control, Volume);
                    }
                }
            }

            base.LateUpdate();
        }

    }
}
