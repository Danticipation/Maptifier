namespace Maptifier.Effects
{
    public class KaleidoscopeEffect : BaseEffect
    {
        public const string Id = "kaleidoscope";

        public override string Name => "Kaleidoscope";
        public override string Id => KaleidoscopeEffect.Id;

        private const string ShaderName = "Maptifier/FX/Kaleidoscope";

        public KaleidoscopeEffect() : base(ShaderName)
        {
            AddParameter("_Segments", "Segments", 2f, 32f, 6f);
            AddParameter("_Rotation", "Rotation", 0f, 6.28f, 0f);
            AddParameter("_CenterX", "CenterX", 0f, 1f, 0.5f);
            AddParameter("_CenterY", "CenterY", 0f, 1f, 0.5f);
        }
    }
}
