using UnityEngine;

namespace RocketSoundEnhancement.PartModules
{
    public class RSE_RCS : RSE_Module
    {
        private ModuleRCSFX moduleRCSFX;

        public override void OnStart(StartState state)
        {
            if(state == StartState.Editor || state == StartState.None)
                return;

            EnableLowpassFilter = true;
            base.OnStart(state);

            moduleRCSFX = part.Modules.GetModule<ModuleRCSFX>();
            Initialized = true;
        }

        public override void LateUpdate()
        {
            if(!HighLogic.LoadedSceneIsFlight || !Initialized || !vessel.loaded || GamePaused)
                return;

            var thrustTransforms = moduleRCSFX.thrusterTransforms;
            var thrustForces = moduleRCSFX.thrustForces;

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

                float smoothControl = AudioUtility.SmoothControl.Evaluate(control) * (30 * Time.deltaTime);
                Controls[sourceLayerName] = Mathf.MoveTowards(Controls[sourceLayerName], control, smoothControl);
                
                PlaySoundLayer(soundLayer, Controls[sourceLayerName], Volume * thrustTransforms.Count);
            }

            base.LateUpdate();
        }
    }
}
