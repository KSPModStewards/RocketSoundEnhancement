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
        public static List<ConfigNode> SoundLayerNodes = new List<ConfigNode>();
        public static Dictionary<string, CollidingObject> CollisionData = new Dictionary<string, CollidingObject>();

        public AudioListener audioListener;
        public LowpassFilter lowpassFilter;
        public AudioLimiter audioLimiter;

        AnimationCurve lowpassCurveExt;
        AnimationCurve lowpassCurveInt;

        int VacuumFreq => HighLogic.CurrentGame.Parameters.CustomParams<LowpassFilterSettings>().VacuumMuffling;
        int InterFreqAtm => HighLogic.CurrentGame.Parameters.CustomParams<LowpassFilterSettings>().InteriorMufflingAtm;
        int InterFreqVac => HighLogic.CurrentGame.Parameters.CustomParams<LowpassFilterSettings>().InteriorMufflingVac;

        string[] ChattererPlayerNames = new string[] {
            "rbr_chatter_player",
            "rbr_beep_player_",
            "rbr_sstv_player"
        };

        bool gamePaused;
        void Start()
        {
            SoundLayerNodes.Clear();
            CollisionData.Clear();

            foreach(var configNode in GameDatabase.Instance.GetConfigNodes("SHIPEFFECTS_SOUNDLAYERS")) {
                SoundLayerNodes.AddRange(configNode.GetNodes("SOUNDLAYER"));
            }

            foreach(var configNode in GameDatabase.Instance.GetConfigNodes("RSE_SETTINGS")) {
                if(configNode.HasValue("nextStageClip")) {
                    StageManager.Instance.nextStageClip = GameDatabase.Instance.GetAudioClip(configNode.GetValue("nextStageClip"));
                }
                if(configNode.HasValue("cannotSeparateClip")) {
                    StageManager.Instance.cannotSeparateClip = GameDatabase.Instance.GetAudioClip(configNode.GetValue("cannotSeparateClip"));
                }

                if(configNode.HasNode("Colliders")) {
                    var colNode = configNode.GetNode("Colliders");
                    foreach(ConfigNode.Value node in colNode.values) {
                        CollidingObject colDataType = (CollidingObject)Enum.Parse(typeof(CollidingObject), node.value, true);
                        if(!CollisionData.ContainsKey(node.name)) {
                            CollisionData.Add(node.name, colDataType);
                        } else {
                            CollisionData[node.name] = colDataType;
                        }
                    }
                }
            }

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

                if(HighLogic.CurrentGame.Parameters.CustomParams<RSESettings>().DisableStagingSound) {
                    GameObject.Destroy(stageSource);
                }
            }

            //Find Chatterer Players and set bypassListenerEffects to true
            var chattererObjects = GameObject.FindObjectsOfType<GameObject>().Where(x => x.name.Contains("_player"));
            if(chattererObjects.Count() > 0) {
                foreach(var chatterer in chattererObjects) {
                    if(ChattererPlayerNames.Contains(Regex.Replace(chatterer.name, @"\d", string.Empty))) {
                        var sources = chatterer.GetComponents<AudioSource>();
                        foreach(var source in sources) {
                            source.bypassListenerEffects = true;
                        }
                    }
                }
            }

            //This is the easiest way to deal with multiple listeners, instead of chasing which listener is active.
            //Lowpass filter reads from whatever listener is active.
            audioListener = gameObject.AddOrGetComponent<AudioListener>();
            audioListener.enabled = false;

            lowpassFilter = gameObject.AddOrGetComponent<LowpassFilter>();
            lowpassFilter.enabled = HighLogic.CurrentGame.Parameters.CustomParams<LowpassFilterSettings>().EnableMuffling;
            lowpassFilter.lowpassResonanceQ = 3;

            lowpassCurveExt = AnimationCurve.Linear(1, 22200, 0, VacuumFreq);
            lowpassCurveInt = AnimationCurve.Linear(1, InterFreqAtm, 0, InterFreqVac);

            audioLimiter = gameObject.AddOrGetComponent<AudioLimiter>();
            audioLimiter.enabled = HighLogic.CurrentGame.Parameters.CustomParams<AudioLimiterSettings>().EnableLimiter;

            GameEvents.onGamePause.Add(onGamePause);
            GameEvents.onGameUnpause.Add(onGameUnPause);
        }

        void LateUpdate()
        {
            if(gamePaused)
                return;

            if(lowpassFilter == null)
                return;

            if(HighLogic.CurrentGame.Parameters.CustomParams<LowpassFilterSettings>().EnableMuffling) {
                if(!lowpassFilter.enabled) {
                    lowpassFilter.enabled = true;
                }

                if(bypassFilter)
                    return;

                if(InternalCamera.Instance.isActive) {
                    lowpassFilter.cutoffFrequency = lowpassCurveInt.Evaluate((float)FlightGlobals.ActiveVessel.atmDensity);
                } else {
                    lowpassFilter.cutoffFrequency = lowpassCurveExt.Evaluate((float)FlightGlobals.ActiveVessel.atmDensity);
                }

            } else if(lowpassFilter.enabled) {
                lowpassFilter.enabled = false;
            }
        }

        Rect windowRect = new Rect(20, 50, 250, 400);
        void OnGUI()
        {
            if(HighLogic.CurrentGame.Parameters.CustomParams<RSESettings>().DebugWindow) {
                windowRect = GUILayout.Window(0, windowRect, InfoWindow, "Rocket Sound Enhancement");
            }
        }

        Vector2 scrollPosition;
        bool bypassFilter = false;
        void InfoWindow(int id)
        {
            GUILayout.BeginVertical(GUILayout.Width(250));

            var vessel = FlightGlobals.ActiveVessel;
            var seModule = vessel.GetComponent<ShipEffects>();

            if(lowpassFilter != null) {
                if(lowpassFilter.enabled) {
                    GUILayout.Label("Lowpass Filter");
                    bypassFilter = GUILayout.Toggle(bypassFilter, "Bypass Automatic Filtering");
                    GUILayout.Label("Cuttoff Frequency: " + lowpassFilter.cutoffFrequency.ToString());
                    lowpassFilter.cutoffFrequency = GUILayout.HorizontalSlider(lowpassFilter.cutoffFrequency, 10, 22200);
                    GUILayout.Label("Resonance Q: " + lowpassFilter.lowpassResonanceQ.ToString());
                    lowpassFilter.lowpassResonanceQ = GUILayout.HorizontalSlider(lowpassFilter.lowpassResonanceQ, 0.5f, 10);
                }
            }

            if(audioLimiter != null) {
                if(audioLimiter.enabled) {
                    string limiterInfo = "Gain: " + audioLimiter.Gain + "\r\n" +
                        "Makeup Gain: " + audioLimiter.MakeUp + "\r\n" +
                        "WindowSize: " + audioLimiter.WindowSize + "\r\n" +
                        "Lookahead: " + audioLimiter.LookAhead + "\r\n" +
                        "Threshold: " + audioLimiter.Threshold + "\r\n" +
                        "Ratio: " + audioLimiter.Ratio + "\r\n" +
                        "Attack: " + audioLimiter.Attack + "\r\n" +
                        "Release: " + audioLimiter.Release + "\r\n" +
                        "RMS: " + audioLimiter.RMS + "\r\n" +
                        "Reduction: " + audioLimiter.Reduction + "\r\n";

                    GUILayout.TextArea(limiterInfo, GUILayout.Height(175));
                }
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

            HighLogic.CurrentGame.Parameters.CustomParams<RSESettings>().DebugWindow = !GUILayout.Button("Close", GUILayout.Height(20));

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
            lowpassCurveExt = AnimationCurve.Linear(1, 22200, 0, VacuumFreq);
            lowpassCurveInt = AnimationCurve.Linear(1, InterFreqAtm, 0, InterFreqVac);

            if(lowpassFilter != null)
                lowpassFilter.enabled = HighLogic.CurrentGame.Parameters.CustomParams<LowpassFilterSettings>().EnableMuffling;

            if(audioLimiter != null) {
                audioLimiter.enabled = HighLogic.CurrentGame.Parameters.CustomParams<AudioLimiterSettings>().EnableLimiter;
                audioLimiter.initalized = false;
                audioLimiter.Initialize();
            }
        }

        void OnDestroy()
        {
            GameEvents.onGamePause.Remove(onGamePause);
            GameEvents.onGameUnpause.Remove(onGameUnPause);
        }
    }
}
