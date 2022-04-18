using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RocketSoundEnhancement
{
    public class RSE_Coupler : RSE_Module
    {
        FXGroup fxGroup;
        bool isDecoupler;

        public override void OnStart(StartState state)
        {
            if(state == StartState.Editor || state == StartState.None)
                return;

            string partParentName = part.name + "_" + this.moduleName;
            audioParent = AudioUtility.CreateAudioParent(part, partParentName);

            var configNode = AudioUtility.GetConfigNode(part.partInfo.name, this.moduleName);

            SoundLayers = AudioUtility.CreateSoundLayerGroup(configNode.GetNodes("SOUNDLAYER"));

            if(part.isLaunchClamp()) {
                fxGroup = part.findFxGroup("activate");
                isDecoupler = true;
            }

            if(part.GetComponent<ModuleDecouplerBase>()) {
                fxGroup = part.findFxGroup("decouple");
                isDecoupler = true;
            }

            if(fxGroup != null) {
                if(SoundLayers.Where(x => x.name == fxGroup.name).Count() > 0) {
                    var soundLayer = SoundLayers.Find(x => x.name == fxGroup.name);
                    if(soundLayer.audioClips != null) {
                        var clip = GameDatabase.Instance.GetAudioClip(soundLayer.audioClips[0]);
                        if(clip != null) {
                            fxGroup.sfx = clip;
                            fxGroup.audio = AudioUtility.CreateOneShotSource(
                                audioParent,
                                soundLayer.volume * GameSettings.SHIP_VOLUME,
                                soundLayer.pitch,
                                soundLayer.spread);

                            Sources.Add(soundLayer.name, fxGroup.audio);

                            var airSimFilter = Sources[soundLayer.name].gameObject.AddComponent<AirSimulationFilter>();
                            AirSimFilters.Add(soundLayer.name, airSimFilter);
                            airSimFilter.enabled = AudioMuffler.EnableMuffling && AudioMuffler.MufflerQuality == AudioMufflerQuality.AirSim;
                            airSimFilter.EnableLowpassFilter = true;
                            airSimFilter.SimulationUpdate = AirSimulationUpdate.Basic;
                        }
                    }
                }
            }

            GameEvents.onDockingComplete.Add(onDock);
            GameEvents.onPartUndockComplete.Add(onUnDock);
            initialized = true;
        }

        private void onUnDock(Part data)
        {
            if(part.flightID == data.flightID && !isDecoupler) {
                PlaySound("undock");
            }
        }

        private void onDock(GameEvents.FromToAction<Part, Part> data)
        {
            if(part.flightID == data.from.flightID && !isDecoupler) {
                PlaySound("dock");
            }
        }

        public override void OnUpdate()
        {
            if(!HighLogic.LoadedSceneIsFlight)
                return;

            var sourceKeys = Sources.Keys;
            if(AudioMuffler.EnableMuffling && AudioMuffler.MufflerQuality == AudioMufflerQuality.AirSim) {
                foreach(var source in sourceKeys) {
                    if(AirSimFilters.ContainsKey(source)) {
                        AirSimulationFilter airSimFilter = AirSimFilters[source];
                        if(Sources[source].isPlaying) {
                            airSimFilter.enabled = true;
                            airSimFilter.Distance = distance;
                        } else {
                            airSimFilter.enabled = false;
                        }
                    }
                }
            }

            foreach(var source in sourceKeys) {
                if(source == "decouple" || source == "activate") {
                    if(AudioMuffler.EnableMuffling) {
                        switch(AudioMuffler.MufflerQuality) {
                            case AudioMufflerQuality.Lite:
                                if(Sources[source].outputAudioMixerGroup != null) {
                                    Sources[source].outputAudioMixerGroup = null;
                                }
                                break;
                            case AudioMufflerQuality.Full | AudioMufflerQuality.AirSim:
                                Sources[source].outputAudioMixerGroup = vessel == FlightGlobals.ActiveVessel ? RSE.Instance.FocusMixer : RSE.Instance.ExternalMixer;
                                break;
                        }
                    }
                    continue;
                }

                if(!Sources[source].isPlaying) {
                    UnityEngine.Object.Destroy(Sources[source]);
                    Sources.Remove(source);
                }
            }
        }

        public void PlaySound(string action)
        {
            if(SoundLayers.Where(x => x.name == action).Count() > 0) {
                var soundLayer = SoundLayers.Find(x => x.name == action);

                if(soundLayer.audioClips == null)
                    return;

                AudioSource source;
                if(Sources.ContainsKey(action)) {
                    source = Sources[action];
                } else {
                    source = AudioUtility.CreateOneShotSource(
                        audioParent,
                        soundLayer.volume * GameSettings.SHIP_VOLUME,
                        soundLayer.pitch,
                        soundLayer.spread);
                    Sources.Add(soundLayer.name, source);
                }

                if(AudioMuffler.EnableMuffling) {
                    switch(AudioMuffler.MufflerQuality) {
                        case AudioMufflerQuality.Lite:
                            if(source.outputAudioMixerGroup != null) {
                                source.outputAudioMixerGroup = null;
                            }
                            break;
                        case AudioMufflerQuality.Full | AudioMufflerQuality.AirSim:
                            source.outputAudioMixerGroup = vessel == FlightGlobals.ActiveVessel ? RSE.Instance.FocusMixer : RSE.Instance.ExternalMixer;
                            break;
                    }
                }

                var clip = GameDatabase.Instance.GetAudioClip(soundLayer.audioClips[0]);
                if(clip != null) {
                    source.PlayOneShot(clip);
                }
            }
        }

        public new void OnDestroy()
        {
            foreach(var source in Sources.Keys) {
                GameObject.Destroy(Sources[source]);
            }

            GameEvents.onDockingComplete.Remove(onDock);
            GameEvents.onPartUndockComplete.Remove(onUnDock);
        }
    }
}
