using ModuleWheels;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;

namespace RocketSoundEnhancement.PartModules
{
    public class RSE_Wheels : RSE_Module
    {
        private Dictionary<string, float> offLoadVolumeScale = new Dictionary<string, float>();
        private Dictionary<string, float> volumeScaleSpools = new Dictionary<string, float>();

        private ModuleWheelBase moduleWheel;
        private ModuleWheelMotor moduleMotor;
        private ModuleWheelDamage moduleWheelDamage;
        private ModuleWheelDeployment moduleDeploy;
        private CollidingObject collidingObject;

        private bool retracted = false;
        private bool motorRunning = false;
        private float motorOutput = 0;
        private float wheelSpeed = 0;
        private float slipDisplacement = 0;

        public RSE_Wheels()
        {
            EnableLowpassFilter = true;
        }

        public override void OnStart(StartState state)
        {
            if (state == StartState.Editor || state == StartState.None)
                return;

            base.OnStart(state);

            moduleWheel = part.GetComponent<ModuleWheelBase>();
            moduleMotor = part.GetComponent<ModuleWheelMotor>();
            moduleDeploy = part.GetComponent<ModuleWheelDeployment>();
            moduleWheelDamage = part.GetComponent<ModuleWheelDamage>();

            Initialized = true;
        }

        public override void LateUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || !Initialized || !vessel.loaded || GamePaused || !moduleWheel || !moduleWheel.Wheel)
                return;

            if (moduleMotor)
            {
                motorRunning = moduleMotor.motorEnabled && moduleMotor.state > ModuleWheelMotor.MotorState.Disabled;
                motorOutput = motorRunning ? Mathf.Clamp(wheelSpeed / moduleMotor.wheelSpeedMax, 0, 2f) : 0;
            }

            if (moduleDeploy)
            {
                retracted = moduleDeploy.stateString == "Retracted";
            }

            foreach (var soundLayerGroup in SoundLayerGroups)
            {
                string soundLayerGroupKey = soundLayerGroup.Key;
                float control = 0;

                if (!retracted)
                {
                    switch (soundLayerGroup.Key)
                    {
                        case "Motor":
                            control = motorOutput;
                            break;
                        case "Speed":
                            control = wheelSpeed;
                            break;
                        case "Ground":
                            control = moduleWheel.isGrounded ? Mathf.Max(wheelSpeed, slipDisplacement) : 0;
                            break;
                        case "Slip":
                            control = moduleWheel.isGrounded ? slipDisplacement : 0;
                            break;
                        default:
                            continue;
                    }
                }

                foreach (var soundLayer in soundLayerGroup.Value)
                {
                    string soundLayerName = soundLayerGroupKey + "_" + soundLayer.name;
                    float finalControl = control;
                    float volumeScale = 1;

                    if (soundLayerGroupKey == "Ground" || soundLayerGroupKey == "Slip")
                    {
                        string collidingObjectString = collidingObject.ToString().ToLower();
                        if (soundLayer.data != "" && !soundLayer.data.Contains(collidingObjectString))
                            finalControl = 0;
                    }

                    if (!Controls.ContainsKey(soundLayerName))
                    {
                        Controls.Add(soundLayerName, 0);
                    }

                    if (soundLayer.spool)
                    {
                        float spoolControl = soundLayerGroupKey == "Motor" ? Mathf.Lerp(motorRunning ? soundLayer.spoolIdle : 0, 1, finalControl) : finalControl;
                        float spoolSpeed = Mathf.Max(soundLayer.spoolSpeed, finalControl * 0.5f);

                        if (soundLayerGroupKey == "Motor" && moduleWheel.wheel.brakeState > 0 && Controls[soundLayerName] > spoolControl)
                        {
                            spoolSpeed = soundLayer.spoolSpeed;
                        }

                        Controls[soundLayerName] = Mathf.MoveTowards(Controls[soundLayerName], spoolControl, soundLayer.spoolSpeed * TimeWarp.deltaTime);
                    }
                    else
                    {
                        float smoothControl = AudioUtility.SmoothControl.Evaluate(Mathf.Max(Controls[soundLayerName], finalControl)) * (60 * Time.deltaTime);
                        Controls[soundLayerName] = Mathf.MoveTowards(Controls[soundLayerName], finalControl, smoothControl);
                    }

                    if (soundLayerGroupKey == "Motor" && offLoadVolumeScale.ContainsKey(soundLayer.name))
                    {
                        volumeScale = moduleMotor.state == ModuleWheelMotor.MotorState.Running ? 1 : offLoadVolumeScale[soundLayer.name];
                        if (soundLayer.spool)
                        {
                            if (!volumeScaleSpools.Keys.Contains(soundLayerName))
                                volumeScaleSpools.Add(soundLayerName, 0);

                            volumeScaleSpools[soundLayerName] = Mathf.MoveTowards(volumeScaleSpools[soundLayerName], volumeScale, soundLayer.spoolSpeed * TimeWarp.deltaTime);
                            volumeScale = volumeScaleSpools[soundLayerName];
                        }
                    }

                    PlaySoundLayer(soundLayer, Controls[soundLayerName], Volume * volumeScale);
                }
            }

            base.LateUpdate();
        }

        public override void FixedUpdate()
        {
            if (!Initialized || !moduleWheel || !moduleWheel.Wheel || GamePaused || !vessel.loaded)
                return;

            base.FixedUpdate();

            if (moduleWheelDamage != null && moduleWheelDamage.isDamaged)
            {
                wheelSpeed = 0;
                slipDisplacement = 0;
                return;
            }

            if (moduleWheel.isGrounded)
            {
                WheelHit hit;
                if (moduleWheel.Wheel.wheelCollider.GetGroundHit(out hit))
                {
                    collidingObject = AudioUtility.GetCollidingObject(hit.collider.gameObject);
                }
            }

            wheelSpeed = Mathf.Abs(moduleWheel.Wheel.WheelRadius * moduleWheel.Wheel.wheelCollider.angularVelocity);

            float x = moduleWheel.Wheel.currentState.localWheelVelocity.x;
            float y = (moduleWheel.Wheel.WheelRadius * moduleWheel.Wheel.wheelCollider.angularVelocity) - moduleWheel.Wheel.currentState.localWheelVelocity.y;

            slipDisplacement = Mathf.Sqrt(x * x + y * y);
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            if (part?.partInfo?.partPrefab != null)
            {
                var prefab = part.partInfo.partPrefab.FindModuleImplementing<RSE_Wheels>();
                offLoadVolumeScale = prefab.offLoadVolumeScale;
                return;
            }

            ConfigNode motor = null;
            if (node.TryGetNode("Motor", ref motor))
            {
                ConfigNode offLoadVolumeScaleNode = motor.GetNode("offLoadVolumeScale");

                if (offLoadVolumeScaleNode != null && offLoadVolumeScaleNode.HasValue())
                {
                    foreach (ConfigNode.Value vnode in offLoadVolumeScaleNode.values)
                    {
                        string soundLayerName = vnode.name;
                        float value = float.Parse(vnode.value);

                        if (offLoadVolumeScale.ContainsKey(soundLayerName))
                        {
                            offLoadVolumeScale[soundLayerName] = value;
                            continue;
                        }

                        offLoadVolumeScale.Add(soundLayerName, value);
                    }
                }
            }
        }

    }
}
