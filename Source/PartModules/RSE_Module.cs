using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Audio;

namespace RocketSoundEnhancement
{
    public class RSE_Module : PartModule
    {
        public Dictionary<string, AudioSource> Sources = new Dictionary<string, AudioSource>();
        public Dictionary<string, AirSimulationFilter> AirSimFilters = new Dictionary<string, AirSimulationFilter>();
        public Dictionary<string, float> Controls = new Dictionary<string, float>();

        public Dictionary<string, List<SoundLayer>> SoundLayerGroups = new Dictionary<string, List<SoundLayer>>();
        public List<SoundLayer> SoundLayers = new List<SoundLayer>();

        public GameObject audioParent { get; protected set; }
        public bool initialized;
        public bool gamePaused;

        public float Volume = 1;
        public float DopplerFactor = 0.5f;

        public bool UseAirSimulation = false;
        public bool EnableCombFilter = false;
        public bool EnableLowpassFilter = false;
        public bool EnableWaveShaperFilter = false;
        public AirSimulationUpdate AirSimUpdateMode;
        public float MaxDistance = 2500;
        public float FarLowpass = 1000f;
        public float AngleHighpass = 0;
        public float MaxCombDelay = 20;
        public float MaxCombMix = 0.25f;
        public float MaxDistortion = 0.5f;


        float distance;
        float angle;
        float machPass;
        float speedOfSound = 340.29f;

        public ConfigNode configNode;
        public bool getSoundLayersandGroups = true;
        public override void OnStart(StartState state)
        {
            string partParentName = part.name + "_" + this.moduleName;
            audioParent = AudioUtility.CreateAudioParent(part, partParentName);
            configNode = AudioUtility.GetConfigNode(part.partInfo.name, this.moduleName);

            if (!float.TryParse(configNode.GetValue("volume"), out Volume)) Volume = 1;
            if (!float.TryParse(configNode.GetValue("DopplerFactor"), out DopplerFactor)) DopplerFactor = 0.5f;

            if (UseAirSimulation = configNode.HasNode("AIRSIMULATION"))
            {
                var node = configNode.GetNode("AIRSIMULATION");

                bool.TryParse(node.GetValue("EnableCombFilter"), out EnableCombFilter);
                bool.TryParse(node.GetValue("EnableLowpassFilter"), out EnableLowpassFilter);
                bool.TryParse(node.GetValue("EnableWaveShaperFilter"), out EnableWaveShaperFilter);

                node.TryGetEnum("UpdateMode", ref AirSimUpdateMode, AirSimulationUpdate.Full);

                if (!float.TryParse(node.GetValue("MaxDistance"), out MaxDistance)) MaxDistance = 2500;
                if (!float.TryParse(node.GetValue("FarLowpass"), out FarLowpass)) FarLowpass = 1000f;
                if (!float.TryParse(node.GetValue("AngleHighpass"), out AngleHighpass)) AngleHighpass = 0;
                if (!float.TryParse(node.GetValue("MaxCombDelay"), out MaxCombDelay)) MaxCombDelay = 20;
                if (!float.TryParse(node.GetValue("MaxCombMix"), out MaxCombMix)) MaxCombMix = 0.25f;
                if (!float.TryParse(node.GetValue("MaxDistortion"), out MaxDistortion)) MaxDistortion = 0.5f;
            }

            if (getSoundLayersandGroups)
            {
                foreach (var node in configNode.GetNodes())
                {
                    string groupName = node.name;
                    var soundLayers = AudioUtility.CreateSoundLayerGroup(node.GetNodes("SOUNDLAYER"));
                    if (soundLayers.Count > 0)
                    {
                        if (SoundLayerGroups.ContainsKey(groupName))
                        {
                            SoundLayerGroups[groupName].AddRange(soundLayers);
                        }
                        else
                        {
                            SoundLayerGroups.Add(groupName, soundLayers);
                        }
                    }
                }

                SoundLayers = AudioUtility.CreateSoundLayerGroup(configNode.GetNodes("SOUNDLAYER"));
            }

            GameEvents.onGamePause.Add(onGamePause);
            GameEvents.onGameUnpause.Add(onGameUnpause);
        }

        public override void OnUpdate()
        {
            if(!initialized)
                return;

            if(Sources.Count > 0) {
                var sourceKeys = Sources.Keys.ToList();

                foreach(var source in sourceKeys) {
                    if(AirSimFilters.ContainsKey(source) && AudioMuffler.MufflerQuality != AudioMufflerQuality.AirSim) {
                        UnityEngine.Object.Destroy(AirSimFilters[source]);
                        AirSimFilters.Remove(source);
                    }

                    if(!Sources[source].isPlaying) {
                        if(AirSimFilters.ContainsKey(source)) {
                            UnityEngine.Object.Destroy(AirSimFilters[source]);
                            AirSimFilters.Remove(source);
                        }

                        UnityEngine.Object.Destroy(Sources[source]);
                        
                        Sources.Remove(source);
                        Controls.Remove(source);
                    }
                }
            }
        }

        public virtual void FixedUpdate()
        {
            if (!initialized)
                return;

            if (AudioMuffler.EnableMuffling && AudioMuffler.MufflerQuality > AudioMufflerQuality.Lite)
            {
                distance = Vector3.Distance(CameraManager.GetCurrentCamera().transform.position, transform.position);
                speedOfSound = vessel.speedOfSound > 0 ? (float)vessel.speedOfSound : 340.29f;
                angle = (1 + Vector3.Dot(vessel.GetComponent<ShipEffects>().MachOriginCameraNormal, (transform.up + vessel.velocityD).normalized)) * 90;
                machPass = 1f - Mathf.Clamp01(angle / vessel.GetComponent<ShipEffects>().MachAngle) * Mathf.Clamp01(vessel.GetComponent<ShipEffects>().Mach);

                if (vessel.isActiveVessel && (InternalCamera.Instance.isActive || MapView.MapCamera.isActiveAndEnabled))
                {
                    machPass = 1;
                    angle = 0;
                }

                CalculateDoppler();
            }
        }

        public float Doppler = 1;
        float dopplerRaw = 1;
        float relativeSpeed = 0;
        float lastDistance = 0;
        public void CalculateDoppler()
        {
            relativeSpeed = (lastDistance - distance) / TimeWarp.fixedDeltaTime;
            lastDistance = distance;
            dopplerRaw = Mathf.Clamp((speedOfSound + ((relativeSpeed) * DopplerFactor)) / speedOfSound, 1 - (DopplerFactor * 0.5f), 1 + DopplerFactor);

            Doppler = Mathf.MoveTowards(Doppler, dopplerRaw, 0.5f * TimeWarp.fixedDeltaTime);
        }

        float pitchVariation = 1;
        public void PlaySoundLayer(string sourceLayerName, SoundLayer soundLayer, float controlInput, float volume, bool oneShot = false, bool rndOneShotVol = false)
        {
            float control = Mathf.Round(controlInput * 1000.0f) * 0.001f;
            float finalVolume, finalPitch;

            if(soundLayer.useFloatCurve) {
                finalVolume = soundLayer.volumeFC.Evaluate(control);
                finalPitch = soundLayer.pitchFC.Evaluate(control);
            } else {
                finalVolume = soundLayer.volume.Value(control);
                finalPitch = soundLayer.pitch.Value(control);
            }

            if (soundLayer.massToVolume != null) { finalVolume *= soundLayer.massToVolume.Value((float)part.physicsMass); }
            if (soundLayer.massToPitch != null) { finalPitch *= soundLayer.massToPitch.Value((float)part.physicsMass); }
            if (AudioMuffler.MufflerQuality > AudioMufflerQuality.Lite) { finalPitch *= Doppler; }
            if (soundLayer.distance != null) { finalVolume *= soundLayer.distance.Value(distance); }

            if(finalVolume < float.Epsilon) {
                if(Sources.ContainsKey(sourceLayerName)) {
                    Sources[sourceLayerName].Stop();
                }
                return;
            }

            AudioSource source;
            if(!Sources.ContainsKey(sourceLayerName)) {
                GameObject sourceGameObject = new GameObject(sourceLayerName);
                sourceGameObject.transform.parent = audioParent.transform;
                sourceGameObject.transform.position = audioParent.transform.position;
                sourceGameObject.transform.rotation = audioParent.transform.rotation;

                source = AudioUtility.CreateSource(sourceGameObject, soundLayer);

                Sources.Add(sourceLayerName, source);
                if(soundLayer.pitchVariation) {
                    pitchVariation = UnityEngine.Random.Range(0.95f, 1.05f);
                }

            } else {
                source = Sources[sourceLayerName];
            }

            if(AudioMuffler.EnableMuffling && AudioMuffler.MufflerQuality == AudioMufflerQuality.AirSim && soundLayer.channel == FXChannel.Exterior) {
                if(UseAirSimulation){
                    ProcessAirSimulation(sourceLayerName, ref source);
                } else {
                    float machPassLog = Mathf.Log(Mathf.Lerp(1, 10, machPass), 10);
                    finalVolume *= Mathf.Min(machPassLog, Mathf.Pow(1 - Mathf.Clamp01(distance / MaxDistance), 2) * 0.5f);
                }
            }

            source.volume = finalVolume * GameSettings.SHIP_VOLUME * volume;
            source.pitch = finalPitch * (soundLayer.pitchVariation && !soundLayer.loopAtRandom ? pitchVariation : 1);

            if(oneShot) {
                int index = 0;
                if(soundLayer.audioClips.Length > 1) {
                    index = UnityEngine.Random.Range(0, soundLayer.audioClips.Length);
                }
                AudioClip clip = GameDatabase.Instance.GetAudioClip(soundLayer.audioClips[index]);
                float volumeScale = rndOneShotVol ? UnityEngine.Random.Range(0.9f, 1.0f) : 1;

                AudioUtility.PlayAtChannel(source, soundLayer.channel, vessel, false, false,true, volumeScale, clip);
            } else {
                AudioUtility.PlayAtChannel(source, soundLayer.channel, vessel, soundLayer.loop, soundLayer.loopAtRandom);
            }
        }

        public void ProcessAirSimulation(string sourceLayerName, ref AudioSource source){
            AirSimulationFilter airSimFilter;

            if(!AirSimFilters.ContainsKey(sourceLayerName)) {
                airSimFilter = source.gameObject.AddComponent<AirSimulationFilter>();
                airSimFilter.enabled = true;

                airSimFilter.EnableCombFilter = EnableCombFilter;
                airSimFilter.EnableLowpassFilter = EnableLowpassFilter;
                airSimFilter.EnableWaveShaperFilter = EnableWaveShaperFilter;
                airSimFilter.SimulationUpdate = AirSimUpdateMode;

                airSimFilter.MaxDistance = MaxDistance;
                airSimFilter.FarLowpass = FarLowpass;
                airSimFilter.AngleHighPass = AngleHighpass;
                airSimFilter.MaxCombDelay = MaxCombDelay;
                airSimFilter.MaxCombMix = MaxCombMix;
                airSimFilter.MaxDistortion = MaxDistortion;

                AirSimFilters.Add(sourceLayerName, airSimFilter);
            } else {
                airSimFilter = AirSimFilters[sourceLayerName];
            }

            airSimFilter.Distance = distance;
            airSimFilter.Mach = vessel.GetComponent<ShipEffects>().Mach;
            airSimFilter.Angle = angle;
            airSimFilter.MachAngle = vessel.GetComponent<ShipEffects>().MachAngle;
            airSimFilter.MachPass = machPass;
        }

        public void onGamePause()
        {
            if(Sources.Count > 0) {
                foreach(var source in Sources.Values) {
                    source.Pause();
                }
            }
            gamePaused = true;
        }

        public void onGameUnpause()
        {
            if(Sources.Count > 0) {
                foreach(var source in Sources.Values) {
                    source.UnPause();
                }
            }
            gamePaused = false;
        }

        public void OnDestroy()
        {
            if(!initialized)
                return;

            if(Sources.Count > 0) {
                foreach(var source in Sources.Values) {
                    source.Stop();
                    UnityEngine.Object.Destroy(source);
                }
            }

            GameEvents.onGamePause.Remove(onGamePause);
            GameEvents.onGameUnpause.Remove(onGameUnpause);
        }
    }
}
