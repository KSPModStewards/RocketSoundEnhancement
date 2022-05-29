using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using RocketSoundEnhancement.AudioFilters;
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

        private GameObject audioParent;
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
        public Vector3 MachTipCameraNormal = new Vector3();
        public Vector3 MachRearCameraNormal = new Vector3();

        public bool SonicBoomedTip;
        public bool SonicBoomedRear;

        public bool initialized;
        bool gamePaused;
        bool ignoreVessel;
        bool noPhysics;
        bool airSimFiltersEnabled;
        float pastAngularVelocity;
        float pastAcceleration;

        public void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || !initialized || gamePaused || noPhysics || ignoreVessel)
                return;

            float accel = vessel.LandedOrSplashed ? (float)vessel.acceleration.magnitude : (float)(vessel.geeForce * PhysicsGlobals.GravitationalAcceleration);
            Acceleration = accel + (Mathf.Abs(pastAngularVelocity - vessel.angularVelocity.magnitude) / Time.fixedDeltaTime);
            Acceleration = Mathf.Round(Acceleration * 100) * 0.01f;
            Jerk = Mathf.Abs(pastAcceleration - Acceleration) / Time.fixedDeltaTime;
            DynamicPressure = (float)vessel.dynamicPressurekPa;

            pastAngularVelocity = vessel.angularVelocity.magnitude;
            pastAcceleration = Acceleration;

            if (Settings.AudioEffectsEnabled && Settings.MufflerQuality == AudioMufflerQuality.AirSim)
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

                MachTipCameraNormal = (CameraManager.GetCurrentCamera().transform.position - vesselTip).normalized;
                Angle = (1 + Vector3.Dot(MachTipCameraNormal, vessel.velocityD.normalized)) * 90;
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

        public void LateUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || !initialized || gamePaused || noPhysics || ignoreVessel)
                return;

            VesselMass = vessel.GetTotalMass();
            var excludedPart = vessel.Parts.Find(x => x.Modules.Contains("ModuleAsteroid"));
            if (excludedPart != null)
            {
                VesselMass -= excludedPart.mass;
            }

            foreach (var soundLayerGroup in SoundLayerGroups)
            {
                float rawControl = GetPhysicsController(soundLayerGroup.Key);
                foreach (var soundLayer in soundLayerGroup.Value)
                {
                    if (vessel.crewableParts == 0 && soundLayer.channel == FXChannel.Interior)
                        continue;

                    if (soundLayerGroup.Key == PhysicsControl.SONICBOOM)
                    {
                        if (!Settings.AudioEffectsEnabled || Settings.MufflerQuality != AudioMufflerQuality.AirSim)
                            continue;

                        if (MachPass > 0.0 && !SonicBoomedTip)
                        {
                            SonicBoomedTip = true;
                            if (vessel.mach > 1) PlaySonicBoom(soundLayer);
                        }

                        if (MachPassRear > 0.0 && !SonicBoomedRear)
                        {
                            SonicBoomedRear = true;
                            if (vessel.mach > 1) PlaySonicBoom(soundLayer);
                        }

                        if (MachPass == 0)
                        {
                            SonicBoomedTip = false;
                            SonicBoomedRear = false;
                        }

                        continue;
                    }

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
                AirSimFilters.Values.ToList().ForEach(x => x.enabled = false);
                airSimFiltersEnabled = false;
            }
        }

        public void PlaySonicBoom(SoundLayer soundLayer)
        {
            if (!(InternalCamera.Instance.isActive && vessel == FlightGlobals.ActiveVessel))
            {
                float vesselSize = vessel.vesselSize.sqrMagnitude;
                PlaySoundLayer(soundLayer, 1, Mathf.Clamp01(Distance / vesselSize) * Mathf.Min(Mach, 4), false, true);
            }
        }

        public void PlaySoundLayer(SoundLayer soundLayer, float control, float volumeScale = 1, bool smoothControl = true, bool AirSimBasic = false)
        {
            string soundLayerName = soundLayer.loop ? soundLayer.name : "oneshotSource";
            if (!Sources.ContainsKey(soundLayerName)) return;

            float finalVolume, finalPitch;
            finalVolume = soundLayer.volumeFC?.Evaluate(control) ?? soundLayer.volume.Value(control);
            finalVolume *= soundLayer.massToVolume?.Value(VesselMass) ?? 1;

            finalPitch = soundLayer.pitchFC?.Evaluate(control) ?? soundLayer.pitch.Value(control);
            finalPitch *= soundLayer.massToPitch?.Value(VesselMass) ?? 1;

            if (smoothControl)
            {
                if (!VolumeControls.ContainsKey(soundLayerName)) { VolumeControls.Add(soundLayerName, 0); }
                if (!PitchControls.ContainsKey(soundLayerName)) { PitchControls.Add(soundLayerName, 1); }

                VolumeControls[soundLayerName] = Mathf.MoveTowards(VolumeControls[soundLayerName], finalVolume, AudioUtility.SmoothControl.Evaluate(finalVolume) * (10 * Time.deltaTime));
                PitchControls[soundLayerName] = Mathf.MoveTowards(PitchControls[soundLayerName], finalPitch, AudioUtility.SmoothControl.Evaluate(finalPitch) * (10 * Time.deltaTime));
                finalVolume = VolumeControls[soundLayerName];
                finalPitch = PitchControls[soundLayerName];
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

            if (Settings.MufflerQuality == AudioMufflerQuality.AirSim && soundLayer.channel == FXChannel.Exterior && AirSimFilters.ContainsKey(soundLayerName))
            {
                AirSimFilters[soundLayerName].enabled = true;
                AirSimFilters[soundLayerName].SimulationUpdate = AirSimBasic ? AirSimulationUpdate.Basic : AirSimulationUpdate.Full;
                AirSimFilters[soundLayerName].Distance = Distance;
                AirSimFilters[soundLayerName].Mach = Mach;
                AirSimFilters[soundLayerName].Angle = Angle;
                AirSimFilters[soundLayerName].MachPass = MachPass;
                AirSimFilters[soundLayerName].MachAngle = Angle;
                airSimFiltersEnabled = true;
            }
            
            AudioSource source = Sources[soundLayerName];
            source.enabled = true;

            if (vessel.isActiveVessel && soundLayer.channel == FXChannel.Interior && InternalCamera.Instance.isActiveAndEnabled)
                source.transform.localPosition = InternalCamera.Instance.transform.localPosition + Vector3.back;

            if (soundLayer.channel == FXChannel.Exterior) { source.transform.position = vessel.CurrentCoM; }

            if (vessel.isActiveVessel && soundLayer.channel == FXChannel.Interior && InternalCamera.Instance.isActiveAndEnabled)
                source.transform.localPosition = InternalCamera.Instance.transform.localPosition + Vector3.back;

            if (soundLayer.channel == FXChannel.Exterior) { source.transform.position = vessel.CurrentCoM; }

            source.volume = finalVolume * volumeScale * GameSettings.SHIP_VOLUME;
            source.pitch = finalPitch;

            int index = soundLayer.audioClips.Length > 1 ? UnityEngine.Random.Range(0, soundLayer.audioClips.Length) : 0;
            if (soundLayer.loop && soundLayer.audioClips != null && (source.clip == null || source.clip != soundLayer.audioClips[index]))
            {
                source.clip = soundLayer.audioClips[index];
                source.time = soundLayer.loopAtRandom ? UnityEngine.Random.Range(0, soundLayer.audioClips[index].length) : 0;
            }

            AudioUtility.PlayAtChannel(source, soundLayer.channel, vessel == FlightGlobals.ActiveVessel, soundLayer.loop, 1, soundLayer.audioClips[index]);
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
                    if (RocketSoundEnhancement.Instance.AeroFX != null) { controller = RocketSoundEnhancement.Instance.AeroFX.FxScalar * RocketSoundEnhancement.Instance.AeroFX.state; }
                    break;
                case PhysicsControl.None:
                    controller = 1;
                    break;
            }
            return controller;
        }

        public bool Initialize()
        {
            if (vessel == null) return false;

            if (vessel.Parts.Count <= 1)
            {
                ignoreVessel = vessel.Parts[0].Modules.Contains("ModuleAsteroid") || vessel.Parts[0].Modules.Contains("KerbalEVA");
                noPhysics = vessel.Parts[0].PhysicsSignificance == 1;
                return true;
            }

            if (Settings.ShipEffectsNodes().Count > 0)
            {
                foreach (var node in Settings.ShipEffectsNodes())
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
                    StartCoroutine(SetupAudioSources(soundLayerGroup.Value));
                }
            }

            GameEvents.onGamePause.Add(onGamePause);
            GameEvents.onGameUnpause.Add(onGameUnpause);
            GameEvents.onPartDeCoupleNewVesselComplete.Add(onNewVessel);
            return true;
        }

        IEnumerator SetupAudioSources(List<SoundLayer> soundLayers)
        {
            foreach (var soundLayer in soundLayers.ToList())
            {
                string soundLayerName = soundLayer.loop ? soundLayer.name : "oneshotSource";
                if (!Sources.ContainsKey(soundLayerName))
                {
                    var sourceGameObject = new GameObject(soundLayerName);
                    sourceGameObject.transform.parent = audioParent.transform;
                    sourceGameObject.transform.position = audioParent.transform.position;
                    sourceGameObject.transform.rotation = audioParent.transform.rotation;

                    var source = AudioUtility.CreateSource(sourceGameObject, soundLayer);
                    source.enabled = false;
                    Sources.Add(soundLayerName, source);

                    var airSimFilter = new AirSimulationFilter();
                    airSimFilter = sourceGameObject.AddComponent<AirSimulationFilter>();
                    airSimFilter.EnableLowpassFilter = true;
                    airSimFilter.EnableWaveShaperFilter = true;

                    AirSimFilters.Add(soundLayerName, airSimFilter);
                }
                yield return null;
            }
        }

        void onNewVessel(Vessel vessel1, Vessel vessel2)
        {
            if(vessel == vessel1  && vessel.launchTime > vessel2.launchTime) return;
            if(vessel == vessel2  && vessel.launchTime > vessel1.launchTime) return;

            ShipEffects olderVessel = vessel != vessel1 ? vessel1.GetComponent<ShipEffects>() : vessel2.GetComponent<ShipEffects>();

            SonicBoomedTip = olderVessel.SonicBoomedTip;
            SonicBoomedRear = olderVessel.SonicBoomedTip;
        }

        public void Unload()
        {
            UnityEngine.Object.Destroy(audioParent);
            Sources.Clear();
            AirSimFilters.Clear();
            VolumeControls.Clear();
            PitchControls.Clear();

            GameEvents.onGamePause.Remove(onGamePause);
            GameEvents.onGameUnpause.Remove(onGameUnpause);
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
            if (Sources.Count > 0) { Sources.Values.ToList().ForEach(x => x.Stop()); }
            UnityEngine.Object.Destroy(audioParent);
            GameEvents.onGamePause.Remove(onGamePause);
            GameEvents.onGameUnpause.Remove(onGameUnpause);
            GameEvents.onPartDeCoupleNewVesselComplete.Remove(onNewVessel);
        }
    }
}
