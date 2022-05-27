using System.Collections;
using System.Collections.Generic;
using System.Linq;
using RocketSoundEnhancement.AudioFilters;
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

        public GameObject audioParent { get; protected set; }
        public bool initialized;
        public bool gamePaused;

        public float Volume = 1;
        public float DopplerFactor = 0.5f;

        public bool UseAirSimulation = false;
        public bool EnableCombFilter = false;
        public bool EnableLowpassFilter = false;
        public bool EnableWaveShaperFilter = false;
        public AirSimulationUpdate AirSimUpdateMode = AirSimulationUpdate.Full;
        public float MaxDistance = 2500;
        public float FarLowpass = 2500;
        public float AngleHighpass = 0;
        public float MaxCombDelay = 20;
        public float MaxCombMix = 0.25f;
        public float MaxDistortion = 0.5f;

        float doppler = 1;
        float distance;
        float angle;
        float mach;
        float machAngle;
        float machPass;
        float lastDistance;
        float loopRandomStart;

        public ConfigNode configNode;
        public bool prepareSoundLayers = true;
        bool airSimFiltersEnabled;

        public override void OnStart(StartState state)
        {
            string partParentName = part.name + "_" + this.moduleName;
            audioParent = AudioUtility.CreateAudioParent(part, partParentName);
            configNode = AudioUtility.GetConfigNode(part.partInfo.name, this.moduleName);

            if (!float.TryParse(configNode.GetValue("volume"), out Volume)) Volume = 1;
            if (!float.TryParse(configNode.GetValue("DopplerFactor"), out DopplerFactor)) DopplerFactor = 0.5f;

            if (configNode.HasNode("AIRSIMULATION"))
            {
                var node = configNode.GetNode("AIRSIMULATION");

                if(node.HasValue("EnableCombFilter")) bool.TryParse(node.GetValue("EnableCombFilter"), out EnableCombFilter);
                if(node.HasValue("EnableLowpassFilter")) bool.TryParse(node.GetValue("EnableLowpassFilter"), out EnableLowpassFilter);
                if(node.HasValue("EnableWaveShaperFilter")) bool.TryParse(node.GetValue("EnableWaveShaperFilter"), out EnableWaveShaperFilter);

                if(node.HasValue("UpdateMode")) node.TryGetEnum("UpdateMode", ref AirSimUpdateMode, AirSimulationUpdate.Full);

                if (node.HasValue("MaxDistance")) MaxDistance = float.Parse(node.GetValue("MaxDistance"));
                if (node.HasValue("FarLowpass")) FarLowpass = float.Parse(node.GetValue("FarLowpass"));
                if (node.HasValue("AngleHighpass")) AngleHighpass = float.Parse(node.GetValue("AngleHighpass"));
                if (node.HasValue("MaxCombDelay")) MaxCombDelay = float.Parse(node.GetValue("MaxCombDelay"));
                if (node.HasValue("MaxCombMix")) MaxCombMix = float.Parse(node.GetValue("MaxCombMix"));
                if (node.HasValue("MaxDistortion")) MaxDistortion = float.Parse(node.GetValue("MaxDistortion"));
            }

            UseAirSimulation = !(!EnableLowpassFilter && !EnableCombFilter && !EnableWaveShaperFilter);

            if (prepareSoundLayers)
            {
                foreach (var node in configNode.GetNodes())
                {
                    var soundLayers = AudioUtility.CreateSoundLayerGroup(node.GetNodes("SOUNDLAYER"));
                    if (soundLayers.Count == 0) continue;

                    var groupName = node.name;
                    if (SoundLayerGroups.ContainsKey(groupName)) { SoundLayerGroups[groupName].AddRange(soundLayers); continue; }

                    SoundLayerGroups.Add(groupName, soundLayers);
                }

                SoundLayers = AudioUtility.CreateSoundLayerGroup(configNode.GetNodes("SOUNDLAYER"));

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

            loopRandomStart = UnityEngine.Random.Range(0f,1.0f);

            GameEvents.onGamePause.Add(onGamePause);
            GameEvents.onGameUnpause.Add(onGameUnpause);
        }

        public IEnumerator SetupAudioSources(List<SoundLayer> soundLayers)
        {
            foreach (var soundLayer in soundLayers.ToList())
            {
                string soundLayerName = soundLayer.loop ? soundLayer.name : "oneshotSource";
                if (!Sources.ContainsKey(soundLayerName))
                {
                    var sourceGameObject = new GameObject(soundLayerName);
                    sourceGameObject.transform.parent = audioParent.transform;
                    sourceGameObject.transform.position = audioParent.transform.position;
                    sourceGameObject.transform.rotation = audioParent.transform.rotation;

                    var source = AudioUtility.CreateSource(sourceGameObject, soundLayer);
                    source.enabled = false;
                    Sources.Add(soundLayerName, source);

                    var airSimFilter = new AirSimulationFilter();
                    airSimFilter = sourceGameObject.AddComponent<AirSimulationFilter>();
                    airSimFilter.enabled = false;

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

                    AirSimFilters.Add(soundLayerName, airSimFilter);
                }
                yield return null;
            }
        }

        public virtual void LateUpdate()
        {
            if (Sources.Count > 0)
            {
                foreach(var source in Sources.Keys)
                {
                    if (Sources[source].isPlaying || !Sources[source].enabled)
                        continue;

                    if (AirSimFilters.ContainsKey(source))
                        AirSimFilters[source].enabled = false;

                    Sources[source].enabled = false;
                }
            }

            if (AirSimFilters.Count > 0 && airSimFiltersEnabled && AudioMuffler.MufflerQuality != AudioMufflerQuality.AirSim)
            {
                AirSimFilters.Values.ToList().ForEach(x => x.enabled = false);
                airSimFiltersEnabled = false;
            }
        }

        public virtual void FixedUpdate()
        {
            if (!initialized || !vessel.loaded)
                return;

            if (AudioMuffler.EnableMuffling && AudioMuffler.MufflerQuality > AudioMufflerQuality.Lite)
            {
                //  Calculate Doppler
                var speedOfSound = vessel.speedOfSound > 0 ? (float)vessel.speedOfSound : 340.29f;
                distance = Vector3.Distance(CameraManager.GetCurrentCamera().transform.position, transform.position);
                var relativeSpeed = (lastDistance - distance) / TimeWarp.fixedDeltaTime;
                lastDistance = distance;

                var dopplerRaw = Mathf.Clamp((speedOfSound + ((relativeSpeed) * DopplerFactor)) / speedOfSound, 1 - (DopplerFactor * 0.5f), 1 + DopplerFactor);
                doppler = Mathf.MoveTowards(doppler, dopplerRaw, 0.5f * TimeWarp.fixedDeltaTime);

                if (AudioMuffler.MufflerQuality != AudioMufflerQuality.AirSim)
                    return;

                angle = (1 + Vector3.Dot(vessel.GetComponent<ShipEffects>().MachTipCameraNormal, (transform.up + vessel.velocityD).normalized)) * 90;
                machPass = 1f - Mathf.Clamp01(angle / vessel.GetComponent<ShipEffects>().MachAngle) * Mathf.Clamp01(vessel.GetComponent<ShipEffects>().Mach);
                
                if (vessel.isActiveVessel && (InternalCamera.Instance.isActive || MapView.MapCamera.isActiveAndEnabled))
                {
                    angle = 0;
                    machPass = 1;
                }
                mach = vessel.GetComponent<ShipEffects>().Mach;
                machAngle = vessel.GetComponent<ShipEffects>().MachAngle;
            }
        }

        public void PlaySoundLayer(SoundLayer soundLayer, float control, float volume, bool rndOneShotVol = false)
        {
            string soundLayerName = soundLayer.loop ? soundLayer.name : "oneshotSource";
            if (!Sources.ContainsKey(soundLayerName)) return;

            float finalVolume, finalPitch;
            finalVolume = soundLayer.volumeFC?.Evaluate(control) ?? soundLayer.volume.Value(control);
            finalVolume *= soundLayer.massToVolume?.Value((float)part.physicsMass) ?? 1;
            finalVolume = Mathf.Round(finalVolume * 1000) * 0.001f;

            if (finalVolume < float.Epsilon)
            {
                if (Sources[soundLayerName].isPlaying && soundLayer.loop)
                    Sources[soundLayerName].Stop();

                return;
            }

            finalPitch = soundLayer.pitchFC?.Evaluate(control) ?? soundLayer.pitch.Value(control);
            finalPitch *= soundLayer.massToPitch?.Value((float)part.physicsMass) ?? 1;
            finalPitch *= AudioMuffler.MufflerQuality > AudioMufflerQuality.Lite ? doppler : 1;

            AudioSource source = Sources[soundLayerName];
            source.enabled = true;
            if (AudioMuffler.EnableMuffling && AudioMuffler.MufflerQuality == AudioMufflerQuality.AirSim && soundLayer.channel == FXChannel.Exterior)
            {
                if (UseAirSimulation && AirSimFilters.ContainsKey(soundLayerName))
                {
                    AirSimFilters[soundLayerName].enabled = true;
                    AirSimFilters[soundLayerName].Distance = distance;
                    AirSimFilters[soundLayerName].Mach = mach;
                    AirSimFilters[soundLayerName].Angle = angle;
                    AirSimFilters[soundLayerName].MachAngle = machAngle;
                    AirSimFilters[soundLayerName].MachPass = machPass;
                    airSimFiltersEnabled = true;
                }
                else
                {
                    float machPassLog = Mathf.Log(Mathf.Lerp(1, 10, machPass), 10);
                    finalVolume *= Mathf.Min(machPassLog, Mathf.Pow(1 - Mathf.Clamp01(distance / MaxDistance), 2) * 0.5f);
                }
            }

            float volumeScale = !soundLayer.loop ? finalVolume * GameSettings.SHIP_VOLUME * volume : 1;
            volumeScale *= rndOneShotVol ? UnityEngine.Random.Range(0.9f, 1.0f) : 1;

            source.volume = !soundLayer.loop ? 1 : finalVolume * GameSettings.SHIP_VOLUME * volume;
            source.pitch = finalPitch;

            int index = soundLayer.audioClips.Length > 1 ? UnityEngine.Random.Range(0, soundLayer.audioClips.Length) : 0;

            if (soundLayer.loop && soundLayer.audioClips != null && (source.clip == null || source.clip != soundLayer.audioClips[index]))
            {
                source.clip = soundLayer.audioClips[index];
                source.time = soundLayer.loopAtRandom ? loopRandomStart * source.clip.length : 0;
            }

            AudioUtility.PlayAtChannel(source, soundLayer.channel, vessel == FlightGlobals.ActiveVessel, soundLayer.loop, volumeScale, !soundLayer.loop ? soundLayer.audioClips[index] : null);
        }

        public void onGamePause()
        {
            if (Sources.Count > 0) { Sources.Values.ToList().ForEach(x => x.Pause()); }
            gamePaused = true;
        }

        public void onGameUnpause()
        {
            if (Sources.Count > 0) { Sources.Values.ToList().ForEach(x => x.UnPause()); }
            gamePaused = false;
        }

        public virtual void OnDestroy()
        {
            if (!initialized) return;
            if (Sources.Count > 0) { Sources.Values.ToList().ForEach(x => x.Stop()); }
            UnityEngine.Object.Destroy(audioParent);
            GameEvents.onGamePause.Remove(onGamePause);
            GameEvents.onGameUnpause.Remove(onGameUnpause);
        }
    }
}
