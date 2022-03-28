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

            SoundLayerGroups.Clear();
            spools.Clear();

            string partParentName = part.name + "_" + this.moduleName;
            audioParent = AudioUtility.CreateAudioParent(part, partParentName);

            moduleWheel = part.GetComponent<ModuleWheelBase>();
            moduleMotor = part.GetComponent<ModuleWheelMotor>();
            moduleDeploy = part.GetComponent<ModuleWheelDeployment>();

            var configNode = AudioUtility.GetConfigNode(part.partInfo.name, this.moduleName);
            if(!float.TryParse(configNode.GetValue("volume"), out volume))
                volume = 1;

            SoundLayerGroups = new Dictionary<string, List<SoundLayer>>();
            spools = new Dictionary<string, float>();
            foreach(var node in configNode.GetNodes()) {

                string soundLayerGroupName = node.name;

                var soundLayers = AudioUtility.CreateSoundLayerGroup(node.GetNodes("SOUNDLAYER"));
                if(soundLayers.Count > 0) {
                    if(SoundLayerGroups.ContainsKey(soundLayerGroupName)) {
                        SoundLayerGroups[soundLayerGroupName].AddRange(soundLayers);
                    } else {
                        SoundLayerGroups.Add(soundLayerGroupName, soundLayers);
                    }
                }
            }

            GameEvents.onGamePause.Add(onGamePause);
            GameEvents.onGameUnpause.Add(onGameUnpause);

            initialized = true;
        }

        public override void OnUpdate()
        {
            if(audioParent == null || !initialized || !moduleWheel || !moduleWheel.Wheel || gamePaused)
                return;

            bool running = false;
            bool motorEnabled = false;
            float driveOutput = 0;
            float wheelSpeed = moduleWheel.Wheel.WheelRadius * moduleWheel.Wheel.wheelCollider.angularVelocity;
            float slipDisplacement = Mathf.Abs(GetSlipDisplacement(wheelSpeed));

            WheelHit hit;

            //ISSUES: it wont detect Vessels :(
            CollidingObject colObjectType = CollidingObject.Dirt;
            if(moduleWheel.Wheel.wheelCollider.GetGroundHit(out hit)) {
                colObjectType = AudioUtility.GetCollidingType(hit.collider.gameObject);
            }

            if(moduleMotor) {
                running = moduleMotor.state == ModuleWheelMotor.MotorState.Running;
                motorEnabled = moduleMotor.motorEnabled;
                driveOutput = moduleMotor.driveOutput;
            }

            bool isRetracted = false;
            if(moduleDeploy) {
                isRetracted = moduleDeploy.stateString == "Retracted";
            }

            foreach(var soundLayerGroup in SoundLayerGroups) {
                string soundLayerKey = soundLayerGroup.Key;
                float rawControl = 0;

                if(!isRetracted) {
                    switch(soundLayerGroup.Key) {
                        case "Torque":
                            rawControl = running ? driveOutput / 100 : 0;
                            break;
                        case "Speed":
                            rawControl = motorEnabled ? Mathf.Abs(wheelSpeed) : 0;
                            break;
                        case "Ground":
                            rawControl = moduleWheel.isGrounded ? Mathf.Abs(wheelSpeed) : 0;
                            break;
                        case "Slip":
                            rawControl = moduleWheel.isGrounded ? slipDisplacement : 0;
                            break;
                        default:
                            continue;
                    }
                }

                foreach(var soundLayer in soundLayerGroup.Value) {
                    float control = rawControl;

                    if(soundLayerKey == "Ground" || soundLayerKey == "Slip") {
                        string layerMaskName = soundLayer.data;
                        if(layerMaskName != "") {
                            switch(colObjectType) {
                                case CollidingObject.Vessel:
                                    if(!layerMaskName.Contains("vessel"))
                                        control = 0;
                                    break;
                                case CollidingObject.Concrete:
                                    if(!layerMaskName.Contains("concrete"))
                                        control = 0;
                                    break;
                                case CollidingObject.Dirt:
                                    if(!layerMaskName.Contains("dirt"))
                                        control = 0;
                                    break;
                            }
                        }
                    }

                    AudioUtility.PlaySoundLayer(audioParent, soundLayer.name, soundLayer, control, volume, Sources, spools, false);
                }
            }

            base.OnUpdate();
        }

        //Do Slip Displacement calculations on our own because KSP's ModuleWheelBase.slipDisplacement is broken for some wheels
        float GetSlipDisplacement(float wheelSpeed)
        {
            float x = moduleWheel.Wheel.currentState.localWheelVelocity.x;
            float y = wheelSpeed - moduleWheel.Wheel.currentState.localWheelVelocity.y;

            return Mathf.Sqrt(x * x + y * y) * TimeWarp.deltaTime;
        }
    }
}
