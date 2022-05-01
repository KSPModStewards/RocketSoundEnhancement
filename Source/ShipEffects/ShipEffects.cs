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
        public Dictionary<string, AirSimulationFilter> AirSimFilters = new Dictionary<string, AirSimulationFilter>();
        Dictionary<string, float> VolumeControls = new Dictionary<string, float>();
        Dictionary<string, float> PitchControls = new Dictionary<string, float>();

        //  Physics Sound Effects Controls
        public float VesselMass;
        public float Acceleration;
        public float Jerk;
        public float ThrustToWeight;
        public float DynamicPressure;

        //  Air Simulation Values
        public float Distance;
        public float DistanceInv;
        public float Angle;
        public float AngleRear;
        public float MachAngle;
        public float MachPass;
        public float MachPassRear;
        public float Mach;
        public Vector3 MachOriginCameraNormal = new Vector3();
        public Vector3 MachRearCameraNormal = new Vector3();

        public bool SonicBoomed1;
        public bool SonicBoomed2;

        public bool initialized;
        bool gamePause;
        bool ignoreVessel;
        bool noPhysics;

        void Initialize()
        {
            SoundLayerGroups.Clear();
            Sources.Clear();
            VolumeControls.Clear();
            PitchControls.Clear();

            if(vessel.Parts.Count <= 1) {
                if(vessel.Parts[0].Modules.Contains("ModuleAsteroid") || vessel.Parts[0].Modules.Contains("KerbalEVA")) {
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

                    if(Enum.TryParse(configNode.name, true, out PhysicsControl controlGroup)) {
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

        float pastVelocity;
        float pastAngularVelocity;
        float pastAcceleration;
        public void FixedUpdate()
        {
            if(!HighLogic.LoadedSceneIsFlight || !initialized || !vessel.loaded || gamePause || noPhysics)
                return;

            Acceleration = Mathf.Abs(pastVelocity - (float)vessel.speed) / Time.fixedDeltaTime;
            Acceleration += (Mathf.Abs(pastAngularVelocity - vessel.angularVelocity.magnitude) / Time.fixedDeltaTime);
            Acceleration = Mathf.Round(Acceleration * 10) * 0.1f;
            Jerk = Mathf.Abs(pastAcceleration - Acceleration) / Time.fixedDeltaTime;
            DynamicPressure = (float)vessel.dynamicPressurekPa;

            pastVelocity = (float)vessel.speed;
            pastAngularVelocity = vessel.angularVelocity.magnitude;
            pastAcceleration = Acceleration;

            if(AudioMuffler.EnableMuffling && AudioMuffler.MufflerQuality == AudioMufflerQuality.AirSim) {
                Distance = Vector3.Distance(CameraManager.GetCurrentCamera().transform.position, transform.position);
                DistanceInv = Mathf.Clamp01(Mathf.Pow(2, -(Distance / 2500 * 10)));
                Mach = (float)vessel.mach * Mathf.Clamp01((float)(vessel.staticPressurekPa * 1000) / 404.1f);
                MachAngle = Mathf.Asin(1 / Mathf.Max(Mach, 1)) * Mathf.Rad2Deg;

                Vector3 vesselTip = transform.position;
                RaycastHit tipHit;
                if(Physics.BoxCast(transform.position + (vessel.velocityD.normalized * vessel.vesselSize.magnitude), vessel.vesselSize, -vessel.velocityD.normalized, out tipHit)) {
                    vesselTip = tipHit.point;
                }

                MachOriginCameraNormal = (CameraManager.GetCurrentCamera().transform.position - vesselTip).normalized;
                Angle = (1 + Vector3.Dot(MachOriginCameraNormal, vessel.velocityD.normalized)) * 90;
                MachPass = 1f - Mathf.Clamp01(Angle / MachAngle) * Mathf.Clamp01(Mach);

                Vector3 vesselRear = transform.position;
                RaycastHit rearHit;
                if(Physics.BoxCast(transform.position - (vessel.velocityD.normalized * vessel.vesselSize.magnitude), vessel.vesselSize, vessel.velocityD.normalized, out rearHit)) {
                    vesselRear = rearHit.point;
                }

                MachRearCameraNormal = (CameraManager.GetCurrentCamera().transform.position - vesselRear).normalized;
                AngleRear = (1 + Vector3.Dot(MachRearCameraNormal, vessel.velocityD.normalized)) * 90;
                MachPassRear = 1f - Mathf.Clamp01(AngleRear / MachAngle) * Mathf.Clamp01(Mach);

                if(vessel == FlightGlobals.ActiveVessel && (InternalCamera.Instance.isActive || MapView.MapCamera.isActiveAndEnabled)) {
                    MachPass = 1;
                    MachPassRear = 1;
                }
            }
        }

        public void PlaySonicBoom(SoundLayer soundLayer, string sourceLayerName)
        {
            if(!(InternalCamera.Instance.isActive && vessel == FlightGlobals.ActiveVessel)) {
                float vesselSize = vessel.vesselSize.sqrMagnitude;
                PlaySoundLayer(gameObject, sourceLayerName, soundLayer, 1, Mathf.Clamp01(Distance / vesselSize) * Mathf.Min(Mach, 4), false, true, true); ;
            }
        }

        Dictionary<AudioSource, float> stockSources = new Dictionary<AudioSource, float>();
        public void LateUpdate()
        {
            if(!HighLogic.LoadedSceneIsFlight || gamePause || noPhysics)
                return;

            if(vessel.loaded && !initialized && !ignoreVessel) {
                Initialize();
                return;
            }

            if(AudioMuffler.EnableMuffling && AudioMuffler.MufflerQuality > AudioMufflerQuality.Lite) {
                foreach(var part in vessel.Parts.ToList()) {
                    var sources = part.gameObject.GetComponents<AudioSource>().ToList();
                    sources.AddRange(part.gameObject.GetComponentsInChildren<AudioSource>());
                    sources = sources.Where(x => !x.name.Contains(AudioUtility.RSETag)).ToList();

                    foreach(var source in sources) {
                        if(source == null)
                            continue;
                        if(!stockSources.ContainsKey(source)) {
                            stockSources.Add(source, source.minDistance);
                        }

                        if(AudioMuffler.MufflerQuality == AudioMufflerQuality.AirSim) {
                            source.minDistance = Mathf.Lerp(0, stockSources[source], (MachPass * Mathf.Max(DistanceInv,0.1f)) * 0.5f);
                        } else {
                            if(source.minDistance != stockSources[source]) {
                                source.minDistance = stockSources[source];
                            }
                        }

                        source.outputAudioMixerGroup = vessel == FlightGlobals.ActiveVessel ? RSE.Instance.FocusMixer : RSE.Instance.ExternalMixer;
                    }
                }
                
            } else {
                if(stockSources.Count > 0) {
                    foreach(var source in stockSources.Keys.ToList()) {
                        if(source != null && source.outputAudioMixerGroup != null) {
                            source.outputAudioMixerGroup = null;
                        }
                        stockSources.Remove(source);
                    }
                }
            }

            if(ignoreVessel || SoundLayerGroups.Count() == 0)
                return;

            VesselMass = vessel.GetTotalMass();

            var excludedPart = vessel.Parts.Find(x => x.Modules.Contains("ModuleAsteroid"));
            if(excludedPart != null) {
                VesselMass -= excludedPart.mass;
            }

            foreach(var soundLayerGroup in SoundLayerGroups) {
                float rawControl = GetController(soundLayerGroup.Key);
                foreach(var soundLayer in soundLayerGroup.Value) {
                    if(vessel.crewableParts == 0 && soundLayer.channel == FXChannel.ShipInternal)
                        continue;

                    string sourceLayerName = soundLayerGroup.Key.ToString() + "_" + soundLayer.name;

                    if(soundLayerGroup.Key == PhysicsControl.SONICBOOM) {
                        if(AudioMuffler.EnableMuffling && AudioMuffler.MufflerQuality == AudioMufflerQuality.AirSim) {
                            if(vessel.atmDensity > 0) {

                                if(vessel.mach > 1) {
                                    if(MachPass > 0 && !SonicBoomed1) {
                                        SonicBoomed1 = true;
                                        PlaySonicBoom(soundLayer, sourceLayerName);
                                    }
                                    //Rear Mach Cone
                                    if(MachPassRear > 0.0 && !SonicBoomed2) {
                                        SonicBoomed2 = true;
                                        PlaySonicBoom(soundLayer, sourceLayerName);
                                    }
                                }

                                if(MachPass == 0) {
                                    SonicBoomed1 = false;
                                    SonicBoomed2 = false;
                                }
                            } else {
                                SonicBoomed1 = false;
                                SonicBoomed2 = false;
                            }
                        }
                    } else {
                        PlaySoundLayer(gameObject, sourceLayerName, soundLayer, rawControl);
                    }
                }
            }

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

                        UnityEngine.Object.Destroy(Sources[source].gameObject);
                        Sources.Remove(source);
                        VolumeControls.Remove(source);
                    }
                }
            }
        }

        public void PlaySoundLayer(GameObject audioGameObject, string sourceLayerName, SoundLayer soundLayer, float control,float volumeScale = 1, bool smoothControl = true, bool oneShot = false, bool AirSimBasic = false)
        {
            float finalVolume = 1;
            float finalPitch = 1;

            if(soundLayer.useFloatCurve) {
                finalVolume = soundLayer.volumeFC.Evaluate(control);
                finalPitch = soundLayer.pitchFC.Evaluate(control);
            } else {
                finalVolume = soundLayer.volume.Value(control);
                finalPitch = soundLayer.pitch.Value(control);
            }

            if(soundLayer.massToVolume != null) {
                finalVolume *= soundLayer.massToVolume.Value(VesselMass);
            }

            if(soundLayer.massToPitch != null) {
                finalPitch *= soundLayer.massToPitch.Value(VesselMass);
            }

            if(soundLayer.distance != null) {
                finalVolume *= soundLayer.distance.Value(Distance);
            }

            if(smoothControl) {
                if(!VolumeControls.ContainsKey(sourceLayerName)) {
                    VolumeControls.Add(sourceLayerName, 0);
                }
                if(!PitchControls.ContainsKey(sourceLayerName)) {
                    PitchControls.Add(sourceLayerName, 1);
                }

                VolumeControls[sourceLayerName] = Mathf.MoveTowards(VolumeControls[sourceLayerName], finalVolume, AudioUtility.SmoothControl.Evaluate(finalVolume) * (10 * Time.deltaTime));
                PitchControls[sourceLayerName] = Mathf.MoveTowards(PitchControls[sourceLayerName], finalPitch, AudioUtility.SmoothControl.Evaluate(finalPitch) * (10 * Time.deltaTime));
                finalVolume = VolumeControls[sourceLayerName];
                finalPitch = PitchControls[sourceLayerName];
            }

            finalVolume = Mathf.Round(finalVolume * 1000) * 0.001f;
            
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
                sourceGameObject.transform.position = vessel.CurrentCoM;
                sourceGameObject.transform.rotation = audioGameObject.transform.rotation;
                source = AudioUtility.CreateSource(sourceGameObject, soundLayer);
                Sources.Add(sourceLayerName, source);
            } else {
                source = Sources[sourceLayerName];
            }

            if(vessel == FlightGlobals.ActiveVessel && soundLayer.channel == FXChannel.ShipInternal && InternalCamera.Instance.isActiveAndEnabled) {
                source.transform.localPosition = InternalCamera.Instance.transform.localPosition + Vector3.back;
            }

            if(soundLayer.channel == FXChannel.ShipBoth) {
                source.transform.position = vessel.CurrentCoM;
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
                            source.outputAudioMixerGroup = vessel.isActiveVessel ? RSE.Instance.FocusMixer : RSE.Instance.ExternalMixer;
                        } else {
                            source.outputAudioMixerGroup = RSE.Instance.InternalMixer;
                        }
                        break;
                    case AudioMufflerQuality.AirSim:
                        if(soundLayer.channel == FXChannel.ShipBoth) {
                            if(AirSimBasic) {
                                source.outputAudioMixerGroup = vessel.isActiveVessel ? RSE.Instance.FocusMixer : RSE.Instance.ExternalMixer;
                            } else {
                                source.outputAudioMixerGroup = RSE.Instance.AirSimMixer;
                            }
                            processAirSim = true;
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
                    airSimFilter.EnableLowpassFilter = true;
                    airSimFilter.EnableWaveShaperFilter = !AirSimBasic;
                    airSimFilter.SimulationUpdate = AirSimBasic ? AirSimulationUpdate.Basic : AirSimulationUpdate.Full;

                    if(AirSimBasic) {
                        airSimFilter.FarLowpass = 5000;
                    }

                    AirSimFilters.Add(sourceLayerName, airSimFilter);
                } else {
                    airSimFilter = AirSimFilters[sourceLayerName];
                }

                if(AirSimBasic) {
                    airSimFilter.Distance = Distance;
                } else {
                    airSimFilter.Distance = Distance;
                    airSimFilter.Mach = Mach;
                    airSimFilter.Angle = Angle;
                    airSimFilter.MachPass = MachPass;
                    airSimFilter.MachAngle = Angle;
                    airSimFilter.MaxLowpassFrequency = vessel.isActiveVessel ? RSE.Instance.FocusMufflingFrequency : RSE.Instance.MufflingFrequency;
                }
            }

            source.volume = finalVolume * volumeScale * GameSettings.SHIP_VOLUME;
            source.pitch = finalPitch;

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

        public float GetController(PhysicsControl physControl)
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
                    controller = vessel.Landed ? (float)vessel.srf_velocity.magnitude : 0;
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
                        controller = (totalThrust * 1000) / (VesselMass * 1000);
                        ThrustToWeight = controller;
                        break;
                    }
                    controller = 0;
                    ThrustToWeight = 0;
                    break;
                case PhysicsControl.REENTRYHEAT:
                    if(RSE.Instance.AeroFX != null) {
                        controller = RSE.Instance.AeroFX.FxScalar * RSE.Instance.AeroFX.state;
                    }
                    break;
                case PhysicsControl.None:
                    controller = 1;
                    break;
            }
            return controller;
        }
    }
}
