using KSP.UI.Screens;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace RocketSoundEnhancement
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class Settings : MonoBehaviour
    {
        public static bool DisableStagingSound = false;

        public static Dictionary<string, CollidingObject> CollisionData = new Dictionary<string, CollidingObject>();
        public static List<ConfigNode> SoundLayerNodes = new List<ConfigNode>();
        void Awake()
        {
            CollisionData.Clear();

            foreach(var configNode in GameDatabase.Instance.GetConfigNodes("RSE_SETTINGS")) {

                if(!bool.TryParse(configNode.GetValue("DisableStagingSound"), out DisableStagingSound)) {
                    DisableStagingSound = false;
                }

                if(configNode.HasNode("Colliders")) {
                    var colNode = configNode.GetNode("Colliders");
                    foreach(ConfigNode.Value node in colNode.values) {
                        CollidingObject colDataType = (CollidingObject)Enum.Parse(typeof(CollidingObject), node.value, true);
                        if(!CollisionData.ContainsKey(node.name)) {
                            CollisionData.Add(node.name, colDataType);
                        } else {
                            CollisionData[node.name] = colDataType;
                        }
                    }
                }
                if(configNode.HasNode("AUDIOLIMITER")) {
                    foreach(var node in configNode.GetNodes("AUDIOLIMITER")) {
                        bool.TryParse(node.GetValue("EnableLimiter"), out AudioLimiter.EnableLimiter);

                        if(!float.TryParse(node.GetValue("Threshold"), out AudioLimiter.Threshold)) {
                            AudioLimiter.Threshold = -12f;
                        }
                        if(!float.TryParse(node.GetValue("Bias"), out AudioLimiter.Bias)) {
                            AudioLimiter.Bias = 70f;
                        }
                        if(!float.TryParse(node.GetValue("Ratio"), out AudioLimiter.Ratio)) {
                            AudioLimiter.Ratio = 2.1f;
                        }
                        if(!float.TryParse(node.GetValue("Gain"), out AudioLimiter.Gain)) {
                            AudioLimiter.Gain = 0;
                        }
                        if(!int.TryParse(node.GetValue("TimeConstant"), out AudioLimiter.TimeConstant)) {
                            AudioLimiter.TimeConstant = 1;
                        }
                        if(!int.TryParse(node.GetValue("LevelDetectorRMSWindow"), out AudioLimiter.LevelDetectorRMSWindow)) {
                            AudioLimiter.LevelDetectorRMSWindow = 100;
                        }
                    }
                }

                if(configNode.HasNode("LOWPASSFILTER")) {
                    foreach(var node in configNode.GetNodes("LOWPASSFILTER")) {

                        bool.TryParse(node.GetValue("EnableMuffling"), out LowpassFilter.EnableMuffling);

                        if(!int.TryParse(node.GetValue("InteriorMufflingAtm"), out LowpassFilter.InteriorMufflingAtm)) {
                            LowpassFilter.InteriorMufflingAtm = 3000;
                        }
                        if(!int.TryParse(node.GetValue("InteriorMufflingVac"), out LowpassFilter.InteriorMufflingVac)) {
                            LowpassFilter.InteriorMufflingVac = 1500;
                        }
                        if(!int.TryParse(node.GetValue("VacuumMuffling"), out LowpassFilter.VacuumMuffling)) {
                            LowpassFilter.VacuumMuffling = 300;
                        }
                        if(!bool.TryParse(node.GetValue("MuffleChatterer"), out LowpassFilter.MuffleChatterer)) {
                            LowpassFilter.MuffleChatterer = false;
                        }
                    }
                }
            }

            SoundLayerNodes.Clear();
            foreach(var configNode in GameDatabase.Instance.GetConfigNodes("SHIPEFFECTS_SOUNDLAYERS")) {
                SoundLayerNodes.AddRange(configNode.GetNodes("SOUNDLAYER"));

                if(configNode.HasValue("nextStageClip")) {
                    StageManager.Instance.nextStageClip = GameDatabase.Instance.GetAudioClip(configNode.GetValue("nextStageClip"));
                }
                if(configNode.HasValue("cannotSeparateClip")) {
                    StageManager.Instance.cannotSeparateClip = GameDatabase.Instance.GetAudioClip(configNode.GetValue("cannotSeparateClip"));
                }
            }
        }
    }

    public class SettingsInGame : GameParameters.CustomParameterNode
    {
        public override string Title { get { return "Rocket Sound Enhancement Settings"; } }
        public override GameParameters.GameMode GameMode { get { return GameParameters.GameMode.ANY; } }
        public override string Section { get { return "Rocket Sound Enhancement"; } }
        public override string DisplaySection { get { return "Rocket Sound Enhancement"; } }
        public override int SectionOrder { get { return 1; } }
        public override bool HasPresets { get { return false; } }
    
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
