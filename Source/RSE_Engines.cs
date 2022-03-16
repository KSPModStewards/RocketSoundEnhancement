using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RocketSoundEnhancement
{
    class RSE_Engines : PartModule
    {
        Dictionary<string, List<SoundLayer>> SoundLayerGroups = new Dictionary<string, List<SoundLayer>>();
        Dictionary<string, AudioSource> Sources = new Dictionary<string, AudioSource>();
        Dictionary<string, float> spools = new Dictionary<string, float>();
        Dictionary<string, bool> ignites = new Dictionary<string, bool>();
        Dictionary<string, bool> flameouts = new Dictionary<string, bool>();

        bool initialized;
        bool gamePaused;

        List<ModuleEngines> engineModules = new List<ModuleEngines>();
        GameObject audioParent;

        public override void OnStart(StartState state)
        {
            if(state == StartState.Editor || state == StartState.None)
                return;

            string partParentName = part.name + "_" + this.moduleName;
            audioParent = part.gameObject.GetChild(partParentName);
            if(audioParent == null) {
                audioParent = new GameObject(partParentName);
                audioParent.transform.rotation = part.transform.rotation;
                audioParent.transform.position = part.transform.position;
                audioParent.transform.parent = part.transform;
            }

            var configNode = AudioUtility.GetConfigNode(part.partInfo.name, this.moduleName);

            engineModules = part.Modules.GetModules<ModuleEngines>();

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

            initialized = true;

            GameEvents.onGamePause.Add(onGamePause);
            GameEvents.onGameUnpause.Add(onGameUnpause);
        }

        public override void OnUpdate()
        {
            if(audioParent == null || !HighLogic.LoadedSceneIsFlight || gamePaused || !initialized)
                return;

            foreach(var engineModule in engineModules) {
                string engineID = engineModule.engineID;
                bool engineIgnited = engineModule.EngineIgnited;
                bool engineFlameout = engineModule.flameout;

                float control = engineModule.GetCurrentThrust() / engineModule.maxThrust;

                if(SoundLayerGroups.ContainsKey(engineID)) {
                    float finalControl = control;

                    foreach(var soundLayer in SoundLayerGroups[engineID]) {
                        string sourceLayerName = engineID + "_" + soundLayer.name;

                        if(!spools.ContainsKey(sourceLayerName)) {
                            spools.Add(sourceLayerName, 0);
                        }

                        if(soundLayer.spool) {
                            if(engineModule.flameout) {
                                spools[sourceLayerName] = Mathf.MoveTowards(spools[sourceLayerName], 0, Mathf.Max(0.1f, engineModule.currentThrottle));
                            } else {
                                float idle = engineModule.EngineIgnited ? soundLayer.spoolIdle : 0;
                                spools[sourceLayerName] = Mathf.MoveTowards(spools[sourceLayerName], Mathf.Max(idle, engineModule.currentThrottle), soundLayer.spoolSpeed * TimeWarp.deltaTime);
                                finalControl = spools[sourceLayerName];
                            }
                        } else {
                            //fix for audiosource clicks
                            spools[sourceLayerName] = Mathf.MoveTowards(spools[sourceLayerName], control, Mathf.Max(0.1f, control));
                            finalControl = spools[sourceLayerName];
                        }

                        //For Looped sounds cleanup
                        if(finalControl < float.Epsilon) {
                            if(Sources.ContainsKey(sourceLayerName)) {
                                Sources[sourceLayerName].Stop();
                            }
                            continue;
                        }

                        AudioSource source;
                        if(!Sources.ContainsKey(sourceLayerName)) {
                            source = AudioUtility.CreateSource(audioParent, soundLayer);
                            Sources.Add(sourceLayerName, source);
                        } else {
                            source = Sources[sourceLayerName];
                        }

                        source.volume = soundLayer.volume.Value(finalControl) * GameSettings.SHIP_VOLUME;
                        source.pitch = soundLayer.pitch.Value(finalControl);

                        AudioUtility.PlayAtChannel(source, soundLayer.channel, soundLayer.loop, soundLayer.loopAtRandom);
                    }
                }

                foreach(var soundLayer in SoundLayerGroups) {
                    switch(soundLayer.Key) {
                        case "Engage":
                            if(engineIgnited && !ignites[engineID]) {
                                ignites[engineID] = true;
                            } else {
                                continue;
                            }
                            break;
                        case "Disengage":
                            if(!engineIgnited && ignites[engineID]) {
                                ignites[engineID] = false;
                            } else {
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

                    ignites[engineID] = engineIgnited;

                    var oneShotLayers = soundLayer.Value;
                    foreach(var oneShotLayer in oneShotLayers) {
                        if(oneShotLayer.audioClips != null) {
                            var clip = GameDatabase.Instance.GetAudioClip(oneShotLayer.audioClips[0]);
                            string oneShotLayerName = soundLayer.Key + "_" + oneShotLayer.name;

                            AudioSource source;

                            if(Sources.ContainsKey(oneShotLayerName)) {
                                source = Sources[oneShotLayerName];
                            } else {
                                source = AudioUtility.CreateOneShotSource(audioParent, 1, oneShotLayer.pitch, oneShotLayer.maxDistance, oneShotLayer.spread);
                                Sources.Add(oneShotLayerName, source);
                            }

                            float finalVolume = oneShotLayer.volume * GameSettings.SHIP_VOLUME;
                            AudioUtility.PlayAtChannel(source, oneShotLayer.channel, false, false, true, finalVolume, clip);
                        }
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
