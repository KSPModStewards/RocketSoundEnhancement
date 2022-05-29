using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KSP.UI.Screens;
using UnityEngine;

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
        int rightWidth = 120;
        int smlRightWidth = 40;
        int smlLeftWidth = 125;

        Rect shipEffectsRect;
        Vector2 shipEffectsScrollPos;
        bool shipEffectsWindowToggle;
        bool settingsWindowToggle;
        bool showAdvanceMuffler = false;

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
                    () => settingsWindowToggle = true,
                    () => settingsWindowToggle = false,
                    null, null,
                    null, null,
                    ApplicationLauncher.AppScenes.FLIGHT, AppIcon
                );
            }
        }

        void OnGUI()
        {
            if (settingsWindowToggle)
            {
                settingsRect = GUILayout.Window(52534500, settingsRect, settingsWindow, "");
            }
            if (shipEffectsWindowToggle)
            {
                shipEffectsRect = GUILayout.Window(52534501, shipEffectsRect, shipEffectsWindow, "ShipEffects Info");
            }
        }

        void settingsWindow(int id)
        {
            GUILayout.BeginVertical();
            GUILayout.Label(AppTitle);
            settingsScrollPos = GUILayout.BeginScrollView(settingsScrollPos, false, true, GUILayout.Height(windowHeight));
            Settings.AudioEffectsEnabled = GUILayout.Toggle(Settings.AudioEffectsEnabled, "Enable Audio Effects");
            
            #region NORMALIZER SETTINGS
            GUILayout.BeginHorizontal();
            GUILayout.Label("NORMALIZER", GUILayout.Width(leftWidth));
            if (GUILayout.Button(Settings.NormalizerPresetName))
            {
                int limiterPresetIndex = Settings.NormalizerPresets.Keys.ToList().IndexOf(Settings.NormalizerPresetName) + 1;

                if (limiterPresetIndex >= Settings.NormalizerPresets.Count)
                {
                    limiterPresetIndex = 0;
                }

                Settings.NormalizerPresetName = Settings.NormalizerPresets.Keys.ToList()[limiterPresetIndex];
                Settings.ApplyNormalizerPreset();
                RocketSoundEnhancement.Instance.UpdateNormalizer();
            }

            GUILayout.EndHorizontal();
            
            if (Settings.NormalizerPresetName == "Custom")
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("FadeInTime", GUILayout.Width(smlLeftWidth));
                Settings.NormalizerPreset.FadeInTime = (float)Math.Round(GUILayout.HorizontalSlider(Settings.NormalizerPreset.FadeInTime, 0, 20000f), 2);
                GUILayout.Label($"{Settings.NormalizerPreset.FadeInTime.ToString("0.00")} ms", GUILayout.Width(rightWidth));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("LowestVolume", GUILayout.Width(smlLeftWidth));
                Settings.NormalizerPreset.LowestVolume = (float)Math.Round(GUILayout.HorizontalSlider(Settings.NormalizerPreset.LowestVolume, 0, 1f), 2);
                GUILayout.Label($"{Settings.NormalizerPreset.LowestVolume.ToString("P")}", GUILayout.Width(rightWidth));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("MaximumAmp", GUILayout.Width(smlLeftWidth));
                Settings.NormalizerPreset.MaximumAmp = (float)Math.Round(GUILayout.HorizontalSlider(Settings.NormalizerPreset.MaximumAmp, 0, 100f), 2);
                GUILayout.Label($"{Settings.NormalizerPreset.MaximumAmp.ToString("0.00")} x", GUILayout.Width(rightWidth));
                GUILayout.EndHorizontal();

                RocketSoundEnhancement.Instance.UpdateNormalizer();
            }

            if (Settings.NormalizerPresetName == "Custom")
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Reset"))
                {
                    Settings.DefaultNormalizer();
                }
                GUILayout.EndHorizontal();
            }
            #endregion

            #region MUFFLER SETTINGS
            GUILayout.BeginHorizontal();
            GUILayout.Label("MUFFLER", GUILayout.Width(leftWidth));
            if (GUILayout.Button(Settings.MufflerPresetName))
            {
                int lowpassPresetIndex = Settings.MufflerPresets.Keys.ToList().IndexOf(Settings.MufflerPresetName) + 1;
                if (lowpassPresetIndex >= Settings.MufflerPresets.Count)
                {
                    lowpassPresetIndex = 0;
                }
                Settings.MufflerPresetName = Settings.MufflerPresets.Keys.ToList()[lowpassPresetIndex];
                Settings.ApplyMufflerPreset();
            }

            if (GUILayout.Button(showAdvanceMuffler ? upArrowUNI : downArrowUNI, GUILayout.Width(smlRightWidth)))
            {
                showAdvanceMuffler = !showAdvanceMuffler;
            }
            GUILayout.EndHorizontal();

            if (showAdvanceMuffler)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Muffling Quality", GUILayout.Width(smlLeftWidth));
                int qualitySlider = ((int)Settings.MufflerQuality);
                qualitySlider = Mathf.RoundToInt(GUILayout.HorizontalSlider(qualitySlider, 0, 1));

                switch (qualitySlider)
                {
                    case 0:
                        Settings.MufflerQuality = AudioMufflerQuality.Normal;
                        break;
                    case 1:
                        Settings.MufflerQuality = AudioMufflerQuality.AirSim;
                        break;
                }

                GUILayout.Label(Settings.MufflerQuality.ToString(), GUILayout.Width(rightWidth));
                GUILayout.EndHorizontal();

                if (Settings.MufflerPresetName == "Custom")
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Exterior Muffling", GUILayout.Width(smlLeftWidth));
                    Settings.MufflerPreset.ExteriorMuffling = (float)Math.Round(GUILayout.HorizontalSlider(Settings.MufflerPreset.ExteriorMuffling, 0, 22200), 1);
                    GUILayout.Label(Settings.MufflerPreset.ExteriorMuffling.ToString() + "hz", GUILayout.Width(rightWidth));
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Interior Muffling", GUILayout.Width(smlLeftWidth));
                    Settings.MufflerPreset.InteriorMuffling = (float)Math.Round(GUILayout.HorizontalSlider(Settings.MufflerPreset.InteriorMuffling, 0, 22200), 1);
                    GUILayout.Label(Settings.MufflerPreset.InteriorMuffling.ToString() + "hz", GUILayout.Width(rightWidth));
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Reset", GUILayout.Width(smlLeftWidth)))
                    {
                        Settings.DefaultMuffler();
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.BeginHorizontal();
                RocketSoundEnhancement.Instance.overrideFiltering = GUILayout.Toggle(RocketSoundEnhancement.Instance.overrideFiltering, "Test Muffling", GUILayout.Width(smlLeftWidth));
                RocketSoundEnhancement.Instance.MufflingFrequency = GUILayout.HorizontalSlider(RocketSoundEnhancement.Instance.MufflingFrequency, 0, 22200);
                GUILayout.Label(RocketSoundEnhancement.Instance.MufflingFrequency.ToString("0.0") + "hz", GUILayout.Width(rightWidth));
                GUILayout.EndHorizontal();
            }
            #endregion
            
            GUILayout.BeginHorizontal();
            GUILayout.Label("Exterior Volume", GUILayout.Width(smlLeftWidth));
            Settings.ExteriorVolume = GUILayout.HorizontalSlider((float)Math.Round(Settings.ExteriorVolume, 2), 0, 2);
            GUILayout.Label(Settings.ExteriorVolume.ToString("0.00"), GUILayout.Width(smlRightWidth));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Interior Volume", GUILayout.Width(smlLeftWidth));
            Settings.InteriorVolume = GUILayout.HorizontalSlider((float)Math.Round(Settings.InteriorVolume, 2), 0, 2);
            GUILayout.Label(Settings.InteriorVolume.ToString("0.00"), GUILayout.Width(smlRightWidth));
            GUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();
            Settings.DisableStagingSound = GUILayout.Toggle(Settings.DisableStagingSound, "Disable Staging Sound");
            shipEffectsWindowToggle = GUILayout.Toggle(shipEffectsWindowToggle, "ShipEffects Info");
            GUILayout.EndScrollView();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Reload Settings"))
            {
                Settings.Load();
                RocketSoundEnhancement.Instance.ApplySettings();
            }
            if (GUILayout.Button("Save Settings"))
            {
                Settings.Save();
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

                if (Settings.AudioEffectsEnabled && Settings.MufflerQuality == AudioMufflerQuality.AirSim)
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
            shipEffectsWindowToggle = !GUILayout.Button("Close");
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