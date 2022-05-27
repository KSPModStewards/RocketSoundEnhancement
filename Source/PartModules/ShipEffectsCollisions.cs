using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RocketSoundEnhancement.PartModules
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
            if (state == StartState.Editor || state == StartState.None)
                return;

            prepareSoundLayers = false;
            EnableLowpassFilter = true;
            base.OnStart(state);

            foreach (var node in configNode.GetNodes())
            {
                var soundLayerNodes = node.GetNodes("SOUNDLAYER");
                CollisionType collisionType;

                if (Enum.TryParse(node.name, out collisionType))
                {
                    SoundLayerColGroups.Add(collisionType, AudioUtility.CreateSoundLayerGroup(soundLayerNodes));
                }
            }

            if (SoundLayerColGroups.Count > 0)
            {
                foreach (var soundLayerGroup in SoundLayerColGroups)
                {
                    StartCoroutine(SetupAudioSources(soundLayerGroup.Value));
                }
            }

            initialized = true;
        }

        public override void LateUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || !initialized || !vessel.loaded || gamePaused)
                return;

            if (collided)
            {
                if (SoundLayerColGroups.ContainsKey(collisionType))
                {
                    float control = 0;

                    if (collision != null)
                    {
                        control = collision.relativeVelocity.magnitude;
                    }

                    foreach (var soundLayer in SoundLayerColGroups[collisionType])
                    {
                        string collidingObjectString = collidingObject.ToString().ToLower();
                        float finalControl = control;
                        if (soundLayer.data != "" && !soundLayer.data.Contains(collidingObjectString))
                            finalControl = 0;

                        if (collidingObject == CollidingObject.Vessel && collision?.gameObject.GetComponentInParent<KerbalEVA>() && collisionType == CollisionType.CollisionStay)
                            finalControl = 0;

                        PlaySoundLayer(soundLayer, finalControl, Volume, true);
                    }
                }
            }
            else
            {
                foreach (var source in Sources.Values)
                {
                    if (source.isPlaying && source.loop)
                    {
                        source.Stop();
                    }
                }
            }

            base.LateUpdate();
        }

        public override void FixedUpdate()
        {
            if (!initialized || gamePaused || !vessel.loaded)
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
