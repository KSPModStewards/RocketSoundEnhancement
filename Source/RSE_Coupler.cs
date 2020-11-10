using Smooth.Collections;
using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace RocketSoundEnhancement
{
    public class RSE_Coupler : PartModule
    {


        Dictionary<string, List<SoundLayer>> SoundLayerGroups = new Dictionary<string, List<SoundLayer>>();
        Dictionary<string, AudioSource> Sources = new Dictionary<string, AudioSource>();

        public bool activated;

        public override void OnStart(StartState state)
        {
            if(state == StartState.Editor || state == StartState.None)
                return;

            var configNode = AudioUtility.GetConfigNode(part.partInfo.name, this.moduleName);

            foreach(var node in configNode.GetNodes()) {

                string _couplerState = node.name;

                var soundLayers = AudioUtility.CreateSoundLayerGroup(node.GetNodes("SOUNDLAYER"));
                if(soundLayers.Count > 0) {
                    if(SoundLayerGroups.ContainsKey(_couplerState)) {
                        SoundLayerGroups[_couplerState].AddRange(soundLayers);
                    } else {
                        SoundLayerGroups.Add(_couplerState, soundLayers);
                    }
                }
            }
        }

        void LateUpdate()
        {
            if(!HighLogic.LoadedSceneIsFlight)
                return;

            foreach(var asource in Sources.Keys.ToList()) {
                if(!Sources[asource].isPlaying) {
                    UnityEngine.Object.Destroy(Sources[asource]);
                    Sources.Remove(asource);
                }
            }
        }

        public void PlayCouplerSound(string action)
        {
            if(SoundLayerGroups.ContainsKey(action)) {
                foreach(var soundLayer in SoundLayerGroups[action]) {
                    PlaySoundLayer(soundLayer);
                }
            }
        }

        void PlaySoundLayer(SoundLayer soundLayer)
        {
            var soundLayerName = soundLayer.name;

            AudioSource source;
            if(!Sources.ContainsKey(soundLayerName)) {
                source = AudioUtility.CreateSource(gameObject, soundLayer);
                Sources.Add(soundLayerName, source);
            } else {
                source = Sources[soundLayerName];
            }

            source.volume = soundLayer.volume * HighLogic.CurrentGame.Parameters.CustomParams<Settings>().ShipVolume;
            AudioUtility.PlayAtChannel(source, soundLayer.channel, false, false, true);
        }

        void OnDestroy()
        {
            foreach(var source in Sources.Keys) {
                GameObject.Destroy(Sources[source]);
            }
        }
    }
}
