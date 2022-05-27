using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KSP.UI.Screens;
using UnityEngine;
using RocketSoundEnhancement.AudioFilters;

namespace RocketSoundEnhancement
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class GUI : MonoBehaviour
    {
        private GUI _instance;
        public GUI Instance { get { return _instance; } }

        public ApplicationLauncherButton AppButton;
        public Texture AppIcon = GameDatabase.Instance.GetTexture("RocketSoundEnhancement/Textures/RSE_Icon", false);
        public Texture AppTitle = GameDatabase.Instance.GetTexture("RocketSoundEnhancement/Textures/RSE_Title", false);

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

        private void Awake()
        {
            _instance = this;
        }

        private void Start()
        {
            settingsRect = new Rect(Screen.width - windowWidth - 40, 40, windowWidth, windowHeight);
            shipEffectsRect = new Rect(settingsRect.x - 500 - 40, settingsRect.y, 500, windowHeight * 2);

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
            RocketSoundEnhancement.Instance.AudioLimiter.enabled = AudioLimiter.EnableLimiter;

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
                RocketSoundEnhancement.Instance.LowpassFilter.enabled = AudioMuffler.EnableMuffling && AudioMuffler.MufflerQuality == AudioMufflerQuality.Lite;

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
                    RocketSoundEnhancement.Instance.overrideFiltering = GUILayout.Toggle(RocketSoundEnhancement.Instance.overrideFiltering, "Test Muffling", GUILayout.Width(smlLeftWidth));
                    if (RocketSoundEnhancement.Instance.LowpassFilter.enabled)
                    {
                        RocketSoundEnhancement.Instance.LowpassFilter.CutoffFrequency = GUILayout.HorizontalSlider(RocketSoundEnhancement.Instance.LowpassFilter.CutoffFrequency, 0, 22200);
                        GUILayout.Label(RocketSoundEnhancement.Instance.LowpassFilter.CutoffFrequency.ToString("0.0") + "hz", GUILayout.Width(rightWidth));
                    }
                    else
                    {
                        RocketSoundEnhancement.Instance.MufflingFrequency = GUILayout.HorizontalSlider(RocketSoundEnhancement.Instance.MufflingFrequency, 0, 22200);
                        GUILayout.Label(RocketSoundEnhancement.Instance.MufflingFrequency.ToString("0.0") + "hz", GUILayout.Width(rightWidth));
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
                RocketSoundEnhancement.Instance.ApplySettings();
            }
            if (GUILayout.Button("Save Settings"))
            {
                Settings.Instance.Save();
                RocketSoundEnhancement.Instance.ApplySettings();
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            UnityEngine.GUI.DragWindow(new Rect(0, 0, 10000, 20));
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
                    "- ACCELERATION: " + shipEffectsModule.GetPhysicsController(PhysicsControl.ACCELERATION).ToString("0.00") + "\r\n" +
                    "- JERK: " + shipEffectsModule.GetPhysicsController(PhysicsControl.JERK).ToString("0.00") + "\r\n" +
                    "- AIRSPEED: " + shipEffectsModule.GetPhysicsController(PhysicsControl.AIRSPEED).ToString("0.00") + "\r\n" +
                    "- GROUNDSPEED: " + shipEffectsModule.GetPhysicsController(PhysicsControl.GROUNDSPEED).ToString("0.00") + "\r\n" +
                    "- DYNAMICPRESSSURE: " + shipEffectsModule.GetPhysicsController(PhysicsControl.DYNAMICPRESSURE).ToString("0.00") + "\r\n" +
                    "- THRUST: " + shipEffectsModule.GetPhysicsController(PhysicsControl.THRUST).ToString("0.00") + "\r\n" +
                    "- REENTRYHEAT: " + shipEffectsModule.GetPhysicsController(PhysicsControl.REENTRYHEAT).ToString("0.00") + "\r\n" +
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
                        "- SonicBoom1: " + shipEffectsModule.SonicBoomedTip + "\r\n" +
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

            UnityEngine.GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        void OnDestroy()
        {
            if (AppButton != null)
            {
                ApplicationLauncher.Instance.RemoveModApplication(AppButton);
                AppButton = null;
            }
        }
    }
}