using HarmonyLib;
using UnityEngine;
using System;

namespace RocketSoundEnhancement.Patches
{
    [HarmonyPatch(typeof(AudioSource), MethodType.Constructor, argumentTypes: new Type[0])]
    internal static class AudioSource_Ctor_Patch
    {
        static void Postfix(AudioSource __instance)
        {
            RocketSoundEnhancement.Instance?.newAudioSources?.Add(__instance);
        }
    }
}


