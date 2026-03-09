namespace Maptifier.Effects
{
    public class PixelateEffect : BaseEffect
    {
        public const string EffectId = "pixelate";

        public override string Name => "Pixelate";
        public override string Id => EffectId;

        private const string ShaderName = "Maptifier/FX/Pixelate";

        public PixelateEffect() : base(ShaderName)
        {
            AddParameter("_PixelSize", "PixelSize", 1f, 100f, 10f);
        }
    }
}
