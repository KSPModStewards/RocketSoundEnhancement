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

            initialized = true;
        }


        public override void OnUpdate()
        {
            if(!initialized)
                return;

            if(Sources.Count > 0) {
                var sourceKeys = Sources.Keys.ToList();

                foreach(var source in sourceKeys) {
                    if(AirSimFilters.ContainsKey(source) && AudioMuffler.MufflerQuality != AudioMufflerQuality.AirSim) {
                        UnityEngine.Object.Destroy(AirSimFilters[source]);
                        AirSimFilters.Remove(source);
                    }

                    if(!Sources[source].isPlaying) {
                        if(AirSimFilters.ContainsKey(source)) {
                            UnityEngine.Object.Destroy(AirSimFilters[source]);
                            AirSimFilters.Remove(source);
                        }

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

        public float distance = 0;
        public float angle = 0;
        public float machPass;
        float speedOfSound = 340.29f;
        public virtual void FixedUpdate()
        {
            if(!initialized)
                return;

            if(AudioMuffler.EnableMuffling && AudioMuffler.MufflerQuality > AudioMufflerQuality.Lite) {
                distance = Vector3.Distance(CameraManager.GetCurrentCamera().transform.position, transform.position);
                speedOfSound = vessel.speedOfSound > 0 ? (float)vessel.speedOfSound : 340.29f;
                angle = (1 + Vector3.Dot(vessel.GetComponent<ShipEffects>().MachOriginCameraNormal, (transform.up + vessel.velocityD).normalized)) * 90;
                machPass = 1f - Mathf.Clamp01(angle / vessel.GetComponent<ShipEffects>().MachAngle) * Mathf.Clamp01(vessel.GetComponent<ShipEffects>().Mach);

                CalculateDoppler();
            }
        }

        public float Doppler = 1;
        public float DopplerFactor = 0.5f;
        float dopplerRaw = 1;

        float relativeSpeed = 0;
        float lastDistance = 0;

        public void CalculateDoppler()
        {
            relativeSpeed = (lastDistance - distance) / Time.fixedDeltaTime;
            lastDistance = distance;
            dopplerRaw = Mathf.Clamp((speedOfSound + ((relativeSpeed) * DopplerFactor)) / speedOfSound, 0.5f, 1.5f);

            Doppler = Mathf.MoveTowards(Doppler, dopplerRaw, 0.5f * Time.fixedDeltaTime);
        }

        float pitchVariation = 1;
        public void PlaySoundLayer(GameObject audioGameObject, string sourceLayerName, SoundLayer soundLayer, float rawControl, float vol, bool spoolProccess = true, bool oneShot = false, bool rndOneShotVol = false)
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

            float finalVolume;
            float finalPitch;

            if(soundLayer.useFloatCurve) {
                finalVolume = soundLayer.volumeFC.Evaluate(control);
                finalPitch = soundLayer.pitchFC.Evaluate(control);
            } else {
                finalVolume = soundLayer.volume.Value(control);
                finalPitch = soundLayer.pitch.Value(control);
            }

            if(soundLayer.massToVolume != null) {
                finalVolume *= soundLayer.massToVolume.Value((float)part.physicsMass);
            }

            if(soundLayer.massToPitch != null) {
                finalPitch *= soundLayer.massToPitch.Value((float)part.physicsMass);
            }

            if(soundLayer.pitchVariation && !soundLayer.loopAtRandom) {
                finalPitch *= pitchVariation;
            }

            if(AudioMuffler.MufflerQuality > AudioMufflerQuality.Lite) {
                finalPitch *= Doppler;
            }

            //For Looped sounds cleanup
            if(finalVolume < float.Epsilon) {
                if(Sources.ContainsKey(sourceLayerName)) {
                    Sources[sourceLayerName].Stop();
                }
                return;
            }

            AudioSource source;
            if(!Sources.ContainsKey(sourceLayerName)) {
                GameObject sourceGameObject = new GameObject(sourceLayerName);
                sourceGameObject.transform.parent = audioGameObject.transform;
                sourceGameObject.transform.position = audioGameObject.transform.position;
                sourceGameObject.transform.rotation = audioGameObject.transform.rotation;

                source = AudioUtility.CreateSource(sourceGameObject, soundLayer);

                Sources.Add(sourceLayerName, source);
                if(soundLayer.pitchVariation) {
                    pitchVariation = UnityEngine.Random.Range(0.95f, 1.05f);
                }

            } else {
                source = Sources[sourceLayerName];
            }

            bool processAirSim = false;
            if(AudioMuffler.EnableMuffling) {
                switch(AudioMuffler.MufflerQuality) {
                    case AudioMufflerQuality.Lite:
                        if(source.outputAudioMixerGroup != null) {
                            source.outputAudioMixerGroup = null;
                        }
                        break;
                    case AudioMufflerQuality.Full:
                        if(soundLayer.channel == FXChannel.ShipBoth) {
                            source.outputAudioMixerGroup = vessel == FlightGlobals.ActiveVessel ? RSE.Instance.FocusMixer : RSE.Instance.ExternalMixer;
                        } else {
                            source.outputAudioMixerGroup = RSE.Instance.InternalMixer;
                        }
                        break;
                    case AudioMufflerQuality.AirSim:
                        if(soundLayer.channel == FXChannel.ShipBoth) {
                            if(UseAirSimFilters) {
                                source.outputAudioMixerGroup = RSE.Instance.AirSimMixer;
                                processAirSim = true;
                            } else {
                                source.outputAudioMixerGroup = vessel == FlightGlobals.ActiveVessel ? RSE.Instance.FocusMixer : RSE.Instance.ExternalMixer;
                            }
                        } else {
                            source.outputAudioMixerGroup = RSE.Instance.InternalMixer;
                        }
                        break;
                }
            }

            if(processAirSim) {
                AirSimulationFilter airSimFilter;
                if(!AirSimFilters.ContainsKey(sourceLayerName)) {
                    airSimFilter = source.gameObject.AddComponent<AirSimulationFilter>();

                    airSimFilter.enabled = true;
                    airSimFilter.EnableCombFilter = EnableCombFilter;
                    airSimFilter.EnableLowpassFilter = EnableLowpassFilter;
                    airSimFilter.EnableWaveShaperFilter = EnableWaveShaperFilter;
                    airSimFilter.SimulationUpdate = AirSimulationUpdate.Full;

                    AirSimFilters.Add(sourceLayerName, airSimFilter);
                } else {
                    airSimFilter = AirSimFilters[sourceLayerName];
                }

                airSimFilter.Distance = distance;
                airSimFilter.MachVelocity = vessel.GetComponent<ShipEffects>().Mach;
                airSimFilter.Angle = angle;
                airSimFilter.MachAngle = vessel.GetComponent<ShipEffects>().MachAngle;
                airSimFilter.MachPass = machPass;
                airSimFilter.MaxLowpassFrequency = vessel == FlightGlobals.ActiveVessel ? RSE.Instance.FocusMufflingFrequency : RSE.Instance.MufflingFrequency;
            }

            source.volume = finalVolume * GameSettings.SHIP_VOLUME * vol;
            source.pitch = finalPitch;

            if(oneShot) {
                int index = 0;
                if(soundLayer.audioClips.Length > 1) {
                    index = UnityEngine.Random.Range(0, soundLayer.audioClips.Length);
                }
                AudioClip clip = GameDatabase.Instance.GetAudioClip(soundLayer.audioClips[index]);
                float volumeScale = rndOneShotVol ? UnityEngine.Random.Range(0.9f, 1.0f) : 1;

                AudioUtility.PlayAtChannel(source, soundLayer.channel, vessel, false, false,true, volumeScale, clip);

            } else {
                AudioUtility.PlayAtChannel(source, soundLayer.channel, vessel, soundLayer.loop, soundLayer.loopAtRandom);
            }
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
