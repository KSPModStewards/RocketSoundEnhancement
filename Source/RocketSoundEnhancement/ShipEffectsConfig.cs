using KSP.UI.Screens;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RocketSoundEnhancement
{
    public static class ShipEffectsConfig
    {
        public static bool MuteStockAeroSounds = false;
        private static AudioClip nextStageClip;
        private static AudioClip cannotSeparateClip;

        public static Dictionary<PhysicsControl, List<SoundLayer>> SoundLayerGroups;

        public static void Load()
        {
            if (SoundLayerGroups != null) return;
            SoundLayerGroups = new Dictionary<PhysicsControl, List<SoundLayer>>();

			foreach (var configNode in GameDatabase.Instance.GetConfigNodes("SHIPEFFECTS_SOUNDLAYERS"))
            {
                if (configNode.HasValue("MuteStockAeroSounds"))
                    bool.TryParse(configNode.GetValue("MuteStockAeroSounds"), out MuteStockAeroSounds);
                if (configNode.HasValue("nextStageClip"))
                    nextStageClip = GameDatabase.Instance.GetAudioClip(configNode.GetValue("nextStageClip"));
                if (configNode.HasValue("cannotSeparateClip"))
                    cannotSeparateClip = GameDatabase.Instance.GetAudioClip(configNode.GetValue("cannotSeparateClip"));

                foreach (var node in configNode.GetNodes())
                {
                    if (!Enum.TryParse(node.name, true, out PhysicsControl controlGroup)) continue;
                    if (!node.HasNode("SOUNDLAYER")) continue;

                    var soundLayers = AudioUtility.CreateSoundLayerGroup(node.GetNodes("SOUNDLAYER"));
                    if (soundLayers.Count == 0) continue;

                    if (SoundLayerGroups.ContainsKey(controlGroup))
                        SoundLayerGroups[controlGroup].AddRange(soundLayers);
                    else
                        SoundLayerGroups.Add(controlGroup, soundLayers);
                }
            }
        }

        public static void Start()
        {
            if (nextStageClip != null)
            {
                StageManager.Instance.nextStageClip = nextStageClip;
            }

            if (cannotSeparateClip != null)
            {
                StageManager.Instance.cannotSeparateClip = cannotSeparateClip;
            }
        }
    }
}
