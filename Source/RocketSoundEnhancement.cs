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
        public AudioMixerGroup InteriorMixer { get { return Mixer.FindMatchingGroups("INTERIOR")[0]; } }
        public AudioMixerGroup ExteriorMixer { get { return Mixer.FindMatchingGroups("EXTERIOR")[0]; } }

        public float MufflingFrequency { get; set; } = 22200;
        public float FocusMufflingFrequency { get; set; } = 22200;
        public bool overrideFiltering;
        float lastCutoffFreq;
        float lastInteriorCutoffFreq;

        HashSet<AudioSource> unManagedSources = new HashSet<AudioSource>();
        HashSet<AudioSource> managedSources = new HashSet<AudioSource>();
        Dictionary<int, float> managedMinDistance = new Dictionary<int, float>();

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
            Settings.Load();

            AeroFX = GameObject.FindObjectOfType<AerodynamicsFX>();

            ApplySettings();

            GameEvents.onGamePause.Add(() => gamePaused = true);
            GameEvents.onGameUnpause.Add(() => gamePaused = false);
        }

        public void ApplySettings()
        {
            var stageSource = StageManager.Instance.GetComponent<AudioSource>();
            if (stageSource)
            {
                stageSource.enabled = !Settings.DisableStagingSound;
            }

            unManagedSources.Clear();
            HashSet<AudioSource> audioSources = GameObject.FindObjectsOfType<AudioSource>().ToHashSet();
            foreach (var source in audioSources)
            {
                if (source == null) continue;
                if (source.name.Contains(AudioUtility.RSETag)) continue;

                if (Settings.CustomAudioSources.Count > 0 && Settings.CustomAudioSources.ContainsKey(source.gameObject.name))
                {
                    if (!unManagedSources.Contains(source)) unManagedSources.Add(source);

                    var mixerChannel = Settings.CustomAudioSources[source.gameObject.name];
                    source.outputAudioMixerGroup = AudioUtility.GetMixerGroup(mixerChannel);
                    continue;
                }

                if (Settings.CustomAudioClips.Count > 0 && source.clip != null && Settings.CustomAudioClips.ContainsKey(source.clip.name))
                {
                    if (!unManagedSources.Contains(source)) unManagedSources.Add(source);

                    var mixerChannel = Settings.CustomAudioClips[source.clip.name];
                    source.outputAudioMixerGroup = AudioUtility.GetMixerGroup(mixerChannel);
                    continue;
                }

                if (source.name == "FX Sound" || source.name == "airspeedNoise")
                {
                    if (source.clip != null && source.clip.name != "sound_wind_constant")
                        source.mute = Settings.MuteStockAeroSounds;

                    source.outputAudioMixerGroup = Settings.AudioEffectsEnabled ? ExteriorMixer : null;
                }
            }

            UpdateLimiter();
        }

        public void UpdateLimiter()
        {
            if (Mixer == null) return;

            float thrs, gain, atk, rls;
            float amount = Settings.Limiter.AutoLimiter;
            bool isCustom = Settings.Limiter.Custom;
            thrs = isCustom ? Settings.Limiter.Threshold : Mathf.Lerp(0, -16, amount);
            gain = isCustom ? Settings.Limiter.Gain : Mathf.Lerp(0, 16, amount);
            atk = isCustom ? Settings.Limiter.Attack : Mathf.Lerp(10, 200, amount);
            rls = isCustom ? Settings.Limiter.Release : Mathf.Lerp(20, 1000, amount);

            mixer.SetFloat("LimiterThreshold",thrs);
            mixer.SetFloat("LimiterGain", gain);
            mixer.SetFloat("LimiterAttack", atk);
            mixer.SetFloat("LimiterRelease", rls);
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

            if (!Settings.AudioEffectsEnabled || Mixer == null)
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

                if (unManagedSources.Contains(source))
                    continue;

                // assume this source is a GUI source and ignore
                if (source.transform.position == Vector3.zero || source.spatialBlend == 0) continue;

                if (source.GetComponent<InternalProp>() || source.GetComponentInParent<InternalProp>())
                {
                    source.outputAudioMixerGroup = InteriorMixer;
                    if (!managedSources.Contains(source))
                    {
                        managedSources.Add(source);
                    }
                    continue;
                }

                if (!managedSources.Contains(source)) { managedSources.Add(source); }

                int managedSourceID = source.GetInstanceID();
                Part sourcePart;
                if ((sourcePart = source.GetComponent<Part>()) || (sourcePart = source.GetComponentInParent<Part>()))
                {
                    source.outputAudioMixerGroup = sourcePart?.vessel == FlightGlobals.ActiveVessel ? FocusMixer : ExteriorMixer;

                    if (Settings.MufflerQuality == AudioMufflerQuality.AirSim)
                    {
                        if (source.gameObject.GetComponents<AudioSource>().Length > 1)
                        {
                            if (!managedMinDistance.ContainsKey(managedSourceID))
                                managedMinDistance.Add(managedSourceID, source.minDistance);

                            float machPass = sourcePart.vessel.GetComponent<ShipEffects>().MachPass;
                            float sourceDistance = Vector3.Distance(CameraManager.GetCurrentCamera().transform.position, source.transform.position);
                            float distanceAttenuation = Mathf.Max(Mathf.Pow(1 - Mathf.Clamp01(sourceDistance / Settings.AirSimMaxDistance), 10), 0.1f) * machPass;
                            source.minDistance = managedMinDistance[managedSourceID] * distanceAttenuation;
                            continue;
                        }
                    }

                    if (managedMinDistance.Count > 0 && managedMinDistance.ContainsKey(managedSourceID))
                    {
                        source.minDistance = managedMinDistance[managedSourceID];
                        managedMinDistance.Remove(managedSourceID);
                    }

                    if (source.GetComponent<AirSimulationFilter>()) UnityEngine.Object.Destroy(source.GetComponent<AirSimulationFilter>());

                    continue;
                }

                source.outputAudioMixerGroup = ExteriorMixer;
                if (Settings.MufflerQuality == AudioMufflerQuality.AirSim && source.name.StartsWith("Explosion"))
                {
                    var airSimFilter = source.gameObject.AddOrGetComponent<AirSimulationFilter>();
                    airSimFilter.enabled = true;
                    airSimFilter.EnableLowpassFilter = true;
                    airSimFilter.SimulationUpdate = AirSimulationUpdate.Basic;
                    airSimFilter.Distance = Vector3.Distance(CameraManager.GetCurrentCamera().transform.position, source.transform.position);
                }
            }

            float atmDensityClamped = Mathf.Clamp01((float)FlightGlobals.ActiveVessel.atmDensity);
            Mixer.SetFloat("ExteriorCutoff", Mathf.Clamp(MufflingFrequency, 10, 22000));
            Mixer.SetFloat("FocusCutoff", Mathf.Clamp(FocusMufflingFrequency, 10, 22000));

            Mixer.SetFloat("ExteriorVolume", Mathf.Lerp(-80, 0, Mathf.Clamp01(MufflingFrequency / 50)));
            Mixer.SetFloat("FocusVolume", Mathf.Lerp(-80, 0, Mathf.Clamp01(FocusMufflingFrequency / 50)));

            if (overrideFiltering) { FocusMufflingFrequency = MufflingFrequency; return; }

            float atmCutOff = Mathf.Lerp(Settings.MufflerPreset.ExteriorMuffling, 22200, atmDensityClamped) * WindModulation();
            float focusMuffling = InternalCamera.Instance.isActive ? Settings.MufflerPreset.InteriorMuffling : atmCutOff;

            if (MapView.MapCamera.isActiveAndEnabled) focusMuffling = Mathf.Min(Settings.MufflerPreset.InteriorMuffling, atmCutOff);

            MufflingFrequency = Mathf.MoveTowards(lastCutoffFreq, Mathf.Min(atmCutOff, focusMuffling), 5000);
            FocusMufflingFrequency = Mathf.MoveTowards(lastInteriorCutoffFreq, focusMuffling, 5000);
            lastCutoffFreq = MufflingFrequency;
            lastInteriorCutoffFreq = FocusMufflingFrequency;
        }

        void OnDestroy()
        {
            GameEvents.onGamePause.Remove(() => gamePaused = true);
            GameEvents.onGameUnpause.Remove(() => gamePaused = false);
        }
    }
}
