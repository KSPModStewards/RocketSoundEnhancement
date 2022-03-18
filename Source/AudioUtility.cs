using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RocketSoundEnhancement
{
    public enum CollidingObject
    {
        Vessel,
        Concrete,
        Dirt
    }

    public enum FXChannel
    {
        ShipInternal,
        ShipExternal,
        ShipBoth
    }

    public enum PhysicsControl
    {
        Acceleration,
        Jerk,
        AirSpeed,
        GroundSpeed,
        Thrust,
        None
    }

    public struct SoundLayer
    {
        public string name;
        public string data;
        public string[] audioClips;
        public FXChannel channel;
        public bool loop;
        public bool loopAtRandom;
        public bool spool;
        public float spoolSpeed;
        public float spoolIdle;
        public float maxDistance;
        public float spread;
        public FXCurve volume;
        public FXCurve pitch;
        public FXCurve massToVolume;
        public FXCurve massToPitch;

    }

    public static class AudioUtility
    {
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

            if(node.HasValue("audioClip")) {
                soundLayer.audioClips = new string[node.GetValues("audioClip").Length];
                for(int i = 0; i < soundLayer.audioClips.Length; i++) {
                    soundLayer.audioClips[i] = node.GetValue("audioClip", i);
                }
            }

            node.TryGetValue("loop", ref soundLayer.loop);
            node.TryGetValue("loopAtRandom", ref soundLayer.loopAtRandom);

            node.TryGetValue("spool", ref soundLayer.spool);

            node.TryGetValue("spoolSpeed", ref soundLayer.spoolSpeed);

            node.TryGetValue("spoolIdle", ref soundLayer.spoolIdle);
            node.TryGetValue("spread", ref soundLayer.spread);

            soundLayer.volume = new FXCurve("volume", 1);
            soundLayer.pitch = new FXCurve("pitch", 1);
            soundLayer.massToVolume = new FXCurve("massToVolume", 1);
            soundLayer.massToPitch = new FXCurve("massToPitch", 1);

            node.TryGetEnum("channel", ref soundLayer.channel, FXChannel.ShipBoth);

            soundLayer.volume.Load("volume", node);
            soundLayer.pitch.Load("pitch", node);
            soundLayer.massToVolume.Load("massToVolume", node);
            soundLayer.massToPitch.Load("massToPitch", node);

            soundLayer.maxDistance = 500f;

            node.TryGetValue("maxDistance", ref soundLayer.maxDistance);

            if(node.HasValue("data")) {
                soundLayer.data = node.GetValue("data");
            } else {
                soundLayer.data = "";
            }

            return soundLayer;
        }

        public static void PlayAtChannel(AudioSource source, FXChannel channel, bool loop = false, bool loopAtRandom = false, bool oneshot = false, float volume = 1.0f, AudioClip audioclip = null)
        {
            if(source == null)
                return;

            if(TimeWarp.CurrentRate > TimeWarp.fetch.physicsWarpRates.Last()) {
                if(source.isPlaying) {
                    source.Stop();
                }
                return;
            }

            switch(channel) {
                case FXChannel.ShipBoth:
                    if(loop) {
                        if(!source.isPlaying) {
                            if(loopAtRandom) {
                                source.time = UnityEngine.Random.Range(0, source.clip.length);
                            }
                            source.Play();
                        }
                    } else {
                        if(oneshot) {
                            if(audioclip != null) {
                                source.PlayOneShot(audioclip, volume);
                            } else {
                                source.PlayOneShot(source.clip, volume);
                            }
                        } else {
                            source.Play();
                        }
                    }
                    break;
                case FXChannel.ShipInternal:
                    source.mute = !InternalCamera.Instance.isActive;
                    source.bypassListenerEffects = true;
                    source.bypassEffects = true;
                    if(loop) {
                        if(!source.isPlaying) {
                            if(loopAtRandom) {
                                source.time = UnityEngine.Random.Range(0, source.clip.length);
                            }
                            source.Play();
                        }
                    } else {
                        if(oneshot) {
                            if(audioclip != null) {
                                source.PlayOneShot(audioclip, volume);
                            } else {
                                source.PlayOneShot(source.clip, volume);
                            }
                        } else {
                            source.Play();
                        }
                    }

                    break;
                case FXChannel.ShipExternal:
                    source.mute = InternalCamera.Instance.isActive;
                    if(loop) {
                        if(!source.isPlaying) {
                            if(loopAtRandom) {
                                source.time = UnityEngine.Random.Range(0, source.clip.length);
                            }
                            source.Play();
                        }
                    } else {
                        if(oneshot) {
                            if(audioclip != null) {
                                source.PlayOneShot(audioclip, volume);
                            } else {
                                source.PlayOneShot(source.clip, volume);
                            }
                        } else {
                            source.Play();
                        }
                    }

                    break;
            }
        }

        public static AudioSource CreateSource(GameObject gameObject, SoundLayer soundLayer)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;

            if(soundLayer.audioClips != null) {
                int index = 0;
                if(soundLayer.audioClips.Length > 1) {
                    index = UnityEngine.Random.Range(0, soundLayer.audioClips.Length);
                }
                source.clip = GameDatabase.Instance.GetAudioClip(soundLayer.audioClips[index]);
            }

            source.volume = soundLayer.volume;
            source.pitch = soundLayer.pitch;
            source.loop = soundLayer.loop;
            source.maxDistance = soundLayer.maxDistance;
            source.spatialBlend = 1;

            if(soundLayer.spread != 0) {
                source.SetCustomCurve(AudioSourceCurveType.Spread, AnimationCurve.Linear(0, soundLayer.spread, 1, 0));
            }

            source.rolloffMode = AudioRolloffMode.Logarithmic;

            return source;
        }

        public static AudioSource CreateOneShotSource(GameObject gameObject, float volume, float pitch, float maxDistance, float spread = 0)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.volume = volume;
            source.pitch = pitch;
            source.loop = false;
            source.spatialBlend = 1;

            if(spread != 0) {
                source.SetCustomCurve(AudioSourceCurveType.Spread, AnimationCurve.Linear(0, spread, 1, 0));
            }

            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.maxDistance = maxDistance;

            return source;
        }

        public static CollidingObject GetCollidingType(GameObject gameObject)
        {
            var part = Part.FromGO(gameObject);
            if(part) {
                if(part.GetComponent<ModuleAsteroid>()) {
                    return CollidingObject.Dirt;
                }
                return CollidingObject.Vessel;
            }

            if(gameObject.tag.ToLower() != "untagged") {
                if(Settings.CollisionData.ContainsKey(gameObject.name)) {
                    return Settings.CollisionData[gameObject.name];
                }

                if(Settings.CollisionData.ContainsKey("default")) {
                    return Settings.CollisionData["default"];
                }
            }

            return CollidingObject.Dirt;
        }
    }
}
