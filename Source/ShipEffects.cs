using System.Collections.Generic;
using System.Linq;
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
        float pastAcceleration;

        public bool initialized;
        bool gamePause;
        bool ignoreVessel;

        void Initialize()
        {
            SoundLayers.Clear();
            Sources.Clear();

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
                if(soundLayer.audioClip != null && !SoundLayers.Contains(soundLayer)) {
                    SoundLayers.Add(soundLayer);
                }
            }

            initialized = true;

            //UnityEngine.Debug.Log("ShipEffects: [" + vessel.GetDisplayName() + "] Loaded with" + " PartCount: " + vessel.Parts.Count());
        }

        void onGamePause()
        {
            gamePause = true;
            if(initialized) {
                foreach(var source in Sources.Values) {
                    source.Pause();
                }
            }
        }
        void onGameUnpause()
        {
            gamePause = false;
            if(initialized) {
                foreach(var source in Sources.Values) {
                    source.UnPause();
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
                float control = GetController(soundLayer.physicsControl);

                if(!controllers.ContainsKey(soundLayer.name)) {
                    controllers.Add(soundLayer.name, 0);
                }

                float pastControl = controllers[soundLayer.name];

                control = Mathf.MoveTowards(pastControl, control, Mathf.Abs(pastControl - control) * TimeWarp.deltaTime);
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

        float GetController(PhysicsControl physControl)
        {
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
                    var engines = vessel.parts.Where(x => x.GetComponent<ModuleEngines>());
                    float totalThrust = 0;

                    if(engines.Count() > 0) {
                        foreach(var engine in engines) {
                            var module = engine.GetComponent<ModuleEngines>();
                            if(module.EngineIgnited) {
                                totalThrust += module.GetCurrentThrust();
                            }
                        }
                    }

                    controller = totalThrust;
                    break;
                case PhysicsControl.None:
                    controller = 1;
                    break;
            }
            return controller;
        }
    }
}
