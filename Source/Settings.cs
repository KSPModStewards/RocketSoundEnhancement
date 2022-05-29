using KSP.UI.Screens;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RocketSoundEnhancement
{
    public struct AudioMufflerPreset
    {
        public float InteriorMuffling;
        public float ExteriorMuffling;
    }

    public struct AudioNormalizerPreset
    {
        public float FadeInTime;
        public float LowestVolume;
        public float MaximumAmp;
    }

    public enum AudioMufflerQuality
    {
        Normal = 0,
        AirSim = 1
    }

    public static class Settings
    {
        public static string ModPath = "GameData/RocketSoundEnhancement/";
        public static string SettingsName = "RSE_SETTINGS";

        public static Dictionary<string, CollidingObject> CollisionData = new Dictionary<string, CollidingObject>();

        public static bool DisableStagingSound = false;
        public static bool MuteStockAeroSounds = false;

        public static float ExteriorVolume = 1;
        public static float InteriorVolume = 1;

        public static Dictionary<string, AudioMufflerPreset> MufflerPresets = new Dictionary<string, AudioMufflerPreset>();
        public static Dictionary<string, AudioNormalizerPreset> NormalizerPresets = new Dictionary<string, AudioNormalizerPreset>();

        public static bool AudioEffectsEnabled = true;
        public static AudioMufflerQuality MufflerQuality = AudioMufflerQuality.Normal;
        public static AudioMufflerPreset MufflerPreset;
        public static AudioNormalizerPreset NormalizerPreset;
        public static string MufflerPresetName;
        public static string NormalizerPresetName;
        public static float AirSimMaxDistance = 5000;

        public static AudioMufflerPreset DefaultAudioMufflerPreset
        {
            get
            {
                var defaultPreset = new AudioMufflerPreset
                {
                    InteriorMuffling = 1500,
                    ExteriorMuffling = 22200,
                };
                return defaultPreset;
            }
        }

        public static AudioNormalizerPreset DefaultAudioNormalizerPreset
        {
            get
            {
                var defaultPreset = new AudioNormalizerPreset
                {
                    FadeInTime = 2500,
                    LowestVolume = 0.5f,
                    MaximumAmp = 4
                };
                return defaultPreset;
            }
        }

        public static void ApplyMufflerPreset()
        {
            Debug.Log("[RSE]: Audio Muffler: Quality = " + MufflerQuality.ToString());
            if (MufflerPresetName != string.Empty && MufflerPresets.ContainsKey(MufflerPresetName))
            {
                MufflerPreset = MufflerPresets[MufflerPresetName];
                Debug.Log("[RSE]: Audio Muffler: " + MufflerPresetName + " Preset Applied");
                return;
            }
            else
            {
                DefaultMuffler();
                Debug.Log("[RSE]: Audio Muffler: Preset Not Found = " + MufflerPresetName + ". Using Default Settings");
            }
        }

        public static void ApplyNormalizerPreset()
        {
            if (NormalizerPresetName != string.Empty && NormalizerPresets.ContainsKey(NormalizerPresetName))
            {
                NormalizerPreset = NormalizerPresets[NormalizerPresetName];
                Debug.Log("[RSE]: Audio Normalizer: " + NormalizerPresetName + " Preset Applied");
            }
            else
            {
                DefaultNormalizer();
                Debug.Log("[RSE]: Audio Normalizer: Preset Not Found = " + NormalizerPresetName + ". Using Default Settings");
            }
        }

        public static void DefaultMuffler()
        {
            MufflerPreset = DefaultAudioMufflerPreset;
            if (!MufflerPresets.ContainsKey("Custom"))
            {
                MufflerPresets.Add("Custom", DefaultAudioMufflerPreset);
            }
        }

        public static void DefaultNormalizer()
        {
            NormalizerPreset = DefaultAudioNormalizerPreset;
            if (!NormalizerPresets.ContainsKey("Custom"))
            {
                NormalizerPresets.Add("Custom", DefaultAudioNormalizerPreset);
            }
        }

        private static List<ConfigNode> _shipEffectsNodes = new List<ConfigNode>();
        public static List<ConfigNode> ShipEffectsNodes()
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

        static ConfigNode _settings;
        public static void Load()
        {
            CollisionData.Clear();
            NormalizerPresets.Clear();
            MufflerPresets.Clear();

            foreach (var configNode in GameDatabase.Instance.GetConfigNodes("SHIPEFFECTS_SOUNDLAYERS"))
            {
                if (configNode.HasValue("MuteStockAeroSounds"))
                {
                    bool.TryParse(configNode.GetValue("MuteStockAeroSounds"), out MuteStockAeroSounds);
                }

                if (configNode.HasValue("nextStageClip"))
                {
                    StageManager.Instance.nextStageClip = GameDatabase.Instance.GetAudioClip(configNode.GetValue("nextStageClip"));
                }
                if (configNode.HasValue("cannotSeparateClip"))
                {
                    StageManager.Instance.cannotSeparateClip = GameDatabase.Instance.GetAudioClip(configNode.GetValue("cannotSeparateClip"));
                }
            }

            _settings = ConfigNode.Load(ModPath + "Settings.cfg");

            if (_settings == null)
            {
                Debug.LogError("[RSE]: Settings.cfg not found! using internal settings");
                _settings = new ConfigNode();
                _settings.AddNode(SettingsName);
            }

            ConfigNode settingsNode = _settings.GetNode(SettingsName);
            if (!settingsNode.HasValue("EnableAudioEffects")) settingsNode.AddValue("EnableAudioEffects", true);

            bool.TryParse(settingsNode.GetValue("EnableAudioEffects"), out AudioEffectsEnabled);

            if (settingsNode.HasValue("ExteriorVolume") && !float.TryParse(settingsNode.GetValue("ExteriorVolume"), out ExteriorVolume)) ExteriorVolume = 1;

            if (settingsNode.HasValue("InteriorVolume") && !float.TryParse(settingsNode.GetValue("InteriorVolume"), out InteriorVolume)) InteriorVolume = 1;

            if (settingsNode.HasValue("DisableStagingSound"))
            {
                if (!bool.TryParse(settingsNode.GetValue("DisableStagingSound"), out DisableStagingSound)) DisableStagingSound = false;
            }
            else
            {
                settingsNode.AddValue("DisableStagingSound", DisableStagingSound);
            }

            if (settingsNode.HasNode("Colliders"))
            {
                var colNode = settingsNode.GetNode("Colliders");
                foreach (ConfigNode.Value node in colNode.values)
                {
                    CollidingObject colDataType = (CollidingObject)Enum.Parse(typeof(CollidingObject), node.value, true);
                    if (!CollisionData.ContainsKey(node.name))
                    {
                        CollisionData.Add(node.name, colDataType);
                    }
                    else
                    {
                        CollisionData[node.name] = colDataType;
                    }
                }
            }
            else
            {
                var defaultColNode = settingsNode.AddNode("Colliders");
                defaultColNode.AddValue("default", CollidingObject.Concrete);
                CollisionData.Add("default", CollidingObject.Concrete);
            }

            #region NORMALIZER
            if (settingsNode.HasNode("NORMALIZER"))
            {
                var normalizerNode = settingsNode.GetNode("NORMALIZER");
                if (normalizerNode.HasValue("Preset")) { NormalizerPresetName = normalizerNode.GetValue("Preset"); }

                foreach (var presetNode in normalizerNode.GetNodes())
                {
                    string presetName = presetNode.name;
                    AudioNormalizerPreset normalizerPreset = new AudioNormalizerPreset();

                    if (NormalizerPresetName == string.Empty) { NormalizerPresetName = presetName; }

                    if (!float.TryParse(presetNode.GetValue("FadeInTime"), out normalizerPreset.FadeInTime))
                        normalizerPreset.FadeInTime = NormalizerPreset.FadeInTime;

                    if (!float.TryParse(presetNode.GetValue("LowestVolume"), out normalizerPreset.LowestVolume))
                        normalizerPreset.LowestVolume = NormalizerPreset.LowestVolume;

                    if (!float.TryParse(presetNode.GetValue("MaximumAmp"), out normalizerPreset.MaximumAmp))
                        normalizerPreset.MaximumAmp = NormalizerPreset.MaximumAmp;

                    if (NormalizerPresets.ContainsKey(presetName))
                    {
                        NormalizerPresets[presetName] = normalizerPreset;
                        continue;
                    }

                    NormalizerPresets.Add(presetName, normalizerPreset);
                }
            }
            else
            {
                NormalizerPresetName = "Custom";
                DefaultNormalizer();

                var defaultNormalizerNode = settingsNode.AddNode("NORMALIZER");
                defaultNormalizerNode.AddValue("Preset", "Custom");

                var defaultPresetNode = defaultNormalizerNode.AddNode("Custom");
                defaultPresetNode.AddValue("FadeInTime", DefaultAudioNormalizerPreset.FadeInTime);
                defaultPresetNode.AddValue("LowestVolume", DefaultAudioNormalizerPreset.LowestVolume);
                defaultPresetNode.AddValue("MaximumAmp", DefaultAudioNormalizerPreset.MaximumAmp);
            }
            #endregion

            #region MUFFLER
            if (settingsNode.HasNode("MUFFLER"))
            {
                var mufflerNode = settingsNode.GetNode("MUFFLER");

                if (mufflerNode.HasValue("MufflerQuality"))
                {
                    if (!Enum.TryParse(mufflerNode.GetValue("MufflerQuality"), true, out MufflerQuality))
                    {
                        MufflerQuality = AudioMufflerQuality.Normal;
                    }
                }
                else
                {
                    mufflerNode.AddValue("MufflerQuality", MufflerQuality);
                }

                if (mufflerNode.HasValue("Preset")) { MufflerPresetName = mufflerNode.GetValue("Preset"); }
                if (mufflerNode.HasValue("AirSimMaxDistance")) { AirSimMaxDistance = float.Parse(mufflerNode.GetValue("AirSimMaxDistance")); }

                foreach (var presetNode in mufflerNode.GetNodes())
                {
                    string presetName = presetNode.name;
                    AudioMufflerPreset mufflerPreset = new AudioMufflerPreset();

                    if (MufflerPresetName == string.Empty)
                    {
                        MufflerPresetName = presetName;
                    }

                    if (!float.TryParse(presetNode.GetValue("InteriorMuffling"), out mufflerPreset.InteriorMuffling))
                    {
                        mufflerPreset.InteriorMuffling = MufflerPreset.InteriorMuffling;
                    }
                    if (!float.TryParse(presetNode.GetValue("ExteriorMuffling"), out mufflerPreset.ExteriorMuffling))
                    {
                        mufflerPreset.ExteriorMuffling = MufflerPreset.ExteriorMuffling;
                    }

                    if (MufflerPresets.ContainsKey(presetName))
                    {
                        MufflerPresets[presetName] = mufflerPreset;
                        continue;
                    }

                    MufflerPresets.Add(presetName, mufflerPreset);
                }
            }
            else
            {
                MufflerPresetName = "Custom";
                DefaultMuffler();

                var defaultMufflerNode = settingsNode.AddNode("MUFFLER");
                defaultMufflerNode.AddValue("Preset", "Custom");

                var defaultPresetNode = defaultMufflerNode.AddNode("Custom");
                defaultPresetNode.AddValue("InteriorMuffling", DefaultAudioMufflerPreset.InteriorMuffling);
                defaultPresetNode.AddValue("ExteriorMuffling", DefaultAudioMufflerPreset.ExteriorMuffling);
            }
            #endregion

            ApplyNormalizerPreset();
            ApplyMufflerPreset();

            _settings.Save(ModPath + "Settings.cfg");
        }

        public static void Save()
        {
            var settingsNode = _settings.GetNode(SettingsName);

            settingsNode.SetValue("EnableAudioEffects", AudioEffectsEnabled, true);
            settingsNode.SetValue("ExteriorVolume", ExteriorVolume, true);
            settingsNode.SetValue("InteriorVolume", InteriorVolume, true);
            settingsNode.SetValue("DisableStagingSound", DisableStagingSound, true);

            if (settingsNode.HasNode("NORMALIZER"))
            {
                var normalizerNode = settingsNode.GetNode("NORMALIZER");
                normalizerNode.SetValue("Preset", NormalizerPresetName, true);

                if (NormalizerPresetName == "Custom")
                {
                    var customPreset = normalizerNode.GetNode("Custom");

                    customPreset.SetValue("FadeInTime", NormalizerPreset.FadeInTime, true);
                    customPreset.SetValue("LowestVolume", NormalizerPreset.LowestVolume, true);
                    customPreset.SetValue("MaximumAmp", NormalizerPreset.MaximumAmp, true);
                }
            }

            if (settingsNode.HasNode("MUFFLER"))
            {
                var mufflerNode = settingsNode.GetNode("MUFFLER");

                mufflerNode.SetValue("MufflerQuality", MufflerQuality.ToString(), true);

                mufflerNode.SetValue("Preset", MufflerPresetName, true);

                if (MufflerPresetName == "Custom")
                {
                    var customPreset = mufflerNode.GetNode("Custom");

                    customPreset.SetValue("InteriorMuffling", MufflerPreset.InteriorMuffling, true);
                    customPreset.SetValue("ExteriorMuffling", MufflerPreset.ExteriorMuffling, true);
                }
            }

            _settings.Save(ModPath + "Settings.cfg");
        }
    }
}
