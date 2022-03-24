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
            "rbr_beep_player_",
            "rbr_sstv_player"
        };

        List<AudioSource> ChattererSources = new List<AudioSource>();

        public static bool MuteRSE = false;

        Rect windowRect;
        int windowWidth = 440;
        int windowHeight = 244;
        int leftWidth = 200;
        int rightWidth = 60;
        int smlRightWidth = 40;
        int smlLeftWidth = 125;
        void Start()
        {
            Settings.Instance.Load();

            windowRect = new Rect(Screen.width - windowWidth - 40, 40, windowWidth, windowHeight);

            foreach(var source in GameObject.FindObjectsOfType<AudioSource>()) {
                if(source.clip != null)
                    source.bypassListenerEffects = false;

                if(source.name.Contains("Music") || source.name.Contains("PartActionController")) {
                    source.bypassListenerEffects = true;
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
                        }
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
        }

        void ApplySettings()
        {
            var stageSource = StageManager.Instance.GetComponent<AudioSource>();
            if(stageSource) {
                stageSource.bypassListenerEffects = true;
                stageSource.enabled = !Settings.Instance.DisableStagingSound;
            }

            lowpassFilter.enabled = LowpassFilter.EnableMuffling;
            audioLimiter.enabled = AudioLimiter.EnableLimiter;

            if(ChattererSources.Count > 0) {
                foreach(var source in ChattererSources) {
                    source.bypassListenerEffects = !Settings.Instance.AffectChatterer;
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
        }

        bool showAdvanceLimiter = false;
        bool showAdvanceLowpass = false;
        string advLimiterIconUNI;
        string advLowpassIconUNI;
        const string downArrowUNI = "\u25BC";
        const string upArrowUNI = "\u25B2";
        Vector2 scrollPos;

        bool bypassAutomaticFiltering;
        void SettingsWindow(int id)
        {
            GUILayout.BeginVertical();
            GUILayout.Label(AppTitle);
            scrollPos = GUILayout.BeginScrollView(scrollPos,false, true, GUILayout.Height(windowHeight));
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

                if(GUILayout.Button(advLimiterIconUNI = showAdvanceLimiter ? upArrowUNI : downArrowUNI,GUILayout.Width(smlRightWidth))) {
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
            LowpassFilter.EnableMuffling = GUILayout.Toggle(LowpassFilter.EnableMuffling, "Audio Muffler", GUILayout.Width(leftWidth));
            lowpassFilter.enabled = LowpassFilter.EnableMuffling;
            if(LowpassFilter.EnableMuffling) {
                if(GUILayout.Button(LowpassFilter.Preset)) {
                    int lowpassPresetIndex = LowpassFilter.Presets.Keys.ToList().IndexOf(LowpassFilter.Preset) + 1;
                    if(lowpassPresetIndex >= LowpassFilter.Presets.Count) {
                        lowpassPresetIndex = 0;
                    }
                    LowpassFilter.Preset = LowpassFilter.Presets.Keys.ToList()[lowpassPresetIndex];
                    LowpassFilter.ApplyPreset();
                }

                if(GUILayout.Button(advLowpassIconUNI = showAdvanceLowpass ? upArrowUNI : downArrowUNI, GUILayout.Width(smlRightWidth))) {
                    showAdvanceLowpass = !showAdvanceLowpass;
                }
                GUILayout.EndHorizontal();
                if(showAdvanceLowpass) {
                    if(LowpassFilter.Preset == "Custom") {
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Vacuum Muffling", GUILayout.Width(smlLeftWidth));
                        LowpassFilter.VacuumMuffling = (float)Math.Round(GUILayout.HorizontalSlider(LowpassFilter.VacuumMuffling, 0, 22200),1);
                        GUILayout.Label(LowpassFilter.VacuumMuffling.ToString() + "hz", GUILayout.Width(rightWidth));
                        GUILayout.EndHorizontal();

                        GUILayout.Label("Interior Muffling");
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("In Atmosphere", GUILayout.Width(smlLeftWidth));
                        LowpassFilter.InteriorMufflingAtm = (float)Math.Round(GUILayout.HorizontalSlider(LowpassFilter.InteriorMufflingAtm, 0, 22200),1);
                        GUILayout.Label(LowpassFilter.InteriorMufflingAtm.ToString() + "hz", GUILayout.Width(rightWidth));
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        GUILayout.Label("In Vacuum", GUILayout.Width(smlLeftWidth));
                        LowpassFilter.InteriorMufflingVac = (float)Math.Round(GUILayout.HorizontalSlider(LowpassFilter.InteriorMufflingVac, 0, 22200),1);
                        GUILayout.Label(LowpassFilter.InteriorMufflingVac.ToString() + "hz", GUILayout.Width(rightWidth));
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        if(GUILayout.Button("Reset",GUILayout.Width(smlLeftWidth))){
                            LowpassFilter.Default();
                        }
                        GUILayout.EndHorizontal();
                    }
                    GUILayout.BeginHorizontal();
                    bypassAutomaticFiltering = GUILayout.Toggle(bypassAutomaticFiltering, "Test Muffling", GUILayout.Width(smlLeftWidth));
                    lowpassFilter.cutoffFrequency = GUILayout.HorizontalSlider(lowpassFilter.cutoffFrequency, 0, 22200);
                    GUILayout.Label(lowpassFilter.cutoffFrequency.ToString("#.#") + "hz", GUILayout.Width(rightWidth)); ;
                    GUILayout.EndHorizontal();
                }
            } else {
                GUILayout.EndHorizontal();
            }

            GUILayout.FlexibleSpace();
            Settings.Instance.DisableStagingSound = GUILayout.Toggle(Settings.Instance.DisableStagingSound, "Disable Staging Sound");
            Settings.Instance.AffectChatterer = GUILayout.Toggle(Settings.Instance.AffectChatterer, "Affect Chatterer");
            MuteRSE = GUILayout.Toggle(MuteRSE, "Mute Rocket Sound Enhancement");
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
        Vector2 scrollPosition;
        void InfoWindow(int id)
        {
            GUILayout.BeginVertical(GUILayout.Width(250));

            var vessel = FlightGlobals.ActiveVessel;
            var seModule = vessel.GetComponent<ShipEffects>();

            if(seModule != null && seModule.initialized) {
                GUILayout.Label("Vessel: " + vessel.GetDisplayName());
                GUILayout.Label("Vessel Parts: " + vessel.Parts.Count());
                GUILayout.Label("SoundLayers: " + seModule.SoundLayers.Count);

                float accel = seModule.Acceleration;
                float jerk = seModule.Jerk;
                var SoundLayers = seModule.SoundLayers;

                string info = "Acceleration: " + accel.ToString("0.00") + "\r\n" +
                              "Jerk: " + jerk.ToString("0.00") + "\r\n" +
                              "Airspeed: " + ((float)vessel.indicatedAirSpeed).ToString("0.00") + "\r\n" +
                              "Thrust Acceleration: " + seModule.ThrustAccel.ToString("0.00") + "\r\n" +
                              "Mass: " + seModule.TotalMass.ToString("0.00") + "\r\n" +
                              "DryMass: " + seModule.DryMass.ToString("0.00") + "\r\n";

                GUILayout.Label(info);
                scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(150));

                if(SoundLayers.Count > 0) {
                    string layerInfo = String.Empty;
                    layerInfo += "Sources: " + seModule.Sources.Count + "\r\n";
                    foreach(var soundLayer in SoundLayers) {
                        layerInfo +=
                            "SoundLayer: " + soundLayer.name + "\r\n" +
                            "Control: " + soundLayer.data + "\r\n";
                        if(soundLayer.audioClips != null) {
                            layerInfo += "AudioClips: " + soundLayer.audioClips.Length.ToString() + "\r\n";
                        } else {
                            layerInfo += "No AudioClips \r\n";
                        }

                        if(seModule.Sources.ContainsKey(soundLayer.name)) {
                            var source = seModule.Sources[soundLayer.name];
                            if(source != null) {
                                layerInfo +=
                                    "Volume: " + source.volume + "\r\n" +
                                    "Pitch: " + source.pitch + "\r\n\r\n";
                            }
                        } else {
                            layerInfo += "Source Null or Inactive" + "\r\n\r\n\r\n";
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

            _appToggle = !GUILayout.Button("Close", GUILayout.Height(20));

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        void LateUpdate()
        {
            if(lowpassFilter == null)
                return;

            if(lowpassFilter.enabled) {
                if(bypassAutomaticFiltering)
                    return;

                float atmDensity = (float)FlightGlobals.ActiveVessel.atmDensity;
                float interiorMuffling = Mathf.Lerp(LowpassFilter.InteriorMufflingVac, LowpassFilter.InteriorMufflingAtm, atmDensity);
                float exteriorMuffling = Mathf.Lerp(LowpassFilter.VacuumMuffling, 22200, atmDensity);

                lowpassFilter.cutoffFrequency = InternalCamera.Instance.isActive ? interiorMuffling : exteriorMuffling;

                if(MapView.MapCamera.isActiveAndEnabled) {
                    lowpassFilter.cutoffFrequency = interiorMuffling < exteriorMuffling ? interiorMuffling : exteriorMuffling;
                }

                if(Settings.Instance.AffectChatterer) {
                    foreach(var source in ChattererSources) {

                        if(source == null)
                            continue;

                        source.bypassListenerEffects = InternalCamera.Instance.isActive;
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

        }
    }
}
