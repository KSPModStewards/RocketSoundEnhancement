namespace RocketSoundEnhancement
{
    public class AudioLimiterSettings
    {
        public bool Custom;
        public float AutoLimiter;
        public float Threshold;
        public float Gain;
        public float Attack;
        public float Release;

        public AudioLimiterSettings()
        {
            Default();
        }

        public void Default()
        {
            Custom = false;
            AutoLimiter = 0.5f;
            Threshold = 0;
            Gain = 0;
            Attack = 10;
            Release = 20;
        }
    }
}
