using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RocketSoundEnhancement
{
    class RSE_Engines : PartModule
    {
        public Dictionary<string, List<SoundLayer>> SoundLayerGroups = new Dictionary<string, List<SoundLayer>>();
        public Dictionary<string, AudioSource> Sources = new Dictionary<string, AudioSource>();

        [KSPField(isPersistant = false)]
        public float volume = 1;

        GameObject audioParent;
        List<ModuleEngines> engineModules = new List<ModuleEngines>();
        Dictionary<string, bool> ignites = new Dictionary<string, bool>();
        Dictionary<string, bool> flameouts = new Dictionary<string, bool>();
        Dictionary<string, float> spools = new Dictionary<string, float>();

        bool initialized;
        bool gamePaused;

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
                    foreach(var soundLayer in SoundLayerGroups[engineID]) {
                        string sourceLayerName = engineID + "_" + soundLayer.name;

                        if(soundLayer.spool && !engineModule.flameout) {
                            if(!spools.ContainsKey(sourceLayerName)) {
                                spools.Add(sourceLayerName, 0);
                            }
                            float idle = 0;
                            if(engineModule.EngineIgnited) {
                                idle = soundLayer.spoolIdle;
                            }

                            spools[sourceLayerName] = Mathf.MoveTowards(spools[sourceLayerName], Mathf.Max(idle, engineModule.currentThrottle), soundLayer.spoolTime * Time.deltaTime);
                            control = spools[sourceLayerName];
                        }

                        //For Looped sounds cleanup
                        if(control < 0.01f) {
                            if(Sources.ContainsKey(sourceLayerName)) {
                                UnityEngine.Object.Destroy(Sources[sourceLayerName]);
                                Sources.Remove(sourceLayerName);
                            }
                            continue;
                        }

                        float finalVolume = soundLayer.volume.Value(control);
                        float finalPitch = soundLayer.pitch.Value(control);

                        AudioSource source;
                        if(!Sources.ContainsKey(sourceLayerName)) {
                            source = AudioUtility.CreateSource(audioParent, soundLayer);
                            Sources.Add(sourceLayerName, source);
                        } else {
                            source = Sources[sourceLayerName];
                        }

                        source.volume = finalVolume * volume * HighLogic.CurrentGame.Parameters.CustomParams<Settings>().ShipVolume;
                        source.pitch = finalPitch;

                        AudioUtility.PlayAtChannel(source, soundLayer.channel, soundLayer.loop, soundLayer.loopAtRandom);
                    }
                }

                foreach(var soundLayer in SoundLayerGroups) {
                    switch(soundLayer.Key) {
                        case "Engage":
                            if(engineIgnited && !ignites[engineID]) {
                                ignites[engineID] = true;
                            } else {
                                ignites[engineID] = engineIgnited;
                                continue;
                            }
                            break;
                        case "Disengage":
                            if(!engineIgnited && ignites[engineID]) {
                                ignites[engineID] = false;
                            } else {
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
                        AudioSource source;
                        if(Sources.ContainsKey(oneShotLayerName)) {
                            source = Sources[oneShotLayerName];
                        } else {
                            float vol = oneShotLayer.volume * volume * HighLogic.CurrentGame.Parameters.CustomParams<Settings>().ShipVolume;
                            source = AudioUtility.CreateOneShotSource(audioParent, vol, oneShotLayer.pitch, oneShotLayer.maxDistance);
                            Sources.Add(oneShotLayerName, source);
                        }
                        source.PlayOneShot(oneShotLayer.audioClip);
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
