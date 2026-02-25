namespace Maptifier.Effects
{
    public class WaveDistortionEffect : BaseEffect
    {
        public const string Id = "wavedistortion";

        public override string Name => "Wave Distortion";
        public override string Id => WaveDistortionEffect.Id;

        private const string ShaderName = "Maptifier/FX/WaveDistortion";

        public WaveDistortionEffect() : base(ShaderName)
        {
            AddParameter("_Frequency", "Frequency", 0f, 20f, 5f);
            AddParameter("_Amplitude", "Amplitude", 0f, 0.1f, 0.02f);
            AddParameter("_Speed", "Speed", 0f, 5f, 1f);
            AddParameter("_Direction", "Direction", 0f, 6.28f, 0f);
        }
    }
}
