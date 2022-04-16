using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace RocketSoundEnhancement
{
    public class ShipEffects : VesselModule
    {
        public Dictionary<PhysicsControl, List<SoundLayer>> SoundLayerGroups = new Dictionary<PhysicsControl, List<SoundLayer>>();
        public Dictionary<string, AudioSource> Sources = new Dictionary<string, AudioSource>();
        Dictionary<string, float> Controls = new Dictionary<string, float>();

        public float TotalMass;
        public float DryMass;
        public float Acceleration;
        public float Jerk;
        public float ThrustAccel;
        public float DynamicPressure;
        public float MachAngle;
        public float MachPass;
        public float SpeedOfSound = 340.29f;
        public Vector3 MachOriginCameraNormal = new Vector3();
        public bool SonicBoomed;
        float pastAcceleration;

        public bool initialized;
        bool gamePause;
        bool ignoreVessel;
        bool noPhysics;

        void Initialize()
        {
            SoundLayerGroups.Clear();
            Sources.Clear();
            Controls.Clear();

            if(vessel.Parts.Count <= 1) {
                if(vessel.Parts[0].PhysicsSignificance == 1 || vessel.Parts[0].Modules.Contains("ModuleAsteroid") || vessel.Parts[0].Modules.Contains("KerbalEVA")) {
                    ignoreVessel = true;

                    if(vessel.Parts[0].PhysicsSignificance == 1) {
                        noPhysics = true;
                        initialized = true;
                        return;
                    }
                }
            }

            if(Settings.Instance.ShipEffectsNodes().Count > 0) {
                foreach(var configNode in Settings.Instance.ShipEffectsNodes()) {
                    PhysicsControl controlGroup;

                    if(PhysicsControl.TryParse(configNode.name, true, out controlGroup)) {
                        if(ignoreVessel && controlGroup != PhysicsControl.SONICBOOM)
                            continue;

                        if(configNode.HasNode("SOUNDLAYER")) {
                            var soundLayers = AudioUtility.CreateSoundLayerGroup(configNode.GetNodes("SOUNDLAYER"));
                            if(!SoundLayerGroups.ContainsKey(controlGroup)) {
                                SoundLayerGroups.Add(controlGroup, soundLayers);
                            } else {
                                SoundLayerGroups[controlGroup] = soundLayers;
                            }
                        }
                    }
                }
            }

            initialized = true;
        }

        void onGamePause()
        {
            gamePause = true;
            if(Sources.Count > 0) {
                foreach(var source in Sources.Values) {
                    if(source.isPlaying) {
                        source.Pause();
                    }
                }
            }
        }
        void onGameUnpause()
        {
            gamePause = false;
            if(Sources.Count > 0) {
                foreach(var source in Sources.Values) {
                    if(source.isPlaying) {
                        source.UnPause();
                    }
                }
            }
        }

        public override void OnStart()
        {
            if(!HighLogic.LoadedSceneIsFlight || !vessel.loaded)
                return;

            Initialize();

            GameEvents.onGamePause.Add(onGamePause);
            GameEvents.onGameUnpause.Add(onGameUnpause);
        }

        public override void OnUnloadVessel()
        {
            if(Sources.Count > 0) {
                foreach(var source in Sources.Values) {
                    source.Stop();
                    UnityEngine.Object.Destroy(source);
                }
                Sources.Clear();
            }
        }

        void OnDestroy()
        {
            if(initialized) {
                if(Sources.Count > 0) {
                    foreach(var source in Sources.Values) {
                        source.Stop();
                        UnityEngine.Object.Destroy(source);
                    }
                }
            }

            GameEvents.onGamePause.Remove(onGamePause);
            GameEvents.onGameUnpause.Remove(onGameUnpause);
        }

        int timeOut;
        public void FixedUpdate()
        {
            if(!HighLogic.LoadedSceneIsFlight || !initialized || !vessel.loaded || gamePause || noPhysics)
                return;

            Acceleration = (float)vessel.geeForce * 9.81f;
            Jerk = Mathf.Abs(pastAcceleration - Acceleration) / Time.fixedDeltaTime;
            pastAcceleration = Acceleration;
            DynamicPressure = (float)vessel.dynamicPressurekPa;

            if(AudioMuffler.EnableMuffling && AudioMuffler.AirSimulation) {
                
                SpeedOfSound = vessel.speedOfSound > 0 ? (float)vessel.speedOfSound : 340.29f;
                Vector3 vesselTip = transform.position;
                RaycastHit tipHit;
                if(Physics.BoxCast(transform.position + (vessel.velocityD.normalized * vessel.vesselSize.magnitude), vessel.vesselSize, -vessel.velocityD.normalized, out tipHit)) {
                    vesselTip = tipHit.point;
                }

                MachOriginCameraNormal = (CameraManager.GetCurrentCamera().transform.position - vesselTip).normalized;
                float machVelocity = ((float)vessel.srfSpeed / SpeedOfSound) * Mathf.Clamp01((float)(vessel.staticPressurekPa * 1000) / 404.1f);
                float angle = (1 + Vector3.Dot(MachOriginCameraNormal, vessel.velocityD.normalized)) * 90;
                MachAngle = Mathf.Asin(1 / Mathf.Max(machVelocity, 1)) * Mathf.Rad2Deg;
                MachPass = 1f - Mathf.Clamp01(angle / MachAngle);

                if(vessel.atmDensity > 0) {
                    if(vessel.srfSpeed > SpeedOfSound && MachPass > 0 && !SonicBoomed) {
                        SonicBoomed = true;

                        if(SoundLayerGroups.ContainsKey(PhysicsControl.SONICBOOM)) {
                            if(!(InternalCamera.Instance.isActive && vessel == FlightGlobals.ActiveVessel)) {
                                foreach(var soundLayer in SoundLayerGroups[PhysicsControl.SONICBOOM]) {
                                    string sourceLayerName = PhysicsControl.SONICBOOM.ToString() + "_" + soundLayer.name;
                                    PlaySoundLayer(gameObject, sourceLayerName, soundLayer, Mathf.Min(machVelocity, 10), false, true);
                                }
                            }
                        }
                    }

                    if(MachPass == 0) {
                        SonicBoomed = false;
                    }
                } else {
                    SonicBoomed = false;
                }
            }
        }

        List<AudioSource> stockSources = new List<AudioSource>();
        public void LateUpdate()
        {
            if(!HighLogic.LoadedSceneIsFlight || !initialized || gamePause || noPhysics)
                return;

            if(AudioMuffler.EnableMuffling && AudioMuffler.AirSimulation) {
                foreach(var part in vessel.Parts.ToList()) {
                    var sources = part.gameObject.GetComponents<AudioSource>().ToList();
                    sources.AddRange(part.gameObject.GetComponentsInChildren<AudioSource>());
                    sources = sources.Where(x => !x.name.Contains(AudioUtility.RSETag)).ToList();

                    foreach(var source in sources) {
                        if(source == null)
                            continue;
                        if(!stockSources.Contains(source))
                            stockSources.Add(source);

                        source.outputAudioMixerGroup = vessel == FlightGlobals.ActiveVessel ? RSE.Instance.FocusMixer : RSE.Instance.ExternalMixer;
                    }
                }
            } else {
                if(stockSources.Count > 0) {
                    foreach(var source in stockSources.ToList()) {
                        if(source != null && source.outputAudioMixerGroup != null) {
                            source.outputAudioMixerGroup = null;
                        }
                        stockSources.Remove(source);
                    }
                }
            }
        }

        public void Update()
        {
            if(vessel.loaded && !initialized && !ignoreVessel) {
                Initialize();
                return;
            }

            if(!HighLogic.LoadedSceneIsFlight || !initialized || gamePause || noPhysics)
                return;

            if(ignoreVessel || SoundLayerGroups.Count() == 0)
                return;

            if(timeOut != 60) {
                timeOut++;
                return;
            }

            TotalMass = vessel.GetTotalMass();
            DryMass = vessel.Parts.Sum(x => x.prefabMass);

            var excludedPart = vessel.Parts.Find(x => x.Modules.Contains("ModuleAsteroid"));
            if(excludedPart != null) {
                TotalMass -= excludedPart.mass;
                DryMass -= excludedPart.prefabMass;
            }

            foreach(var soundLayerGroup in SoundLayerGroups) {
                if(soundLayerGroup.Key == PhysicsControl.SONICBOOM)
                    continue;

                float rawControl = GetController(soundLayerGroup.Key);
                foreach(var soundLayer in soundLayerGroup.Value) {
                    string sourceLayerName = soundLayerGroup.Key.ToString() + "_" + soundLayer.name;

                    PlaySoundLayer(gameObject, sourceLayerName, soundLayer, rawControl);
                }
            }

            if(Sources.Count > 0) {
                var sourceKeys = Sources.Keys.ToList();
                foreach(var source in sourceKeys) {
                    if(!Sources[source].isPlaying) {
                        UnityEngine.Object.Destroy(Sources[source].gameObject);
                        Sources.Remove(source);
                        Controls.Remove(source);
                    }
                }
            }
        }

        public void PlaySoundLayer(GameObject audioGameObject, string sourceLayerName, SoundLayer soundLayer, float rawControl, bool smoothControl = true, bool oneShot = false)
        {
            float control = rawControl;

            if(smoothControl) {
                if(!Controls.ContainsKey(sourceLayerName)) {
                    Controls.Add(sourceLayerName, 0);
                }
                Controls[sourceLayerName] = Mathf.MoveTowards(Controls[sourceLayerName], control, Mathf.Max(control, 20) * TimeWarp.deltaTime);
                control = Controls[sourceLayerName];
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
                GameObject sourceGameObject = new GameObject(sourceLayerName);
                sourceGameObject.transform.parent = audioGameObject.transform;
                sourceGameObject.transform.position = audioGameObject.transform.position;
                sourceGameObject.transform.rotation = audioGameObject.transform.rotation;

                source = AudioUtility.CreateSource(sourceGameObject, soundLayer);

                Sources.Add(sourceLayerName, source);

            } else {
                source = Sources[sourceLayerName];
            }

            if(AudioMuffler.AirSimulation) {
                if(soundLayer.channel == FXChannel.ShipInternal && vessel == FlightGlobals.ActiveVessel) {
                    source.outputAudioMixerGroup = RSE.Instance.InternalMixer;
                } else {
                    source.outputAudioMixerGroup = vessel == FlightGlobals.ActiveVessel ? RSE.Instance.FocusMixer : RSE.Instance.ExternalMixer;
                }
            } else {
                if(source.outputAudioMixerGroup != null)
                    source.outputAudioMixerGroup = null;
            }

            if(soundLayer.useFloatCurve) {
                source.volume = soundLayer.volumeFC.Evaluate(control) * GameSettings.SHIP_VOLUME;
                source.pitch = soundLayer.pitchFC.Evaluate(control);
            } else {
                source.volume = soundLayer.volume.Value(control) * GameSettings.SHIP_VOLUME;
                source.pitch = soundLayer.pitch.Value(control);
            }

            if(soundLayer.massToVolume != null) {
                source.volume *= soundLayer.massToVolume.Value(TotalMass);
            }

            if(soundLayer.massToPitch != null) {
                source.pitch *= soundLayer.massToPitch.Value(TotalMass);
            }

            if(oneShot) {
                int index = 0;
                if(soundLayer.audioClips.Length > 1) {
                    index = UnityEngine.Random.Range(0, soundLayer.audioClips.Length);
                }
                AudioClip clip = GameDatabase.Instance.GetAudioClip(soundLayer.audioClips[index]);
                AudioUtility.PlayAtChannel(source, soundLayer.channel, vessel, false, false, true, 1, clip);
            } else {
                AudioUtility.PlayAtChannel(source, soundLayer.channel, vessel, soundLayer.loop, soundLayer.loopAtRandom);
            }
        }

        float GetController(PhysicsControl physControl)
        {
            float controller = 0;
            switch(physControl) {
                case PhysicsControl.ACCELERATION:
                    controller = Acceleration;
                    break;
                case PhysicsControl.JERK:
                    controller = Jerk;
                    break;
                case PhysicsControl.AIRSPEED:
                    controller = (float)vessel.indicatedAirSpeed;
                    break;
                case PhysicsControl.GROUNDSPEED:
                    if(vessel.Landed)
                        controller = (float)vessel.srf_velocity.magnitude;
                    break;
                case PhysicsControl.DYNAMICPRESSURE:
                    controller = (float)vessel.dynamicPressurekPa;
                    break;
                case PhysicsControl.THRUST:
                    float totalThrust = 0;
                    var engines = vessel.parts.Where(x => x.GetComponent<ModuleEngines>());
                    if(engines.Count() > 0) {
                        foreach(var engine in engines) {
                            var module = engine.GetComponent<ModuleEngines>();
                            if(module.EngineIgnited) {
                                totalThrust += module.GetCurrentThrust();
                            }
                        }
                        controller = (totalThrust * 1000) / (vessel.GetTotalMass() * 1000);
                        ThrustAccel = controller;
                        break;
                    }
                    controller = 0;
                    ThrustAccel = 0;
                    break;
                case PhysicsControl.SONICBOOM:
                    break;
                case PhysicsControl.None:
                    controller = 1;
                    break;

            }
            return controller;
        }
    }
}
