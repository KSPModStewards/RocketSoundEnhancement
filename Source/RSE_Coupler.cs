using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RocketSoundEnhancement
{
    public class RSE_Coupler : PartModule
    {
        public List<SoundLayer> SoundLayers = new List<SoundLayer>();
        public Dictionary<string, AudioSource> Sources = new Dictionary<string, AudioSource>();

        FXGroup fxGroup;
        GameObject audioParent;
        bool isDecoupler;

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

                    if(soundLayer.audioClip != null) {
                        fxGroup.sfx = soundLayer.audioClip;
                        fxGroup.audio = AudioUtility.CreateOneShotSource(
                            audioParent,
                            soundLayer.volume * HighLogic.CurrentGame.Parameters.CustomParams<Settings>().ShipVolume,
                            soundLayer.pitch,
                            soundLayer.maxDistance,
                            soundLayer.spread);

                        Sources.Add(soundLayer.name, fxGroup.audio);
                    }
                }
            }

            GameEvents.onGameUnpause.Add(onGameUnpause);
            GameEvents.onDockingComplete.Add(onDock);
            GameEvents.onPartUndockComplete.Add(onUnDock);
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

        private void onGameUnpause()
        {
            foreach(var sound in SoundLayers) {
                if(Sources.ContainsKey(sound.name)) {
                    Sources[sound.name].volume = sound.volume * HighLogic.CurrentGame.Parameters.CustomParams<Settings>().ShipVolume;
                }
            }
        }

        public override void OnUpdate()
        {
            if(!HighLogic.LoadedSceneIsFlight)
                return;

            foreach(var asource in Sources.Keys) {
                if(asource == "decouple" || asource == "activate")
                    continue;

                if(!Sources[asource].isPlaying) {
                    UnityEngine.Object.Destroy(Sources[asource]);
                    Sources.Remove(asource);
                }
            }
        }

        public void PlaySound(string action)
        {
            if(SoundLayers.Where(x => x.name == action).Count() > 0) {
                var soundLayer = SoundLayers.Find(x => x.name == action);

                if(soundLayer.audioClip == null)
                    return;

                AudioSource source;
                if(Sources.ContainsKey(action)) {
                    source = Sources[action];
                } else {
                    source = AudioUtility.CreateOneShotSource(
                        audioParent,
                        soundLayer.volume * HighLogic.CurrentGame.Parameters.CustomParams<Settings>().ShipVolume,
                        soundLayer.pitch,
                        soundLayer.maxDistance,
                        soundLayer.spread);
                    Sources.Add(soundLayer.name, source);
                }

                source.PlayOneShot(soundLayer.audioClip);
            }
        }

        void OnDestroy()
        {
            foreach(var source in Sources.Keys) {
                GameObject.Destroy(Sources[source]);
            }

            GameEvents.onGameUnpause.Remove(onGameUnpause);
            GameEvents.onDockingComplete.Remove(onDock);
            GameEvents.onPartUndockComplete.Remove(onUnDock);
        }
    }
}
