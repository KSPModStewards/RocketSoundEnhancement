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
        public float Angle;
        public float AngleRear;
        public float MachAngle;
        public float MachPass;
        public float MachPassRear;
        public float Mach;
        public Vector3 MachOriginCameraNormal = new Vector3();
        public Vector3 MachRearCameraNormal = new Vector3();

        public bool SonicBoomTip;
        public bool SonicBoomedRear;

        public bool initialized;
        bool gamePaused;
        bool ignoreVessel;
        bool noPhysics;
        float pastAngularVelocity;
        float pastAcceleration;

        void Initialize()
        {
            if(Sources.Count() > 0){
                Sources.Where(x => x.Value != null).ToList().ForEach(x => UnityEngine.Object.Destroy(x.Value));
            }
            Sources.Clear();
            SoundLayerGroups.Clear();
            VolumeControls.Clear();
            PitchControls.Clear();

            if (vessel.Parts.Count <= 1)
            {
                ignoreVessel = vessel.Parts[0].Modules.Contains("ModuleAsteroid") || vessel.Parts[0].Modules.Contains("KerbalEVA");
                noPhysics = vessel.Parts[0].PhysicsSignificance == 1;
                initialized = true;
                return;
            }

            if (Settings.Instance.ShipEffectsNodes().Count > 0)
            {
                foreach (var node in Settings.Instance.ShipEffectsNodes())
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

            initialized = true;
        }


        public override void OnStart()
        {
            if (!HighLogic.LoadedSceneIsFlight || !vessel.loaded)
                return;

            Initialize();

            GameEvents.onGamePause.Add(onGamePause);
            GameEvents.onGameUnpause.Add(onGameUnpause);
        }

        public override void OnUnloadVessel()
        {
            if (Sources.Count > 0)
            {
                foreach (var source in Sources.Values)
                {
                    source.Stop();
                    UnityEngine.Object.Destroy(source.gameObject);
                }
                Sources.Clear();
            }
        }

        public void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || !initialized || !vessel.loaded || gamePaused || noPhysics)
                return;

            Acceleration = (float)vessel.acceleration_immediate.magnitude + (Mathf.Abs(pastAngularVelocity - vessel.angularVelocity.magnitude) / Time.fixedDeltaTime);
            Acceleration = Mathf.Round(Acceleration * 100) * 0.01f;
            Jerk = Mathf.Abs(pastAcceleration - Acceleration) / Time.fixedDeltaTime;
            DynamicPressure = (float)vessel.dynamicPressurekPa;

            pastAngularVelocity = vessel.angularVelocity.magnitude;
            pastAcceleration = Acceleration;

            if (AudioMuffler.EnableMuffling && AudioMuffler.MufflerQuality == AudioMufflerQuality.AirSim)
            {
                Distance = Vector3.Distance(CameraManager.GetCurrentCamera().transform.position, transform.position);
                Mach = (float)vessel.mach * Mathf.Clamp01((float)(vessel.staticPressurekPa * 1000) / 404.1f);
                MachAngle = Mathf.Asin(1 / Mathf.Max(Mach, 1)) * Mathf.Rad2Deg;

                Vector3 vesselTip = transform.position;
                RaycastHit tipHit;
                if (Physics.BoxCast(transform.position + (vessel.velocityD.normalized * vessel.vesselSize.magnitude), vessel.vesselSize, -vessel.velocityD.normalized, out tipHit))
                {
                    vesselTip = tipHit.point;
                }

                MachOriginCameraNormal = (CameraManager.GetCurrentCamera().transform.position - vesselTip).normalized;
                Angle = (1 + Vector3.Dot(MachOriginCameraNormal, vessel.velocityD.normalized)) * 90;
                MachPass = 1f - Mathf.Clamp01(Angle / MachAngle) * Mathf.Clamp01(Mach);

                Vector3 vesselRear = transform.position;
                RaycastHit rearHit;
                if (Physics.BoxCast(transform.position - (vessel.velocityD.normalized * vessel.vesselSize.magnitude), vessel.vesselSize, vessel.velocityD.normalized, out rearHit))
                {
                    vesselRear = rearHit.point;
                }

                MachRearCameraNormal = (CameraManager.GetCurrentCamera().transform.position - vesselRear).normalized;
                AngleRear = (1 + Vector3.Dot(MachRearCameraNormal, vessel.velocityD.normalized)) * 90;
                MachPassRear = 1f - Mathf.Clamp01(AngleRear / MachAngle) * Mathf.Clamp01(Mach);

                if (vessel == FlightGlobals.ActiveVessel && (InternalCamera.Instance.isActive || MapView.MapCamera.isActiveAndEnabled))
                {
                    MachPass = 1;
                    MachPassRear = 1;
                }
            }
        }

        public void PlaySonicBoom(SoundLayer soundLayer, string sourceLayerName)
        {
            if (!(InternalCamera.Instance.isActive && vessel == FlightGlobals.ActiveVessel))
            {
                float vesselSize = vessel.vesselSize.sqrMagnitude;
                PlaySoundLayer(sourceLayerName, soundLayer, 1, Mathf.Clamp01(Distance / vesselSize) * Mathf.Min(Mach, 4), false, true, true); ;
            }
        }

        Dictionary<AudioSource, float> stockSources = new Dictionary<AudioSource, float>();
        public void LateUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || gamePaused || noPhysics || !vessel.loaded)
                return;

            if (vessel.loaded && !initialized && !ignoreVessel)
            {
                Initialize();
                return;
            }

            if (AudioMuffler.EnableMuffling && AudioMuffler.MufflerQuality > AudioMufflerQuality.Lite)
            {
                foreach (var part in vessel.parts)
                {
                    var partSources = part.gameObject.GetComponents<AudioSource>().ToList();
                    partSources.AddRange(part.gameObject.GetComponentsInChildren<AudioSource>());
                    partSources = partSources.Where(x => !x.name.Contains(AudioUtility.RSETag)).ToList();

                    foreach (var source in partSources)
                    {
                        if (source == null)
                            continue;

                        if (!stockSources.ContainsKey(source))
                        {
                            stockSources.Add(source, source.minDistance);
                        }

                        source.outputAudioMixerGroup = AudioUtility.GetMixerGroup(FXChannel.Exterior, vessel.isActiveVessel);

                        if (AudioMuffler.MufflerQuality == AudioMufflerQuality.AirSim)
                        {
                            float sourceDistance = Vector3.Distance(CameraManager.GetCurrentCamera().transform.position, source.transform.position);
                            float distanceAttenuation = Mathf.Max(Mathf.Pow(1 - Mathf.Clamp01(sourceDistance / AudioMuffler.AirSimMaxDistance), 10), 0.1f) * MachPass;
                            source.minDistance = stockSources[source] * distanceAttenuation;
                            continue;
                        }

                        if (source.minDistance != stockSources[source]) { source.minDistance = stockSources[source]; }
                    }

                }
            }
            else
            {
                if (stockSources.Count > 0)
                {
                    foreach (var source in stockSources.Keys.ToList())
                    {
                        source.outputAudioMixerGroup = null;
                        stockSources.Remove(source);
                    }
                }
            }

            if (ignoreVessel || SoundLayerGroups.Count() == 0)
                return;

            VesselMass = vessel.GetTotalMass();
            var excludedPart = vessel.Parts.Find(x => x.Modules.Contains("ModuleAsteroid"));
            if (excludedPart != null)
            {
                VesselMass -= excludedPart.mass;
            }

            foreach (var soundLayerGroup in SoundLayerGroups)
            {
                float rawControl = GetController(soundLayerGroup.Key);
                foreach (var soundLayer in soundLayerGroup.Value)
                {
                    if (vessel.crewableParts == 0 && soundLayer.channel == FXChannel.Interior)
                        continue;

                    string sourceLayerName = soundLayerGroup.Key.ToString() + "_" + soundLayer.name;
                    if (soundLayerGroup.Key == PhysicsControl.SONICBOOM)
                    {
                        if (!AudioMuffler.EnableMuffling || AudioMuffler.MufflerQuality < AudioMufflerQuality.AirSim)
                            continue;

                        if (MachPass > 0.0 && !SonicBoomTip)
                        {
                            SonicBoomTip = true;
                            if (vessel.mach > 1) PlaySonicBoom(soundLayer, sourceLayerName);
                        }

                        if (MachPassRear > 0.0 && !SonicBoomedRear)
                        {
                            SonicBoomedRear = true;
                            if (vessel.mach > 1) PlaySonicBoom(soundLayer, sourceLayerName);
                        }

                        if (MachPass == 0)
                        {
                            SonicBoomTip = false;
                            SonicBoomedRear = false;
                        }

                        continue;
                    }

                    PlaySoundLayer(sourceLayerName, soundLayer, rawControl);
                }
            }

            if (Sources.Count == 0)
                return;

            var sourceKeys = Sources.Keys.ToList();
            foreach (var source in sourceKeys)
            {
                if (AirSimFilters.ContainsKey(source) && AudioMuffler.MufflerQuality != AudioMufflerQuality.AirSim)
                {
                    UnityEngine.Object.Destroy(AirSimFilters[source]);
                    AirSimFilters.Remove(source);
                }

                if (Sources[source].isPlaying)
                    continue;

                UnityEngine.Object.Destroy(Sources[source].gameObject);

                if (AirSimFilters.ContainsKey(source)) { AirSimFilters.Remove(source); }
                Sources.Remove(source);
                VolumeControls.Remove(source);
                PitchControls.Remove(source);
            }
        }

        public void PlaySoundLayer(string sourceLayerName, SoundLayer soundLayer, float control, float volumeScale = 1, bool smoothControl = true, bool oneShot = false, bool AirSimBasic = false)
        {
            float finalVolume, finalPitch;

            finalVolume = soundLayer.volumeFC != null ? soundLayer.volumeFC.Evaluate(control) : soundLayer.volume.Value(control);
            finalPitch = soundLayer.pitchFC != null ? soundLayer.pitchFC.Evaluate(control) : soundLayer.pitch.Value(control);

            finalVolume *= soundLayer.massToVolume?.Value(VesselMass) ?? 1;
            finalPitch *= soundLayer.massToPitch?.Value(VesselMass) ?? 1;

            if (smoothControl)
            {
                if (!VolumeControls.ContainsKey(sourceLayerName)) { VolumeControls.Add(sourceLayerName, 0); }
                if (!PitchControls.ContainsKey(sourceLayerName)) { PitchControls.Add(sourceLayerName, 1); }

                VolumeControls[sourceLayerName] = Mathf.MoveTowards(VolumeControls[sourceLayerName], finalVolume, AudioUtility.SmoothControl.Evaluate(finalVolume) * (10 * Time.deltaTime));
                PitchControls[sourceLayerName] = Mathf.MoveTowards(PitchControls[sourceLayerName], finalPitch, AudioUtility.SmoothControl.Evaluate(finalPitch) * (10 * Time.deltaTime));
                finalVolume = VolumeControls[sourceLayerName];
                finalPitch = PitchControls[sourceLayerName];
            }

            finalVolume = Mathf.Round(finalVolume * 1000) * 0.001f;

            if (finalVolume < float.Epsilon)
            {
                if (Sources.ContainsKey(sourceLayerName))
                {
                    Sources[sourceLayerName].Stop();
                }
                return;
            }

            AudioSource source = Sources.ContainsKey(sourceLayerName) ? Sources[sourceLayerName] : null;
            if (!source)
            {
                GameObject sourceGameObject = new GameObject(sourceLayerName);
                sourceGameObject.transform.parent = gameObject.transform;
                sourceGameObject.transform.position = vessel.CurrentCoM;
                sourceGameObject.transform.rotation = gameObject.transform.rotation;
                source = AudioUtility.CreateSource(sourceGameObject, soundLayer);
                Sources.Add(sourceLayerName, source);

                source.time = soundLayer.loopAtRandom ? UnityEngine.Random.Range(0, source.clip.length / 2) : 0;
            }

            if (vessel.isActiveVessel && soundLayer.channel == FXChannel.Interior && InternalCamera.Instance.isActiveAndEnabled)
            {
                source.transform.localPosition = InternalCamera.Instance.transform.localPosition + Vector3.back;
            }

            if (soundLayer.channel == FXChannel.Exterior) { source.transform.position = vessel.CurrentCoM; }

            if (AudioMuffler.MufflerQuality == AudioMufflerQuality.AirSim && soundLayer.channel == FXChannel.Exterior)
            {
                AirSimulationFilter airSimFilter = AirSimFilters.ContainsKey(sourceLayerName) ? AirSimFilters[sourceLayerName] : null;
                if (!airSimFilter)
                {
                    airSimFilter = source.gameObject.AddComponent<AirSimulationFilter>();
                    airSimFilter.enabled = true;
                    airSimFilter.EnableLowpassFilter = true;
                    airSimFilter.EnableWaveShaperFilter = true;
                    airSimFilter.MaxDistortion = 0.75f;
                    airSimFilter.SimulationUpdate = AirSimBasic ? AirSimulationUpdate.Basic : AirSimulationUpdate.Full;

                    AirSimFilters.Add(sourceLayerName, airSimFilter);
                }

                airSimFilter.Distance = Distance;
                airSimFilter.Mach = Mach;
                airSimFilter.Angle = Angle;
                airSimFilter.MachPass = MachPass;
                airSimFilter.MachAngle = Angle;
            }

            source.volume = finalVolume * volumeScale * GameSettings.SHIP_VOLUME;
            source.pitch = finalPitch;

            int index = UnityEngine.Random.Range(0, soundLayer.audioClips.Length);
            var clip = GameDatabase.Instance.GetAudioClip(soundLayer.audioClips[index]);

            AudioUtility.PlayAtChannel(source, soundLayer.channel, vessel == FlightGlobals.ActiveVessel, soundLayer.loop, oneShot, 1, clip);
        }

        public float GetController(PhysicsControl physControl)
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
                    var engines = vessel.parts.Where(x => x.GetComponent<ModuleEngines>()).ToList();
                    if (engines.Count() == 0) { ThrustToWeight = 0; break; }
                    float totalThrust = 0;
                    foreach (var engine in engines)
                    {
                        totalThrust += engine.GetComponents<ModuleEngines>().Sum(x => x.GetCurrentThrust());
                    }
                    controller = totalThrust / VesselMass;
                    ThrustToWeight = controller;
                    break;
                case PhysicsControl.REENTRYHEAT:
                    if (RSE.Instance.AeroFX != null) { controller = RSE.Instance.AeroFX.FxScalar * RSE.Instance.AeroFX.state; }
                    break;
                case PhysicsControl.None:
                    controller = 1;
                    break;
            }
            return controller;
        }

        public void onGamePause()
        {
            if (Sources.Count > 0) { Sources.Values.ToList().ForEach(x => x.Pause()); }
            
            gamePaused = true;
        }

        public void onGameUnpause()
        {
            if (Sources.Count > 0) { Sources.Values.ToList().ForEach(x => x.UnPause()); }

            gamePaused = false;
        }

        void OnDestroy()
        {
            if (!initialized) return;

            if (Sources.Count > 0)
            {
                foreach (var source in Sources.Values)
                {
                    source.Stop();
                    UnityEngine.Object.Destroy(source.gameObject);
                }
            }

            GameEvents.onGamePause.Remove(onGamePause);
            GameEvents.onGameUnpause.Remove(onGameUnpause);
        }
    }
}
