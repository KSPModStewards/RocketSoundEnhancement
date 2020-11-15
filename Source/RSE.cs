using KSP.UI.Screens;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RocketSoundEnhancement
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class RSE : MonoBehaviour
    {
        public static List<ConfigNode> SoundLayerNodes = new List<ConfigNode>();

        public AudioListener audioListener;
        public LowpassFilter lowpassFilter;

        AnimationCurve lowpassCurve;

        bool gamePaused;
        void Start()
        {
            SoundLayerNodes.Clear();
            foreach(var node in GameDatabase.Instance.GetConfigNodes("SHIPEFFECTS_SOUNDLAYERS")) {
                if(node.HasValue("nextStageClip")) {
                    StageManager.Instance.nextStageClip = GameDatabase.Instance.GetAudioClip(node.GetValue("nextStageClip"));
                }
                if(node.HasValue("cannotSeparateClip")) {
                    StageManager.Instance.cannotSeparateClip = GameDatabase.Instance.GetAudioClip(node.GetValue("cannotSeparateClip"));
                }
                SoundLayerNodes.AddRange(node.GetNodes("SOUNDLAYER"));
            }

            foreach(var source in GameObject.FindObjectsOfType<AudioSource>()) {
                if(source.name.Contains("Music") || source.name.Contains("PartActionController")) {
                    source.bypassListenerEffects = true;
                }
                if(source.name.Contains("airspeedNoise")) {
                    source.bypassListenerEffects = false;
                }
            }

            if(HighLogic.CurrentGame.Parameters.CustomParams<Settings>().DisableStagingSound) {
                GameObject.Destroy(StageManager.Instance.GetComponent<AudioSource>());
            }

            //This is the easiest way to deal with multiple listeners, instead of chasing which listener is active.
            //Lowpass filter reads from whatever listener is active.
            audioListener = gameObject.AddOrGetComponent<AudioListener>();
            audioListener.enabled = false;

            lowpassFilter = gameObject.AddOrGetComponent<LowpassFilter>();
            lowpassFilter.enabled = HighLogic.CurrentGame.Parameters.CustomParams<Settings>().EnableMuffling;
            lowpassFilter.lowpassResonanceQ = 3;
            lowpassCurve = AnimationCurve.Linear(1, 22200, 0, HighLogic.CurrentGame.Parameters.CustomParams<Settings>().VaccumMuffling);

            GameEvents.onGamePause.Add(onGamePause);
            GameEvents.onGameUnpause.Add(onGameUnPause);
        }

        void LateUpdate()
        {
            if(gamePaused)
                return;

            if(lowpassFilter == null)
                return;

            if(HighLogic.CurrentGame.Parameters.CustomParams<Settings>().EnableMuffling) {
                if(!lowpassFilter.enabled) {
                    lowpassFilter.enabled = true;
                }

                if(bypassFilter)
                    return;

                if(InternalCamera.Instance.isActive) {
                    lowpassFilter.cutoffFrequency = HighLogic.CurrentGame.Parameters.CustomParams<Settings>().InteriorMuffling;
                } else {
                    lowpassFilter.cutoffFrequency = lowpassCurve.Evaluate((float)FlightGlobals.ActiveVessel.atmDensity);
                }
            } else if(lowpassFilter.enabled) {
                lowpassFilter.enabled = false;
            }
        }

        Rect windowRect = new Rect(20, 50, 250, 400);
        void OnGUI()
        {
            if(HighLogic.CurrentGame.Parameters.CustomParams<Settings>().DebugWindow) {
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
                            "Control: " + soundLayer.physicsControl + "\r\n";

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

            HighLogic.CurrentGame.Parameters.CustomParams<Settings>().DebugWindow = !GUILayout.Button("Close", GUILayout.Height(20));

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
            lowpassCurve = AnimationCurve.Linear(1, 22200, 0, HighLogic.CurrentGame.Parameters.CustomParams<Settings>().VaccumMuffling);
        }

        void OnDestroy()
        {
            GameEvents.onGamePause.Remove(onGamePause);
            GameEvents.onGameUnpause.Remove(onGameUnPause);
        }
    }
}
