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
        Dictionary<CollisionType, List<SoundLayer>> SoundLayerCollisionGroups = new Dictionary<CollisionType, List<SoundLayer>>();

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
                    SoundLayerCollisionGroups.Add(collisionType, AudioUtility.CreateSoundLayerGroup(soundLayerNodes));
                }
            }

            if (SoundLayerCollisionGroups.Count > 0)
            {
                foreach (var soundLayerGroup in SoundLayerCollisionGroups)
                {
                    StartCoroutine(SetupAudioSources(soundLayerGroup.Value));
                }
            }

            initialized = true;
        }

        public override void LateUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || !initialized || !vessel.loaded || gamePaused) return;

            base.LateUpdate();

            if (!collided)
            {
                foreach (var source in Sources.Values)
                {
                    if (source.volume == 0 && source.loop)
                        source.Stop();
                    if (source.isPlaying && source.loop)
                        source.volume = 0;
                }
                return;
            }

            if (!SoundLayerCollisionGroups.ContainsKey(collisionType)) return;

            string collidingObjectString = collidingObject.ToString().ToLower();
            float control = collision != null ? collision.relativeVelocity.magnitude : 0;

            foreach (var soundLayer in SoundLayerCollisionGroups[collisionType])
            {
                float finalControl = control;
                if (soundLayer.data != "" && !soundLayer.data.Contains(collidingObjectString)) finalControl = 0;

                PlaySoundLayer(soundLayer, finalControl, Volume, true);
            }
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
