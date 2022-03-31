using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RocketSoundEnhancement
{
    public class RSE_Module : PartModule
    {
        public Dictionary<string, AudioSource> Sources = new Dictionary<string, AudioSource>();
        public Dictionary<string, LowpassFilter> LPFilters = new Dictionary<string, LowpassFilter>();
        public Dictionary<string, float> spools = new Dictionary<string, float>();

        public Dictionary<string, List<SoundLayer>> SoundLayerGroups;
        public List<SoundLayer> SoundLayers;

        public GameObject audioParent;

        public bool initialized;
        public bool gamePaused;

        public float volume = 1;

        public override void OnStart(StartState state)
        {
            GameEvents.onGamePause.Add(onGamePause);
            GameEvents.onGameUnpause.Add(onGameUnpause);
        }

        [KSPField(isPersistant = false, guiActive = true)]
        float Mach = 0;
        [KSPField(isPersistant = false, guiActive = true)]
        float Doppler = 0;
        [KSPField(isPersistant = false, guiActive = true)]
        float CutOffFrequency = 0;
        [KSPField(isPersistant = false, guiActive = true)]
        float ResonanceQ = 0;

        public override void OnUpdate()
        {
            if(Settings.Instance.RealisticMuffling && LPFilters.Count > 0) {
                //Variables
                Vector3 sourceVelocity = part.vessel.GetSrfVelocity();
                float atmDensity = (float)vessel.atmDensity;
                float staticPressurePa = (float)vessel.staticPressurekPa * 1000;
                float atmTemperature = (float)vessel.atmosphericTemperature;
                float maxDistance = 1000;
                float DopplerFactor = 1f;

                float velocityRelativeDirection = Vector3.Dot(-CameraManager.GetCurrentCamera().transform.forward, sourceVelocity.normalized);
                float cameraDistance = Vector3.Distance(gameObject.transform.position, CameraManager.GetCurrentCamera().transform.position);
                float distanceInv = Mathf.Clamp(Mathf.Pow(2, -(cameraDistance / maxDistance * 10)), 0, 1);
                float speedOfSound = Mathf.Sqrt(1.4f * 286f * (atmTemperature));
                float mach = sourceVelocity.magnitude / speedOfSound;

                //Calculate Doppler (simpler way)
                //I don't know how accurate this is, but this works better. less wobbly.
                //reduce doppler if we're near the source.
                float doppler = 1 + ((velocityRelativeDirection * Mathf.Clamp(cameraDistance / 100, 0, 1)) * Mathf.Min(mach, 1));
                doppler *= DopplerFactor;

                //Emulate Air Absorption
                //To-do, mach cone
                float cutOffFreqLP = Mathf.Lerp(1000, 22200, distanceInv);
                float resonanceQ = Mathf.Lerp(0.75f, 3, distanceInv);

                //Data Display
                Mach = mach;
                Doppler = doppler;
                CutOffFrequency = cutOffFreqLP;
                ResonanceQ = resonanceQ;

                var sourcesKeys = Sources.Keys.ToList();
                foreach(var sourceKey in sourcesKeys) {
                    if(Sources[sourceKey].isPlaying) {

                        Sources[sourceKey].pitch *= Mathf.Clamp(doppler, 0.5f, 1.5f);
                        Sources[sourceKey].volume *= Mathf.Max(doppler, 1f); 

                        if(LPFilters.ContainsKey(sourceKey)) {
                            LPFilters[sourceKey].enabled = true;
                            LPFilters[sourceKey].cutoffFrequency = cutOffFreqLP;
                            LPFilters[sourceKey].lowpassResonanceQ = resonanceQ;
                        }
                    }
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
                            UnityEngine.Object.Destroy(LPFilters[source]);
                        }
                        
                        Sources.Remove(source);
                        spools.Remove(source);

                        if(LPFilters.ContainsKey(source)) {
                            LPFilters.Remove(source);
                        }
                    } else {
                        LPFilters[source].enabled = Settings.Instance.RealisticMuffling;
                    }
                }
            }
        }

        float pitchVariation = 1;
        public void PlaySoundLayer(GameObject audioGameObject, string sourceLayerName, SoundLayer soundLayer, float rawControl, float vol, bool doPitchVariation = false, bool spoolProccess = true)
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
            LowpassFilter lpFilter;

            if(!Sources.ContainsKey(sourceLayerName)) {
                var go = new GameObject(sourceLayerName);
                go.transform.parent = audioGameObject.transform;
                go.transform.position = audioGameObject.transform.position;
                go.transform.rotation = audioGameObject.transform.rotation;

                source = AudioUtility.CreateSource(go, soundLayer);
                Sources.Add(sourceLayerName, source);

                if(doPitchVariation) {
                    pitchVariation = UnityEngine.Random.Range(0.9f, 1.1f);
                }
                if(LPFilters != null) {
                    lpFilter = go.AddOrGetComponent<LowpassFilter>();
                    lpFilter.enabled = Settings.Instance.RealisticMuffling;
                    lpFilter.lowpassResonanceQ = 3;
                    lpFilter.cutoffFrequency = 22200;

                    if(LPFilters.ContainsKey(sourceLayerName)) {
                        LPFilters[sourceLayerName] = lpFilter;
                    } else {
                        LPFilters.Add(sourceLayerName, lpFilter);
                    }
                }
            } else {
                source = Sources[sourceLayerName];
            }

            source.volume = soundLayer.volume.Value(control) * GameSettings.SHIP_VOLUME * vol;
            source.pitch = soundLayer.pitch.Value(control);

            if(doPitchVariation) {
                source.pitch *= pitchVariation;
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
