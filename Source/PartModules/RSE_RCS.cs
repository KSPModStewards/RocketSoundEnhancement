using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RocketSoundEnhancement
{
    public class RSE_RCS : RSE_Module
    {
        ModuleRCSFX moduleRCSFX;

        float[] lastThrustControl;

        public override void OnStart(StartState state)
        {
            if(state == StartState.Editor || state == StartState.None)
                return;

            moduleRCSFX = part.Modules.GetModule<ModuleRCSFX>();
            lastThrustControl = new float[moduleRCSFX.thrustForces.Length];

            var configNode = AudioUtility.GetConfigNode(part.partInfo.name, this.moduleName);
            if(!float.TryParse(configNode.GetValue("volume"), out volume))
                volume = 1;

            SoundLayers = AudioUtility.CreateSoundLayerGroup(configNode.GetNodes("SOUNDLAYER"));

            base.OnStart(state);

            UseAirSimFilters = true;
            EnableLowpassFilter = true;

            initialized = true;
        }

        public override void OnUpdate()
        {
            if(!HighLogic.LoadedSceneIsFlight || gamePaused || !initialized)
                return;

            var thrustTransforms = moduleRCSFX.thrusterTransforms;
            var thrustForces = moduleRCSFX.thrustForces;

            //Cheaper to use one AudioSource.
            float rawControl = 0;
            for(int i = 0; i < thrustTransforms.Count; i++) {
                rawControl += thrustForces[i] / moduleRCSFX.thrusterPower;
            }

            foreach(var soundLayer in SoundLayers) {
                string sourceLayerName = soundLayer.name;

                PlaySoundLayer(gameObject, sourceLayerName, soundLayer, rawControl / thrustTransforms.Count, volume * thrustTransforms.Count);
            }

            base.OnUpdate();
        }
    }
}
