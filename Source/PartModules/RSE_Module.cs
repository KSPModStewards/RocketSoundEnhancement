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
        public Dictionary<string, float> Controls = new Dictionary<string, float>();

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

        //public float MaxAirSimDistance = 2000;
        //public float FarLowpass = 1000f;
        //public float AngleHighPass = 500;
        //public float MaxCombDelay = 20;
        //public float MaxCombMix = 0.5f;
        //public float MaxDist = 0.5f;

        float speedOfSound = 340.29f;
        float distance = 0;
        public override void OnUpdate()
        {
            if(Sources.Count > 0) {
                var sourceKeys = Sources.Keys.ToList();
                if(Settings.Instance.AirSimulation) {
                    speedOfSound = vessel.speedOfSound > 0 ? (float)vessel.speedOfSound : 340.29f;
                    
                    //Calculate Air Simulation
                    if(UseAirSimFilters) {
                        Vector3 cameraToSourceNormal = (CameraManager.GetCurrentCamera().transform.position - transform.position).normalized;
                        //float speed = (float)vessel.srfSpeed;
                        //Vector3 cameraToSourceNormal = (CameraManager.GetCurrentCamera().transform.position - transform.position).normalized;
                        //float angle = Vector3.Dot(cameraToSourceNormal, (transform.up + vessel.velocityD).normalized);
                        //float vesselSize = vessel.vesselSize.magnitude;
                        //float atmPressure = (float)vessel.staticPressurekPa * 1000f;
                        //
                        //float distanceInv = Mathf.Clamp01(Mathf.Pow(2, -(distance / MaxAirSimDistance * 10)));                          //Inverse Distance
                        //float machVelocity = (speed / speedOfSound) * Mathf.Clamp01(atmPressure / 404.1f);                              //Current Mach Tapered by Pressure on Vacuum Approach.
                        //float machVelocityClamped = Mathf.Clamp01(machVelocity);
                        //float angleDegrees = (1 + angle) * 90f;                                                                         //Camera Angle
                        //float machAngle = Mathf.Asin(1 / Mathf.Max(machVelocity, 1)) * Mathf.Rad2Deg;                                   //Mach Angle
                        //float anglePos = Mathf.Clamp01((angleDegrees - machAngle) / machAngle) * Mathf.Clamp01(distance / vesselSize);  //For Highpass when the camera is at front
                        //float angleAbs = (1 - angle) * 0.5f;
                        //float machPass = 1f - Mathf.Clamp01((angleDegrees - 12.5f) / machAngle) * machVelocityClamped;                  //The Mach Cone
                        //
                        //machPass = Mathf.Clamp01(machPass / Mathf.Lerp(0.1f, 1f, Mathf.Clamp01(distance / 100)));                       //Soften Mach Cone by Distance
                        //machPass = Mathf.Lerp(1, machPass, Mathf.Clamp01(distance / vesselSize));                                       //Taper Mach Effects if Near the Vessel.

                        foreach(var source in sourceKeys) {
                            if(Sources[source].isPlaying) {
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

                                //airSimFilter.LowpassFrequency = Mathf.Lerp(FarLowpass, 22000f, distanceInv) * Mathf.Max(machPass, 0.05f);                    //Only make it quieter outside the Cone, don't make it silent.
                                //airSimFilter.HighPassFrequency = Mathf.Lerp(0, AngleHighPass * (1 + (machVelocityClamped * 2f)), anglePos);
                                //airSimFilter.CombDelay = MaxCombDelay * distanceInv;
                                //airSimFilter.CombMix = Mathf.Lerp(MaxCombMix, MaxCombMix * 0.5f * angleAbs, distanceInv);
                                //airSimFilter.Distortion = Mathf.Lerp(MaxDist, 0.7f * machVelocityClamped, distanceInv);

                                airSimFilter.Distance = distance;
                                airSimFilter.Velocity = (float)vessel.srfSpeed; 
                                airSimFilter.Angle = Vector3.Dot(cameraToSourceNormal, (transform.up + vessel.velocityD).normalized);
                                airSimFilter.VesselSize = vessel.vesselSize.magnitude;
                                airSimFilter.SpeedOfSound = speedOfSound;
                                airSimFilter.AtmosphericPressurePa = (float)vessel.staticPressurekPa * 1000f;

                                if(AudioMuffler.VacuumMuffling == 0 && vessel != FlightGlobals.ActiveVessel) {
                                    airSimFilter.LowpassFrequency *= Mathf.Clamp01((float)vessel.atmDensity);
                                }

                            } else {
                                if(AirSimFilters.ContainsKey(source)) {
                                    UnityEngine.Object.Destroy(AirSimFilters[source]);
                                    AirSimFilters.Remove(source);
                                }
                            }
                        }
                    }
                }

                foreach(var source in sourceKeys) {
                    if(AirSimFilters.ContainsKey(source) && !Settings.Instance.AirSimulation) {
                        UnityEngine.Object.Destroy(AirSimFilters[source]);
                        AirSimFilters.Remove(source);
                    }
                    if(!Sources[source].isPlaying) {

                        // we dont want to accidentally delete the actual part
                        if(Sources[source].gameObject.name == source) {
                            UnityEngine.Object.Destroy(Sources[source].gameObject);
                        } else {
                            UnityEngine.Object.Destroy(Sources[source]);
                        }
                        
                        Sources.Remove(source);
                        Controls.Remove(source);
                    }
                }
            }
        }

        public override void OnFixedUpdate()
        {
            if(Settings.Instance.AirSimulation) {
                distance = Vector3.Distance(CameraManager.GetCurrentCamera().transform.position, transform.position);
                CalculateDoppler();
            }
        }

        public float Doppler = 1;
        float dopplerRaw = 1;
        float dopplerFactor = 0.8f;

        float relativeSpeed = 0;
        float lastDistance = 0;

        public void CalculateDoppler()
        {
            relativeSpeed = (lastDistance - distance) / Time.fixedDeltaTime;
            lastDistance = distance;
            dopplerRaw = Mathf.Clamp((speedOfSound + ((relativeSpeed) * dopplerFactor)) / speedOfSound, 0.5f, 1.5f);

            Doppler = Mathf.MoveTowards(Doppler, dopplerRaw, 0.5f * Time.fixedDeltaTime);
        }

        float pitchVariation = 1;
        public void PlaySoundLayer(GameObject audioGameObject, string sourceLayerName, SoundLayer soundLayer, float rawControl, float vol, bool spoolProccess = true)
        {
            float control = rawControl;

            if(spoolProccess) {
                if(!Controls.ContainsKey(sourceLayerName)) {
                    Controls.Add(sourceLayerName, 0);
                }

                if(soundLayer.spool) {
                    Controls[sourceLayerName] = Mathf.MoveTowards(Controls[sourceLayerName], control, soundLayer.spoolSpeed * TimeWarp.deltaTime);
                    control = Controls[sourceLayerName];
                } else {
                    //fix for audiosource clicks
                    Controls[sourceLayerName] = Mathf.MoveTowards(Controls[sourceLayerName], control, AudioUtility.SmoothControl.Evaluate(control) * (60 * Time.deltaTime));
                    control = Controls[sourceLayerName];
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
