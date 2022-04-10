using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RocketSoundEnhancement
{
    public class RSE_Module : PartModule
    {
        public Dictionary<string, AudioSource> Sources = new Dictionary<string, AudioSource>();
        public Dictionary<string, AirSimulationFilter> AirSimFilters = new Dictionary<string, AirSimulationFilter>();
        public Dictionary<string, float> spools = new Dictionary<string, float>();

        public Dictionary<string, List<SoundLayer>> SoundLayerGroups;
        public List<SoundLayer> SoundLayers;

        public GameObject audioParent;

        public bool initialized;
        public bool gamePaused;
        public bool UseAirSimFilters = false;
        public bool EnableCombFilter = false;
        public bool EnableLowpassFilter = false;
        public bool EnableWaveShaperFilter = false;

        public float volume = 1;

        public override void OnStart(StartState state)
        {
            GameEvents.onGamePause.Add(onGamePause);
            GameEvents.onGameUnpause.Add(onGameUnpause);
        }

        [KSPField(isPersistant = false, guiActive = true, advancedTweakable = true)]
        public float distance = 0;
        [KSPField(isPersistant = false, guiActive = true, advancedTweakable = true)]
        public float speedOfSound = 340.29f;
        [KSPField(isPersistant = false, guiActive = true, advancedTweakable = true)]
        public float speed = 0;
        [KSPField(isPersistant = false, guiActive = true, advancedTweakable = true)]
        public float angle = 0;
        [KSPField(isPersistant = false, guiActive = true, advancedTweakable = true)]
        public float Doppler = 1;
        public override void OnUpdate()
        {
            if(Settings.Instance.AirSimulation) {
                CalculateDoppler();
                speedOfSound = vessel.speedOfSound > 0 ? (float)vessel.speedOfSound : 340.29f;
                if(UseAirSimFilters) {
                    distance = Vector3.Distance(FlightGlobals.camera_position, transform.position);
                    speed = (float)vessel.srfSpeed;
                    var cameraToSourceVector = (CameraManager.GetCurrentCamera().transform.position - transform.position).normalized;
                    angle = Vector3.Dot(cameraToSourceVector, (transform.up + vessel.velocityD).normalized);
                }
            }

            if(Sources.Count > 0) {
                var sourceKeys = Sources.Keys.ToList();
                foreach(var source in sourceKeys) {
                    if(!Sources[source].isPlaying) {

                        // we dont want to accidentally delete the actual part
                        if(Sources[source].gameObject.name == source) {
                            UnityEngine.Object.Destroy(Sources[source].gameObject);
                        } else {
                            UnityEngine.Object.Destroy(Sources[source]);
                        }
                        
                        Sources.Remove(source);
                        spools.Remove(source);

                        if(AirSimFilters.ContainsKey(source))
                            AirSimFilters.Remove(source);
                    } else {
                        if(UseAirSimFilters) {
                            if(Settings.Instance.AirSimulation) {
                                AirSimulationFilter airSimFilter;
                                if(!AirSimFilters.ContainsKey(source)) {
                                    airSimFilter = Sources[source].gameObject.AddComponent<AirSimulationFilter>();
                                    AirSimFilters.Add(source, airSimFilter);

                                    airSimFilter.enabled = true;
                                    airSimFilter.EnableCombFilter = EnableCombFilter;
                                    airSimFilter.EnableLowpassFilter = EnableLowpassFilter;
                                    airSimFilter.EnableWaveShaperFilter = EnableWaveShaperFilter;
                                } else {
                                    airSimFilter = AirSimFilters[source];
                                }
                                airSimFilter.Distance = distance;
                                airSimFilter.Speed = speed;
                                airSimFilter.SpeedOfSound = speedOfSound;
                                airSimFilter.Angle = angle;
                                airSimFilter.VesselSize = vessel.vesselSize.magnitude;
                                airSimFilter.AtmPressure = (float)vessel.staticPressurekPa * 1000f;
                            } else {
                                if(AirSimFilters.ContainsKey(source)) {
                                    UnityEngine.Object.Destroy(AirSimFilters[source]);
                                    AirSimFilters.Remove(source);
                                }
                            }
                        }
                    }
                }
            }
        }

        float dopplerRaw = 1;
        float dopplerFactor = 0.2f;

        Vector3 pastCameraPosition = new Vector3();
        Vector3 pastSourcePosition = new Vector3();
        public void CalculateDoppler()
        {
            Vector3 sourceSpeed = (pastSourcePosition - transform.position) / TimeWarp.fixedDeltaTime;
            pastSourcePosition = transform.position;
            Vector3 listenerSpeed = (pastCameraPosition - FlightGlobals.camera_position) / TimeWarp.fixedDeltaTime;
            pastCameraPosition = FlightGlobals.camera_position;

            sourceSpeed = Vector3.ClampMagnitude(sourceSpeed, speedOfSound);
            listenerSpeed = Vector3.ClampMagnitude(listenerSpeed, speedOfSound);

            var distanceVector = ((Vector3)FlightGlobals.camera_position - transform.position);
            float listenerRelativeSpeed = Vector3.Dot(distanceVector, listenerSpeed) / distanceVector.magnitude;
            float emitterRelativeSpeed = Vector3.Dot(distanceVector, sourceSpeed) / distanceVector.magnitude;

            listenerRelativeSpeed = Mathf.Min(listenerRelativeSpeed, speedOfSound);
            emitterRelativeSpeed = Mathf.Min(emitterRelativeSpeed, speedOfSound);
            dopplerRaw = Mathf.Clamp(Mathf.Abs(speedOfSound + listenerRelativeSpeed) / (speedOfSound + emitterRelativeSpeed), 0.5f, 2f);
            dopplerRaw = (1 - dopplerFactor) + (dopplerRaw * dopplerFactor);

            Doppler = Mathf.MoveTowards(Doppler, dopplerRaw, 0.5f * TimeWarp.deltaTime);
        }

        float pitchVariation = 1;
        public void PlaySoundLayer(GameObject audioGameObject, string sourceLayerName, SoundLayer soundLayer, float rawControl, float vol, bool spoolProccess = true)
        {
            float control = rawControl;

            if(spoolProccess) {
                if(!spools.ContainsKey(sourceLayerName)) {
                    spools.Add(sourceLayerName, 0);
                }

                if(soundLayer.spool) {
                    spools[sourceLayerName] = Mathf.MoveTowards(spools[sourceLayerName], control, soundLayer.spoolSpeed * TimeWarp.deltaTime);
                    control = spools[sourceLayerName];
                } else {
                    //fix for audiosource clicks
                    spools[sourceLayerName] = Mathf.MoveTowards(spools[sourceLayerName], control, AudioUtility.SmoothControl.Evaluate(control) * (60 * Time.deltaTime));
                    control = spools[sourceLayerName];
                }
            }

            control = Mathf.Round(control * 1000.0f) * 0.001f;

            //For Looped sounds cleanup
            if(control < float.Epsilon) {
                if(Sources.ContainsKey(sourceLayerName)) {
                    Sources[sourceLayerName].Stop();
                }
                return;
            }

            AudioSource source;

            if(!Sources.ContainsKey(sourceLayerName)) {
                var go = new GameObject(sourceLayerName);
                go.transform.parent = audioGameObject.transform;
                go.transform.position = audioGameObject.transform.position;
                go.transform.rotation = audioGameObject.transform.rotation;

                source = AudioUtility.CreateSource(go, soundLayer);
                Sources.Add(sourceLayerName, source);

                if(soundLayer.pitchVariation) {
                    pitchVariation = UnityEngine.Random.Range(0.95f, 1.05f);
                }

            } else {
                source = Sources[sourceLayerName];
            }

            if(soundLayer.useFloatCurve) {
                source.volume = soundLayer.volumeFC.Evaluate(control) * GameSettings.SHIP_VOLUME * vol;
                source.pitch = soundLayer.pitchFC.Evaluate(control);
            } else {

                source.volume = soundLayer.volume.Value(control) * GameSettings.SHIP_VOLUME * vol;
                source.pitch = soundLayer.pitch.Value(control);
            }

            if(soundLayer.pitchVariation && !soundLayer.loopAtRandom) {
                source.pitch *= pitchVariation;
            }

            if(Settings.Instance.AirSimulation) {
                source.pitch *= Doppler;
            }

            AudioUtility.PlayAtChannel(source, soundLayer.channel, soundLayer.loop, soundLayer.loopAtRandom);
        }

        public void onGamePause()
        {
            if(Sources.Count > 0) {
                foreach(var source in Sources.Values) {
                    source.Pause();
                }
            }
            gamePaused = true;
        }

        public void onGameUnpause()
        {
            if(Sources.Count > 0) {
                foreach(var source in Sources.Values) {
                    source.UnPause();
                }
            }
            gamePaused = false;
        }

        public void OnDestroy()
        {
            if(!initialized)
                return;

            if(Sources.Count > 0) {
                foreach(var source in Sources.Values) {
                    source.Stop();
                    UnityEngine.Object.Destroy(source);
                }
            }

            GameEvents.onGamePause.Remove(onGamePause);
            GameEvents.onGameUnpause.Remove(onGameUnpause);
        }
    }
}
