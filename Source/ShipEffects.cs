using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace RocketSoundEnhancement
{
    public class ShipEffects : VesselModule
    {
        public List<SoundLayer> SoundLayers = new List<SoundLayer>();
        public Dictionary<string, AudioSource> Sources = new Dictionary<string, AudioSource>();
        Dictionary<string, float> controllers = new Dictionary<string, float>();

        public float TotalMass;
        public float DryMass;
        public float Acceleration;
        public float Jerk;
        public float ThrustAccel;
        float pastAcceleration;

        public bool initialized;
        bool gamePause;
        bool ignoreVessel;

        void Initialize()
        {
            SoundLayers.Clear();
            Sources.Clear();
            controllers.Clear();

            if(vessel.Parts.Count <= 1) {
                if(vessel.Parts[0].PhysicsSignificance == 1 || vessel.Parts[0].Modules.Contains("ModuleAsteroid") || vessel.Parts[0].Modules.Contains("KerbalEVA")) {
                    initialized = false;
                    ignoreVessel = true;
                    //UnityEngine.Debug.Log("ShipEffects: [" + vessel.GetDisplayName() + "] Ignored, " + " Not A Ship or Physicsless.");
                    return;
                }
            }

            foreach(var layerNode in RSE.SoundLayerNodes) {
                var soundLayer = AudioUtility.CreateSoundLayer(layerNode);
                if(soundLayer.audioClips != null && !SoundLayers.Contains(soundLayer)) {
                    SoundLayers.Add(soundLayer);
                }
            }

            initialized = true;

            //UnityEngine.Debug.Log("ShipEffects: [" + vessel.GetDisplayName() + "] Loaded with" + " PartCount: " + vessel.Parts.Count());
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

        protected override void OnStart()
        {
            if(vessel == null || vessel.isEVA || !vessel.loaded || !HighLogic.LoadedSceneIsFlight)
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
        void LateUpdate()
        {
            if(vessel.loaded && !initialized && !ignoreVessel) {
                Initialize();
                return;
            }

            if(vessel == null | !vessel.loaded || vessel.isEVA || !HighLogic.LoadedSceneIsFlight || !initialized || ignoreVessel || gamePause)
                return;

            //calculate forces
            Acceleration = (float)vessel.geeForce * 9.81f;
            Jerk = Mathf.Abs(pastAcceleration - Acceleration) / Time.fixedDeltaTime;
            pastAcceleration = Acceleration;

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

            float controlDampener = Mathf.Lerp(0.2f, 0.1f, DryMass / TotalMass);

            foreach(var soundLayer in SoundLayers) {
                if(!controllers.ContainsKey(soundLayer.name)) {
                    controllers.Add(soundLayer.name, 0);
                }
                float controller = GetController(soundLayer.data);
                float control = Mathf.MoveTowards(controllers[soundLayer.name], controller, Mathf.Max(1, Mathf.Abs(controllers[soundLayer.name] - controller)) * TimeWarp.deltaTime);
                controllers[soundLayer.name] = control;

                float finalVolume = soundLayer.volume.Value(control) * soundLayer.massToVolume.Value(TotalMass);
                float finalPitch = soundLayer.pitch.Value(control) * soundLayer.massToPitch.Value(TotalMass);

                bool skip = (soundLayer.channel == FXChannel.ShipInternal && vessel != FlightGlobals.ActiveVessel);

                if(finalVolume < float.Epsilon || float.IsNaN(control) || float.IsInfinity(control) || skip) {
                    if(Sources.ContainsKey(soundLayer.name)) {
                        UnityEngine.Object.Destroy(Sources[soundLayer.name]);
                        Sources.Remove(soundLayer.name);
                    }
                    continue;
                }

                AudioSource source;
                if(Sources.ContainsKey(soundLayer.name)) {
                    source = Sources[soundLayer.name];
                } else {
                    source = AudioUtility.CreateSource(vessel.gameObject, soundLayer);
                    Sources.Add(soundLayer.name, source);
                }

                source.volume = finalVolume * HighLogic.CurrentGame.Parameters.CustomParams<Settings>().EffectsVolume;
                source.pitch = finalPitch;

                AudioUtility.PlayAtChannel(source, soundLayer.channel, soundLayer.loop, soundLayer.loopAtRandom);
            }
        }

        float GetController(string data)
        {
            PhysicsControl physControl = (PhysicsControl)Enum.Parse(typeof(PhysicsControl), data, true);

            float controller = 0;


            switch(physControl) {
                case PhysicsControl.Acceleration:
                    controller = Acceleration;
                    break;
                case PhysicsControl.Jerk:
                    controller = Jerk;
                    break;
                case PhysicsControl.AirSpeed:
                    controller = (float)vessel.indicatedAirSpeed;
                    break;
                case PhysicsControl.GroundSpeed:
                    if(vessel.Landed)
                        controller = (float)vessel.srf_velocity.magnitude;
                    break;
                case PhysicsControl.Thrust:
                    float totalThrust = 0;
                    var engines = vessel.parts.Where(x => x.GetComponent<ModuleEngines>());
                    if(engines.Count() > 0) {
                        foreach(var engine in engines) {
                            var module = engine.GetComponent<ModuleEngines>();
                            if(module.EngineIgnited) {
                                totalThrust += module.GetCurrentThrust();
                            }
                        }
                        controller = (totalThrust * 1000) / (vessel.GetTotalMass() * 1000); //Convert to Newtons and kg
                        ThrustAccel = controller;
                        break;
                    }
                    controller = 0;
                    ThrustAccel = 0;
                    break;
                case PhysicsControl.None:
                    controller = 1;
                    break;
            }
            return controller;
        }
    }
}
