namespace Maptifier.Effects
{
    public class ChromaticAberrationEffect : BaseEffect
    {
        public const string Id = "chromaticaberration";

        public override string Name => "Chromatic Aberration";
        public override string Id => ChromaticAberrationEffect.Id;

        private const string ShaderName = "Maptifier/FX/ChromaticAberration";

        public ChromaticAberrationEffect() : base(ShaderName)
        {
            AddParameter("_Intensity", "Intensity", 0f, 0.05f, 0.01f);
            AddParameter("_Angle", "Angle", 0f, 6.28f, 0f);
        }
    }
}
