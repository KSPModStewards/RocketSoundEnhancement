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
        public ApplicationLauncherButton AppButton;
        public Texture AppIcon = GameDatabase.Instance.GetTexture("RocketSoundEnhancement/Textures/RSE_Icon", false);
        public Texture AppTitle = GameDatabase.Instance.GetTexture("RocketSoundEnhancement/Textures/RSE_Title", false);

        public AudioListener audioListener;
        public LowpassFilter lowpassFilter;
        public AudioLimiter audioLimiter;

        string[] ChattererPlayerNames = new string[] {
            "rbr_chatter_player",
            "rbr_background_player_",
            "rbr_beep_player_",
            "rbr_sstv_player"
        };

        List<AudioSource> ChattererSources = new List<AudioSource>();
        List<AudioSource> StockSources = new List<AudioSource>();

        public static bool MuteRSE = false;
        bool gamePaused;

        Rect windowRect;
        int windowWidth = 440;
        int windowHeight = 244;
        int leftWidth = 200;
        int rightWidth = 60;
        int smlRightWidth = 40;
        int smlLeftWidth = 125;
        Rect shipEffectsRect;
        void Start()
        {
            Settings.Instance.Load();

            windowRect = new Rect(Screen.width - windowWidth - 40, 40, windowWidth, windowHeight);
            shipEffectsRect = new Rect(windowRect.x - 250 - 40, windowRect.y, 250, windowHeight * 2);

            foreach(var source in GameObject.FindObjectsOfType<AudioSource>()) {
                if(source.clip != null)
                    source.bypassListenerEffects = false;

                if(source.name.Contains(AudioUtility.RSETag))
                    continue;

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
                stageSource.bypassListenerEffects = true;

                stageSource.enabled = !Settings.Instance.DisableStagingSound;
            }

            lowpassFilter.enabled = AudioMuffler.EnableMuffling && !AudioMuffler.AirSimulation;
            audioLimiter.enabled = AudioLimiter.EnableLimiter;

            if(ChattererSources.Count > 0) {
                foreach(var source in ChattererSources) {
                    if(AudioMuffler.AirSimulation) {
                        source.outputAudioMixerGroup = InternalMixer;
                    } else {
                        source.bypassListenerEffects = !AudioMuffler.AffectChatterer;
                        source.outputAudioMixerGroup = MasterMixer;
                    }
                }
            }

            foreach(var source in StockSources) {
                source.outputAudioMixerGroup = AudioMuffler.EnableMuffling && AudioMuffler.AirSimulation ? ExternalMixer : MasterMixer;
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

        bool showAdvanceLimiter = false;
        bool showAdvanceLowpass = false;
        const string downArrowUNI = "\u25BC";
        const string upArrowUNI = "\u25B2";
        Vector2 settingsScrollPos;

        void SettingsWindow(int id)
        {
            GUILayout.BeginVertical();
            GUILayout.Label(AppTitle);
            settingsScrollPos = GUILayout.BeginScrollView(settingsScrollPos,false, true, GUILayout.Height(windowHeight));
            GUILayout.BeginHorizontal();
            AudioLimiter.EnableLimiter = GUILayout.Toggle(AudioLimiter.EnableLimiter, "Sound Effects Mastering", GUILayout.Width(leftWidth));
            audioLimiter.enabled = AudioLimiter.EnableLimiter;

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

            GUILayout.BeginHorizontal();
            AudioMuffler.EnableMuffling = GUILayout.Toggle(AudioMuffler.EnableMuffling, "Audio Muffler", GUILayout.Width(leftWidth));
            if(AudioMuffler.EnableMuffling) {
                lowpassFilter.enabled = AudioMuffler.EnableMuffling && !AudioMuffler.AirSimulation;

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
                    GUILayout.Label("Basic Muffler Enabled: " + lowpassFilter.enabled);
                    GUILayout.BeginHorizontal();
                    AudioMuffler.AirSimulation = GUILayout.Toggle(AudioMuffler.AirSimulation, "Air Simulation", GUILayout.Width(leftWidth));
                    GUILayout.Label("Experimental, High CPU Usage.");
                    GUILayout.EndHorizontal();
                    AudioMuffler.AffectChatterer = GUILayout.Toggle(AudioMuffler.AffectChatterer, "Affect Chatterer");

                    if(AudioMuffler.Preset == "Custom") {
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Vacuum Muffling", GUILayout.Width(smlLeftWidth));
                        AudioMuffler.VacuumMuffling = (float)Math.Round(GUILayout.HorizontalSlider(AudioMuffler.VacuumMuffling, 0, 22200),1);
                        GUILayout.Label(AudioMuffler.VacuumMuffling.ToString() + "hz", GUILayout.Width(rightWidth));
                        GUILayout.EndHorizontal();

                        GUILayout.Label("Interior Muffling");
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("In Atmosphere", GUILayout.Width(smlLeftWidth));
                        AudioMuffler.InteriorMufflingAtm = (float)Math.Round(GUILayout.HorizontalSlider(AudioMuffler.InteriorMufflingAtm, 0, 22200),1);
                        GUILayout.Label(AudioMuffler.InteriorMufflingAtm.ToString() + "hz", GUILayout.Width(rightWidth));
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        GUILayout.Label("In Vacuum", GUILayout.Width(smlLeftWidth));
                        AudioMuffler.InteriorMufflingVac = (float)Math.Round(GUILayout.HorizontalSlider(AudioMuffler.InteriorMufflingVac, 0, 22200),1);
                        GUILayout.Label(AudioMuffler.InteriorMufflingVac.ToString() + "hz", GUILayout.Width(rightWidth));
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        if(GUILayout.Button("Reset",GUILayout.Width(smlLeftWidth))){
                            AudioMuffler.Default();
                        }
                        GUILayout.EndHorizontal();
                    }

                    GUILayout.BeginHorizontal();
                    bypassAutomaticFiltering = GUILayout.Toggle(bypassAutomaticFiltering, "Test Muffling", GUILayout.Width(smlLeftWidth));
                    if(AudioMuffler.AirSimulation) {
                        MufflingFrequency = GUILayout.HorizontalSlider(MufflingFrequency, 0, 22200);
                        GUILayout.Label(MufflingFrequency.ToString("#.#") + "hz", GUILayout.Width(rightWidth));
                    } else {
                        lowpassFilter.cutoffFrequency = GUILayout.HorizontalSlider(lowpassFilter.cutoffFrequency, 0, 22200);
                        GUILayout.Label(lowpassFilter.cutoffFrequency.ToString("#.#") + "hz", GUILayout.Width(rightWidth));
                    }

                    GUILayout.EndHorizontal();
                }
            } else {
                GUILayout.EndHorizontal();
            }

            GUILayout.FlexibleSpace();
            Settings.Instance.DisableStagingSound = GUILayout.Toggle(Settings.Instance.DisableStagingSound, "Disable Staging Sound"); 
            MuteRSE = GUILayout.Toggle(MuteRSE, "Mute Rocket Sound Enhancement");
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

        //ToDo
        //ShipEffects Debug Window
        Vector2 shipEffectsScrollPos;
        bool shipEffectsInfo;
        void ShipEffectsInfo(int id)
        {
            GUILayout.BeginVertical(GUILayout.Width(250));

            var vessel = FlightGlobals.ActiveVessel;
            var seModule = vessel.GetComponent<ShipEffects>();

            if(seModule != null && seModule.initialized) {
                GUILayout.Label("Vessel: " + vessel.GetDisplayName());
                GUILayout.Label("Vessel Parts: " + vessel.Parts.Count());

                float accel = seModule.Acceleration;
                float jerk = seModule.Jerk;

                string info = "Acceleration: " + accel.ToString("0.00") + "\r\n" +
                              "Jerk: " + jerk.ToString("0.00") + "\r\n" +
                              "Airspeed: " + ((float)vessel.indicatedAirSpeed).ToString("0.00") + "\r\n" +
                              "Dynamic Pressure (kPa): " + ((float)vessel.dynamicPressurekPa).ToString("0.00") + "\r\n" +
                              "Speed Of Sound: " + ((float)vessel.speedOfSound).ToString("0.00") + "\r\n" +
                              "Thrust Acceleration: " + seModule.ThrustAccel.ToString("0.00") + "\r\n" +
                              "Mass: " + seModule.TotalMass.ToString("0.00") + "\r\n" +
                              "DryMass: " + seModule.DryMass.ToString("0.00") + "\r\n" +
                              "MachAngle: " + seModule.MachAngle.ToString("0.00") + "\r\n" +
                              "MachPass: " + seModule.MachPass.ToString("0.00") + "\r\n" +
                              "SonicBoom: " + seModule.SonicBoomed + "\r\n";

                GUILayout.Label(info);
                shipEffectsScrollPos = GUILayout.BeginScrollView(shipEffectsScrollPos, GUILayout.Height(windowHeight));

                if(seModule.SoundLayerGroups.Count > 0) {
                    string layerInfo = String.Empty;
                    layerInfo += "Sources: " + seModule.Sources.Count + "\r\n";

                    foreach(var soundLayerGroup in seModule.SoundLayerGroups) {
                        layerInfo +=
                            "Group Name: " + soundLayerGroup.Key.ToString() + "\r\n";

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


        public static AudioMixerGroup MasterMixer { get { return Mixer.FindMatchingGroups("Master")[0]; } }
        public static AudioMixerGroup AirSimMixer { get { return Mixer.FindMatchingGroups("AIRSIM")[0]; } }
        public static AudioMixerGroup FocusMixer { get { return Mixer.FindMatchingGroups("FOCUS")[0]; } }
        public static AudioMixerGroup InternalMixer { get { return Mixer.FindMatchingGroups("INTERNAL")[0]; } }
        public static AudioMixerGroup ExternalMixer { get { return Mixer.FindMatchingGroups("EXTERNAL")[0]; } }

        public static float MufflingFrequency = 22200;
        public static float FocusMufflingFrequency = 22200;
        bool bypassAutomaticFiltering;

        private static AudioMixer mixer;
        public static AudioMixer Mixer
        {
            get {
                if(mixer == null) {
                    string path = KSPUtil.ApplicationRootPath + "GameData/RocketSoundEnhancement/Plugins";
                    AssetBundle assetBundle = AssetBundle.LoadFromFile(path + "/rse_bundle");
                    if(assetBundle != null) {
                        mixer = assetBundle.LoadAsset("RSE_Mixer") as AudioMixer;
                    }
                }
                return mixer;
            }
        }

        float lastCutoffFreq;
        float lastIntCutoffFreq;
        void LateUpdate()
        {
            if(gamePaused || !AudioMuffler.EnableMuffling)
                return;

            if(Mixer != null && AudioMuffler.AirSimulation) {
                if(!bypassAutomaticFiltering) {
                    float atmDensity = Mathf.Clamp01((float)FlightGlobals.ActiveVessel.atmDensity);
                    float interiorMuffling = Mathf.Lerp(AudioMuffler.InteriorMufflingVac, AudioMuffler.InteriorMufflingAtm, atmDensity);
                    float maxFrequency = InternalCamera.Instance.isActive ? interiorMuffling : 22200;
                    float atmCutOff = Mathf.Lerp(AudioMuffler.VacuumMuffling, maxFrequency, atmDensity);

                    MufflingFrequency = Mathf.MoveTowards(lastCutoffFreq, atmCutOff, 5000);
                    lastCutoffFreq = MufflingFrequency;

                    interiorMuffling = InternalCamera.Instance.isActive ? interiorMuffling : atmCutOff;
                    if(MapView.MapCamera.isActiveAndEnabled) {
                        interiorMuffling = interiorMuffling < atmCutOff ? interiorMuffling : atmCutOff;
                    }

                    FocusMufflingFrequency = Mathf.MoveTowards(lastCutoffFreq, interiorMuffling, 5000);
                    lastIntCutoffFreq = FocusMufflingFrequency;

                } else {
                    FocusMufflingFrequency = MufflingFrequency;
                }

                Mixer.SetFloat("FocusCutoff", Mathf.Clamp(FocusMufflingFrequency, 20, 22200));
                Mixer.SetFloat("ExternalCutoff", Mathf.Clamp(MufflingFrequency, 20, 22200));

                if(MufflingFrequency < 20) {
                    Mixer.SetFloat("ExternalVolume", Mathf.Lerp(-80, 0, (MufflingFrequency / 20)));
                }

                if(FocusMufflingFrequency < 20) {
                    Mixer.SetFloat("FocusVolume", Mathf.Lerp(-80, 0, (FocusMufflingFrequency / 20)));
                }

                if(AudioMuffler.AffectChatterer) {
                    foreach(var source in ChattererSources) {

                        if(source == null)
                            continue;

                        source.outputAudioMixerGroup = InternalCamera.Instance.isActive ? InternalMixer : FocusMixer;
                    }
                }
            }

            if(lowpassFilter != null && lowpassFilter.enabled && !AudioMuffler.AirSimulation) {
                if(bypassAutomaticFiltering)
                    return;

                float atmDensity = (float)FlightGlobals.ActiveVessel.atmDensity;
                float interiorMuffling = Mathf.Lerp(AudioMuffler.InteriorMufflingVac, AudioMuffler.InteriorMufflingAtm, atmDensity);
                float exteriorMuffling = Mathf.Lerp(AudioMuffler.VacuumMuffling, 22200, atmDensity);

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
                        if(source.outputAudioMixerGroup != MasterMixer) {
                            source.outputAudioMixerGroup = MasterMixer;
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
