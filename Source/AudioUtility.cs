using System;
using System.Collections.Generic;
using System.Linq;
using RocketSoundEnhancement.AudioFilters;
using UnityEngine;
using UnityEngine.Audio;

namespace RocketSoundEnhancement
{
    public enum CollidingObject
    {
        Dirt,
        Concrete,
        Vessel
    }

    public enum FXChannel
    {
        Exterior,
        Interior
    }

    public enum PhysicsControl
    {
        ACCELERATION,
        JERK,
        AIRSPEED,
        GROUNDSPEED,
        SONICBOOM,
        DYNAMICPRESSURE,
        THRUST,
        REENTRYHEAT,
        None
    }

    public struct SoundLayer
    {
        public string name;
        public string data;
        public AudioClip[] audioClips;
        public FXChannel channel;
        public bool loop;
        public bool loopAtRandom;
        public bool spool;
        public float spoolSpeed;
        public float spoolIdle;
        public float spread;
        public float MaxDistance;
        public AudioRolloffMode rolloffMode;
        public FXCurve volume;
        public FXCurve pitch;
        public FloatCurve volumeFC;
        public FloatCurve pitchFC;
        public FXCurve massToVolume;
        public FXCurve massToPitch;
        public FloatCurve rollOffCurve;
    }

    public static class AudioUtility
    {
        public static AnimationCurve SmoothControl = AnimationCurve.EaseInOut(0f, 0.04f, 1f, 1f);
        public static string RSETag = "RSE";

        public static ConfigNode GetConfigNode(string partInfoName, string moduleName, string moduleID = "")
        {
            var configs = GameDatabase.Instance.GetConfigs("PART");

            foreach(var configNode in configs) {
                if(configNode.name.Replace("_", ".") == partInfoName) {
                    if(moduleID == "") {
                        return Array.FindAll(configNode.config.GetNodes("MODULE"), x => x.GetValue("name") == moduleName).FirstOrDefault();
                    } else {
                        return Array.FindAll(configNode.config.GetNodes("MODULE"), x => x.GetValue("name") == moduleName && x.GetValue("moduleID") == moduleID).FirstOrDefault();
                    }
                }
            }
            return null;
        }

        public static List<SoundLayer> CreateSoundLayerGroup(ConfigNode[] groupNodes)
        {
            var group = new List<SoundLayer>();
            foreach(var node in groupNodes) {
                group.Add(CreateSoundLayer(node));
            }
            return group;
        }

        public static SoundLayer CreateSoundLayer(ConfigNode node)
        {
            var soundLayer = new SoundLayer();

            soundLayer.name = node.GetValue("name");

            if (node.HasValue("audioClip"))
            {
                var clips = new List<AudioClip>();
                var clipValues = node.GetValues("audioClip");
                for (int i = 0; i < clipValues.Length; i++)
                {
                    string value = clipValues[i];
                    AudioClip clip;
                    if (clip = GameDatabase.Instance.GetAudioClip(value))
                    {
                        clips.Add(clip);
                    }
                }
                soundLayer.audioClips = clips.ToArray();
            }

            if (!node.TryGetValue("loopAtRandom", ref soundLayer.loopAtRandom)) { soundLayer.loopAtRandom = true; }
            node.TryGetValue("loop", ref soundLayer.loop);
            node.TryGetValue("spool", ref soundLayer.spool);
            node.TryGetValue("spoolSpeed", ref soundLayer.spoolSpeed);
            node.TryGetValue("spoolIdle", ref soundLayer.spoolIdle);
            node.TryGetValue("spread", ref soundLayer.spread);
            node.TryGetEnum("channel", ref soundLayer.channel, FXChannel.Exterior);
            node.TryGetEnum("rolloffMode", ref soundLayer.rolloffMode, AudioRolloffMode.Logarithmic);
            if (!node.TryGetValue("MaxDistance", ref soundLayer.MaxDistance)) soundLayer.MaxDistance = 500;

            if (node.HasNode("rolloffCurve"))
            {
                soundLayer.rollOffCurve = new FloatCurve();
                soundLayer.rollOffCurve.Load(node.GetNode("rolloffCurve"));
            }

            soundLayer.volume = new FXCurve("volume", 1);
            soundLayer.pitch = new FXCurve("pitch", 1);
            soundLayer.volume.Load("volume", node);
            soundLayer.pitch.Load("pitch", node);

            if (node.HasNode("volumeFC"))
            {
                soundLayer.volumeFC = new FloatCurve();
                soundLayer.volumeFC.Load(node.GetNode("volumeFC"));
                soundLayer.volumeFC.Curve.preWrapMode = WrapMode.ClampForever;
                soundLayer.volumeFC.Curve.postWrapMode = WrapMode.ClampForever;
            }

            if (node.HasNode("pitchFC"))
            {
                soundLayer.pitchFC = new FloatCurve();
                soundLayer.pitchFC.Load(node.GetNode("pitchFC"));
                soundLayer.pitchFC.Curve.preWrapMode = WrapMode.ClampForever;
                soundLayer.pitchFC.Curve.postWrapMode = WrapMode.ClampForever;
            }
            
            if (node.HasValue("massToVolume"))
            {
                soundLayer.massToVolume = new FXCurve("massToVolume", 1);
                soundLayer.massToVolume.Load("massToVolume", node);
            }

            if (node.HasValue("massToPitch"))
            {
                soundLayer.massToPitch = new FXCurve("massToPitch", 1);
                soundLayer.massToPitch.Load("massToPitch", node);
            }

            soundLayer.data = node.HasValue("data") ? node.GetValue("data") : "";

            return soundLayer;
        }

        public static AudioSource CreateSource(GameObject sourceGameObject, SoundLayer soundLayer)
        {
            var source = sourceGameObject.AddComponent<AudioSource>();
            source.name = RSETag + "_" + sourceGameObject.name;
            source.playOnAwake = false;
            source.volume = soundLayer.volume;
            source.pitch = soundLayer.pitch;
            source.loop = soundLayer.loop;
            source.spatialBlend = 1;

            source.rolloffMode = soundLayer.rolloffMode;
            if (soundLayer.rolloffMode > AudioRolloffMode.Logarithmic) { source.maxDistance = soundLayer.MaxDistance; }
            if (soundLayer.rolloffMode == AudioRolloffMode.Custom && soundLayer.rollOffCurve != null)
            {
                source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, soundLayer.rollOffCurve.Curve);
            }

            if (soundLayer.spread > 0) { source.SetCustomCurve(AudioSourceCurveType.Spread, AnimationCurve.Linear(0, soundLayer.spread, 1, 0)); }
            
            return source;
        }

        public static CollidingObject GetCollidingObject(GameObject gameObject)
        {
            var part = Part.FromGO(gameObject);
            if (part)
            {
                if (part.GetComponent<ModuleAsteroid>())
                    return CollidingObject.Dirt;

                return CollidingObject.Vessel;
            }

            if (gameObject.tag.ToLower() != "untagged")
            {
                if (Settings.CollisionData.ContainsKey(gameObject.name))
                    return Settings.CollisionData[gameObject.name];

                if (Settings.CollisionData.ContainsKey("default"))
                    return Settings.CollisionData["default"];
            }

            return CollidingObject.Dirt;
        }

        public static GameObject CreateAudioParent(Part part, string partName)
        {
            var audioParent = part.gameObject.GetChild(partName);
            if (!audioParent)
            {
                audioParent = new GameObject(partName);
                audioParent.transform.parent = part.transform;
                audioParent.transform.localRotation = Quaternion.Euler(0, 0, 0);
                audioParent.transform.localPosition = Vector3.zero;
            }
            return audioParent;
        }

        public static AudioMixerGroup GetMixerGroup(FXChannel channel, bool isActiveVessel)
        {
            if (Settings.AudioEffectsEnabled)
            {
                switch (channel)
                {
                    case FXChannel.Interior: return RocketSoundEnhancement.Instance.InteriorMixer;
                    case FXChannel.Exterior: return isActiveVessel ? RocketSoundEnhancement.Instance.FocusMixer : RocketSoundEnhancement.Instance.ExteriorMixer;
                }
            }
            return null;
        }

        public static void PlayAtChannel(AudioSource source, FXChannel channel, bool isActiveVessel, bool loop = false, float volumeScale = 1.0f, AudioClip audioclip = null)
        {
            if (source == null || !source.isActiveAndEnabled) return;

            if (TimeWarp.CurrentRate > TimeWarp.fetch.physicsWarpRates.Last()) source.volume = 0;

            source.outputAudioMixerGroup = GetMixerGroup(channel, isActiveVessel);
            switch (channel)
            {
                case FXChannel.Exterior:
                    source.volume *= Settings.ExteriorVolume;
                    break;
                case FXChannel.Interior:
                    source.volume *= Settings.InteriorVolume;
                    source.mute = isActiveVessel ? !InternalCamera.Instance.isActive : true;
                    break;
            }

            if (!loop) { source.PlayOneShot(audioclip != null ? audioclip : source.clip, volumeScale); return; }

            if (loop && !source.isPlaying) source.Play();
        }
    }
}