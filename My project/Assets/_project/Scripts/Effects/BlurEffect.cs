namespace Maptifier.Effects
{
    public class BlurEffect : BaseEffect
    {
        public const string Id = "blur";

        public override string Name => "Blur";
        public override string Id => BlurEffect.Id;

        private const string ShaderName = "Maptifier/FX/DualKawaseBlur";

        public override int PassCount => 2;

        public BlurEffect() : base(ShaderName)
        {
            AddParameter("_Offset", "Radius", 0f, 8f, 2f);
        }
    }
}
