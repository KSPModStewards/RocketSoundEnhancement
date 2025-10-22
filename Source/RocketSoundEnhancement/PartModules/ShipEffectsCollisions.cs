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
        private Dictionary<CollisionType, List<SoundLayer>> SoundLayerCollisionGroups = new Dictionary<CollisionType, List<SoundLayer>>();

        private bool collided;
        private Collision collision;
        private CollidingObject collidingObject;
        private CollisionType collisionType;

        public ShipEffectsCollisions()
        {
            PrepareSoundLayers = false;
            EnableLowpassFilter = true;
        }

        public override void OnStart(StartState state)
        {
            if (state == StartState.Editor || state == StartState.None)
                return;

            base.OnStart(state);

            if (SoundLayerCollisionGroups.Count > 0)
            {
                foreach (var soundLayerGroup in SoundLayerCollisionGroups)
                {
                    StartCoroutine(SetupAudioSources(soundLayerGroup.Value));
                }
            }

            Initialized = true;
        }

        public override void LateUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || !Initialized || !vessel.loaded || GamePaused) return;

            if (!collided)
            {
                foreach (var source in Sources.Values)
                {
                    if (source.volume == 0 && source.loop)
                        source.Stop();
                    if (source.isPlaying && source.loop)
                        source.volume = 0;
                }
                enabled = false;
                goto baseLateUpdate;
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

            baseLateUpdate:
            base.LateUpdate();
        }

        public override void FixedUpdate()
        {
            if (!Initialized || GamePaused || !vessel.loaded)
                return;

            collided = false;
            collisionType = CollisionType.CollisionStay;
            base.FixedUpdate();
        }

        public void OnCollisionEnter(Collision col)
        {
            enabled = true;

            collided = true;
            collidingObject = AudioUtility.GetCollidingObject(col.gameObject);
            collisionType = CollisionType.CollisionEnter;
            collision = col;
        }

        public void OnCollisionStay(Collision col)
        {
            collided = true;
            collidingObject = AudioUtility.GetCollidingObject(col.gameObject);
            collisionType = CollisionType.CollisionStay;
            collision = col;
        }

        public void OnCollisionExit(Collision col)
        {
            collided = false;
            collidingObject = AudioUtility.GetCollidingObject(col.gameObject);
            collisionType = CollisionType.CollisionExit;
            collision = col;
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            if (part?.partInfo?.partPrefab != null)
            {
                int moduleIndex = part.modules.IndexOf(this);
				var prefab = part.partInfo.partPrefab.modules[moduleIndex] as ShipEffectsCollisions;

				SoundLayerCollisionGroups = prefab.SoundLayerCollisionGroups;
                return;
            }

            foreach (var child in node.GetNodes())
            {
                var soundLayerNodes = child.GetNodes("SOUNDLAYER");

                if (Enum.TryParse(child.name, out CollisionType collisionType))
                {
                    SoundLayerCollisionGroups.Add(collisionType, AudioUtility.CreateSoundLayerGroup(soundLayerNodes));
                }
            }
        }
    }
}
