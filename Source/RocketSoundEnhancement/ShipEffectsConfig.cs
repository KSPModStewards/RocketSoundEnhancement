using KSP.UI.Screens;
using System.Collections.Generic;

namespace RocketSoundEnhancement
{
    public static class ShipEffectsConfig
    {
        public static bool MuteStockAeroSounds = false;

        private static List<ConfigNode> _shipEffectsNodes = new List<ConfigNode>();
        public static List<ConfigNode> ShipEffectsConfigNode
        {
            get
            {
                if (_shipEffectsNodes.Count == 0)
                {
                    foreach (var configNode in GameDatabase.Instance.GetConfigNodes("SHIPEFFECTS_SOUNDLAYERS"))
                    {
                        _shipEffectsNodes.AddRange(configNode.GetNodes());
                    }
                }

                return _shipEffectsNodes;
            }
        }

        public static void Load()
        {
            foreach (var configNode in GameDatabase.Instance.GetConfigNodes("SHIPEFFECTS_SOUNDLAYERS"))
            {
                if (configNode.HasValue("MuteStockAeroSounds"))
                    bool.TryParse(configNode.GetValue("MuteStockAeroSounds"), out MuteStockAeroSounds);
                if (configNode.HasValue("nextStageClip"))
                    StageManager.Instance.nextStageClip = GameDatabase.Instance.GetAudioClip(configNode.GetValue("nextStageClip"));
                if (configNode.HasValue("cannotSeparateClip"))
                    StageManager.Instance.cannotSeparateClip = GameDatabase.Instance.GetAudioClip(configNode.GetValue("cannotSeparateClip"));
            }
        }
    }
}
