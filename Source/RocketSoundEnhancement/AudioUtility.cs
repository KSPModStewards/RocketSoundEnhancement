﻿using System;
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
        Vessel,
        KerbalEVA
    }

    public enum FXChannel
    {
        Exterior,
        Interior
    }

    public enum MixerGroup
    {
        Ignore,
        Exterior,
        Interior,
        Focus
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

    public class SoundLayer
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
        public float maxDistance;
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

        public static List<SoundLayer> CreateSoundLayerGroup(ConfigNode[] groupNodes)
        {
            var group = new List<SoundLayer>();
            foreach (var node in groupNodes)
            {
                group.Add(CreateSoundLayer(node));
            }
            return group;
        }

        public static SoundLayer CreateSoundLayer(ConfigNode node)
        {
            var soundLayer = new SoundLayer { name = node.GetValue("name") };

            if (node.HasValue("audioClip"))
            {
                var clips = new List<AudioClip>();
                var clipValues = node.GetValues("audioClip");
                for (int i = 0; i < clipValues.Length; i++)
                {
                    string value = clipValues[i];
                    AudioClip clip = GameDatabase.Instance.GetAudioClip(value);
                    if (clip == null)
                    {
                        Debug.Log($"[RSE]: Cannot find AudioClip [{value}] in SoundLayer [{soundLayer.name}]");
                        continue;
                    }

                    clips.Add(clip);
                }
                soundLayer.audioClips = clips.ToArray();
            }

            if(soundLayer.audioClips == null || soundLayer.audioClips.Length == 0)
            {
                Debug.Log($"[RSE]: [{soundLayer.name}] audioClip is empty.");
            }

            if (!node.TryGetValue("loopAtRandom", ref soundLayer.loopAtRandom)) { soundLayer.loopAtRandom = true; }
            node.TryGetValue("loop", ref soundLayer.loop);
            node.TryGetValue("spool", ref soundLayer.spool);
            node.TryGetValue("spoolSpeed", ref soundLayer.spoolSpeed);
            node.TryGetValue("spoolIdle", ref soundLayer.spoolIdle);
            node.TryGetValue("spread", ref soundLayer.spread);
            node.TryGetEnum("channel", ref soundLayer.channel, FXChannel.Exterior);
            node.TryGetEnum("rolloffMode", ref soundLayer.rolloffMode, AudioRolloffMode.Logarithmic);
            if (!node.TryGetValue("maxDistance", ref soundLayer.maxDistance)) soundLayer.maxDistance = 500;

            if (node.HasNode("rolloffCurve"))
            {
                soundLayer.rollOffCurve = new FloatCurve();
                soundLayer.rollOffCurve.Load(node.GetNode("rolloffCurve"));
                soundLayer.rollOffCurve.Curve.preWrapMode = WrapMode.ClampForever;
                soundLayer.rollOffCurve.Curve.postWrapMode = WrapMode.ClampForever;
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
        public static AudioSource CreateSource(GameObject sourceGameObject, FXCurve volume, FXCurve pitch, bool loop = false, float spread = 0.0f)
        {
            var source = sourceGameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.volume = volume;
            source.pitch = pitch;
            source.loop = loop;
            source.spatialBlend = 1;
            source.rolloffMode = AudioRolloffMode.Logarithmic;

            if (spread > 0) { source.SetCustomCurve(AudioSourceCurveType.Spread, AnimationCurve.Linear(0, spread, 1, 0)); }

            return source;
        }

        public static AudioSource CreateSource(GameObject sourceGameObject, SoundLayer soundLayer)
        {
            var source = sourceGameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.volume = soundLayer.volume;
            source.pitch = soundLayer.pitch;
            source.loop = soundLayer.loop;
            source.spatialBlend = 1;

            source.rolloffMode = soundLayer.rolloffMode;
            if (soundLayer.rolloffMode > AudioRolloffMode.Logarithmic) { source.maxDistance = soundLayer.maxDistance; }
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
                if (part.GetComponent<ModuleAsteroid>()) return CollidingObject.Dirt;
                if (part.isVesselEVA) return CollidingObject.KerbalEVA;
                return CollidingObject.Vessel;
            }
            if (gameObject.tag.ToLower() != "untagged")
            {
                if (Settings.CollisionData.ContainsKey(gameObject.name)) return Settings.CollisionData[gameObject.name];
                if (Settings.CollisionData.ContainsKey("default")) return Settings.CollisionData["default"];
            }
            return CollidingObject.Dirt;
        }

        public static GameObject CreateAudioParent(Part part, string partName)
        {
            var audioParent = part.gameObject.GetChild($"{RSETag}_partName");
            if (!audioParent)
            {
                audioParent = new GameObject(partName);
                audioParent.transform.parent = part.transform;
                audioParent.transform.localRotation = Quaternion.Euler(0, 0, 0);
                audioParent.transform.localPosition = Vector3.zero;
            }
            return audioParent;
        }

        public static AudioMixerGroup GetMixerGroup(MixerGroup group)
        {
            if (Settings.EnableAudioEffects)
            {
                switch (group)
                {
                    case MixerGroup.Exterior: return RocketSoundEnhancement.ExteriorMixer;
                    case MixerGroup.Interior: return RocketSoundEnhancement.InteriorMixer;
                    case MixerGroup.Focus: return RocketSoundEnhancement.FocusMixer;
                    default: return null;
                }
            }
            return null;
        }

        public static AudioMixerGroup GetMixerGroup(FXChannel channel, bool isActiveVessel)
        {
            if (Settings.EnableAudioEffects)
            {
                switch (channel)
                {
                    case FXChannel.Interior: return RocketSoundEnhancement.InteriorMixer;
                    case FXChannel.Exterior: return isActiveVessel ? RocketSoundEnhancement.FocusMixer : RocketSoundEnhancement.ExteriorMixer;
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
                    source.mute = !isActiveVessel || !InternalCamera.Instance.isActive;
                    break;
            }

            if (!loop)
            {
                source.PlayOneShot(audioclip, volumeScale);
                return;
            }

            if (loop && !source.isPlaying) source.Play();
        }
    }
}