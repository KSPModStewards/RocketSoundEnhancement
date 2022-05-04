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
            float control = 0;
            for(int i = 0; i < thrustTransforms.Count; i++) {
                control += thrustForces[i] / moduleRCSFX.thrusterPower;
            }

            control /= thrustTransforms.Count > 0 ? thrustTransforms.Count : 1;

            foreach(var soundLayer in SoundLayers) {
                string sourceLayerName = soundLayer.name;

                if(!Controls.ContainsKey(sourceLayerName)) {
                    Controls.Add(sourceLayerName, 0);
                }

                float smoothControl = AudioUtility.SmoothControl.Evaluate(control) * (60 * Time.deltaTime);
                Controls[sourceLayerName] = Mathf.MoveTowards(Controls[sourceLayerName], control, smoothControl);
                
                PlaySoundLayer(sourceLayerName, soundLayer, Controls[sourceLayerName], Volume * thrustTransforms.Count);
            }

            base.OnUpdate();
        }
    }
}
