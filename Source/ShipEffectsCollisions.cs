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

    public class ShipEffectsCollisions : PartModule
    {
        Dictionary<CollisionType, List<SoundLayer>> SoundLayerGroups = new Dictionary<CollisionType, List<SoundLayer>>();
        Dictionary<string, AudioSource> Sources = new Dictionary<string, AudioSource>();

        public bool collided;
        public bool collidedStay;

        public override void OnStart(StartState state)
        {
            if(state == StartState.Editor || state == StartState.None)
                return;

            var configNode = AudioUtility.GetConfigNode(part.partInfo.name, this.moduleName);

            foreach(var groupNode in configNode.GetNodes()) {
                var soundLayerNodes = groupNode.GetNodes("SOUNDLAYER");
                CollisionType collisionType;

                if(Enum.TryParse(groupNode.name, out collisionType)) {
                    var soundLayers = AudioUtility.CreateSoundLayerGroup(soundLayerNodes);
                    if(SoundLayerGroups.ContainsKey(collisionType)) {
                        SoundLayerGroups[collisionType].AddRange(soundLayers);
                    } else {
                        SoundLayerGroups.Add(collisionType, soundLayers);
                    }
                }
            }
        }

        public override void OnUpdate()
        {
            if(Sources.Count > 0) {
                var sourceKeys = Sources.Keys.ToList();
                foreach(var source in sourceKeys) {
                    if(!Sources[source].isPlaying) {
                        UnityEngine.Object.Destroy(Sources[source]);
                        Sources.Remove(source);
                    }
                }
            }
        }

        void OnDestroy()
        {
            if(Sources.Count > 0) {
                foreach(var source in Sources.Values) {
                    source.Stop();
                    UnityEngine.Object.Destroy(source);
                }
            }
        }

        void OnCollisionEnter(Collision col)
        {
            if(SoundLayerGroups.ContainsKey(CollisionType.CollisionEnter)) {
                foreach(var soundLayer in SoundLayerGroups[CollisionType.CollisionEnter]) {
                    PlaySound(soundLayer, col.relativeVelocity.magnitude, false, true);
                }
            }
            collided = true;
        }

        void OnCollisionStay(Collision col)
        {
            if(SoundLayerGroups.ContainsKey(CollisionType.CollisionStay)) {
                foreach(var soundLayer in SoundLayerGroups[CollisionType.CollisionStay]) {
                    PlaySound(soundLayer, col.relativeVelocity.magnitude, soundLayer.loop);
                }
            }
        }

        void OnCollisionExit(Collision col)
        {
            if(SoundLayerGroups.ContainsKey(CollisionType.CollisionStay)) {
                foreach(var layer in SoundLayerGroups[CollisionType.CollisionStay]) {
                    if(Sources.ContainsKey(layer.name)) {
                        Sources[layer.name].Stop();
                    }
                }
            }

            if(SoundLayerGroups.ContainsKey(CollisionType.CollisionExit)) {
                foreach(var soundLayer in SoundLayerGroups[CollisionType.CollisionExit]) {
                    PlaySound(soundLayer, col.relativeVelocity.magnitude, false, true);
                }
            }
            collided = false;
        }

        void PlaySound(SoundLayer soundLayer, float control, bool loop = false, bool oneshot = false)
        {
            float finalVolume = soundLayer.volume.Value(control) * soundLayer.massToVolume.Value(control);

            if(finalVolume > float.Epsilon) {
                if(!Sources.ContainsKey(soundLayer.name)) {
                    Sources.Add(soundLayer.name, AudioUtility.CreateSource(part.gameObject, soundLayer));
                }

                var source = Sources[soundLayer.name];

                if(source == null)
                    return;

                source.volume = finalVolume * HighLogic.CurrentGame.Parameters.CustomParams<Settings>().EffectsVolume;
                source.pitch = soundLayer.pitch.Value(control) * soundLayer.massToPitch.Value(part.vessel.GetComponent<ShipEffects>().TotalMass);
                if(oneshot) {
                    source.volume *= UnityEngine.Random.Range(0.8f, 1.0f);
                    source.pitch *= UnityEngine.Random.Range(0.95f, 1.05f);
                }

                AudioUtility.PlayAtChannel(source, soundLayer.channel, loop, oneshot);
            }
        }
    }
}
