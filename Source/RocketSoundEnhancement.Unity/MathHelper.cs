using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace RocketSoundEnhancement.Unity
{
    public static class MathHelper
    {
        public static float AmountToFrequency(float amount)
        {
            return Mathf.Round(11000 * (1 - Mathf.Log(Mathf.Lerp(0.1f, 10, amount), 10)));
        }

        public static float FrequencyToAmount(float frequency)
        {
            return Round(Mathf.InverseLerp(0.1f, 10, Mathf.Pow(10, 1 - (frequency / 11000))), 2);
        }

        public static float Round(float value, int decimals = 0)
        {
            return Mathf.Round(value * Mathf.Pow(10, decimals)) / Mathf.Pow(10, decimals);
        }
    }
}
