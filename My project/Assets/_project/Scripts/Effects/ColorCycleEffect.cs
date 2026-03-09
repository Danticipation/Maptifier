namespace Maptifier.Effects
{
    public class ColorCycleEffect : BaseEffect
    {
        public const string EffectId = "colorcycle";

        public override string Name => "Color Cycle";
        public override string Id => EffectId;

        private const string ShaderName = "Maptifier/FX/ColorCycle";

        public ColorCycleEffect() : base(ShaderName)
        {
            AddParameter("_Speed", "Speed", 0f, 5f, 1f);
            AddParameter("_HueRange", "HueRange", 0f, 1f, 1f);
            AddParameter("_SaturationBoost", "SaturationBoost", 0f, 2f, 1f);
        }
    }
}
