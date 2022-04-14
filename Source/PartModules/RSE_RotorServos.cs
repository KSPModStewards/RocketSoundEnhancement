using Expansions.Serenity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RocketSoundEnhancement
{
    class RSE_RotorServos : RSE_Module
    {
        BaseServo servoModule;

        public override void OnStart(StartState state)
        {
            if(state == StartState.Editor || state == StartState.None)
                return;

            servoModule = part.GetComponent<BaseServo>();

            base.OnStart(state);
        }

        public override void OnUpdate()
        {
            if(!HighLogic.LoadedSceneIsFlight || gamePaused || !initialized)
                return;

        }
    }
}
