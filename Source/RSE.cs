using KSP.UI.Screens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace RocketSoundEnhancement
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class RSE : MonoBehaviour
    {
        public AudioListener audioListener;
        public LowpassFilter lowpassFilter;
        public AudioLimiter audioLimiter;

        AnimationCurve lowpassCurveExt;
        AnimationCurve lowpassCurveInt;

        string[] ChattererPlayerNames = new string[] {
            "rbr_chatter_player",
            "rbr_beep_player_",
            "rbr_sstv_player"
        };

        List<AudioSource> ChattererSources = new List<AudioSource>();

        bool gamePaused;
        void Start()
        {
            foreach(var source in GameObject.FindObjectsOfType<AudioSource>()) {
                if(source.name.Contains("Music") || source.name.Contains("PartActionController")) {
                    source.bypassListenerEffects = true;
                }
                if(source.name.Contains("airspeedNoise")) {
                    source.bypassListenerEffects = false;
                }
            }

            var stageSource = StageManager.Instance.GetComponent<AudioSource>();
            if(stageSource) {
                stageSource.bypassListenerEffects = true;

                if(Settings.DisableStagingSound) {
                    GameObject.Destroy(stageSource);
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
            
                            source.bypassListenerEffects = !LowpassFilter.MuffleChatterer;
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
            lowpassFilter.enabled = LowpassFilter.EnableMuffling;
            lowpassFilter.lowpassResonanceQ = 3;

            audioLimiter = gameObject.AddOrGetComponent<AudioLimiter>();
            audioLimiter.enabled = AudioLimiter.EnableLimiter;

            lowpassCurveExt = AnimationCurve.Linear(1, 22200, 0, LowpassFilter.VacuumMuffling);
            lowpassCurveInt = AnimationCurve.Linear(1, LowpassFilter.InteriorMufflingAtm, 0, LowpassFilter.InteriorMufflingVac);

            GameEvents.onGamePause.Add(onGamePause);
            GameEvents.onGameUnpause.Add(onGameUnPause);
        }

        void LateUpdate()
        {
            if(gamePaused)
                return;

            if(lowpassFilter == null)
                return;

            if(lowpassFilter.enabled) {
                if(bypassAutomaticFiltering)
                    return;

                if(InternalCamera.Instance.isActive) {
                    lowpassFilter.cutoffFrequency = lowpassCurveInt.Evaluate((float)FlightGlobals.ActiveVessel.atmDensity);
                } else {
                    lowpassFilter.cutoffFrequency = lowpassCurveExt.Evaluate((float)FlightGlobals.ActiveVessel.atmDensity);
                }

                if(LowpassFilter.MuffleChatterer) {
                    foreach(var source in ChattererSources) {

                        if(source == null)
                            continue;

                        source.bypassListenerEffects = InternalCamera.Instance.isActive;
                    }
                }
            }
        }

        Rect windowRect = new Rect(20, 50, 250, 400);
        void OnGUI()
        {
            if(HighLogic.CurrentGame.Parameters.CustomParams<SettingsInGame>().DebugWindow) {
                windowRect = GUILayout.Window(0, windowRect, InfoWindow, "Rocket Sound Enhancement");
            }
        }

        Vector2 scrollPosition;
        bool bypassAutomaticFiltering = false;
        void InfoWindow(int id)
        {
            GUILayout.BeginVertical(GUILayout.Width(250));

            var vessel = FlightGlobals.ActiveVessel;
            var seModule = vessel.GetComponent<ShipEffects>();

            if(lowpassFilter != null) {
                if(lowpassFilter.enabled) {
                    GUILayout.Label("Lowpass Filter");
                    bypassAutomaticFiltering = GUILayout.Toggle(bypassAutomaticFiltering, "Bypass Automatic Filtering");
                    GUILayout.Label("Cuttoff Frequency: " + lowpassFilter.cutoffFrequency.ToString());
                    lowpassFilter.cutoffFrequency = GUILayout.HorizontalSlider(lowpassFilter.cutoffFrequency, 10, 22200);
                    GUILayout.Label("Resonance Q: " + lowpassFilter.lowpassResonanceQ.ToString());
                    lowpassFilter.lowpassResonanceQ = GUILayout.HorizontalSlider(lowpassFilter.lowpassResonanceQ, 0.5f, 10);
                }
            }

            if(audioLimiter != null) {
                audioLimiter.enabled = GUILayout.Toggle(audioLimiter.enabled, "Enable Audio Limiter");

                GUILayout.Label("Threshold: " + AudioLimiter.Threshold.ToString());
                AudioLimiter.Threshold = GUILayout.HorizontalSlider(AudioLimiter.Threshold, -60f, 0f);

                GUILayout.Label("Bias: " + AudioLimiter.Bias.ToString());
                AudioLimiter.Bias = GUILayout.HorizontalSlider(AudioLimiter.Bias, 0.1f, 100f);

                GUILayout.Label("Ratio: " + AudioLimiter.Ratio.ToString());
                AudioLimiter.Ratio = GUILayout.HorizontalSlider(AudioLimiter.Ratio, 1f, 20f);

                GUILayout.Label("Gain: " + AudioLimiter.Gain.ToString());
                AudioLimiter.Gain = GUILayout.HorizontalSlider(AudioLimiter.Gain, -30f, 30f);

                GUILayout.Label("Time Constant: " + AudioLimiter.TimeConstant.ToString());
                AudioLimiter.TimeConstant = Mathf.RoundToInt(GUILayout.HorizontalSlider(AudioLimiter.TimeConstant, 1, 6));

                GUILayout.Label("Level Detector RMS Window: " + AudioLimiter.LevelDetectorRMSWindow.ToString());
                AudioLimiter.LevelDetectorRMSWindow = Mathf.RoundToInt(GUILayout.HorizontalSlider(AudioLimiter.LevelDetectorRMSWindow, 1, 1000));

                GUILayout.Label("Current Compression Ratio: " + AudioLimiter.CurrentCompressionRatio.ToString());
                GUILayout.HorizontalSlider(AudioLimiter.CurrentCompressionRatio, 1f, 50f);

                GUILayout.Label("Gain Reduction: " + AudioLimiter.GainReduction.ToString());
                GUILayout.HorizontalSlider(AudioLimiter.GainReduction, -90f, 0f);
            }

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
                scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(225));

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

            HighLogic.CurrentGame.Parameters.CustomParams<SettingsInGame>().DebugWindow = !GUILayout.Button("Close", GUILayout.Height(20));

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, 1000, 20));
        }

        void onGamePause()
        {
            gamePaused = true;
        }

        void onGameUnPause()
        {
            gamePaused = false;
            lowpassCurveExt = AnimationCurve.Linear(1, 22200, 0, LowpassFilter.VacuumMuffling);
            lowpassCurveInt = AnimationCurve.Linear(1, LowpassFilter.InteriorMufflingAtm, 0, LowpassFilter.InteriorMufflingVac);

            if(lowpassFilter != null)
                lowpassFilter.enabled = LowpassFilter.EnableMuffling;

            if(!LowpassFilter.MuffleChatterer) {
                foreach(var source in ChattererSources) {
                    if(source == null)
                        continue;
                    source.bypassListenerEffects = true;
                }
            }
        }

        void OnDestroy()
        {
            GameEvents.onGamePause.Remove(onGamePause);
            GameEvents.onGameUnpause.Remove(onGameUnPause);
        }
    }
}
