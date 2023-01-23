using KSP.UI.Screens;
using RocketSoundEnhancement.AudioFilters;
using RocketSoundEnhancement.Unity;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Audio;

namespace RocketSoundEnhancement
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class RocketSoundEnhancement : MonoBehaviour
    {
        public static RocketSoundEnhancement instance = null;
        public static RocketSoundEnhancement Instance { get { return instance; } }

        private static AssetBundle rse_Bundle;
        public static AssetBundle RSE_Bundle
        {
            get
            {
                if (rse_Bundle == null)
                {
                    string path = KSPUtil.ApplicationRootPath + Settings.ModPath + "Plugins/";
                    rse_Bundle = AssetBundle.LoadFromFile(path + "rse_bundle");
                    Debug.Log("[RSE]: AssetBundle loaded");
                }
                return rse_Bundle;
            }
        }

        private static AudioMixer mixer;
        public static AudioMixer Mixer
        {
            get
            {
                if (mixer == null)
                {
                    mixer = RSE_Bundle.LoadAsset("RSE_Mixer") as AudioMixer;
                    Debug.Log("[RSE]: AudioMixer loaded");
                }
                return mixer;
            }
        }

        public static AudioMixerGroup MasterMixer { get { return Mixer.FindMatchingGroups("Master")[0]; } }
        public static AudioMixerGroup FocusMixer { get { return Mixer.FindMatchingGroups("FOCUS")[0]; } }
        public static AudioMixerGroup InteriorMixer { get { return Mixer.FindMatchingGroups("INTERIOR")[0]; } }
        public static AudioMixerGroup ExteriorMixer { get { return Mixer.FindMatchingGroups("EXTERIOR")[0]; } }

        public float MufflingFrequency { get; set; } = 30000;
        public float FocusMufflingFrequency { get; set; } = 30000;
        private float lastCutoffFreq;
        private float lastInteriorCutoffFreq;

        private HashSet<int> managedSources = new HashSet<int>();

        private bool gamePaused;

        private void Awake()
        {
            if (instance != null) { Destroy(instance); }

            instance = this;
        }

        private void Start()
        {
            Settings.Load();
            ShipEffectsConfig.Load();

            ApplySettings();

            FlightCamera.fetch.AudioListenerGameObject.transform.localPosition = Vector3.zero;
            FlightCamera.fetch.AudioListenerGameObject.transform.localEulerAngles = Vector3.zero;

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

            managedSources.Clear();
            foreach (var sourceObj in FindObjectsOfType(typeof(AudioSource)))
            {
                var source = (AudioSource)sourceObj;
                if (source == null) continue;
                int instanceID = source.GetInstanceID();

                if (source.name.Contains(AudioUtility.RSETag))
                {
                    managedSources.Add(instanceID);
                }
                else if (Settings.CustomAudioSources.TryGetValue(source.gameObject.name, out MixerGroup mixerChannel))
                {
                    managedSources.Add(instanceID);

                    source.outputAudioMixerGroup = AudioUtility.GetMixerGroup(mixerChannel);
                }
                else if (source.clip != null && Settings.CustomAudioClips.TryGetValue(source.clip.name, out mixerChannel))
                {
                    managedSources.Add(instanceID);

                    source.outputAudioMixerGroup = AudioUtility.GetMixerGroup(mixerChannel);
                }
                else if (source.name == "FX Sound" || source.name == "airspeedNoise")
                {
                    if (source.clip != null && source.clip.name != "sound_wind_constant")
                        source.mute = ShipEffectsConfig.MuteStockAeroSounds;

                    managedSources.Add(instanceID);
                    source.outputAudioMixerGroup = Settings.EnableAudioEffects ? ExteriorMixer : null;
                }
            }

            UpdateLimiter();
        }

        public void UpdateLimiter()
        {
            if (Mixer == null) return;

            float thrs, gain, atk, rls;
            float amount = Settings.AutoLimiter;
            bool isCustom = Settings.EnableCustomLimiter;
            thrs = isCustom ? Settings.LimiterThreshold : Mathf.Lerp(0, -16, amount);
            gain = isCustom ? Settings.LimiterGain : Mathf.Lerp(0, 16, amount);
            atk = isCustom ? Settings.LimiterAttack : Mathf.Lerp(10, 200, amount);
            rls = isCustom ? Settings.LimiterRelease : Mathf.Lerp(20, 1000, amount);

            Mixer.SetFloat("LimiterThreshold",thrs);
            Mixer.SetFloat("LimiterGain", gain);
            Mixer.SetFloat("LimiterAttack", atk);
            Mixer.SetFloat("LimiterRelease", rls);
        }

        public float WindModulation()
        {
            float windModulationSpeed = 2f;
            float windModulationAmount = 0.1f;
            float windModulation = 1 - windModulationAmount + Mathf.PerlinNoise(Time.time * windModulationSpeed, 0) * windModulationAmount;
            return Mathf.Lerp(1, windModulation, Mathf.Clamp01((float)FlightGlobals.ActiveVessel.atmDensity));
        }

        public void LateUpdate()
        {
            if (gamePaused || !HighLogic.LoadedSceneIsFlight) return;

            if (!Settings.EnableAudioEffects || Mixer == null)
            {
                // TODO: some kind of latch so this only runs once
                foreach (var sourceObj in FindObjectsOfType(typeof(AudioSource)))
                {
                    AudioSource source = (AudioSource)sourceObj;
                    if (source != null) source.outputAudioMixerGroup = null;
                }
                managedSources.Clear();
                
                return;
            }

            // NOTE: the generic (strongly typed) version of FindObjectsOfType is slower!
            foreach (var sourceObj in FindObjectsOfType(typeof(AudioSource)))
            {
                AudioSource source = (AudioSource)sourceObj;

                int instanceID = source.GetInstanceID();

                // handle deleted sources
                if (instanceID == 0)
                {
                    continue;
                }

                // if the source was already in the set, we're done
                if (!managedSources.Add(instanceID)) continue;

                if (source.name.Contains(AudioUtility.RSETag)) continue;

                // assume this source is a GUI source and ignore
                if (source.transform.position == Vector3.zero || source.spatialBlend == 0)
                {
                    continue;
                }

                if (source.GetComponentInParent<InternalProp>())
                {
                    source.outputAudioMixerGroup = InteriorMixer;
                    continue;
                }

                Part sourcePart;
                if (sourcePart = source.GetComponentInParent<Part>())
                {
                    source.outputAudioMixerGroup = sourcePart?.vessel == FlightGlobals.ActiveVessel ? FocusMixer : ExteriorMixer;

                    var partAudioManager = sourcePart.GetComponent<RSE_PartAudioManager>();

                    if (Settings.MufflerQuality > AudioMufflerQuality.Normal && Settings.MachEffectsAmount > 0)
                    {
                        if (partAudioManager == null)
                        {
                            partAudioManager = sourcePart.gameObject.AddComponent<RSE_PartAudioManager>();
                            partAudioManager.Initialize(sourcePart, source);
                        }
                    }
                    else if (partAudioManager != null)
                    {
                        Component.Destroy(partAudioManager);
                    }
                    continue;
                }

                source.outputAudioMixerGroup = ExteriorMixer;
                if (Settings.MufflerQuality == AudioMufflerQuality.AirSim && source.gameObject.GetComponents<AudioSource>().Length == 1)
                {
                    var airSimFilter = source.gameObject.AddOrGetComponent<AirSimulationFilter>();
                    airSimFilter.SetFilterProperties();
                }
            }

            float atmDensityClamped = Mathf.Clamp01((float)FlightGlobals.ActiveVessel.atmDensity);
            float atmCutOff = Mathf.Lerp(Settings.MufflerExternalMode, 30000, atmDensityClamped) * WindModulation();
            float focusOutsideMuffling = Settings.ClampActiveVesselMuffling ? Mathf.Clamp(atmCutOff, Settings.MufflerInternalMode, 30000) : atmCutOff;
            float focusMuffling = InternalCamera.Instance.isActive ? Settings.MufflerInternalMode : focusOutsideMuffling;

            if (MapView.MapCamera.isActiveAndEnabled) focusMuffling = Mathf.Min(Settings.MufflerInternalMode, atmCutOff);

            MufflingFrequency = Mathf.MoveTowards(lastCutoffFreq, Mathf.Min(atmCutOff, focusMuffling), 5000);
            FocusMufflingFrequency = Mathf.MoveTowards(lastInteriorCutoffFreq, focusMuffling, 5000);
            lastCutoffFreq = MufflingFrequency;
            lastInteriorCutoffFreq = FocusMufflingFrequency;

            Mixer.SetFloat("ExteriorCutoff", Mathf.Clamp(MufflingFrequency, 10, 22000));
            Mixer.SetFloat("FocusCutoff", Mathf.Clamp(FocusMufflingFrequency, 10, 22000));

            Mixer.SetFloat("ExteriorVolume", Mathf.Lerp(-80, 0, Mathf.Clamp01(MufflingFrequency / 50)));
            Mixer.SetFloat("FocusVolume", Mathf.Lerp(-80, 0, Mathf.Clamp01(FocusMufflingFrequency / 50)));
        }

        private void OnDestroy()
        {
            AudioListener.volume = 0;   // Temp fix for sound stuttering between scene changes

            GameEvents.onGamePause.Remove(() => gamePaused = true);
            GameEvents.onGameUnpause.Remove(() => gamePaused = false);

            instance = null;
        }
    }

    class RSE_PartAudioManager : MonoBehaviour
    {
        Part part;
        ShipEffects shipEffects;
        AudioSource source;
        float managedMinDistance;

        public void Initialize(Part part, AudioSource source)
        {
            this.part = part;
            this.source = source;
            managedMinDistance = source.minDistance;
        }

        void LateUpdate()
        {
            // the vessel object may change on decoupling etc so we can't just cache the shipEffects object
            part.vessel.GetComponentCached(ref shipEffects);

            float machPass = shipEffects.MachPass;
            source.minDistance = managedMinDistance * machPass;
        }
    }
}
