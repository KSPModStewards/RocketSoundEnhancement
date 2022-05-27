using KSP.UI.Screens;
using RocketSoundEnhancement.AudioFilters;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Audio;

namespace RocketSoundEnhancement
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class RocketSoundEnhancement : MonoBehaviour
    {
        public static RocketSoundEnhancement _instance = null;
        public static RocketSoundEnhancement Instance { get { return _instance; } }

        public AerodynamicsFX AeroFX;

        private AudioMixer mixer;
        public AudioMixer Mixer
        {
            get
            {
                if (mixer == null)
                {
                    mixer = Startup.RSE_Bundle.LoadAsset("RSE_Mixer") as AudioMixer;
                }
                return mixer;
            }
        }

        public AudioMixerGroup MasterMixer { get { return Mixer.FindMatchingGroups("Master")[0]; } }
        public AudioMixerGroup FocusMixer { get { return Mixer.FindMatchingGroups("FOCUS")[0]; } }
        public AudioMixerGroup InternalMixer { get { return Mixer.FindMatchingGroups("INTERNAL")[0]; } }
        public AudioMixerGroup ExternalMixer { get { return Mixer.FindMatchingGroups("EXTERNAL")[0]; } }

        public LowpassFilter LowpassFilter;
        public AudioLimiter AudioLimiter;
        public float MufflingFrequency { get; set; } = 22200;
        public float FocusMufflingFrequency { get; set; } = 22200;
        public bool overrideFiltering;
        float lastCutoffFreq;
        float lastInteriorCutoffFreq;

        HashSet<AudioSource> managedSources = new HashSet<AudioSource>();
        Dictionary<int, float> managedMinDistance = new Dictionary<int, float>();

        List<AudioSource> preservedSources = new List<AudioSource>();
        List<AudioSource> chattererSources = new List<AudioSource>();
        List<AudioSource> otherSources = new List<AudioSource>();

        string[] chatterer_PlayerNames = {
            "rbr_chatter_player",
            "rbr_sstv_player",
            "rbr_background_player_",
            "rbr_beep_player_"
        };

        string[] aae_chatterer_clipNames = {
            "breathing",
            "airlock"
        };

        string[] preservedSourceNames = {
            "MusicLogic",
            "SoundtrackEditor",
            "PartActionController(Clone)"
        };

        private bool gamePaused;

        void Awake()
        {
            _instance = this;
        }

        void Start()
        {
            Settings.Instance.Load();

            AeroFX = GameObject.FindObjectOfType<AerodynamicsFX>();

            HashSet<AudioSource> audioSources = GameObject.FindObjectsOfType<AudioSource>().ToHashSet();
            foreach (var source in audioSources)
            {
                if (source == null) continue;
                if (source.name.Contains(AudioUtility.RSETag)) continue;

                if (preservedSourceNames.Any(source.gameObject.name.Contains))
                {
                    preservedSources.Add(source);
                    continue;
                }

                if (chatterer_PlayerNames.Any(source.gameObject.name.Contains))
                {
                    chattererSources.Add(source);
                    continue;
                }

                if (source.clip != null && aae_chatterer_clipNames.Any(source.clip.name.Contains))
                {
                    chattererSources.Add(source);
                    continue;
                }

                if ((source.name == "FX Sound" || source.name == "airspeedNoise"))
                    otherSources.Add(source);
            }

            AudioListener listenerDummy = gameObject.AddOrGetComponent<AudioListener>();
            listenerDummy.enabled = false;
            LowpassFilter = gameObject.AddOrGetComponent<LowpassFilter>();
            AudioLimiter = gameObject.AddOrGetComponent<AudioLimiter>();

            ApplySettings();

            GameEvents.onGamePause.Add(() => gamePaused = true);
            GameEvents.onGameUnpause.Add(() => gamePaused = false);
            GameEvents.onPartDeCoupleNewVesselComplete.Add(onNewVessel);
        }

        public void ApplySettings()
        {
            LowpassFilter.enabled = AudioMuffler.EnableMuffling && AudioMuffler.MufflerQuality == AudioMufflerQuality.Lite;
            AudioLimiter.enabled = AudioLimiter.EnableLimiter;

            bool fullMuffling = AudioMuffler.EnableMuffling && AudioMuffler.MufflerQuality > AudioMufflerQuality.Lite;
            bool liteMuffling = AudioMuffler.EnableMuffling && AudioMuffler.MufflerQuality == AudioMufflerQuality.Lite;

            var stageSource = StageManager.Instance.GetComponent<AudioSource>();
            if (stageSource)
            {
                stageSource.enabled = !Settings.Instance.DisableStagingSound;
                if (AudioMuffler.EnableMuffling)
                {
                    stageSource.outputAudioMixerGroup = fullMuffling ? InternalMixer : null;
                    stageSource.bypassListenerEffects = liteMuffling;
                }
            }

            foreach (var preservedSources in preservedSources)
            {
                preservedSources.bypassListenerEffects = AudioMuffler.EnableMuffling;
            }

            foreach (var chattererSource in chattererSources)
            {
                if (chattererSource.clip != null && aae_chatterer_clipNames.Any(chattererSource.clip.name.Contains))
                {
                    bool isBreathSounds = chattererSource.clip.name.ToLower().Contains("breathing");
                    chattererSource.outputAudioMixerGroup = fullMuffling ? (isBreathSounds ? InternalMixer : FocusMixer) : null;
                    chattererSource.bypassListenerEffects = AudioMuffler.EnableMuffling && AudioMuffler.MufflerQuality == AudioMufflerQuality.Lite;
                    continue;
                }

                chattererSource.bypassListenerEffects = !AudioMuffler.AffectChatterer;
            }

            foreach (var source in otherSources)
            {
                if (source == null) continue;

                if (source.transform.position == Vector3.zero)
                {
                    source.bypassListenerEffects = true;
                }

                if (source.clip != null && source.clip.name != "sound_wind_constant")
                {
                    source.mute = Settings.Instance.MuteStockAeroSounds;
                }

                source.outputAudioMixerGroup = fullMuffling ? ExternalMixer : null;
            }
        }

        void onNewVessel(Vessel vessel1, Vessel vessel2)
        {
            var se1 = vessel1.GetComponent<ShipEffects>();
            var se2 = vessel2.GetComponent<ShipEffects>();

            if (vessel1.launchTime > vessel2.launchTime)
            {
                se1.SonicBoomTip = se2.SonicBoomTip;
                se1.SonicBoomedRear = se2.SonicBoomedRear;
            }
            else
            {
                se2.SonicBoomTip = se1.SonicBoomTip;
                se2.SonicBoomedRear = se1.SonicBoomedRear;
            }
        }

        public float WindModulation()
        {
            float windModulationSpeed = 2f;
            float windModulationAmount = 0.1f;
            float windModulation = 1 - windModulationAmount + Mathf.PerlinNoise(Time.time * windModulationSpeed, 0) * windModulationAmount;
            return Mathf.Lerp(1, windModulation, Mathf.Clamp01((float)FlightGlobals.ActiveVessel.atmDensity));
        }

        void LateUpdate()
        {
            if (gamePaused) return;

            if (!AudioMuffler.EnableMuffling)
            {
                if (managedSources.Count > 0)
                {
                    foreach (var source in managedSources.ToHashSet())
                    {
                        if (source != null) source.outputAudioMixerGroup = null;
                        managedSources.Remove(source);
                    }
                }
                if (managedMinDistance.Count > 0)
                {
                    managedMinDistance.Clear();
                }
                return;
            }

            HashSet<AudioSource> audioSources = GameObject.FindObjectsOfType<AudioSource>().Where(x => !x.name.Contains(AudioUtility.RSETag)).ToHashSet();
            foreach (AudioSource source in audioSources)
            {
                if (source == null)
                {
                    if (managedSources.Contains(source))
                    {
                        managedSources.Remove(source);
                        managedMinDistance.Remove(source.GetInstanceID());
                    }
                    continue;
                }

                if (preservedSourceNames.Any(source.gameObject.name.Contains)
                || chatterer_PlayerNames.Any(source.gameObject.name.Contains)
                || chattererSources.Contains(source))
                    continue;

                int managedSourceID = source.GetInstanceID();

                if (source.GetComponent<InternalProp>() || source.GetComponentInParent<InternalProp>())
                {
                    source.outputAudioMixerGroup = AudioMuffler.MufflerQuality > AudioMufflerQuality.Lite ? InternalMixer : null;
                    source.bypassListenerEffects = AudioMuffler.MufflerQuality == AudioMufflerQuality.Lite;

                    if (!managedSources.Contains(source))
                    {
                        managedSources.Add(source);
                    }
                    continue;
                }

                if (AudioMuffler.MufflerQuality == AudioMufflerQuality.Lite)
                {
                    if (managedSources.Contains(source))
                    {
                        source.outputAudioMixerGroup = null;
                        managedSources.Remove(source);
                    }

                    continue;
                }

                if (!managedSources.Contains(source)) { managedSources.Add(source); }

                Part sourcePart;
                if ((sourcePart = source.GetComponent<Part>()) || (sourcePart = source.GetComponentInParent<Part>()))
                {
                    source.outputAudioMixerGroup = sourcePart?.vessel == FlightGlobals.ActiveVessel ? FocusMixer : ExternalMixer;

                    if (AudioMuffler.MufflerQuality == AudioMufflerQuality.AirSim)
                    {
                        if (!managedMinDistance.ContainsKey(managedSourceID))
                            managedMinDistance.Add(managedSourceID, source.minDistance);

                        float machPass = sourcePart.vessel.GetComponent<ShipEffects>().MachPass;
                        float sourceDistance = Vector3.Distance(CameraManager.GetCurrentCamera().transform.position, source.transform.position);
                        float distanceAttenuation = Mathf.Max(Mathf.Pow(1 - Mathf.Clamp01(sourceDistance / AudioMuffler.AirSimMaxDistance), 10), 0.1f) * machPass;
                        source.minDistance = managedMinDistance[managedSourceID] * distanceAttenuation;

                        continue;
                    }

                    if (managedMinDistance.Count > 0 && managedMinDistance.ContainsKey(managedSourceID))
                    {
                        source.minDistance = managedMinDistance[managedSourceID];
                        managedMinDistance.Remove(managedSourceID);
                    }

                    continue;
                }

                source.outputAudioMixerGroup = ExternalMixer;

                if (AudioMuffler.MufflerQuality == AudioMufflerQuality.AirSim && source.name.StartsWith("Explosion"))
                {
                    var airSim = source.gameObject.AddOrGetComponent<AirSimulationFilter>();
                    airSim.enabled = true;
                    airSim.EnableLowpassFilter = true;
                    airSim.SimulationUpdate = AirSimulationUpdate.Basic;
                    airSim.Distance = Vector3.Distance(CameraManager.GetCurrentCamera().transform.position, source.transform.position);
                }
            }

            float atmDensityClamped = Mathf.Clamp01((float)FlightGlobals.ActiveVessel.atmDensity);

            if (AudioMuffler.MufflerQuality > AudioMufflerQuality.Lite && Mixer != null)
            {
                if (!overrideFiltering)
                {
                    float atmCutOff = Mathf.Lerp(AudioMuffler.ExteriorMuffling, 22200, atmDensityClamped) * WindModulation();
                    float focusMuffling = InternalCamera.Instance.isActive ? AudioMuffler.InteriorMuffling : atmCutOff;

                    if (MapView.MapCamera.isActiveAndEnabled)
                    {
                        focusMuffling = Mathf.Min(AudioMuffler.InteriorMuffling, atmCutOff);
                    }

                    MufflingFrequency = Mathf.MoveTowards(lastCutoffFreq, Mathf.Min(atmCutOff, focusMuffling), 5000);
                    FocusMufflingFrequency = Mathf.MoveTowards(lastInteriorCutoffFreq, focusMuffling, 5000);
                    lastCutoffFreq = MufflingFrequency;
                    lastInteriorCutoffFreq = FocusMufflingFrequency;
                }
                else
                {
                    FocusMufflingFrequency = MufflingFrequency;
                }

                Mixer.SetFloat("ExternalCutoff", Mathf.Clamp(MufflingFrequency, 10, 22000));
                Mixer.SetFloat("FocusCutoff", Mathf.Clamp(FocusMufflingFrequency, 10, 22000));

                Mixer.SetFloat("ExternalVolume", Mathf.Lerp(-80, 0, Mathf.Clamp01(MufflingFrequency / 50)));
                Mixer.SetFloat("FocusVolume", Mathf.Lerp(-80, 0, Mathf.Clamp01(FocusMufflingFrequency / 50)));

                return;
            }

            if (AudioMuffler.MufflerQuality == AudioMufflerQuality.Lite && LowpassFilter != null && LowpassFilter.enabled && !overrideFiltering)
            {
                float interiorMuffling = AudioMuffler.InteriorMuffling;
                float exteriorMuffling = Mathf.Lerp(AudioMuffler.ExteriorMuffling, 22200, atmDensityClamped);

                float targetFrequency = InternalCamera.Instance.isActive ? interiorMuffling : exteriorMuffling;
                if (MapView.MapCamera.isActiveAndEnabled)
                {
                    targetFrequency = interiorMuffling < exteriorMuffling ? interiorMuffling : exteriorMuffling;
                }

                float smoothCutoff = Mathf.MoveTowards(lastCutoffFreq, targetFrequency, 5000);
                lastCutoffFreq = smoothCutoff;
                LowpassFilter.CutoffFrequency = smoothCutoff;
            }
        }

        void OnDestroy()
        {
            GameEvents.onGamePause.Remove(() => gamePaused = true);
            GameEvents.onGameUnpause.Remove(() => gamePaused = false);
            GameEvents.onPartDeCoupleNewVesselComplete.Remove(onNewVessel);
        }
    }
}
