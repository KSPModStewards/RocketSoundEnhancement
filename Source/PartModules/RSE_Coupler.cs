using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RocketSoundEnhancement
{
    public class RSE_Coupler : RSE_Module
    {
        AudioSource launchClampSource;
        ModuleDecouplerBase moduleDecoupler;
        FXGroup fxGroup;
        bool isDecoupler;
        bool hasDecoupled;

        public override void OnStart(StartState state)
        {
            if(state == StartState.Editor || state == StartState.None)
                return;

            base.OnStart(state);

            if(part.isLaunchClamp()) {
                fxGroup = part.findFxGroup("activate");
            }

            if(part.GetComponent<ModuleDecouplerBase>()) {
                moduleDecoupler = part.GetComponent<ModuleDecouplerBase>();
                hasDecoupled = moduleDecoupler.isDecoupled;
                isDecoupler = true;
            }

            if(fxGroup != null) {
                if(SoundLayers.Where(x => x.name == fxGroup.name).Count() > 0) {
                    var soundLayer = SoundLayers.Find(x => x.name == fxGroup.name);
                    if(soundLayer.audioClips != null) {
                        int index = UnityEngine.Random.Range(0, soundLayer.audioClips.Length);
                        var clip = GameDatabase.Instance.GetAudioClip(soundLayer.audioClips[index]);
                        if(clip != null) {
                            fxGroup.sfx = clip;
                            fxGroup.audio = AudioUtility.CreateOneShotSource(
                                audioParent,
                                soundLayer.volume * GameSettings.SHIP_VOLUME,
                                soundLayer.pitch,
                                soundLayer.spread);
                            launchClampSource = fxGroup.audio;
                        }
                    }
                }
            }

            UseAirSimFilters = true;
            EnableLowpassFilter = true;

            GameEvents.onDockingComplete.Add(onDock);
            GameEvents.onPartUndockComplete.Add(onUnDock);

            initialized = true;
        }

        private void onUnDock(Part data)
        {
            if(part.flightID == data.flightID && !isDecoupler) {
                PlaySound("Undock");
            }
        }

        private void onDock(GameEvents.FromToAction<Part, Part> data)
        {
            if(part.flightID == data.from.flightID && !isDecoupler) {
                PlaySound("Dock");
            }
        }

        public override void OnUpdate()
        {
            if(!HighLogic.LoadedSceneIsFlight && !initialized)
                return;

            if(moduleDecoupler != null && SoundLayerGroups.ContainsKey("Decouple")) {
                if(moduleDecoupler.isDecoupled && !hasDecoupled) {
                    foreach(var soundlayer in SoundLayerGroups["Decouple"]) {
                        PlaySoundLayer(audioParent, soundlayer.name, soundlayer, 1, 1, false, true);
                    }
                    hasDecoupled = moduleDecoupler.isDecoupled;
                }
            }

            if(launchClampSource != null) {
                if(AudioMuffler.EnableMuffling) {
                    switch(AudioMuffler.MufflerQuality) {
                        case AudioMufflerQuality.Lite:
                            if(launchClampSource.outputAudioMixerGroup != null) {
                                launchClampSource.outputAudioMixerGroup = null;
                            }
                            break;
                        case AudioMufflerQuality.Full:
                        case AudioMufflerQuality.AirSim:
                            launchClampSource.outputAudioMixerGroup = vessel.isActiveVessel ? RSE.Instance.FocusMixer : RSE.Instance.ExternalMixer;
                            break;
                    }
                }
            }

            base.OnUpdate();
        }

        public void PlaySound(string action)
        {
            if(SoundLayerGroups.ContainsKey(action)) {
                foreach(var soundLayer in SoundLayerGroups[action]) {
                    PlaySoundLayer(audioParent, action + "_" + soundLayer.name, soundLayer, 1, 1, false, true);
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
