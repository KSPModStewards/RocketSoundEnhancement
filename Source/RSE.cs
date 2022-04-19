using KSP.UI.Screens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Audio;

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

        public AudioListener audioListener;
        public LowpassFilter lowpassFilter;
        public AudioLimiter audioLimiter;

        public AerodynamicsFX AeroFX;

        string[] ChattererPlayerNames = {
            "rbr_chatter_player",
            "rbr_background_player_",
            "rbr_beep_player_",
            "rbr_sstv_player"
        };

        List<AudioSource> ChattererSources = new List<AudioSource>();
        List<AudioSource> StockSources = new List<AudioSource>();
        List<AudioSource> cachSources = new List<AudioSource>();

        bool gamePaused;

        Rect windowRect;
        int windowWidth = 440;
        int windowHeight = 244;
        int leftWidth = 200;
        int rightWidth = 60;
        int smlRightWidth = 40;
        int smlLeftWidth = 125;
        bool showAdvanceLimiter = false;
        bool showAdvanceLowpass = false;
        const string downArrowUNI = "\u25BC";
        const string upArrowUNI = "\u25B2";
        Vector2 settingsScrollPos;

        Rect shipEffectsRect;
        Vector2 shipEffectsScrollPos;
        bool shipEffectsInfo;

        public float MufflingFrequency = 22200;
        public float FocusMufflingFrequency = 22200;
        bool bypassAutomaticFiltering;
        float windModulationAmount = 0.1f;
        float windModulationSpeed = 2f;
        float lastCutoffFreq;
        float lastInteriorCutoffFreq;

        void Awake()
        {
            _instance = this;
        }

        void Start()
        {
            Settings.Instance.Load();

            windowRect = new Rect(Screen.width - windowWidth - 40, 40, windowWidth, windowHeight);
            shipEffectsRect = new Rect(windowRect.x - 250 - 40, windowRect.y, 250, windowHeight * 2);

            AeroFX = GameObject.FindObjectOfType<AerodynamicsFX>();

            foreach(var source in GameObject.FindObjectsOfType<AudioSource>()) {
                if(source == null || source.name.Contains(AudioUtility.RSETag))
                    continue;

                if(source.clip != null)
                    source.bypassListenerEffects = false;

                if(source.name.Contains("Music") || source.name.ToLower().Contains("soundtrackeditor") || source.name.Contains("PartActionController")) {
                    source.bypassListenerEffects = true;
                } else {
                    StockSources.Add(source);
                }
            }

            //Find Chatterer Players
            var chattererObjects = GameObject.FindObjectsOfType<GameObject>().Where(x => x.name.Contains("_player"));
            if(chattererObjects.Count() > 0) {
                foreach(var chatterer in chattererObjects) {
                    if(ChattererPlayerNames.Contains(Regex.Replace(chatterer.name, @"\d", string.Empty))) {
                        foreach(var source in chatterer.GetComponents<AudioSource>()) {
                            if(source == null)
                                continue;
                            ChattererSources.Add(source);

                            if(StockSources.Contains(source)) {
                                StockSources.Remove(source);
                            }
                        }
                    }
                }
            }
            //Find Other Chatterer Sources
            var otherChattererSources = GameObject.FindObjectsOfType<AudioSource>().Where(x => x.clip != null && x.clip.name.ToLower().Contains("chatter"));
            if(otherChattererSources.Count() > 0) {
                foreach(var source in otherChattererSources) {
                    if(!ChattererSources.Contains(source)) {
                        ChattererSources.Add(source);
                    }
                    if(StockSources.Contains(source)) {
                        StockSources.Remove(source);
                    }
                }
            }

            //Attach our master filters to this dummy AudioListener
            //Any Effects applied here also applies to the other listeners in game
            audioListener = gameObject.AddOrGetComponent<AudioListener>();
            audioListener.enabled = false;
            lowpassFilter = gameObject.AddOrGetComponent<LowpassFilter>();
            audioLimiter = gameObject.AddOrGetComponent<AudioLimiter>();

            ApplySettings();
            AddLauncher();

            GameEvents.onGamePause.Add(() => gamePaused = true);
            GameEvents.onGameUnpause.Add(() => gamePaused = false);
        }

        void ApplySettings()
        {
            var stageSource = StageManager.Instance.GetComponent<AudioSource>();
            if(stageSource) {
                if(AudioMuffler.MufflerQuality > AudioMufflerQuality.Lite) {
                    stageSource.outputAudioMixerGroup = InternalMixer;
                } else {
                    stageSource.bypassListenerEffects = true;
                }

                stageSource.enabled = !Settings.Instance.DisableStagingSound;
            }

            lowpassFilter.enabled = AudioMuffler.EnableMuffling && AudioMuffler.MufflerQuality == AudioMufflerQuality.Lite;
            audioLimiter.enabled = AudioLimiter.EnableLimiter;

            if(ChattererSources.Count > 0) {
                foreach(var source in ChattererSources) {
                    if(AudioMuffler.EnableMuffling) {
                        switch(AudioMuffler.MufflerQuality) {
                            case AudioMufflerQuality.Lite:
                                source.outputAudioMixerGroup = null;
                                source.bypassListenerEffects = !AudioMuffler.AffectChatterer;
                                break;
                            case AudioMufflerQuality.Full:
                            case AudioMufflerQuality.AirSim:
                                if(AudioMuffler.AffectChatterer) {
                                    source.outputAudioMixerGroup = InternalCamera.Instance.isActive ? InternalMixer : FocusMixer;
                                } else {
                                    source.outputAudioMixerGroup = InternalMixer;
                                }

                                break;
                        }
                    } else {
                        if(source.outputAudioMixerGroup != null)
                            source.outputAudioMixerGroup = null;
                    }
                }
            }

            foreach(var source in StockSources) {
                if(source == null)
                    continue;

                if(source.name == "FX Sound" || source.name == "airspeedNoise") {
                    if(source.clip != null) {
                        if(source.clip.name != "sound_wind_constant") {
                            source.mute = Settings.Instance.MuteStockAeroSounds;
                        }
                    }
                }
                if(AudioMuffler.EnableMuffling) {
                    switch(AudioMuffler.MufflerQuality) {
                        case AudioMufflerQuality.Lite:
                            if(source.outputAudioMixerGroup != null)
                                source.outputAudioMixerGroup = null;
                            break;
                        case AudioMufflerQuality.Full:
                        case AudioMufflerQuality.AirSim:
                            source.outputAudioMixerGroup = ExternalMixer;
                            break;
                    }
                } else {
                    if(source.outputAudioMixerGroup != null)
                        source.outputAudioMixerGroup = null;
                }
            }
        }

        bool _appToggle;
        private void AddLauncher()
        {
            if(AppButton != null)
                return;

            AppButton = ApplicationLauncher.Instance.AddModApplication(
                    () => _appToggle = true,
                    () => _appToggle = false,
                    null, null,
                    null, null,
                    ApplicationLauncher.AppScenes.FLIGHT, AppIcon
                    );

        }

        void OnGUI()
        {
            if(_appToggle) {
                windowRect = GUILayout.Window(52534500, windowRect, SettingsWindow,"");
            }
            if(shipEffectsInfo) {
                shipEffectsRect = GUILayout.Window(52534501, shipEffectsRect, ShipEffectsInfo, "ShipEffects Info");
            }
        }

        void SettingsWindow(int id)
        {
            GUILayout.BeginVertical();
            GUILayout.Label(AppTitle);
            settingsScrollPos = GUILayout.BeginScrollView(settingsScrollPos,false, true, GUILayout.Height(windowHeight));
            GUILayout.BeginHorizontal();
            AudioLimiter.EnableLimiter = GUILayout.Toggle(AudioLimiter.EnableLimiter, "Sound Effects Mastering", GUILayout.Width(leftWidth));
            audioLimiter.enabled = AudioLimiter.EnableLimiter;

            #region AUDIOLIMITER SETTINGS
            if(AudioLimiter.EnableLimiter) {
                if(GUILayout.Button(AudioLimiter.Preset)) {
                    int limiterPresetIndex = AudioLimiter.Presets.Keys.ToList().IndexOf(AudioLimiter.Preset) + 1;

                    if(limiterPresetIndex >= AudioLimiter.Presets.Count) {
                        limiterPresetIndex = 0;
                    }
                    
                    AudioLimiter.Preset = AudioLimiter.Presets.Keys.ToList()[limiterPresetIndex];
                    AudioLimiter.ApplyPreset();
                }

                if(GUILayout.Button(showAdvanceLimiter ? upArrowUNI : downArrowUNI,GUILayout.Width(smlRightWidth))) {
                    showAdvanceLimiter = !showAdvanceLimiter;
                }
                GUILayout.EndHorizontal();
                if(showAdvanceLimiter) {
                    if(AudioLimiter.Preset == "Custom") {
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Threshold", GUILayout.Width(smlLeftWidth));
                        AudioLimiter.Threshold = (float)Math.Round(GUILayout.HorizontalSlider(AudioLimiter.Threshold, -60f, 0f),2);
                        GUILayout.Label(AudioLimiter.Threshold.ToString() + "db", GUILayout.Width(rightWidth));
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Bias", GUILayout.Width(smlLeftWidth));
                        AudioLimiter.Bias = (float)Math.Round(GUILayout.HorizontalSlider(AudioLimiter.Bias, 0.1f, 100f),2);
                        GUILayout.Label(AudioLimiter.Bias.ToString(), GUILayout.Width(rightWidth));
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Ratio", GUILayout.Width(smlLeftWidth));
                        AudioLimiter.Ratio = (float)Math.Round(GUILayout.HorizontalSlider(AudioLimiter.Ratio, 1f, 20f),2);
                        GUILayout.Label(AudioLimiter.Ratio.ToString(),GUILayout.Width(rightWidth));
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Gain", GUILayout.Width(smlLeftWidth));
                        AudioLimiter.Gain = (float)Math.Round(GUILayout.HorizontalSlider(AudioLimiter.Gain, -30f, 30f),2);
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
                    if(AudioLimiter.Preset == "Custom") {
                        if(GUILayout.Button("Reset")) {
                            AudioLimiter.Default();
                        }
                    }
                    GUILayout.Box("Compression Ratio: " + AudioLimiter.CurrentCompressionRatio.ToString("0.00"));
                    GUILayout.Box("Gain Reduction: " + AudioLimiter.GainReduction.ToString("0.00") + "db");
                    GUILayout.EndHorizontal();
                }
            } else {
                GUILayout.EndHorizontal();
            }
            #endregion

            #region AUDIOMUFFLER SETTINGS
            GUILayout.BeginHorizontal();
            AudioMuffler.EnableMuffling = GUILayout.Toggle(AudioMuffler.EnableMuffling, "Audio Muffler", GUILayout.Width(leftWidth));
            if(AudioMuffler.EnableMuffling) {
                lowpassFilter.enabled = AudioMuffler.EnableMuffling && AudioMuffler.MufflerQuality == AudioMufflerQuality.Lite;

                if(GUILayout.Button(AudioMuffler.Preset)) {
                    int lowpassPresetIndex = AudioMuffler.Presets.Keys.ToList().IndexOf(AudioMuffler.Preset) + 1;
                    if(lowpassPresetIndex >= AudioMuffler.Presets.Count) {
                        lowpassPresetIndex = 0;
                    }
                    AudioMuffler.Preset = AudioMuffler.Presets.Keys.ToList()[lowpassPresetIndex];
                    AudioMuffler.ApplyPreset();
                }

                if(GUILayout.Button(showAdvanceLowpass ? upArrowUNI : downArrowUNI, GUILayout.Width(smlRightWidth))) {
                    showAdvanceLowpass = !showAdvanceLowpass;
                }

                GUILayout.EndHorizontal();
                if(showAdvanceLowpass) {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Muffling Quality", GUILayout.Width(smlLeftWidth));
                    int qualitySlider = ((int)AudioMuffler.MufflerQuality);
                    qualitySlider = Mathf.RoundToInt(GUILayout.HorizontalSlider(qualitySlider, 0, 2));

                    switch(qualitySlider) {
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

                    if(AudioMuffler.Preset == "Custom") {
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Exterior Muffling", GUILayout.Width(smlLeftWidth));
                        AudioMuffler.ExteriorMuffling = (float)Math.Round(GUILayout.HorizontalSlider(AudioMuffler.ExteriorMuffling, 0, 22200),1);
                        GUILayout.Label(AudioMuffler.ExteriorMuffling.ToString() + "hz", GUILayout.Width(rightWidth));
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Interior Muffling", GUILayout.Width(smlLeftWidth));
                        AudioMuffler.InteriorMuffling = (float)Math.Round(GUILayout.HorizontalSlider(AudioMuffler.InteriorMuffling, 0, 22200),1);
                        GUILayout.Label(AudioMuffler.InteriorMuffling.ToString() + "hz", GUILayout.Width(rightWidth));
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        if(GUILayout.Button("Reset",GUILayout.Width(smlLeftWidth))){
                            AudioMuffler.Default();
                        }
                        GUILayout.EndHorizontal();
                    }
                    GUILayout.BeginHorizontal();
                    bypassAutomaticFiltering = GUILayout.Toggle(bypassAutomaticFiltering, "Test Muffling", GUILayout.Width(smlLeftWidth));
                    if(lowpassFilter.enabled) {
                        lowpassFilter.cutoffFrequency = GUILayout.HorizontalSlider(lowpassFilter.cutoffFrequency, 0, 22200);
                        GUILayout.Label(lowpassFilter.cutoffFrequency.ToString("0.0") + "hz", GUILayout.Width(rightWidth));
                    } else {
                        MufflingFrequency = GUILayout.HorizontalSlider(MufflingFrequency, 0, 22200);
                        GUILayout.Label(MufflingFrequency.ToString("0.0") + "hz", GUILayout.Width(rightWidth));
                    }

                    GUILayout.EndHorizontal();
                    AudioMuffler.AffectChatterer = GUILayout.Toggle(AudioMuffler.AffectChatterer, "Affect Chatterer");
                }
            } else {
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

            shipEffectsInfo = GUILayout.Toggle(shipEffectsInfo, "ShipEffects Info");
            GUILayout.EndScrollView();
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("Reload Settings")) {
                Settings.Instance.Load();
                ApplySettings();
            }
            if(GUILayout.Button("Save Settings")) {
                Settings.Instance.Save();
                ApplySettings();
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        void ShipEffectsInfo(int id)
        {
            GUILayout.BeginVertical(GUILayout.Width(250));

            var vessel = FlightGlobals.ActiveVessel;
            var seModule = vessel.GetComponent<ShipEffects>();

            if(seModule != null && seModule.initialized) {
                GUILayout.Label("Vessel: " + vessel.GetDisplayName());
                GUILayout.Label("Vessel Parts: " + vessel.Parts.Count());


                string info =
                    "SHIPEFFECTS CONTROLS \r\n" +
                    "- ACCELERATION: " + seModule.GetController(PhysicsControl.ACCELERATION).ToString("0.00") + "\r\n" +
                    "- JERK: " + seModule.GetController(PhysicsControl.JERK).ToString("0.00") + "\r\n" +
                    "- AIRSPEED: " + seModule.GetController(PhysicsControl.AIRSPEED).ToString("0.00") + "\r\n" +
                    "- GROUNDSPEED: " + seModule.GetController(PhysicsControl.GROUNDSPEED).ToString("0.00") + "\r\n" +
                    "- DYNAMICPRESSSURE: " + seModule.GetController(PhysicsControl.DYNAMICPRESSURE).ToString("0.00") + "\r\n" +
                    "- THRUST: " + seModule.GetController(PhysicsControl.THRUST).ToString("0.00") + "\r\n" +
                    "- REENTRYHEAT: " + seModule.GetController(PhysicsControl.REENTRYHEAT).ToString("0.00") + "\r\n" +
                    "- Mass: " + seModule.VesselMass.ToString("0.00") + "\r\n\r\n";


                if(AudioMuffler.EnableMuffling && AudioMuffler.MufflerQuality == AudioMufflerQuality.AirSim) {
                    info +=
                        "AIR SIM PARAMETERS \r\n" +
                        "- Distance: " + seModule.Distance.ToString("0.00") + "\r\n" +
                        "- DistanceInv: " + seModule.DistanceInv.ToString("0.00") + "\r\n" +
                        "- MachVelocity: " + seModule.Mach.ToString("0.00") + "\r\n" +
                        "- Angle: " + seModule.Angle.ToString("0.00") + "\r\n" +
                        "- MachAngle: " + seModule.MachAngle.ToString("0.00") + "\r\n" +
                        "- MachPass: " + seModule.MachPass.ToString("0.00") + "\r\n" +
                        "- MachPassRear: " + seModule.MachPassRear.ToString("0.00") + "\r\n" +
                        "- SonicBoom1: " + seModule.SonicBoomed1 + "\r\n" +
                        "- SonicBoom2: " + seModule.SonicBoomed2 + "\r\n\r\n";
                }

                GUILayout.Label(info);
                shipEffectsScrollPos = GUILayout.BeginScrollView(shipEffectsScrollPos, GUILayout.Height(windowHeight));
                if(seModule.SoundLayerGroups.Count > 0) {
                    string layerInfo = String.Empty;
                    layerInfo += "Sources: " + seModule.Sources.Count + "\r\n";

                    foreach(var soundLayerGroup in seModule.SoundLayerGroups) {
                        layerInfo +=
                            soundLayerGroup.Key.ToString() + "\r\n";

                        foreach(var soundLayer in soundLayerGroup.Value) {
                            string sourceLayerName = soundLayerGroup.Key.ToString() + "_" + soundLayer.name;

                            layerInfo += "SoundLayer: " + soundLayer.name + "\r\n";
                            if(soundLayer.audioClips != null) {
                                layerInfo += "AudioClips: " + soundLayer.audioClips.Length.ToString() + "\r\n";
                            } else {
                                layerInfo += "No AudioClips \r\n";
                            }

                            if(seModule.Sources.ContainsKey(sourceLayerName)) {
                                var source = seModule.Sources[sourceLayerName];
                                if(source != null) {
                                    layerInfo +=
                                        "Volume: " + source.volume + "\r\n" +
                                        "Pitch: " + source.pitch + "\r\n\r\n";
                                }
                            } else {
                                layerInfo += "Source Null or Inactive" + "\r\n\r\n\r\n";
                            }
                        }
                    }

                    GUILayout.Label(layerInfo);
                } else {
                    GUILayout.Label("No SoundLayers");
                }

                GUILayout.EndScrollView();
            } else {
                GUILayout.Label("No Active Vessel");
            }

            shipEffectsInfo = !GUILayout.Button("Close", GUILayout.Height(20));

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        public AudioMixerGroup MasterMixer { get { return Mixer.FindMatchingGroups("Master")[0]; } }
        public AudioMixerGroup AirSimMixer { get { return Mixer.FindMatchingGroups("AIRSIM")[0]; } }
        public AudioMixerGroup FocusMixer { get { return Mixer.FindMatchingGroups("FOCUS")[0]; } }
        public AudioMixerGroup InternalMixer { get { return Mixer.FindMatchingGroups("INTERNAL")[0]; } }
        public AudioMixerGroup ExternalMixer { get { return Mixer.FindMatchingGroups("EXTERNAL")[0]; } }

        private AudioMixer mixer;
        public AudioMixer Mixer
        {
            get {
                if(mixer == null) {
                    mixer = Startup.RSE_Bundle.LoadAsset("RSE_Mixer") as AudioMixer;
                }
                return mixer;
            }
        }

        public float WindModulation()
        {
            float windModulation = 1 - windModulationAmount + Mathf.PerlinNoise(Time.time * windModulationSpeed, 0) * windModulationAmount;
            return Mathf.Lerp(1, windModulation, Mathf.Clamp01((float)FlightGlobals.ActiveVessel.atmDensity));
        }

        void LateUpdate()
        {
            if(AudioMuffler.MufflerQuality == AudioMufflerQuality.Lite || !AudioMuffler.EnableMuffling && cachSources.Count > 0) {
                foreach(var source in cachSources.ToList()) {
                    if(source != null) {
                        source.outputAudioMixerGroup = null;
                    }
                    cachSources.Remove(source);
                }
            }

            if(gamePaused || !AudioMuffler.EnableMuffling)
                return;

            if(AudioMuffler.MufflerQuality > AudioMufflerQuality.Lite && Mixer != null) {
                var soundSources = GameObject.FindObjectsOfType<AudioSource>().ToList();
                foreach(var soundSource in soundSources) {
                    if(soundSource == null || StockSources.Contains(soundSource) || ChattererSources.Contains(soundSource))
                        continue;

                    if(soundSource.outputAudioMixerGroup == null) {
                        soundSource.outputAudioMixerGroup = ExternalMixer;
                        if(!cachSources.Contains(soundSource)) {
                            cachSources.Add(soundSource);
                        }
                    }
                }

                if(!bypassAutomaticFiltering) {
                    float atmDensity = Mathf.Clamp01((float)FlightGlobals.ActiveVessel.atmDensity);
                    float maxFrequency = InternalCamera.Instance.isActive ? AudioMuffler.InteriorMuffling : 22200;
                    float atmCutOff = Mathf.Lerp(AudioMuffler.ExteriorMuffling, maxFrequency, atmDensity) * WindModulation();
                    float focusMuffling = InternalCamera.Instance.isActive ? AudioMuffler.InteriorMuffling : atmCutOff;

                    if(MapView.MapCamera.isActiveAndEnabled) {
                        focusMuffling = AudioMuffler.InteriorMuffling < atmCutOff ? AudioMuffler.InteriorMuffling : atmCutOff;
                        atmCutOff = AudioMuffler.InteriorMuffling < atmCutOff ? AudioMuffler.InteriorMuffling : atmCutOff;
                    }

                    MufflingFrequency = Mathf.MoveTowards(lastCutoffFreq, atmCutOff, 5000);
                    lastCutoffFreq = MufflingFrequency;

                    FocusMufflingFrequency = Mathf.MoveTowards(lastInteriorCutoffFreq, focusMuffling, 5000);
                    lastInteriorCutoffFreq = FocusMufflingFrequency;

                } else {
                    FocusMufflingFrequency = MufflingFrequency;
                }

                Mixer.SetFloat("FocusCutoff", Mathf.Clamp(FocusMufflingFrequency, 0, 22200));
                Mixer.SetFloat("ExternalCutoff", Mathf.Clamp(MufflingFrequency, 0, 22200));

                if(AudioMuffler.AffectChatterer) {
                    foreach(var source in ChattererSources) {
                        if(source == null)
                            continue;

                        source.outputAudioMixerGroup = InternalCamera.Instance.isActive ? InternalMixer : FocusMixer;
                    }
                }
            } else {
                if(lowpassFilter != null && lowpassFilter.enabled) {
                    if(bypassAutomaticFiltering)
                        return;

                    float atmDensity = (float)FlightGlobals.ActiveVessel.atmDensity;
                    float interiorMuffling = AudioMuffler.InteriorMuffling;
                    float exteriorMuffling = Mathf.Lerp(AudioMuffler.ExteriorMuffling, 22200, atmDensity);

                    float targetFrequency = InternalCamera.Instance.isActive ? interiorMuffling : exteriorMuffling;
                    if(MapView.MapCamera.isActiveAndEnabled) {
                        targetFrequency = interiorMuffling < exteriorMuffling ? interiorMuffling : exteriorMuffling;
                    }

                    float smoothCutoff = Mathf.MoveTowards(lastCutoffFreq, targetFrequency, 5000);
                    lastCutoffFreq = smoothCutoff;

                    lowpassFilter.cutoffFrequency = smoothCutoff;

                    if(AudioMuffler.AffectChatterer) {
                        foreach(var source in ChattererSources) {

                            if(source == null)
                                continue;

                            source.bypassListenerEffects = InternalCamera.Instance.isActive;
                            if(source.outputAudioMixerGroup != null) {
                                source.outputAudioMixerGroup = null;
                            }
                        }
                    }
                }
            }
        }

        public void OnDestroy()
        {
            if(AppButton != null) {
                ApplicationLauncher.Instance.RemoveModApplication(AppButton);
                AppButton = null;
            }
            GameEvents.onGamePause.Remove(() => gamePaused = true);
            GameEvents.onGameUnpause.Remove(() => gamePaused = false);
        }
    }
}
