using KSP.UI.Screens;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Profiling;

namespace RocketSoundEnhancement
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class RSE : MonoBehaviour
    {
        public static RSE _instance = null;
        public static RSE Instance { get { return _instance; } }

        public ApplicationLauncherButton AppButton;
        public Texture AppIcon = GameDatabase.Instance.GetTexture("RocketSoundEnhancement/Textures/RSE_Icon", false);
        public Texture AppTitle = GameDatabase.Instance.GetTexture("RocketSoundEnhancement/Textures/RSE_Title", false);

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
        bool overrideFiltering;
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

        bool gamePaused;

        const string downArrowUNI = "\u25BC";
        const string upArrowUNI = "\u25B2";

        Rect settingsRect;
        Vector2 settingsScrollPos;
        int windowWidth = 440;
        int windowHeight = 244;
        int leftWidth = 200;
        int rightWidth = 60;
        int smlRightWidth = 40;
        int smlLeftWidth = 125;

        Rect shipEffectsRect;
        Vector2 shipEffectsScrollPos;
        bool _shipEffectsWindowToggle;
        bool _settingsWindowToggle;
        bool showAdvanceLimiter = false;
        bool showAdvanceLowpass = false;

        void Awake()
        {
            _instance = this;
        }

        void Start()
        {
            Settings.Instance.Load();

            settingsRect = new Rect(Screen.width - windowWidth - 40, 40, windowWidth, windowHeight);
            shipEffectsRect = new Rect(settingsRect.x - 500 - 40, settingsRect.y, 500, windowHeight * 2);

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

            if (AppButton == null)
            {
                AppButton = ApplicationLauncher.Instance.AddModApplication(
                    () => _settingsWindowToggle = true,
                    () => _settingsWindowToggle = false,
                    null, null,
                    null, null,
                    ApplicationLauncher.AppScenes.FLIGHT, AppIcon
                );
            }

            GameEvents.onGamePause.Add(() => gamePaused = true);
            GameEvents.onGameUnpause.Add(() => gamePaused = false);
            GameEvents.onPartDeCoupleNewVesselComplete.Add(onNewVessel);
        }

        void ApplySettings()
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

        void OnGUI()
        {
            if (_settingsWindowToggle)
            {
                settingsRect = GUILayout.Window(52534500, settingsRect, settingsWindow, "");
            }
            if (_shipEffectsWindowToggle)
            {
                shipEffectsRect = GUILayout.Window(52534501, shipEffectsRect, shipEffectsWindow, "ShipEffects Info");
            }
        }

        void settingsWindow(int id)
        {
            GUILayout.BeginVertical();
            GUILayout.Label(AppTitle);
            settingsScrollPos = GUILayout.BeginScrollView(settingsScrollPos, false, true, GUILayout.Height(windowHeight));
            GUILayout.BeginHorizontal();
            AudioLimiter.EnableLimiter = GUILayout.Toggle(AudioLimiter.EnableLimiter, "Sound Effects Mastering", GUILayout.Width(leftWidth));
            AudioLimiter.enabled = AudioLimiter.EnableLimiter;

            #region AUDIOLIMITER SETTINGS
            if (AudioLimiter.EnableLimiter)
            {
                if (GUILayout.Button(AudioLimiter.Preset))
                {
                    int limiterPresetIndex = AudioLimiter.Presets.Keys.ToList().IndexOf(AudioLimiter.Preset) + 1;

                    if (limiterPresetIndex >= AudioLimiter.Presets.Count)
                    {
                        limiterPresetIndex = 0;
                    }

                    AudioLimiter.Preset = AudioLimiter.Presets.Keys.ToList()[limiterPresetIndex];
                    AudioLimiter.ApplyPreset();
                }

                if (GUILayout.Button(showAdvanceLimiter ? upArrowUNI : downArrowUNI, GUILayout.Width(smlRightWidth)))
                {
                    showAdvanceLimiter = !showAdvanceLimiter;
                }
                GUILayout.EndHorizontal();
                if (showAdvanceLimiter)
                {
                    if (AudioLimiter.Preset == "Custom")
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Threshold", GUILayout.Width(smlLeftWidth));
                        AudioLimiter.Threshold = (float)Math.Round(GUILayout.HorizontalSlider(AudioLimiter.Threshold, -60f, 0f), 2);
                        GUILayout.Label(AudioLimiter.Threshold.ToString() + "db", GUILayout.Width(rightWidth));
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Bias", GUILayout.Width(smlLeftWidth));
                        AudioLimiter.Bias = (float)Math.Round(GUILayout.HorizontalSlider(AudioLimiter.Bias, 0.1f, 100f), 2);
                        GUILayout.Label(AudioLimiter.Bias.ToString(), GUILayout.Width(rightWidth));
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Ratio", GUILayout.Width(smlLeftWidth));
                        AudioLimiter.Ratio = (float)Math.Round(GUILayout.HorizontalSlider(AudioLimiter.Ratio, 1f, 20f), 2);
                        GUILayout.Label(AudioLimiter.Ratio.ToString(), GUILayout.Width(rightWidth));
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Gain", GUILayout.Width(smlLeftWidth));
                        AudioLimiter.Gain = (float)Math.Round(GUILayout.HorizontalSlider(AudioLimiter.Gain, -30f, 30f), 2);
                        GUILayout.Label(AudioLimiter.Gain.ToString() + "db", GUILayout.Width(rightWidth));
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Time Constant", GUILayout.Width(smlLeftWidth));
                        AudioLimiter.TimeConstant = Mathf.RoundToInt(GUILayout.HorizontalSlider(AudioLimiter.TimeConstant, 1, 6));
                        GUILayout.Label(AudioLimiter.TimeConstant.ToString(), GUILayout.Width(rightWidth));
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        GUILayout.Label("RMS Window", GUILayout.Width(smlLeftWidth));
                        AudioLimiter.LevelDetectorRMSWindow = Mathf.RoundToInt(GUILayout.HorizontalSlider(AudioLimiter.LevelDetectorRMSWindow, 1, 1000));
                        GUILayout.Label(AudioLimiter.LevelDetectorRMSWindow.ToString() + "ms", GUILayout.Width(rightWidth));
                        GUILayout.EndHorizontal();

                    }
                    GUILayout.BeginHorizontal();
                    if (AudioLimiter.Preset == "Custom")
                    {
                        if (GUILayout.Button("Reset"))
                        {
                            AudioLimiter.Default();
                        }
                    }
                    GUILayout.Box("Compression Ratio: " + AudioLimiter.CurrentCompressionRatio.ToString("0.00"));
                    GUILayout.Box("Gain Reduction: " + AudioLimiter.GainReduction.ToString("0.00") + "db");
                    GUILayout.EndHorizontal();
                }
            }
            else
            {
                GUILayout.EndHorizontal();
            }
            #endregion

            #region AUDIOMUFFLER SETTINGS
            GUILayout.BeginHorizontal();
            AudioMuffler.EnableMuffling = GUILayout.Toggle(AudioMuffler.EnableMuffling, "Audio Muffler", GUILayout.Width(leftWidth));
            if (AudioMuffler.EnableMuffling)
            {
                LowpassFilter.enabled = AudioMuffler.EnableMuffling && AudioMuffler.MufflerQuality == AudioMufflerQuality.Lite;

                if (GUILayout.Button(AudioMuffler.Preset))
                {
                    int lowpassPresetIndex = AudioMuffler.Presets.Keys.ToList().IndexOf(AudioMuffler.Preset) + 1;
                    if (lowpassPresetIndex >= AudioMuffler.Presets.Count)
                    {
                        lowpassPresetIndex = 0;
                    }
                    AudioMuffler.Preset = AudioMuffler.Presets.Keys.ToList()[lowpassPresetIndex];
                    AudioMuffler.ApplyPreset();
                }

                if (GUILayout.Button(showAdvanceLowpass ? upArrowUNI : downArrowUNI, GUILayout.Width(smlRightWidth)))
                {
                    showAdvanceLowpass = !showAdvanceLowpass;
                }

                GUILayout.EndHorizontal();
                if (showAdvanceLowpass)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Muffling Quality", GUILayout.Width(smlLeftWidth));
                    int qualitySlider = ((int)AudioMuffler.MufflerQuality);
                    qualitySlider = Mathf.RoundToInt(GUILayout.HorizontalSlider(qualitySlider, 0, 2));

                    switch (qualitySlider)
                    {
                        case 0:
                            AudioMuffler.MufflerQuality = AudioMufflerQuality.Lite;
                            break;
                        case 1:
                            AudioMuffler.MufflerQuality = AudioMufflerQuality.Full;
                            break;
                        case 2:
                            AudioMuffler.MufflerQuality = AudioMufflerQuality.AirSim;
                            break;
                    }

                    GUILayout.Label(AudioMuffler.MufflerQuality.ToString(), GUILayout.Width(rightWidth));
                    GUILayout.EndHorizontal();

                    if (AudioMuffler.Preset == "Custom")
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Exterior Muffling", GUILayout.Width(smlLeftWidth));
                        AudioMuffler.ExteriorMuffling = (float)Math.Round(GUILayout.HorizontalSlider(AudioMuffler.ExteriorMuffling, 0, 22200), 1);
                        GUILayout.Label(AudioMuffler.ExteriorMuffling.ToString() + "hz", GUILayout.Width(rightWidth));
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Interior Muffling", GUILayout.Width(smlLeftWidth));
                        AudioMuffler.InteriorMuffling = (float)Math.Round(GUILayout.HorizontalSlider(AudioMuffler.InteriorMuffling, 0, 22200), 1);
                        GUILayout.Label(AudioMuffler.InteriorMuffling.ToString() + "hz", GUILayout.Width(rightWidth));
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        if (GUILayout.Button("Reset", GUILayout.Width(smlLeftWidth)))
                        {
                            AudioMuffler.Default();
                        }
                        GUILayout.EndHorizontal();
                    }
                    GUILayout.BeginHorizontal();
                    overrideFiltering = GUILayout.Toggle(overrideFiltering, "Test Muffling", GUILayout.Width(smlLeftWidth));
                    if (LowpassFilter.enabled)
                    {
                        LowpassFilter.CutoffFrequency = GUILayout.HorizontalSlider(LowpassFilter.CutoffFrequency, 0, 22200);
                        GUILayout.Label(LowpassFilter.CutoffFrequency.ToString("0.0") + "hz", GUILayout.Width(rightWidth));
                    }
                    else
                    {
                        MufflingFrequency = GUILayout.HorizontalSlider(MufflingFrequency, 0, 22200);
                        GUILayout.Label(MufflingFrequency.ToString("0.0") + "hz", GUILayout.Width(rightWidth));
                    }
                    GUILayout.EndHorizontal();
                    AudioMuffler.AffectChatterer = GUILayout.Toggle(AudioMuffler.AffectChatterer, "Affect Chatterer");
                }
            }
            else
            {
                GUILayout.EndHorizontal();
            }
            #endregion

            GUILayout.BeginHorizontal();
            GUILayout.Label("Exterior Volume", GUILayout.Width(smlLeftWidth));
            Settings.Instance.ExteriorVolume = GUILayout.HorizontalSlider((float)Math.Round(Settings.Instance.ExteriorVolume, 2), 0, 2);
            GUILayout.Label(Settings.Instance.ExteriorVolume.ToString("0.00"), GUILayout.Width(smlRightWidth));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Interior Volume", GUILayout.Width(smlLeftWidth));
            Settings.Instance.InteriorVolume = GUILayout.HorizontalSlider((float)Math.Round(Settings.Instance.InteriorVolume, 2), 0, 2);
            GUILayout.Label(Settings.Instance.InteriorVolume.ToString("0.00"), GUILayout.Width(smlRightWidth));
            GUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();
            Settings.Instance.DisableStagingSound = GUILayout.Toggle(Settings.Instance.DisableStagingSound, "Disable Staging Sound");

            _shipEffectsWindowToggle = GUILayout.Toggle(_shipEffectsWindowToggle, "ShipEffects Info");
            GUILayout.EndScrollView();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Reload Settings"))
            {
                Settings.Instance.Load();
                ApplySettings();
            }
            if (GUILayout.Button("Save Settings"))
            {
                Settings.Instance.Save();
                ApplySettings();
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        void shipEffectsWindow(int id)
        {
            var vessel = FlightGlobals.ActiveVessel;
            var shipEffectsModule = vessel.GetComponent<ShipEffects>();

            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            GUILayout.Label("Vessel: " + vessel.GetDisplayName());
            GUILayout.Label("Part Count: " + vessel.Parts.Count());
            GUILayout.Label("Vessel Mass: " + shipEffectsModule.VesselMass.ToString("0.0"));

            if (shipEffectsModule != null && shipEffectsModule.initialized)
            {
                string info =
                    "SHIPEFFECTS CONTROLS \r\n" +
                    "- ACCELERATION: " + shipEffectsModule.GetController(PhysicsControl.ACCELERATION).ToString("0.00") + "\r\n" +
                    "- JERK: " + shipEffectsModule.GetController(PhysicsControl.JERK).ToString("0.00") + "\r\n" +
                    "- AIRSPEED: " + shipEffectsModule.GetController(PhysicsControl.AIRSPEED).ToString("0.00") + "\r\n" +
                    "- GROUNDSPEED: " + shipEffectsModule.GetController(PhysicsControl.GROUNDSPEED).ToString("0.00") + "\r\n" +
                    "- DYNAMICPRESSSURE: " + shipEffectsModule.GetController(PhysicsControl.DYNAMICPRESSURE).ToString("0.00") + "\r\n" +
                    "- THRUST: " + shipEffectsModule.GetController(PhysicsControl.THRUST).ToString("0.00") + "\r\n" +
                    "- REENTRYHEAT: " + shipEffectsModule.GetController(PhysicsControl.REENTRYHEAT).ToString("0.00") + "\r\n" +
                    "- Mass: " + shipEffectsModule.VesselMass.ToString("0.00") + "\r\n\r\n";

                if (AudioMuffler.EnableMuffling && AudioMuffler.MufflerQuality == AudioMufflerQuality.AirSim)
                {
                    info +=
                        "AIR SIM PARAMETERS \r\n" +
                        "- Distance: " + shipEffectsModule.Distance.ToString("0.00") + "\r\n" +
                        "- Mach: " + shipEffectsModule.Mach.ToString("0.00") + "\r\n" +
                        "- Angle: " + shipEffectsModule.Angle.ToString("0.00") + "\r\n" +
                        "- MachAngle: " + shipEffectsModule.MachAngle.ToString("0.00") + "\r\n" +
                        "- MachPass: " + shipEffectsModule.MachPass.ToString("0.00") + "\r\n" +
                        "- MachPassRear: " + shipEffectsModule.MachPassRear.ToString("0.00") + "\r\n" +
                        "- SonicBoom1: " + shipEffectsModule.SonicBoomTip + "\r\n" +
                        "- SonicBoom2: " + shipEffectsModule.SonicBoomedRear + "\r\n\r\n";
                }
                GUILayout.Label(info);
            }
            else
            {
                GUILayout.Label("No Active Vessel");
            }
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            if (shipEffectsModule != null && shipEffectsModule.initialized)
            {
                shipEffectsScrollPos = GUILayout.BeginScrollView(shipEffectsScrollPos, GUILayout.Height(windowHeight * 2));
                if (shipEffectsModule.SoundLayerGroups.Count > 0)
                {
                    string layerInfo = String.Empty;
                    layerInfo += "Sources: " + shipEffectsModule.Sources.Count + "\r\n";

                    foreach (var soundLayerGroup in shipEffectsModule.SoundLayerGroups)
                    {
                        layerInfo +=
                            soundLayerGroup.Key.ToString() + "\r\n";

                        foreach (var soundLayer in soundLayerGroup.Value)
                        {
                            string sourceLayerName = soundLayerGroup.Key.ToString() + "_" + soundLayer.name;

                            layerInfo += "SoundLayer: " + soundLayer.name + "\r\n";
                            if (soundLayer.audioClips != null)
                            {
                                layerInfo += "AudioClips: " + soundLayer.audioClips.Length.ToString() + "\r\n";
                            }
                            else
                            {
                                layerInfo += "No AudioClips \r\n";
                            }

                            if (shipEffectsModule.Sources.ContainsKey(sourceLayerName))
                            {
                                var source = shipEffectsModule.Sources[sourceLayerName];
                                if (source != null)
                                {
                                    layerInfo +=
                                        "Volume: " + source.volume + "\r\n" +
                                        "Pitch: " + source.pitch + "\r\n\r\n";
                                }
                            }
                            else
                            {
                                layerInfo += "Source Null or Inactive" + "\r\n\r\n\r\n";
                            }
                        }
                    }
                    GUILayout.Label(layerInfo);
                }
                else
                {
                    GUILayout.Label("No SoundLayers");
                }

                GUILayout.EndScrollView();
            }

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            _shipEffectsWindowToggle = !GUILayout.Button("Close");
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUI.DragWindow(new Rect(0, 0, 10000, 20));
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
            if (AppButton != null)
            {
                ApplicationLauncher.Instance.RemoveModApplication(AppButton);
                AppButton = null;
            }
            GameEvents.onGamePause.Remove(() => gamePaused = true);
            GameEvents.onGameUnpause.Remove(() => gamePaused = false);
            GameEvents.onPartDeCoupleNewVesselComplete.Remove(onNewVessel);
        }
    }
}
