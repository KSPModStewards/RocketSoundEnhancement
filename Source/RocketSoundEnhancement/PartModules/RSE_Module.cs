using System.Collections;
using System.Collections.Generic;
using System.Linq;
using RocketSoundEnhancement.AudioFilters;
using RocketSoundEnhancement.Unity;
using UnityEngine;

namespace RocketSoundEnhancement.PartModules
{
    public class RSE_Module : PartModule
    {
        public Dictionary<string, AudioSource> Sources = new Dictionary<string, AudioSource>();
        public Dictionary<string, AirSimulationFilter> AirSimFilters = new Dictionary<string, AirSimulationFilter>();
        public Dictionary<string, float> Controls = new Dictionary<string, float>();

        public Dictionary<string, List<SoundLayer>> SoundLayerGroups = new Dictionary<string, List<SoundLayer>>();
        public List<SoundLayer> SoundLayers = new List<SoundLayer>();

        public GameObject AudioParent { get; protected set; }

        public ConfigNode PartConfigNode;
        public bool PrepareSoundLayers = true;
        public bool Initialized;
        public bool GamePaused;
        public bool AirSimFiltersEnabled;

        public float Volume = 1;
        public float DopplerFactor = 0.5f;

        public bool UseAirSimulation = false;
        public bool EnableCombFilter = false;
        public bool EnableLowpassFilter = false;
        public bool EnableDistortionFilter = false;
        public AirSimulationUpdate AirSimUpdateMode = AirSimulationUpdate.Full;
        public float FarLowpass = Settings.AirSimFarLowpass;
        public float MaxCombDelay = Settings.AirSimMaxCombDelay;
        public float MaxCombMix = Settings.AirSimMaxCombMix;
        public float MaxDistortion = Settings.AirSimMaxDistortion;
        public float AngleHighpass;

        private float doppler = 1;
        private float distance = 0;
        private float angle = 0;
        private float mach = 0;
        private float machAngle = 90;
        private float machPass = 1;
        private float lastDistance;
        private float loopRandomStart;

        private int slowUpdate;
        private System.Random random;

        public override void OnStart(StartState state)
        {
            string partParentName = part.name + "_" + this.moduleName;
            AudioParent = AudioUtility.CreateAudioParent(part, partParentName);
            PartConfigNode = AudioUtility.GetConfigNode(part.partInfo.name, this.moduleName);

            if (!float.TryParse(PartConfigNode.GetValue("volume"), out Volume)) Volume = 1;
            if (!float.TryParse(PartConfigNode.GetValue("DopplerFactor"), out DopplerFactor)) DopplerFactor = 0.5f;

            if (PartConfigNode.HasNode("AIRSIMULATION"))
            {
                var node = PartConfigNode.GetNode("AIRSIMULATION");

                if (node.HasValue("EnableCombFilter")) bool.TryParse(node.GetValue("EnableCombFilter"), out EnableCombFilter);
                if (node.HasValue("EnableLowpassFilter")) bool.TryParse(node.GetValue("EnableLowpassFilter"), out EnableLowpassFilter);
                if (node.HasValue("EnableDistortionFilter")) bool.TryParse(node.GetValue("EnableDistortionFilter"), out EnableDistortionFilter);

                if (node.HasValue("UpdateMode")) node.TryGetEnum("UpdateMode", ref AirSimUpdateMode, AirSimulationUpdate.Full);

                if (node.HasValue("FarLowpass")) FarLowpass = float.Parse(node.GetValue("FarLowpass"));
                if (node.HasValue("MaxCombDelay")) MaxCombDelay = float.Parse(node.GetValue("MaxCombDelay"));
                if (node.HasValue("MaxCombMix")) MaxCombMix = float.Parse(node.GetValue("MaxCombMix"));
                if (node.HasValue("MaxDistortion")) MaxDistortion = float.Parse(node.GetValue("MaxDistortion"));
                if (node.HasValue("AngleHighpass")) AngleHighpass = float.Parse(node.GetValue("AngleHighpass"));
            }

            UseAirSimulation = !(!EnableLowpassFilter && !EnableCombFilter && !EnableDistortionFilter);

            if (PrepareSoundLayers)
            {
                foreach (var node in PartConfigNode.GetNodes())
                {
                    var soundLayers = AudioUtility.CreateSoundLayerGroup(node.GetNodes("SOUNDLAYER"));
                    if (soundLayers.Count == 0) continue;

                    var groupName = node.name;

                    if(node.name == "SOUNDLAYERGROUP")
                    {
                        groupName = node.GetValue("name");
                    }

                    if (SoundLayerGroups.ContainsKey(groupName)) { SoundLayerGroups[groupName].AddRange(soundLayers); continue; }

                    SoundLayerGroups.Add(groupName, soundLayers);
                }

                SoundLayers = AudioUtility.CreateSoundLayerGroup(PartConfigNode.GetNodes("SOUNDLAYER"));

                if (SoundLayerGroups.Count > 0)
                {
                    foreach (var soundLayerGroup in SoundLayerGroups)
                    {
                        StartCoroutine(SetupAudioSources(soundLayerGroup.Value));
                    }
                }

                if (SoundLayers.Count > 0)
                {
                    StartCoroutine(SetupAudioSources(SoundLayers));
                }
            }

            random = new System.Random(GetInstanceID());
            loopRandomStart = (float)random.NextDouble();

            GameEvents.onGamePause.Add(OnGamePause);
            GameEvents.onGameUnpause.Add(OnGameUnpause);
        }

        public IEnumerator SetupAudioSources(List<SoundLayer> soundLayers)
        {
            foreach (var soundLayer in soundLayers.ToList())
            {
                string soundLayerName = soundLayer.name;
                if (!Sources.ContainsKey(soundLayerName))
                {
                    var sourceGameObject = new GameObject($"{AudioUtility.RSETag}_{soundLayerName}");
                    sourceGameObject.transform.SetParent(AudioParent.transform, false);
                    sourceGameObject.transform.localPosition = Vector3.zero;
                    sourceGameObject.transform.localRotation = Quaternion.Euler(0, 0, 0);

                    var source = AudioUtility.CreateSource(sourceGameObject, soundLayer);
                    source.enabled = false;
                    Sources.Add(soundLayerName, source);

                    var airSimFilter = sourceGameObject.AddComponent<AirSimulationFilter>();
                    airSimFilter.enabled = false;

                    airSimFilter.EnableCombFilter = EnableCombFilter;
                    airSimFilter.EnableLowpassFilter = EnableLowpassFilter;
                    airSimFilter.EnableDistortionFilter = EnableDistortionFilter;
                    airSimFilter.SimulationUpdate = AirSimUpdateMode;

                    airSimFilter.MaxDistance = Settings.AirSimMaxDistance;
                    airSimFilter.FarLowpass = FarLowpass;
                    airSimFilter.MaxCombDelay = MaxCombDelay;
                    airSimFilter.MaxCombMix = MaxCombMix;
                    airSimFilter.MaxDistortion = MaxDistortion;
                    airSimFilter.AngleHighpass = AngleHighpass;

                    AirSimFilters.Add(soundLayerName, airSimFilter);
                }
                yield return null;
            }
        }

        public virtual void LateUpdate()
        {
            slowUpdate++;
            if (slowUpdate >= 60)
            {
                if (Sources.Count > 0)
                {
                    foreach (var source in Sources.Keys)
                    {
                        //disable the filter but keep the fields updated
                        if (!Sources[source].isPlaying && AirSimFilters.ContainsKey(source))
                        {
                            AirSimFilters[source].enabled = false;
                            AirSimFilters[source].Distance = distance;
                            AirSimFilters[source].Mach = mach;
                            AirSimFilters[source].Angle = angle;
                            AirSimFilters[source].MachAngle = machAngle;
                            AirSimFilters[source].MachPass = machPass;
                        }

                        if (Sources[source].isPlaying || !Sources[source].enabled)
                            continue;

                        Sources[source].enabled = false;

                        loopRandomStart = (float)random.NextDouble();
                    }
                }
                slowUpdate = 0;
            }

            if (AirSimFilters.Count > 0 && AirSimFiltersEnabled && Settings.MufflerQuality != AudioMufflerQuality.AirSim)
            {
                AirSimFilters.Values.ToList().ForEach(x => x.enabled = false);
                AirSimFiltersEnabled = false;
            }
        }

        public virtual void FixedUpdate()
        {
            if (!Initialized || !vessel.loaded)
                return;

            if (Settings.EnableAudioEffects && Settings.MufflerQuality > AudioMufflerQuality.Normal)
            {
                //  Calculate Doppler
                if (DopplerFactor > 0)
                {
                    float speedOfSound = vessel.speedOfSound > 0 ? (float)vessel.speedOfSound : 340.29f;
                    distance = Vector3.Distance(CameraManager.GetCurrentCamera().transform.position, transform.position);
                    float relativeSpeed = (lastDistance - distance) / TimeWarp.fixedDeltaTime;
                    lastDistance = distance;
                    float dopplerFactor = DopplerFactor * Settings.DopplerFactor;
                    float dopplerRaw = Mathf.Clamp((speedOfSound + ((relativeSpeed) * dopplerFactor)) / speedOfSound, 1 - (dopplerFactor * 0.5f), 1 + dopplerFactor);
                    doppler = Mathf.MoveTowards(doppler, dopplerRaw, 0.5f * TimeWarp.fixedDeltaTime);
                }

                angle = (1 + Vector3.Dot(vessel.GetComponent<ShipEffects>().MachTipCameraNormal, (transform.up + vessel.velocityD).normalized)) * 90;

                bool isActiveAndInternal = vessel == FlightGlobals.ActiveVessel && InternalCamera.Instance.isActive;
                if (isActiveAndInternal)
                {
                    angle = 0;
                    machPass = 1;
                    return;
                }

                if (Settings.MachEffectsAmount > 0)
                {
                    mach = Mathf.Clamp01(vessel.GetComponent<ShipEffects>().Mach);
                    machAngle = vessel.GetComponent<ShipEffects>().MachAngle;
                    machPass = Mathf.Lerp(1, Settings.MachEffectLowerLimit, Mathf.Clamp01(angle / machAngle) * mach);
                }
                else
                {
                    mach = 0;
                    machAngle = 90;
                    machPass = 1;
                }
            }
        }

        public void PlaySoundLayer(SoundLayer soundLayer, float control, float volume, bool rndOneShotVol = false)
        {
            string soundLayerName = soundLayer.name;
            if (!Sources.ContainsKey(soundLayerName)) return;

            float finalVolume, finalPitch;
            finalVolume = soundLayer.volumeFC?.Evaluate(control) ?? soundLayer.volume.Value(control);
            finalVolume *= soundLayer.massToVolume?.Value((float)part.physicsMass) ?? 1;

            finalPitch = soundLayer.pitchFC?.Evaluate(control) ?? soundLayer.pitch.Value(control);
            finalPitch *= soundLayer.massToPitch?.Value((float)part.physicsMass) ?? 1;
            finalPitch *= Settings.EnableAudioEffects ? doppler : 1;

            if (finalVolume < float.Epsilon)
            {
                if (Sources[soundLayerName].volume == 0 && soundLayer.loop)
                    Sources[soundLayerName].Stop();

                if (Sources[soundLayerName].isPlaying && soundLayer.loop)
                    Sources[soundLayerName].volume = 0;
                    
                return;
            }

            AudioSource source = Sources[soundLayerName];
            source.enabled = true;

            if (Settings.EnableAudioEffects && Settings.MufflerQuality > AudioMufflerQuality.Normal && soundLayer.channel == FXChannel.Exterior)
            {
                if (Settings.MufflerQuality == AudioMufflerQuality.AirSim && AirSimFilters.ContainsKey(soundLayerName) && UseAirSimulation)
                {
                    AirSimFilters[soundLayerName].enabled = true;
                    AirSimFilters[soundLayerName].Distance = distance;
                    AirSimFilters[soundLayerName].Mach = mach;
                    AirSimFilters[soundLayerName].Angle = angle;
                    AirSimFilters[soundLayerName].MachAngle = machAngle;
                    AirSimFilters[soundLayerName].MachPass = machPass;
                    AirSimFiltersEnabled = true;
                }
                else
                {
                    if (Settings.MachEffectsAmount > 0)
                    {
                        finalVolume *= Mathf.Log10(Mathf.Lerp(0.1f, 10, machPass)) * 0.5f;
                    }
                }
            }

            float volumeScale = !soundLayer.loop ? finalVolume * GameSettings.SHIP_VOLUME * volume : 1;
            volumeScale *= rndOneShotVol ? Random.Range(0.9f, 1.0f) : 1;

            source.volume = !soundLayer.loop ? 1 : finalVolume * GameSettings.SHIP_VOLUME * volume;
            source.pitch = finalPitch;

            int index = soundLayer.audioClips.Length > 1 ? Random.Range(0, soundLayer.audioClips.Length - 1) : 0;

            if (soundLayer.loop && soundLayer.audioClips != null && (source.clip == null || source.clip != soundLayer.audioClips[index]))
            {
                source.clip = soundLayer.audioClips[index];
                source.time = soundLayer.loopAtRandom ? loopRandomStart * source.clip.length : 0;
            }

            AudioUtility.PlayAtChannel(source, soundLayer.channel, vessel == FlightGlobals.ActiveVessel, soundLayer.loop, volumeScale, !soundLayer.loop ? soundLayer.audioClips[index] : null);
        }

        public void OnGamePause()
        {
            if (Sources.Count > 0) { Sources.Values.ToList().ForEach(x => x.Pause()); }
            GamePaused = true;
        }

        public void OnGameUnpause()
        {
            if (Sources.Count > 0) { Sources.Values.ToList().ForEach(x => x.UnPause()); }

            GamePaused = false;
        }

        public virtual void OnDestroy()
        {
            if (!Initialized) return;
            if (Sources.Count > 0) { Sources.Values.ToList().ForEach(x => x.Stop()); }
            Destroy(AudioParent);

            GameEvents.onGamePause.Remove(OnGamePause);
            GameEvents.onGameUnpause.Remove(OnGameUnpause);
        }
    }
}
