using System;
using System.Reflection;

namespace RocketSoundEnhancement
{
    public class Settings : GameParameters.CustomParameterNode
    {
        public override string Title { get { return "Rocket Sound Enhancement Settings"; } }
        public override GameParameters.GameMode GameMode { get { return GameParameters.GameMode.ANY; } }
        public override string Section { get { return "Rocket Sound Enhancement"; } }
        public override string DisplaySection { get { return "Rocket Sound Enhancement"; } }
        public override int SectionOrder { get { return 1; } }
        public override bool HasPresets { get { return false; } }

        [GameParameters.CustomFloatParameterUI("Effects Volume", minValue = 0f, maxValue = 2f, displayFormat = "P0")]
        public float EffectsVolume = 1f;

        [GameParameters.CustomFloatParameterUI("Ship Volume", minValue = 0f, maxValue = 2f, displayFormat = "P0")]
        public float ShipVolume = 1f;

        [GameParameters.CustomParameterUI("Disable Staging Sound", toolTip = "Requires Save and Reload")]
        public bool DisableStagingSound = false;

        [GameParameters.CustomParameterUI("Debug Window")]
        public bool DebugWindow = false;

        public override bool Enabled(MemberInfo member, GameParameters parameters)
        {
            return true;
        }
        public override bool Interactible(MemberInfo member, GameParameters parameters)
        {
            return true;
        }
    }
}
