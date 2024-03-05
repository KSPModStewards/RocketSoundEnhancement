﻿using System;
using System.Collections;
using System.Collections.Generic;
using RocketSoundEnhancement.AudioFilters;
using RocketSoundEnhancement.Unity;
using UnityEngine;

namespace RocketSoundEnhancement
{
    public class ShipEffects : VesselModule
    {
        public static AerodynamicsFX aeroFx;
        public static AerodynamicsFX AeroFX
        {
            get
            {
                if (aeroFx == null)
                    aeroFx = FindObjectOfType<AerodynamicsFX>();

                return aeroFx;
            }
        }

        public Dictionary<PhysicsControl, List<SoundLayer>> SoundLayerGroups = new Dictionary<PhysicsControl, List<SoundLayer>>();
        public Dictionary<string, AudioSource> Sources = new Dictionary<string, AudioSource>();
        public Dictionary<string, AirSimulationFilter> AirSimFilters = new Dictionary<string, AirSimulationFilter>();

        private Dictionary<string, float> volumeControls = new Dictionary<string, float>();
        private Dictionary<string, float> pitchControls = new Dictionary<string, float>();

        private GameObject audioParent;

        //  Physics Sound Effects Controls
        public float Acceleration;
        public float Jerk;
        public float ThrustToWeight;
        public float DynamicPressure;
        public float VesselMass;

        //  Air Simulation Values
        public float Distance = 0;
        public float Angle = 0;
        public float MachAngle = 90;
        public float MachPass = 1;
        public float Mach = 0;
        public Vector3 MachTipCameraNormal = new Vector3();

        public bool SonicBoomed = true;

        private bool initialized;
        private bool gamePaused;
        private bool ignoreVessel;
        private bool noPhysics;
        private bool airSimFiltersEnabled;
        private float pastAngularVelocity;
        private float pastAcceleration;

        public bool Initialize()
        {
            if (vessel == null) return false;

            if (vessel.Parts.Count <= 1)
            {
                ignoreVessel = vessel.Parts[0].Modules.Contains("ModuleAsteroid") || vessel.Parts[0].Modules.Contains("KerbalEVA");
                noPhysics = vessel.Parts[0].PhysicsSignificance == 1;
                return true;
            }

            if (ShipEffectsConfig.ShipEffectsConfigNode.Count > 0)
            {
                foreach (var node in ShipEffectsConfig.ShipEffectsConfigNode)
                {
                    if (!Enum.TryParse(node.name, true, out PhysicsControl controlGroup)) continue;
                    if (ignoreVessel && controlGroup != PhysicsControl.SONICBOOM) continue;

                    if (node.HasNode("SOUNDLAYER"))
                    {
                        var soundLayers = AudioUtility.CreateSoundLayerGroup(node.GetNodes("SOUNDLAYER"));
                        if (soundLayers.Count == 0) continue;

                        if (SoundLayerGroups.ContainsKey(controlGroup)) { SoundLayerGroups[controlGroup].AddRange(soundLayers); continue; }

                        SoundLayerGroups.Add(controlGroup, soundLayers);
                    }
                }
            }

            audioParent = new GameObject($"ShipEffects_{vessel.vesselName}");
            audioParent.transform.rotation = vessel.transform.rotation;
            audioParent.transform.position = vessel.transform.position;
            audioParent.transform.parent = vessel.transform;

            if (SoundLayerGroups.Count > 0)
            {
                foreach (var soundLayerGroup in SoundLayerGroups)
                {
                    var hasAirSimFilter = soundLayerGroup.Key != PhysicsControl.SONICBOOM;
                    StartCoroutine(SetupAudioSources(soundLayerGroup.Value, hasAirSimFilter));
                }
            }

            CacheVesselData();

            GameEvents.onVesselPartCountChanged.Add(OnVesselPartCountChanged);

            GameEvents.onGamePause.Add(OnGamePause);
            GameEvents.onGameUnpause.Add(OnGameUnpause);
            return true;
        }

        List<Part> asteroidParts = new List<Part>();
        List<ModuleEngines> engines = new List<ModuleEngines>();

        void CacheVesselData()
		{
            asteroidParts.Clear();
            engines.Clear();

            foreach (var part in vessel.parts)
            {
                foreach (var module in part.modules)
                {
                    if (module is ModuleAsteroid asteroid)
                    {
                        asteroidParts.Add(part);
                    }
                    else if (module is ModuleEngines engine)
                    {
                        engines.Add(engine);
                    }
                }
            }
        }

        private void OnVesselPartCountChanged(Vessel data)
        {
            if (data != vessel) return;

            CacheVesselData();
        }

		IEnumerator SetupAudioSources(List<SoundLayer> soundLayers, bool hasAirSimFilter = true)
        {
            foreach (var soundLayer in soundLayers)
            {
                string soundLayerName = soundLayer.name;
                if (!Sources.ContainsKey(soundLayerName))
                {
                    var sourceGameObject = new GameObject($"{AudioUtility.RSETag}_{soundLayerName}");
                    sourceGameObject.transform.parent = audioParent.transform;
                    sourceGameObject.transform.position = audioParent.transform.position;
                    sourceGameObject.transform.rotation = audioParent.transform.rotation;

                    var source = AudioUtility.CreateSource(sourceGameObject, soundLayer);
                    source.enabled = false;
                    Sources.Add(soundLayerName, source);

                    if (hasAirSimFilter)
                    {
                        var airSimFilter = sourceGameObject.AddComponent<AirSimulationFilter>();
                        airSimFilter.EnableLowpassFilter = true;
                        airSimFilter.EnableDistortionFilter = true;

                        airSimFilter.MaxDistance = Settings.AirSimMaxDistance;
                        airSimFilter.FarLowpass = Settings.AirSimFarLowpass;
                        airSimFilter.MaxDistortion = Settings.AirSimMaxDistortion;

                        AirSimFilters.Add(soundLayerName, airSimFilter);
                    }
                }
                yield return null;
            }
        }

        public void Unload()
        {
            Destroy(audioParent);
            Sources.Clear();
            AirSimFilters.Clear();
            volumeControls.Clear();
            pitchControls.Clear();

            GameEvents.onGamePause.Remove(OnGamePause);
            GameEvents.onGameUnpause.Remove(OnGameUnpause);

            asteroidParts.Clear();
            engines.Clear();
            GameEvents.onVesselPartCountChanged.Remove(OnVesselPartCountChanged);

            initialized = false;
        }

        public override void OnLoadVessel()
        {
            if (initialized) return;
            initialized = Initialize();
        }

        public override void OnUnloadVessel()
        {
            if (!initialized) return;
            Unload();
        }

        private void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || !initialized || gamePaused || noPhysics || ignoreVessel)
                return;

            if (!vessel.loaded)
            {
                Unload();
                return;
            }

            float acceleration = vessel.LandedOrSplashed ? (float)vessel.acceleration.magnitude : (float)(vessel.geeForce * PhysicsGlobals.GravitationalAcceleration);
            Acceleration = acceleration + (Mathf.Abs(pastAngularVelocity - vessel.angularVelocity.magnitude) / Time.fixedDeltaTime);
            Acceleration = Mathf.Round(Acceleration * 100) * 0.01f;
            Jerk = Mathf.Abs(pastAcceleration - Acceleration) / Time.fixedDeltaTime;
            DynamicPressure = (float)vessel.dynamicPressurekPa;

            pastAngularVelocity = vessel.angularVelocity.magnitude;
            pastAcceleration = Acceleration;

            VesselMass = (float)vessel.totalMass;
            
            foreach (var excludedPart in asteroidParts)
            {
                VesselMass -= excludedPart.mass;
            }

            if (Settings.EnableAudioEffects && Settings.MufflerQuality > AudioMufflerQuality.Normal)
            {
                bool isActiveAndInternal = vessel == FlightGlobals.ActiveVessel && InternalCamera.Instance.isActive;
                var velocityDirection = vessel.velocityD.normalized * vessel.vesselSize.magnitude;
                var positionTip = transform.position + velocityDirection;
                var positionRear = transform.position - velocityDirection;
                var vesselTip = transform.position;
                var vesselRear = transform.position;
                RaycastHit tipHit;

                if (Physics.BoxCast(positionTip, vessel.vesselSize, -vessel.velocityD.normalized, out tipHit))
                {
                    vesselTip = tipHit.point;
                }

                var cameraPosition = CameraManager.GetCurrentCamera().transform.position;

                MachTipCameraNormal = (cameraPosition - vesselTip).normalized;
                Distance = Vector3.Distance(cameraPosition, transform.position);  
                Angle = (1 + Vector3.Dot(MachTipCameraNormal, vessel.velocityD.normalized)) * 90;

                if (isActiveAndInternal)
                {
                    Angle = 0;
                    MachPass = 1;
                    return;
                }

                if (Settings.MachEffectsAmount > 0)
                {
                    Mach = (float)vessel.mach * Mathf.Clamp01((float)(vessel.staticPressurekPa * 1000) / 404.1f);
                    MachAngle = Mathf.Asin(1 / Mathf.Max(Mach, 1)) * Mathf.Rad2Deg;
                    MachPass = Mathf.Lerp(1, Settings.MachEffectLowerLimit, Mathf.Clamp01(Angle / MachAngle) * Mathf.Clamp01(Mach));
                }
                else
                {
                    Mach = 0;
                    MachAngle = 90;
                    MachPass = 1;
                }
            }
        }

        private void LateUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || !initialized || gamePaused || noPhysics || ignoreVessel)
                return;

            if (SoundLayerGroups.ContainsKey(PhysicsControl.SONICBOOM) && Settings.MachEffectsAmount > 0 && !MapView.MapCamera.isActiveAndEnabled)
            {
                if (MachPass > Settings.MachEffectLowerLimit && !SonicBoomed)
                {
                    SonicBoomed = true;
                    if (vessel.mach > 1)
                    {
                        foreach (var soundLayer in SoundLayerGroups[PhysicsControl.SONICBOOM])
                        {
                            if (vessel.crewableParts == 0 && soundLayer.channel == FXChannel.Interior)
                                continue;

                            PlaySonicBoom(soundLayer);
                        }
                    }
                }

                if (MachPass == Settings.MachEffectLowerLimit)
                {
                    SonicBoomed = false;
                }
            }

            foreach (var soundLayerGroup in SoundLayerGroups)
            {
                if (soundLayerGroup.Key == PhysicsControl.SONICBOOM) continue;

                float rawControl = GetPhysicsController(soundLayerGroup.Key);
                foreach (var soundLayer in soundLayerGroup.Value)
                {
                    if (vessel.crewableParts == 0 && soundLayer.channel == FXChannel.Interior)
                        continue;

                    PlaySoundLayer(soundLayer, rawControl);
                }
            }

            if (Sources.Count > 0)
            {
                foreach(var source in Sources.Keys)
                {
                    if (Sources[source].isPlaying || !Sources[source].enabled)
                        continue;

                    if (AirSimFilters.ContainsKey(source) && AirSimFilters[source].enabled)
                        AirSimFilters[source].enabled = false;
                    Sources[source].enabled = false;
                }
            }

            if (AirSimFilters.Count > 0 && airSimFiltersEnabled && Settings.MufflerQuality != AudioMufflerQuality.AirSim)
            {
                foreach (var filter in AirSimFilters.Values)
                {
                    filter.enabled = false;
                }
                airSimFiltersEnabled = false;
            }
        }

        public void PlaySonicBoom(SoundLayer soundLayer)
        {
            if (InternalCamera.Instance.isActive && vessel == FlightGlobals.ActiveVessel)
                return;

            PlaySoundLayer(soundLayer, Mathf.Min(Mach, 4) , false, true);
        }

        public void PlaySoundLayer(SoundLayer soundLayer, float control, bool smoothControl = true, bool isSonicBoom = false)
        {
            string soundLayerName = soundLayer.name;
            if (!Sources.ContainsKey(soundLayerName)) return;

            float finalVolume, finalPitch;
            finalVolume = soundLayer.volumeFC?.Evaluate(control) ?? soundLayer.volume.Value(control);
            finalVolume *= soundLayer.massToVolume?.Value(VesselMass) ?? 1;

            finalPitch = soundLayer.pitchFC?.Evaluate(control) ?? soundLayer.pitch.Value(control);
            finalPitch *= soundLayer.massToPitch?.Value(VesselMass) ?? 1;

            if (smoothControl)
            {
                if (!volumeControls.ContainsKey(soundLayerName)) { volumeControls.Add(soundLayerName, 0); }
                if (!pitchControls.ContainsKey(soundLayerName)) { pitchControls.Add(soundLayerName, 1); }

                volumeControls[soundLayerName] = Mathf.MoveTowards(volumeControls[soundLayerName], finalVolume, AudioUtility.SmoothControl.Evaluate(finalVolume) * (10 * Time.deltaTime));
                pitchControls[soundLayerName] = Mathf.MoveTowards(pitchControls[soundLayerName], finalPitch, AudioUtility.SmoothControl.Evaluate(finalPitch) * (10 * Time.deltaTime));
                finalVolume = volumeControls[soundLayerName];
                finalPitch = pitchControls[soundLayerName];
            }

            finalVolume = Mathf.Round(finalVolume * 1000) * 0.001f;

            if (finalVolume < float.Epsilon)
            {
                if (Sources[soundLayerName].volume == 0 && soundLayer.loop)
                {
                    Sources[soundLayerName].Stop();
                }

                if (Sources[soundLayerName].isPlaying && soundLayer.loop)
                {
                    Sources[soundLayerName].volume = 0;
                }
                return;
            }

            AudioSource source = Sources[soundLayerName];
            source.enabled = true;

            if (vessel.isActiveVessel && soundLayer.channel == FXChannel.Interior && InternalCamera.Instance.isActiveAndEnabled)
                source.transform.localPosition = InternalCamera.Instance.transform.localPosition + Vector3.back;

            if (soundLayer.channel == FXChannel.Exterior) { source.transform.position = vessel.CurrentCoM; }

            if (Settings.MufflerQuality > AudioMufflerQuality.Normal && soundLayer.channel == FXChannel.Exterior && !isSonicBoom)
            {
                if (Settings.MufflerQuality == AudioMufflerQuality.AirSim && AirSimFilters.TryGetValue(soundLayerName, out var airSimFilter))
                {
                    airSimFilter.enabled = true;
                    airSimFilter.Distance = Distance;
                    airSimFilter.Mach = Mach;
                    airSimFilter.Angle = Angle;
                    airSimFilter.MachPass = MachPass;
                    airSimFilter.MachAngle = MachAngle;
                    airSimFiltersEnabled = true;
                }
                else
                {
                    if (Settings.MachEffectsAmount > 0)
                    {
                        float machLog = Mathf.Log10(Mathf.Lerp(0.1f, 10, MachPass)) * 0.5f;
                        finalVolume *= machLog;
                    }
                }
            }

            source.volume = finalVolume * GameSettings.SHIP_VOLUME;
            source.pitch = finalPitch;

            int clipIndex = soundLayer.audioClips.Length > 1 ? UnityEngine.Random.Range(0, soundLayer.audioClips.Length) : 0;
            if (soundLayer.loop && soundLayer.audioClips != null && (source.clip == null || source.clip != soundLayer.audioClips[clipIndex]))
            {
                source.clip = soundLayer.audioClips[clipIndex];
                source.time = soundLayer.loopAtRandom ? UnityEngine.Random.Range(0, soundLayer.audioClips[clipIndex].length) : 0;
            }

            AudioUtility.PlayAtChannel(source, soundLayer.channel, vessel == FlightGlobals.ActiveVessel, soundLayer.loop, 1, soundLayer.audioClips[clipIndex]);
        }

        public float GetPhysicsController(PhysicsControl physControl)
        {
            float controller = 0;
            switch (physControl)
            {
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
                    foreach (var engine in engines)
                    {
                        totalThrust += engine.GetCurrentThrust();
                    }
                    controller = totalThrust / VesselMass;
                    ThrustToWeight = controller;
                    break;
                case PhysicsControl.REENTRYHEAT:
                    if (AeroFX != null) { controller = AeroFX.FxScalar * AeroFX.state; }
                    break;
                case PhysicsControl.None:
                    controller = 1;
                    break;
            }
            return controller;
        }

        private void OnGamePause()
        {
            foreach (var source in Sources.Values)
            {
                source.Pause();
            }
            gamePaused = true;
        }

        private void OnGameUnpause()
        {
            foreach (var source in Sources.Values)
            {
                source.UnPause();
            }
            gamePaused = false;
        }

        private void OnDestroy()
        {
            if (!initialized) return;
            foreach (var source in Sources.Values)
            {
                source.Stop();
            }
            Unload();
        }
    }
}
