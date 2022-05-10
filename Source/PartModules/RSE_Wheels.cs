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

            EnableLowpassFilter = true;
            base.OnStart(state);

            moduleWheel = part.GetComponent<ModuleWheelBase>();
            moduleMotor = part.GetComponent<ModuleWheelMotor>();
            moduleDeploy = part.GetComponent<ModuleWheelDeployment>();

            initialized = true;
        }

        float motorOutput = 0;
        float wheelSpeed = 0;
        float slipDisplacement = 0;
        bool retracted = false;
        bool motorRunning = false;
        CollidingObject collidingObject;
        public override void OnUpdate()
        {
            if(!HighLogic.LoadedSceneIsFlight || !initialized || !moduleWheel || !moduleWheel.Wheel || gamePaused)
                return;

            if(moduleMotor) {
                motorRunning = moduleMotor.motorEnabled && moduleMotor.state > ModuleWheelMotor.MotorState.Disabled;
                motorOutput = moduleMotor.state == ModuleWheelMotor.MotorState.Running ? wheelSpeed / moduleMotor.wheelSpeedMax : 0;
            }

            if(moduleDeploy) {
                retracted = moduleDeploy.stateString == "Retracted";
            }

            foreach(var soundLayerGroup in SoundLayerGroups) {
                string soundLayerGroupKey = soundLayerGroup.Key;
                float control = 0;

                if(!retracted) {
                    switch(soundLayerGroup.Key) {
                        case "Motor":
                            control = motorOutput;
                            break;
                        case "Speed":
                            control = wheelSpeed;
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

                    if(!Controls.ContainsKey(sourceLayerName)) {
                        Controls.Add(sourceLayerName, 0);
                    }

                    if(soundLayer.spool) {
                        float spoolControl = soundLayerGroupKey == "Motor" ? Mathf.Lerp(motorRunning ? soundLayer.spoolIdle : 0, 1, finalControl) : finalControl;
                        float spoolSpeed = Mathf.Max(soundLayer.spoolSpeed, finalControl * 0.5f);

                        if(soundLayerGroupKey == "Motor" && moduleWheel.wheel.brakeState > 0 && Controls[sourceLayerName] > spoolControl){
                            spoolSpeed = moduleWheel.wheel.brakeState * 0.5f;
                        }

                        Controls[sourceLayerName] = Mathf.MoveTowards(Controls[sourceLayerName], spoolControl, spoolSpeed * TimeWarp.deltaTime);
                    } else {
                        float smoothControl = AudioUtility.SmoothControl.Evaluate(Mathf.Max(Controls[sourceLayerName], finalControl)) * (60 * Time.deltaTime);
                        Controls[sourceLayerName] = Mathf.MoveTowards(Controls[sourceLayerName], finalControl, smoothControl);
                    }

                    PlaySoundLayer(sourceLayerName, soundLayer, Controls[sourceLayerName], Volume);
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
