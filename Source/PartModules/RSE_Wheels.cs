using ModuleWheels;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RocketSoundEnhancement
{
    class RSE_Wheels : RSE_Module
    {
        ModuleWheelBase moduleWheel;
        ModuleWheelMotor moduleMotor;
        ModuleWheelDeployment moduleDeploy;

        public override void OnStart(StartState state)
        {
            if(state == StartState.Editor || state == StartState.None)
                return;

            base.OnStart(state);

            moduleWheel = part.GetComponent<ModuleWheelBase>();
            moduleMotor = part.GetComponent<ModuleWheelMotor>();
            moduleDeploy = part.GetComponent<ModuleWheelDeployment>();

            initialized = true;
        }

        bool retracted = false;
        [KSPField(isPersistant = false, guiActive = true)]
        public float wheelSpeed = 0;
        [KSPField(isPersistant = false, guiActive = true)]
        public float slipDisplacement = 0;
        CollidingObject collidingObject;
        public override void OnUpdate()
        {
            if(!HighLogic.LoadedSceneIsFlight || !initialized || !moduleWheel || !moduleWheel.Wheel || gamePaused)
                return;

            bool running = false;
            bool motorEnabled = false;
            float driveOutput = 0;

            if(moduleMotor) {
                running = moduleMotor.state == ModuleWheelMotor.MotorState.Running;
                motorEnabled = moduleMotor.motorEnabled;
                driveOutput = moduleMotor.driveOutput;
            }

            if(moduleDeploy) {
                retracted = moduleDeploy.stateString == "Retracted";
            }

            foreach(var soundLayerGroup in SoundLayerGroups) {
                string soundLayerGroupKey = soundLayerGroup.Key;
                float control = 0;

                if(!retracted) {
                    switch(soundLayerGroup.Key) {
                        case "Torque":
                            control = running ? driveOutput / 100 : 0;
                            break;
                        case "Speed":
                            control = motorEnabled ? wheelSpeed : 0;
                            break;
                        case "Ground":
                            control = moduleWheel.isGrounded ?  Mathf.Max(wheelSpeed, slipDisplacement): 0;
                            break;
                        case "Slip":
                            control = moduleWheel.isGrounded ? slipDisplacement : 0;
                            break;
                        default:
                            continue;
                    }
                }

                foreach(var soundLayer in soundLayerGroup.Value) {
                    string sourceLayerName = soundLayerGroupKey + "_" + soundLayer.name;
                    float finalControl = control;

                    if(soundLayerGroupKey == "Ground" || soundLayerGroupKey == "Slip") {
                        string layerMaskName = soundLayer.data;
                        if(layerMaskName != "") {
                            switch(collidingObject) {
                                case CollidingObject.Vessel:
                                    if(!layerMaskName.Contains("vessel"))
                                    finalControl = 0;
                                    break;
                                case CollidingObject.Concrete:
                                    if(!layerMaskName.Contains("concrete"))
                                    finalControl = 0;
                                    break;
                                case CollidingObject.Dirt:
                                    if(!layerMaskName.Contains("dirt"))
                                    finalControl = 0;
                                    break;
                            }
                        }
                    }
                    
                    if(soundLayer.spool) {
                        if(!Controls.ContainsKey(sourceLayerName)) {
                            Controls.Add(sourceLayerName, 0);
                        }
                        Controls[sourceLayerName] = Mathf.MoveTowards(Controls[sourceLayerName], control, soundLayer.spoolSpeed * TimeWarp.deltaTime);
                        finalControl = Controls[sourceLayerName];
                    }

                    PlaySoundLayer(sourceLayerName, soundLayer, finalControl, Volume);
                }
            }

            base.OnUpdate();
        }

        public override void FixedUpdate()
        {
            if(!initialized || !moduleWheel || !moduleWheel.Wheel || gamePaused)
                return;

            WheelHit hit;
            if(moduleWheel.Wheel.wheelCollider.GetGroundHit(out hit)) {
                collidingObject = AudioUtility.GetCollidingObject(hit.collider.gameObject);
            }else{
                collidingObject = CollidingObject.Dirt;
            }

            wheelSpeed = Mathf.Abs(moduleWheel.Wheel.WheelRadius * moduleWheel.Wheel.wheelCollider.angularVelocity);

            float x = moduleWheel.Wheel.currentState.localWheelVelocity.x;
            float y = (moduleWheel.Wheel.WheelRadius * moduleWheel.Wheel.wheelCollider.angularVelocity) - moduleWheel.Wheel.currentState.localWheelVelocity.y;

            slipDisplacement = Mathf.Sqrt(x * x + y * y);

            base.FixedUpdate();
        }
    }
}
