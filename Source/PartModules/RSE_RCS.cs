using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RocketSoundEnhancement
{
    public class RSE_RCS : RSE_Module
    {
        ModuleRCSFX moduleRCSFX;

        public override void OnStart(StartState state)
        {
            if(state == StartState.Editor || state == StartState.None)
                return;

            base.OnStart(state);

            moduleRCSFX = part.Modules.GetModule<ModuleRCSFX>();
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

                PlaySoundLayer(audioParent, sourceLayerName, soundLayer, rawControl / thrustTransforms.Count, Volume * thrustTransforms.Count);
            }

            base.OnUpdate();
        }
    }
}
