using Smooth.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RocketSoundEnhancement
{
    public class ShipEffectsCollisions : PartModule
    {
        public Dictionary<string, List<SoundLayer>> SoundLayerGroups = new Dictionary<string, List<SoundLayer>>();

        public bool collided;
        public bool collidedStay;

        public Dictionary<string, AudioSource> Sources = new Dictionary<string, AudioSource>();
        public override void OnStart(StartState state)
        {
            if(state == StartState.Editor || state == StartState.None)
                return;

            var configNode = AudioUtility.GetConfigNode(part.partInfo.name, this.moduleName);

            foreach(var groupNode in configNode.GetNodes()) {
                var soundLayerNodes = groupNode.GetNodes("SOUNDLAYER");

                string collisionType = groupNode.name;

                if(SoundLayerGroups.ContainsKey(collisionType)) {
                    SoundLayerGroups[collisionType].AddRange(AudioUtility.CreateSoundLayerGroup(soundLayerNodes));
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

        void OnCollisionEnter(Collision collision)
        {
            if(SoundLayerGroups.ContainsKey("COLLISIONENTER")) {
                foreach(var soundLayer in SoundLayerGroups["COLLISIONENTER"]) {
                    PlaySound(soundLayer, collision.relativeVelocity.magnitude, true);
                }
            }
            collided = true;
        }

        void OnCollisionStay(Collision collision)
        {
            if(SoundLayerGroups.ContainsKey("COLLISIONSTAY")) {
                foreach(var soundLayer in SoundLayerGroups["COLLISIONSTAY"]) {
                    PlaySound(soundLayer, collision.relativeVelocity.magnitude, soundLayer.loop);
                }
            }
        }

        void OnCollisionExit(Collision other)
        {
            if(SoundLayerGroups.ContainsKey("COLLISIONEXIT")) {
                foreach(var soundLayer in SoundLayerGroups["COLLISIONEXIT"]) {
                    PlaySound(soundLayer, other.relativeVelocity.magnitude, true);
                }
            }
            collided = false;
        }

        void PlaySound(SoundLayer soundLayer, float control, bool loop = false, bool oneshot = false)
        {
            if(!Sources.ContainsKey(soundLayer.name)) {
                Sources.Add(soundLayer.name, AudioUtility.CreateSource(part.gameObject, soundLayer));
            }

            var source = Sources[soundLayer.name];

            if(source == null)
                return;

            float finalVolume = soundLayer.volume.Value(control) * soundLayer.massToVolume.Value(control);
            float finalPitch = soundLayer.pitch.Value(control) * soundLayer.massToPitch.Value(part.vessel.GetComponent<ShipEffects>().TotalMass);

            if(finalVolume > float.Epsilon) {
                source.volume = finalVolume * HighLogic.CurrentGame.Parameters.CustomParams<Settings>().EffectsVolume;
                source.pitch = finalPitch;
                if(oneshot) {

                } else {
                    AudioUtility.PlayAtChannel(source, soundLayer.channel, loop, oneshot);
                }
            }
        }
    }
}
