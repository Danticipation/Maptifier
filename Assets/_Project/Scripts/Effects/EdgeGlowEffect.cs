namespace Maptifier.Effects
{
    public class EdgeGlowEffect : BaseEffect
    {
        public const string Id = "edgeglow";

        public override string Name => "Edge Glow";
        public override string Id => EdgeGlowEffect.Id;

        private const string ShaderName = "Maptifier/FX/EdgeGlow";

        public EdgeGlowEffect() : base(ShaderName)
        {
            AddParameter("_Threshold", "Threshold", 0f, 1f, 0.3f);
            AddParameter("_GlowWidth", "GlowWidth", 0f, 10f, 2f);
            AddParameter("_GlowR", "GlowR", 0f, 1f, 0f);
            AddParameter("_GlowG", "GlowG", 0f, 1f, 1f);
            AddParameter("_GlowB", "GlowB", 0f, 1f, 1f);
        }
    }
}
