using System;
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
        float machPass;
        float lastDistance;
        float loopRandomStart;

        public ConfigNode configNode;
        public bool getSoundLayersandGroups = true;
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

            if (getSoundLayersandGroups)
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
            }

            loopRandomStart = UnityEngine.Random.Range(0f,1.0f);

            GameEvents.onGamePause.Add(onGamePause);
            GameEvents.onGameUnpause.Add(onGameUnpause);
        }

        public override void OnUpdate()
        {
            if (!initialized || Sources.Count == 0)
                return;

            var sourceKeys = Sources.Keys.ToList();
            foreach (var source in sourceKeys)
            {
                if (AirSimFilters.ContainsKey(source) && AudioMuffler.MufflerQuality != AudioMufflerQuality.AirSim)
                {
                    UnityEngine.Object.Destroy(AirSimFilters[source]);
                    AirSimFilters.Remove(source);
                }

                if (Sources[source].isPlaying)
                    continue;

                UnityEngine.Object.Destroy(Sources[source].gameObject);

                if (AirSimFilters.ContainsKey(source)) { AirSimFilters.Remove(source); }
                Sources.Remove(source);
                Controls.Remove(source);
            }
        }

        public virtual void FixedUpdate()
        {
            if (!initialized)
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

                angle = (1 + Vector3.Dot(vessel.GetComponent<ShipEffects>().MachOriginCameraNormal, (transform.up + vessel.velocityD).normalized)) * 90;
                machPass = 1f - Mathf.Clamp01(angle / vessel.GetComponent<ShipEffects>().MachAngle) * Mathf.Clamp01(vessel.GetComponent<ShipEffects>().Mach);
                
                if (vessel.isActiveVessel && (InternalCamera.Instance.isActive || MapView.MapCamera.isActiveAndEnabled))
                {
                    angle = 0;
                    machPass = 1;
                }
            }
        }

        public void PlaySoundLayer(string sourceLayerName, SoundLayer soundLayer, float controlInput, float volume, bool oneShot = false, bool rndOneShotVol = false)
        {
            float control = Mathf.Round(controlInput * 1000.0f) * 0.001f;
            float finalVolume, finalPitch;

            finalVolume = soundLayer.volumeFC != null ? soundLayer.volumeFC.Evaluate(control) : soundLayer.volume.Value(control);
            finalPitch = soundLayer.pitchFC != null ? soundLayer.pitchFC.Evaluate(control) : soundLayer.pitch.Value(control);

            finalVolume *= soundLayer.massToVolume?.Value((float)part.physicsMass) ?? 1;
            finalPitch *= soundLayer.massToPitch?.Value((float)part.physicsMass) ?? 1;
            finalPitch *= AudioMuffler.MufflerQuality > AudioMufflerQuality.Lite ? doppler : 1;

            if(control < float.Epsilon) {
                if(Sources.ContainsKey(sourceLayerName)) {
                    Sources[sourceLayerName].Stop();
                }
                return;
            }

            AudioSource source = Sources.ContainsKey(sourceLayerName) ? Sources[sourceLayerName] : null;
            if (!source)
            {
                var sourceGameObject = new GameObject(sourceLayerName);
                sourceGameObject.transform.parent = audioParent.transform;
                sourceGameObject.transform.position = audioParent.transform.position;
                sourceGameObject.transform.rotation = audioParent.transform.rotation;

                source = AudioUtility.CreateSource(sourceGameObject, soundLayer);
                Sources.Add(sourceLayerName, source);

                source.time = soundLayer.loopAtRandom ? loopRandomStart * source.clip.length : 0;
            }

            if(AudioMuffler.EnableMuffling && AudioMuffler.MufflerQuality == AudioMufflerQuality.AirSim && soundLayer.channel == FXChannel.Exterior) {
                if(UseAirSimulation){
                    ProcessAirSimulation(sourceLayerName, source);
                } else {
                    float machPassLog = Mathf.Log(Mathf.Lerp(1, 10, machPass), 10);
                    finalVolume *= Mathf.Min(machPassLog, Mathf.Pow(1 - Mathf.Clamp01(distance / MaxDistance), 2) * 0.5f);
                }
            }

            source.volume = finalVolume * GameSettings.SHIP_VOLUME * volume;
            source.pitch = finalPitch;

            float volumeScale = rndOneShotVol && oneShot ? UnityEngine.Random.Range(0.9f, 1.0f) : 1;
            int index = UnityEngine.Random.Range(0, soundLayer.audioClips.Length);
            var clip = GameDatabase.Instance.GetAudioClip(soundLayer.audioClips[index]);

            AudioUtility.PlayAtChannel(source, soundLayer.channel, vessel == FlightGlobals.ActiveVessel, soundLayer.loop, oneShot, volumeScale, clip);
        }

        public void ProcessAirSimulation(string sourceLayerName, AudioSource source)
        {
            AirSimulationFilter airSimFilter = AirSimFilters.ContainsKey(sourceLayerName) ? AirSimFilters[sourceLayerName] : null;
            if (!airSimFilter)
            {
                airSimFilter = new AirSimulationFilter();
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
            }

            airSimFilter.Distance = distance;
            airSimFilter.Mach = vessel.GetComponent<ShipEffects>().Mach;
            airSimFilter.Angle = angle;
            airSimFilter.MachAngle = vessel.GetComponent<ShipEffects>().MachAngle;
            airSimFilter.MachPass = machPass;
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

        public void OnDestroy()
        {
            if (!initialized)
                return;

            if(Sources.Count > 0) {
                foreach(var source in Sources.Values) {
                    source.Stop();
                }
            }

            UnityEngine.Object.Destroy(audioParent);

            GameEvents.onGamePause.Remove(onGamePause);
            GameEvents.onGameUnpause.Remove(onGameUnpause);
        }
    }
}
