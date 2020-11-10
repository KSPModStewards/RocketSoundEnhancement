using System;
using System.Reflection;
using UnityEngine;

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

        [GameParameters.CustomParameterUI("Enable Muffling")]
        public bool EnableMuffling = true;

        [GameParameters.CustomIntParameterUI("Interior Muffling", minValue = 10, maxValue = 22200)]
        public int InteriorMuffling = 1500;

        [GameParameters.CustomIntParameterUI("Vaccum Muffling", minValue = 10, maxValue = 22200)]
        public int VaccumMuffling = 300;

        [GameParameters.CustomParameterUI("Debug Window")]
        public bool DebugWindow = false;

        public override void SetDifficultyPreset(GameParameters.Preset preset)
        {
        }

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
