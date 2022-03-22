using KSP.UI.Screens;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace RocketSoundEnhancement
{
    //[KSPAddon(KSPAddon.Startup.Flight, false)]
    public class Settings : MonoBehaviour
    {
        public static Settings Instance { get; private set; } = new Settings();

        public string ModPath = "GameData/RocketSoundEnhancement/";
        public string SettingsName = "RSE_SETTINGS";

        public Dictionary<string, CollidingObject> CollisionData = new Dictionary<string, CollidingObject>();

        public bool DisableStagingSound = false;
        public bool AffectChatterer = false;

        private List<ConfigNode> _shipEffectsNodes = new List<ConfigNode>();
        public List<ConfigNode> ShipEffectsNodes()
        {
            if(_shipEffectsNodes.Count == 0) {
                foreach(var configNode in GameDatabase.Instance.GetConfigNodes("SHIPEFFECTS_SOUNDLAYERS")) {
                    _shipEffectsNodes.AddRange(configNode.GetNodes("SOUNDLAYER"));

                    if(configNode.HasValue("nextStageClip")) {
                        StageManager.Instance.nextStageClip = GameDatabase.Instance.GetAudioClip(configNode.GetValue("nextStageClip"));
                    }
                    if(configNode.HasValue("cannotSeparateClip")) {
                        StageManager.Instance.cannotSeparateClip = GameDatabase.Instance.GetAudioClip(configNode.GetValue("cannotSeparateClip"));
                    }
                }
            }

            return _shipEffectsNodes;
        }

        ConfigNode _settings;
        public void Load()
        {
            CollisionData.Clear();
            AudioLimiter.Presets.Clear();
            LowpassFilter.Presets.Clear();

            _settings = ConfigNode.Load(ModPath + "Settings.cfg");

            if(_settings == null) {
                Debug.LogError("RSE: Settings.cfg not found! using internal settings");
                _settings = new ConfigNode();
                _settings.AddNode(SettingsName);
            }

            ConfigNode settingsNode = _settings.GetNode(SettingsName);
            if(settingsNode.HasValue("DisableStagingSound")) {
                if(!bool.TryParse(settingsNode.GetValue("DisableStagingSound"), out DisableStagingSound)) {
                    DisableStagingSound = false; 
                }
            } else {
                settingsNode.AddValue("DisableStagingSound", DisableStagingSound);
            }

            if(settingsNode.HasValue("AffectChatterer")) {
                if(!bool.TryParse(settingsNode.GetValue("AffectChatterer"), out AffectChatterer)) {
                    AffectChatterer = false;
                }
            } else {
                settingsNode.AddValue("AffectChatterer", AffectChatterer);
            }

            if(settingsNode.HasNode("Colliders")) {
                var colNode = settingsNode.GetNode("Colliders");
                foreach(ConfigNode.Value node in colNode.values) {
                    CollidingObject colDataType = (CollidingObject)Enum.Parse(typeof(CollidingObject), node.value, true);
                    if(!CollisionData.ContainsKey(node.name)) {
                        CollisionData.Add(node.name, colDataType);
                    } else {
                        CollisionData[node.name] = colDataType;
                    }
                }
            } else {
                //Add the default Collider Collection
                var defaultColNode = settingsNode.AddNode("Colliders");
                defaultColNode.AddValue("default", CollidingObject.Concrete);
                CollisionData.Add("default", CollidingObject.Concrete);

                //runway_lev1_v2 = CollidingObject.Dirt
            }

            if(settingsNode.HasNode("AUDIOLIMITER")) {
                var audioLimiterNode = settingsNode.GetNode("AUDIOLIMITER");

                if(!audioLimiterNode.HasValue("EnableLimiter")) {
                    audioLimiterNode.AddValue("EnableLimiter", true);
                }

                bool.TryParse(audioLimiterNode.GetValue("EnableLimiter"), out AudioLimiter.EnableLimiter);

                if(audioLimiterNode.HasValue("Preset")) {
                    AudioLimiter.Preset = audioLimiterNode.GetValue("Preset");
                }

                foreach(var presetNode in audioLimiterNode.GetNodes()) {
                    string presetName = presetNode.name;
                    AudioLimiterPreset limiterPreset = new AudioLimiterPreset();

                    if(AudioLimiter.Preset == string.Empty) {
                        AudioLimiter.Preset = presetName;
                    }

                    if(!float.TryParse(presetNode.GetValue("Threshold"), out limiterPreset.Threshold)) {
                        limiterPreset.Threshold = AudioLimiter.Threshold;
                    }
                    if(!float.TryParse(presetNode.GetValue("Bias"), out limiterPreset.Bias)) {
                        limiterPreset.Bias = AudioLimiter.Bias;
                    }
                    if(!float.TryParse(presetNode.GetValue("Ratio"), out limiterPreset.Ratio)) {
                        limiterPreset.Ratio = AudioLimiter.Ratio;
                    }
                    if(!float.TryParse(presetNode.GetValue("Gain"), out limiterPreset.Gain)) {
                        limiterPreset.Gain = AudioLimiter.Gain;
                    }
                    if(!int.TryParse(presetNode.GetValue("TimeConstant"), out limiterPreset.TimeConstant)) {
                        limiterPreset.TimeConstant = AudioLimiter.TimeConstant;
                    }
                    if(!int.TryParse(presetNode.GetValue("LevelDetectorRMSWindow"), out limiterPreset.LevelDetectorRMSWindow)) {
                        limiterPreset.LevelDetectorRMSWindow = AudioLimiter.LevelDetectorRMSWindow;
                    }

                    if(!AudioLimiter.Presets.ContainsKey(presetName)) {
                        AudioLimiter.Presets.Add(presetName, limiterPreset);
                    } else {
                        AudioLimiter.Presets[presetName] = limiterPreset;
                    }
                }

                AudioLimiter.ApplyPreset();
            } else {
                var defaultLimiterNode = settingsNode.AddNode("AUDIOLIMITER");
                defaultLimiterNode.AddValue("EnableLimiter", AudioLimiter.EnableLimiter);
                defaultLimiterNode.AddValue("Preset", "Custom");
                AudioLimiter.Preset = "Custom";

                AudioLimiter.Default();
                var defaultPresetNode = defaultLimiterNode.AddNode("Custom");
                defaultPresetNode.AddValue("Threshold", AudioLimiter.DefaultLimiterPreset.Threshold);
                defaultPresetNode.AddValue("Bias", AudioLimiter.DefaultLimiterPreset.Bias);
                defaultPresetNode.AddValue("Ratio", AudioLimiter.DefaultLimiterPreset.Ratio);
                defaultPresetNode.AddValue("Gain", AudioLimiter.DefaultLimiterPreset.Gain);
                defaultPresetNode.AddValue("TimeConstant", AudioLimiter.DefaultLimiterPreset.TimeConstant);
                defaultPresetNode.AddValue("LevelDetectorRMSWindow", AudioLimiter.DefaultLimiterPreset.LevelDetectorRMSWindow);
            }

            if(settingsNode.HasNode("LOWPASSFILTER")) {
                var lowpassFilterNode = settingsNode.GetNode("LOWPASSFILTER");

                if(!lowpassFilterNode.HasValue("EnableMuffling")) {
                    lowpassFilterNode.AddValue("EnableMuffling", true);
                }

                bool.TryParse(lowpassFilterNode.GetValue("EnableMuffling"), out LowpassFilter.EnableMuffling);

                if(lowpassFilterNode.HasValue("Preset")) {
                    LowpassFilter.Preset = lowpassFilterNode.GetValue("Preset");
                }

                foreach(var presetNode in lowpassFilterNode.GetNodes()) {
                    string presetName = presetNode.name;
                    LowpassFilterPreset lowpassFilterPreset = new LowpassFilterPreset();

                    if(LowpassFilter.Preset == string.Empty) {
                        LowpassFilter.Preset = presetName;
                    }

                    if(!float.TryParse(presetNode.GetValue("InteriorMufflingAtm"), out lowpassFilterPreset.InteriorMufflingAtm)) {
                        lowpassFilterPreset.InteriorMufflingAtm = LowpassFilter.InteriorMufflingAtm;
                    }
                    if(!float.TryParse(presetNode.GetValue("InteriorMufflingVac"), out lowpassFilterPreset.InteriorMufflingVac)) {
                        lowpassFilterPreset.InteriorMufflingVac = LowpassFilter.InteriorMufflingVac;
                    }
                    if(!float.TryParse(presetNode.GetValue("VacuumMuffling"), out lowpassFilterPreset.VacuumMuffling)) {
                        lowpassFilterPreset.VacuumMuffling = LowpassFilter.VacuumMuffling;
                    }

                    if(!LowpassFilter.Presets.ContainsKey(presetName)) {
                        LowpassFilter.Presets.Add(presetName, lowpassFilterPreset);
                    } else {
                        LowpassFilter.Presets[presetName] = lowpassFilterPreset;
                    }
                }

                LowpassFilter.ApplyPreset();
            } else {
                var defaultLowpassFilterNode = settingsNode.AddNode("LOWPASSFILTER");
                defaultLowpassFilterNode.AddValue("EnableMuffling", LowpassFilter.EnableMuffling);
                defaultLowpassFilterNode.AddValue("Preset", "Custom");
                LowpassFilter.Preset = "Custom";

                LowpassFilter.Default();
                var defaultPresetNode = defaultLowpassFilterNode.AddNode("Custom");
                defaultPresetNode.AddValue("InteriorMufflingAtm", LowpassFilter.DefaultLowpassFilterPreset.InteriorMufflingAtm);
                defaultPresetNode.AddValue("InteriorMufflingVac", LowpassFilter.DefaultLowpassFilterPreset.InteriorMufflingVac);
                defaultPresetNode.AddValue("VacuumMuffling", LowpassFilter.DefaultLowpassFilterPreset.VacuumMuffling);
            }

            _settings.Save(ModPath + "Settings.cfg");
        }

        public void Save()
        {
            var settingsNode = _settings.GetNode(SettingsName);
            settingsNode.SetValue("DisableStagingSound", DisableStagingSound, true);
            settingsNode.SetValue("AffectChatterer", AffectChatterer, true);

            if(settingsNode.HasNode("AUDIOLIMITER")) {
                var limiterNode = settingsNode.GetNode("AUDIOLIMITER");

                limiterNode.SetValue("EnableLimiter", AudioLimiter.EnableLimiter, true);
                limiterNode.SetValue("Preset", AudioLimiter.Preset, true);

                if(AudioLimiter.Preset == "Custom") {
                    var customPreset = limiterNode.GetNode("Custom");

                    customPreset.SetValue("Threshold", AudioLimiter.Threshold, true);
                    customPreset.SetValue("Bias", AudioLimiter.Bias, true);
                    customPreset.SetValue("Ratio", AudioLimiter.Ratio, true);
                    customPreset.SetValue("Gain", AudioLimiter.Gain, true);
                    customPreset.SetValue("TimeConstant", AudioLimiter.TimeConstant, true);
                    customPreset.SetValue("LevelDetectorRMSWindow", AudioLimiter.LevelDetectorRMSWindow, true);
                }
            }

            if(settingsNode.HasNode("LOWPASSFILTER")) {
                var lowpassFilterNode = settingsNode.GetNode("LOWPASSFILTER");

                lowpassFilterNode.SetValue("EnableMuffling", LowpassFilter.EnableMuffling, true);
                lowpassFilterNode.SetValue("Preset", LowpassFilter.Preset, true);

                if(LowpassFilter.Preset == "Custom") {
                    var customPreset = lowpassFilterNode.GetNode("Custom");

                    customPreset.SetValue("InteriorMufflingAtm", LowpassFilter.InteriorMufflingAtm, true);
                    customPreset.SetValue("InteriorMufflingVac", LowpassFilter.InteriorMufflingVac, true);
                    customPreset.SetValue("VacuumMuffling", LowpassFilter.VacuumMuffling, true);
                }
            }

            _settings.Save(ModPath  + "Settings.cfg");
        }
    }
}
