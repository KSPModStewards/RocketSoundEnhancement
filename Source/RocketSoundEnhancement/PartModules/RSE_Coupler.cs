using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RocketSoundEnhancement.PartModules
{
    public class RSE_Coupler : RSE_Module
    {
        ModuleDecouplerBase moduleDecoupler;
        bool isDecoupler;
        bool hasDecoupled;

        public override void OnStart(StartState state)
        {
            if(state == StartState.Editor || state == StartState.None)
                return;

            EnableLowpassFilter = true;
            EnableWaveShaperFilter = true;
            base.OnStart(state);

            if(part.GetComponent<ModuleDecouplerBase>()) {
                moduleDecoupler = part.GetComponent<ModuleDecouplerBase>();
                hasDecoupled = moduleDecoupler.isDecoupled;
                isDecoupler = true;
            }

            if (SoundLayerGroups.ContainsKey("LaunchClamp") && part.isLaunchClamp())
            {
                var clampSoundLayer = SoundLayerGroups["LaunchClamp"].FirstOrDefault();
                if (clampSoundLayer.audioClips != null)
                {
                    var fxGroup = part.GetComponent<LaunchClamp>().releaseFx;
                    int index = clampSoundLayer.audioClips.Length > 1 ? UnityEngine.Random.Range(0, clampSoundLayer.audioClips.Length) : 0;
                    fxGroup.sfx = clampSoundLayer.audioClips[index];
                    fxGroup.audio = AudioUtility.CreateSource(audioParent, clampSoundLayer);
                    Sources.Add("launchClamp", fxGroup.audio);
                }
            }

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

        public override void LateUpdate()
        {
            if(!HighLogic.LoadedSceneIsFlight || !initialized || !vessel.loaded || gamePaused)
                return;

            if(moduleDecoupler != null && SoundLayerGroups.ContainsKey("Decouple")) {
                if(moduleDecoupler.isDecoupled && !hasDecoupled) {
                    foreach(var soundlayer in SoundLayerGroups["Decouple"]) {
                        PlaySoundLayer(soundlayer, 1, 1);
                    }
                    hasDecoupled = moduleDecoupler.isDecoupled;
                }
            }

            base.LateUpdate();
        }

        public void PlaySound(string action)
        {
            if (SoundLayerGroups.ContainsKey(action))
            {
                foreach (var soundLayer in SoundLayerGroups[action])
                {
                    PlaySoundLayer(soundLayer, 1, 1);
                }
            }
        }

        public override void OnDestroy()
        {
            GameEvents.onDockingComplete.Remove(onDock);
            GameEvents.onPartUndockComplete.Remove(onUnDock);

            base.OnDestroy();
        }
    }
}
