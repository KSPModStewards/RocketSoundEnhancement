using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LogValueTest : MonoBehaviour
{
    [Range(0,1)]
    public float Input;
    public float Output;
    public float BackToInput;
    public AnimationCurve curve = new AnimationCurve();

    public void OnValidate()
    {

        Output = AmountToFrequency(Input);
        BackToInput = FrequencyToAmount(Output);
        curve = new AnimationCurve();
        for(float i = 0; i < 1; i+=0.01f)
        {
            curve.AddKey(i, AmountToFrequency(i));
        }
    }

    public float AmountToFrequency(float amount)
    {
        return Mathf.Round(11000 * (1 - Mathf.Log(Mathf.Lerp(0.1f, 10, amount), 10)));
    }

    public float FrequencyToAmount(float frequency)
    {
        return Round(Mathf.InverseLerp(0.1f, 10, Mathf.Pow(10, 1 - (frequency / 11000))), 2);
    }

    public float Round(float value, int decimals = 0)
    {
        return Mathf.Round(value * Mathf.Pow(10, decimals)) / Mathf.Pow(10, decimals);
    }
}
