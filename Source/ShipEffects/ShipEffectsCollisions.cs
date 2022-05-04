using ModuleWheels;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RocketSoundEnhancement
{
    public enum CollisionType
    {
        CollisionEnter,
        CollisionStay,
        CollisionExit
    }

    public class ShipEffectsCollisions : RSE_Module
    {
        Dictionary<CollisionType, List<SoundLayer>> SoundLayerColGroups = new Dictionary<CollisionType, List<SoundLayer>>();

        bool collided;
        Collision collision;
        CollidingObject collidingObject;
        CollisionType collisionType;
        
        public override void OnStart(StartState state)
        {
            if(state == StartState.Editor || state == StartState.None)
                return;

            getSoundLayersandGroups = false;

            base.OnStart(state);

            foreach(var node in configNode.GetNodes()) {
                var soundLayerNodes = node.GetNodes("SOUNDLAYER");
                CollisionType collisionType;

                if(Enum.TryParse(node.name, out collisionType)) {
                    var soundLayers = AudioUtility.CreateSoundLayerGroup(soundLayerNodes);
                    if(SoundLayerColGroups.ContainsKey(collisionType)) {
                        SoundLayerColGroups[collisionType].AddRange(soundLayers);
                    } else {
                        SoundLayerColGroups.Add(collisionType, soundLayers);
                    }
                }
            }

            initialized = true;
        }

        public override void OnUpdate()
        {
            if(!HighLogic.LoadedSceneIsFlight || gamePaused || !initialized)
                return;
            
            if(collided) {
                if(SoundLayerColGroups.ContainsKey(collisionType)) {
                    float control = 0;

                    if(collision != null) {
                        control = collision.relativeVelocity.magnitude;
                    }

                    if(collisionType == CollisionType.CollisionExit) {
                        control = collision.relativeVelocity.magnitude;
                    }

                    foreach(var soundLayer in SoundLayerColGroups[collisionType]) {
                        string soundLayerName = collisionType + "_" + soundLayer.name;
                        float finalControl = control;

                        var layerMaskName = soundLayer.data.ToLower();
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

                        PlaySoundLayer(soundLayerName, soundLayer, control, Volume, collisionType != CollisionType.CollisionStay);
                    }
                }
            } else {
                foreach(var source in Sources.Values) {
                    if(source.isPlaying && source.loop) {
                        source.Stop();
                    }
                }
            }
            
            base.OnUpdate();
        }

        public override void FixedUpdate()
        {
            if(!initialized || gamePaused)
                return;

            collided = false;
            collisionType = CollisionType.CollisionStay;
            base.FixedUpdate();
        }

        void OnCollisionEnter(Collision col)
        {
            collided = true;
            collidingObject = AudioUtility.GetCollidingObject(col.gameObject);
            collisionType = CollisionType.CollisionEnter;
            collision = col;
        }

        void OnCollisionStay(Collision col)
        {
            collided = true;
            collidingObject = AudioUtility.GetCollidingObject(col.gameObject);
            collisionType = CollisionType.CollisionStay;
            collision = col;
        }

        void OnCollisionExit(Collision col)
        {
            collided = false;
            collidingObject = AudioUtility.GetCollidingObject(col.gameObject);
            collisionType = CollisionType.CollisionExit;
            collision = col;
        }
    }
}
