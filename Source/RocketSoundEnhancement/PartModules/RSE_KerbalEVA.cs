using System.Linq;
using UnityEngine;

namespace RocketSoundEnhancement.PartModules
{
    public class RSE_KerbalEVA : RSE_Module
    {
        public RSE_KerbalEVA()
        {
            EnableLowpassFilter = true;
        }

        public override void OnStart(StartState state)
        {
            if (state == StartState.Editor || state == StartState.None)
                return;

            base.OnStart(state);

            Initialized = true;
        }

        public override void LateUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || !Initialized || !vessel.loaded || GamePaused)
                return;

            foreach (var soundLayer in SoundLayers)
            {
                string sourceLayerName = soundLayer.name;

                if (!Controls.ContainsKey(sourceLayerName))
                {
                    Controls.Add(sourceLayerName, 0);
                }
                var fxGroup = part.fxGroups.FirstOrDefault(g => g.name == sourceLayerName);

                float control = 0;
                if (fxGroup.activeLatch) // thruster is firing
                {
                    control = fxGroup.power;
                }
                float smoothControl = AudioUtility.SmoothControl.Evaluate(control) * (60 * Time.deltaTime);
                Controls[sourceLayerName] = Mathf.MoveTowards(Controls[sourceLayerName], control, smoothControl);

                PlaySoundLayer(soundLayer, Controls[sourceLayerName], Volume);
            }
        }
    }
}
