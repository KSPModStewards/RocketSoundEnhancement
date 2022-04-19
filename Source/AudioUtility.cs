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
        ShipBoth
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
        public string[] audioClips;
        public FXChannel channel;
        public bool loop;
        public bool loopAtRandom;
        public bool pitchVariation;
        public bool spool;
        public bool useFloatCurve;
        public float spoolSpeed;
        public float spoolIdle;
        public float spread;
        public FXCurve volume;
        public FXCurve pitch;
        public FloatCurve volumeFC;
        public FloatCurve pitchFC;
        public FXCurve massToVolume;
        public FXCurve massToPitch;
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

            if(node.HasValue("audioClip")) {
                soundLayer.audioClips = new string[node.GetValues("audioClip").Length];
                for(int i = 0; i < soundLayer.audioClips.Length; i++) {
                    soundLayer.audioClips[i] = node.GetValue("audioClip", i);
                }
            }

            node.TryGetValue("loop", ref soundLayer.loop);
            node.TryGetValue("loopAtRandom", ref soundLayer.loopAtRandom);
            if(!node.TryGetValue("pitchVariation", ref soundLayer.pitchVariation)) {
                soundLayer.pitchVariation = true;
            }

            node.TryGetValue("spool", ref soundLayer.spool);

            node.TryGetValue("spoolSpeed", ref soundLayer.spoolSpeed);

            node.TryGetValue("spoolIdle", ref soundLayer.spoolIdle);
            node.TryGetValue("spread", ref soundLayer.spread);

            soundLayer.volume = new FXCurve("volume", 1);
            soundLayer.pitch = new FXCurve("pitch", 1);

            node.TryGetEnum("channel", ref soundLayer.channel, FXChannel.ShipBoth);

            soundLayer.volume.Load("volume", node);
            soundLayer.pitch.Load("pitch", node);

            if(node.TryGetValue("useFloatCurve", ref soundLayer.useFloatCurve)) {
                soundLayer.volumeFC = new FloatCurve();
                soundLayer.pitchFC = new FloatCurve();

                if(node.HasNode("volumeFC"))
                    soundLayer.volumeFC.Load(node.GetNode("volumeFC"));

                if(node.HasNode("pitchFC"))
                    soundLayer.pitchFC.Load(node.GetNode("pitchFC"));
            }

            if(node.HasValue("massToVolume")) {
                soundLayer.massToVolume = new FXCurve("massToVolume", 1);
                soundLayer.massToVolume.Load("massToVolume", node);
            }

            if(node.HasValue("massToPitch")) {
                soundLayer.massToPitch = new FXCurve("massToPitch", 1);
                soundLayer.massToPitch.Load("massToPitch", node);
            }

            if(node.HasValue("data")) {
                soundLayer.data = node.GetValue("data");
            } else {
                soundLayer.data = "";
            }

            return soundLayer;
        }

        public static AudioSource CreateSource(GameObject gameObject, SoundLayer soundLayer,bool oneShot = false)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.name = RSETag + "_" + gameObject.name;
            source.playOnAwake = false;

            if(!oneShot) {
                if(soundLayer.audioClips != null) {
                    int index = 0;
                    if(soundLayer.audioClips.Length > 1) {
                        index = UnityEngine.Random.Range(0, soundLayer.audioClips.Length);
                    }
                    source.clip = GameDatabase.Instance.GetAudioClip(soundLayer.audioClips[index]);

                }

                if(source.clip == null)
                    return null;
            }

            source.volume = soundLayer.volume;
            source.pitch = soundLayer.pitch;
            source.loop = soundLayer.loop;
            source.spatialBlend = 1;

            if(soundLayer.spread != 0) {
                source.SetCustomCurve(AudioSourceCurveType.Spread, AnimationCurve.Linear(0, soundLayer.spread, 1, 0));
            }

            source.rolloffMode = AudioRolloffMode.Logarithmic;

            return source;
        }

        public static AudioSource CreateOneShotSource(GameObject gameObject, float volume, float pitch, float spread = 0)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.name = RSETag + "_" + gameObject.name;
            source.playOnAwake = false;
            source.volume = volume;
            source.pitch = pitch;
            source.loop = false;
            source.spatialBlend = 1;

            if(spread != 0) {
                source.SetCustomCurve(AudioSourceCurveType.Spread, AnimationCurve.Linear(0, spread, 1, 0));
            }

            source.rolloffMode = AudioRolloffMode.Logarithmic;

            return source;
        }

        public static CollidingObject GetCollidingObject(GameObject gameObject)
        {
            var part = Part.FromGO(gameObject);
            if(part) {
                if(part.GetComponent<ModuleAsteroid>()) {
                    return CollidingObject.Dirt;
                }
                return CollidingObject.Vessel;
            }

            if(gameObject.tag.ToLower() != "untagged") {
                if(Settings.Instance.CollisionData.ContainsKey(gameObject.name)) {
                    return Settings.Instance.CollisionData[gameObject.name];
                }

                if(Settings.Instance.CollisionData.ContainsKey("default")) {
                    return Settings.Instance.CollisionData["default"];
                }
            }

            return CollidingObject.Dirt;
        }

        public static GameObject CreateAudioParent(Part part, string partName)
        {
            var audioParent = part.gameObject.GetChild(partName);
            if(audioParent == null) {
                audioParent = new GameObject(partName);
                audioParent.transform.rotation = part.transform.rotation;
                audioParent.transform.position = part.transform.position;
                audioParent.transform.parent = part.transform;
            }
            return audioParent;
        }

        public static void PlayAtChannel(AudioSource source, FXChannel channel,Vessel vessel, bool loop = false, bool loopAtRandom = false, bool oneshot = false, float volume = 1.0f, AudioClip audioclip = null)
        {
            if(source == null)
                return;

            if(TimeWarp.CurrentRate > TimeWarp.fetch.physicsWarpRates.Last()) {
                if(source.isPlaying) {
                    source.Stop();
                }
                return;
            }

            if(!source.isActiveAndEnabled)
                return;

            switch(channel) {
                case FXChannel.ShipBoth:
                    source.volume *= Settings.Instance.ExteriorVolume;
                    if(loop) {
                        if(!source.isPlaying) {
                            if(loopAtRandom) {
                                source.time = UnityEngine.Random.Range(0, source.clip.length / 2);
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
                    source.volume *= Settings.Instance.InteriorVolume;

                    if(vessel != null) {
                        source.mute = vessel == FlightGlobals.ActiveVessel ? !InternalCamera.Instance.isActive : true;

                    } else {
                        source.mute = !InternalCamera.Instance.isActive;
                    }
                    
                    if(AudioMuffler.MufflerQuality == 0) {
                        source.bypassListenerEffects = true;
                        source.bypassEffects = true;
                    }

                    if(loop) {
                        if(!source.isPlaying) {
                            if(loopAtRandom) {
                                source.time = UnityEngine.Random.Range(0, source.clip.length / 2);
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

    }
}