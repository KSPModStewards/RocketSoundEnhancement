using KSP.UI.Screens;
using RocketSoundEnhancement.Unity;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RocketSoundEnhancement
{
    public static class Settings
    {
        public static string ModPath = "GameData/RocketSoundEnhancement/";
        public static string SettingsNodeName = "RSE_SETTINGS";
        public static int VoiceCount = 32;

        public static Dictionary<string, CollidingObject> CollisionData = new Dictionary<string, CollidingObject>();
        public static Dictionary<string, MixerGroup> CustomAudioSources = new Dictionary<string, MixerGroup>();
        public static Dictionary<string, MixerGroup> CustomAudioClips = new Dictionary<string, MixerGroup>();

        public static AudioMufflerQuality MufflerQuality = AudioMufflerQuality.Normal;

        public static bool EnableAudioEffects = true;
        public static bool EnableCustomLimiter = false;
        public static bool DisableStagingSound = false;
        public static bool ClampActiveVesselMuffling = false;

        public static float DopplerFactor = 1f;

        public static float InteriorVolume = 1;
        public static float ExteriorVolume = 1;

        public static float AutoLimiter = 0.5f;
        public static float LimiterThreshold = 0;
        public static float LimiterGain = 0;
        public static float LimiterAttack = 10;
        public static float LimiterRelease = 20;

        public static float AirSimMaxDistance = 5000;
        public static float MufflerInternalMode = 1500;
        public static float MufflerExternalMode = 22000;
        public static float MachEffectsAmount = 0.8f;
        public static float MachEffectLowerLimit
        {
            get {
                return (1 - Mathf.Log10(Mathf.Lerp(0.1f, 10, MachEffectsAmount))) * 0.5f;
            }
        }

        private static bool initialized;
        private static ConfigNode settingsConfigNode;
        public static void SetLimiterDefaults()
        {
            EnableCustomLimiter = false;
            AutoLimiter = 0.5f;
            LimiterThreshold = 0;
            LimiterGain = 0;
            LimiterAttack = 10;
            LimiterRelease = 20;
        }

        public static void SetMufflerDefaults()
        {
            MufflerInternalMode = 1500;
            MufflerExternalMode = 1000;
        }

        private static void loadLimiterSettings(ConfigNode settingsNode)
        {
            if (!settingsNode.HasNode("LIMITER"))
            {
                SetLimiterDefaults();
                var defaultLimiterNode = settingsNode.AddNode("LIMITER");
                defaultLimiterNode.AddValue("EnableCustomLimiter", EnableCustomLimiter);
                defaultLimiterNode.AddValue("AutoLimiter", AutoLimiter);
                defaultLimiterNode.AddValue("Threshold", LimiterThreshold);
                defaultLimiterNode.AddValue("Gain", LimiterGain);
                defaultLimiterNode.AddValue("Attack", LimiterAttack);
                defaultLimiterNode.AddValue("Attack", LimiterRelease);
                return;
            }

            var limiterNode = settingsNode.GetNode("LIMITER");
            if (!limiterNode.HasValue("EnableCustomLimiter") || !bool.TryParse(limiterNode.GetValue("EnableCustomLimiter"), out EnableCustomLimiter))
                limiterNode.AddValue("EnableCustomLimiter", EnableCustomLimiter);

            if (!limiterNode.HasValue("AutoLimiter") || !float.TryParse(limiterNode.GetValue("AutoLimiter"), out AutoLimiter))
                limiterNode.AddValue("AutoLimiter", AutoLimiter);

            if (!limiterNode.HasValue("Threshold") || !float.TryParse(limiterNode.GetValue("Threshold"), out LimiterThreshold))
                limiterNode.AddValue("Threshold", LimiterThreshold);

            if (!limiterNode.HasValue("Gain") || !float.TryParse(limiterNode.GetValue("Gain"), out LimiterGain))
                limiterNode.AddValue("Gain", LimiterGain);

            if (!limiterNode.HasValue("Attack") || !float.TryParse(limiterNode.GetValue("Attack"), out LimiterAttack))
                limiterNode.AddValue("Attack", LimiterAttack);

            if (!limiterNode.HasValue("Release") || !float.TryParse(limiterNode.GetValue("Release"), out LimiterRelease))
                limiterNode.AddValue("Attack", LimiterRelease);
        }

        private static void loadMufflerSettings(ConfigNode settingsNode)
        {
            if (!settingsNode.HasNode("MUFFLER"))
            {
                SetMufflerDefaults();

                var defaultMufflerNode = settingsNode.AddNode("MUFFLER");
                defaultMufflerNode.AddValue("ClampActiveVesselMuffling", ClampActiveVesselMuffling);
                defaultMufflerNode.AddValue("MachEffectsAmount", MachEffectsAmount);
                defaultMufflerNode.AddValue("MufflerQuality", MufflerQuality);
                defaultMufflerNode.AddValue("InternalMode", MufflerInternalMode);
                defaultMufflerNode.AddValue("ExternalMode", MufflerExternalMode);
                return;
            }

            var mufflerNode = settingsNode.GetNode("MUFFLER");
            if (!mufflerNode.HasValue("ClampActiveVesselMuffling") || !bool.TryParse(mufflerNode.GetValue("ClampActiveVesselMuffling"), out ClampActiveVesselMuffling))
                mufflerNode.AddValue("ClampActiveVesselMuffling", ClampActiveVesselMuffling);

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
            if (!mufflerNode.HasValue("InternalMode") || !float.TryParse(mufflerNode.GetValue("InternalMode"), out MufflerInternalMode))
                mufflerNode.AddValue("InternalMode", MufflerInternalMode);

            if (!mufflerNode.HasValue("ExternalMode") || !float.TryParse(mufflerNode.GetValue("ExternalMode"), out MufflerExternalMode))
                mufflerNode.AddValue("ExternalMode", MufflerExternalMode);

            if (!mufflerNode.HasValue("MachEffectsAmount") || !float.TryParse(mufflerNode.GetValue("MachEffectsAmount"), out MachEffectsAmount))
                mufflerNode.AddValue("MachEffectsAmount", MachEffectsAmount);

            if (!mufflerNode.HasValue("DopplerFactor") || !float.TryParse(mufflerNode.GetValue("DopplerFactor"), out DopplerFactor))
                mufflerNode.AddValue("DopplerFactor", DopplerFactor);

            if (mufflerNode.HasValue("AirSimMaxDistance"))
                float.TryParse(mufflerNode.GetValue("AirSimMaxDistance"), out AirSimMaxDistance);

        }

        public static void Load()
        {
            CustomAudioSources.Clear();
            CustomAudioClips.Clear();
            CollisionData.Clear();

            settingsConfigNode = ConfigNode.Load(ModPath + "Settings.cfg");
            if (settingsConfigNode == null)
            {
                Debug.LogError("[RSE]: Settings.cfg not found! using internal settings");
                settingsConfigNode = new ConfigNode();
                settingsConfigNode.AddNode(SettingsNodeName);
            }

            ConfigNode settingsNode = settingsConfigNode.GetNode(SettingsNodeName);

            if (!settingsNode.HasValue("EnableAudioEffects")) settingsNode.AddValue("EnableAudioEffects", true);
            bool.TryParse(settingsNode.GetValue("EnableAudioEffects"), out EnableAudioEffects);

            if (!settingsNode.HasValue("DisableStagingSound") || !bool.TryParse(settingsNode.GetValue("DisableStagingSound"), out DisableStagingSound))
                settingsNode.AddValue("DisableStagingSound", DisableStagingSound);

            if (settingsNode.HasValue("ExteriorVolume") && !float.TryParse(settingsNode.GetValue("ExteriorVolume"), out ExteriorVolume)) ExteriorVolume = 1;
            if (settingsNode.HasValue("InteriorVolume") && !float.TryParse(settingsNode.GetValue("InteriorVolume"), out InteriorVolume)) InteriorVolume = 1;


            if (settingsNode.HasNode("Colliders"))
            {
                var colNode = settingsNode.GetNode("Colliders");
                foreach (ConfigNode.Value node in colNode.values)
                {
                    CollidingObject colDataType = CollidingObject.Dirt;
                    if (!CollisionData.ContainsKey(node.name) && Enum.TryParse<CollidingObject>(node.value, true, out colDataType))
                        CollisionData.Add(node.name, colDataType);
                }
            }
            else
            {
                var defaultColNode = settingsNode.AddNode("Colliders");
                defaultColNode.AddValue("default", CollidingObject.Concrete);
                CollisionData.Add("default", CollidingObject.Concrete);
            }

            if (!settingsNode.HasNode("CustomMixerRouting")) settingsNode.AddNode("CustomMixerRouting");

            var mixerRoutingNode = settingsNode.nodes.GetNode("CustomMixerRouting");
            if (mixerRoutingNode.HasNode("AudioSources"))
            {
                var sourcesNode = mixerRoutingNode.GetNode("AudioSources");
                foreach (ConfigNode.Value node in sourcesNode.values)
                {
                    MixerGroup channel;
                    if (!CustomAudioSources.ContainsKey(node.name) && Enum.TryParse<MixerGroup>(node.value, true, out channel))
                        CustomAudioSources.Add(node.name, channel);
                }
            }
            else
            {
                var defaultSourcesNode = mixerRoutingNode.AddNode("AudioSources");
                defaultSourcesNode.AddValue("MusicLogic", MixerGroup.Ignore);
                defaultSourcesNode.AddValue("SoundtrackEditor", MixerGroup.Ignore);
                defaultSourcesNode.AddValue("PartActionController(Clone)", MixerGroup.Ignore);
                CustomAudioSources.Add("MusicLogic", MixerGroup.Ignore);
                CustomAudioSources.Add("SoundtrackEditor", MixerGroup.Ignore);
                CustomAudioSources.Add("PartActionController", MixerGroup.Ignore);
            }

            if (mixerRoutingNode.HasNode("AudioClips"))
            {
                var clipsNode = mixerRoutingNode.GetNode("AudioClips");
                foreach (ConfigNode.Value node in clipsNode.values)
                {
                    MixerGroup channel = MixerGroup.Ignore;
                    if (!CustomAudioClips.ContainsKey(node.name) && Enum.TryParse<MixerGroup>(node.value, true, out channel))
                        CustomAudioClips.Add(node.name, channel);
                }
            }
            else
            {
                mixerRoutingNode.AddNode("AudioClips");
            }

            loadLimiterSettings(settingsNode);
            loadMufflerSettings(settingsNode);

            settingsConfigNode.Save(ModPath + "Settings.cfg");
            initialized = true;
        }

        public static void Save()
        {
            if (!initialized) Load();

            ConfigNode settingsNode = settingsConfigNode.GetNode(SettingsNodeName);

            settingsNode.SetValue("EnableAudioEffects", EnableAudioEffects, true);
            settingsNode.SetValue("ExteriorVolume", ExteriorVolume, true);
            settingsNode.SetValue("InteriorVolume", InteriorVolume, true);
            settingsNode.SetValue("DisableStagingSound", DisableStagingSound, true);

            var limiterNode = settingsNode.GetNode("LIMITER");
            limiterNode.SetValue("EnableCustomLimiter", EnableCustomLimiter);
            limiterNode.SetValue("AutoLimiter", AutoLimiter);
            limiterNode.SetValue("Threshold", LimiterThreshold);
            limiterNode.SetValue("Gain", LimiterGain);
            limiterNode.SetValue("Attack", LimiterAttack);
            limiterNode.SetValue("Release", LimiterRelease);

            var mufflerNode = settingsNode.GetNode("MUFFLER");
            mufflerNode.SetValue("MufflerQuality", MufflerQuality.ToString(), true);
            mufflerNode.SetValue("ClampActiveVesselMuffling", ClampActiveVesselMuffling, true);
            mufflerNode.SetValue("InternalMode", MufflerInternalMode, true);
            mufflerNode.SetValue("ExternalMode", MufflerExternalMode, true);
            mufflerNode.SetValue("MachEffectsAmount", MachEffectsAmount, true);
            mufflerNode.SetValue("DopplerFactor", DopplerFactor, true);
            mufflerNode.SetValue("AirSimMaxDistance", AirSimMaxDistance, false);

            settingsConfigNode.Save(ModPath + "Settings.cfg");
        }
    }
}