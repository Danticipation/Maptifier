namespace Maptifier.Effects
{
    public class TunnelEffect : BaseEffect
    {
        public const string Id = "tunnel";

        public override string Name => "Tunnel";
        public override string Id => TunnelEffect.Id;

        private const string ShaderName = "Maptifier/FX/Tunnel";

        public TunnelEffect() : base(ShaderName)
        {
            AddParameter("_Speed", "Speed", 0f, 5f, 1f);
            AddParameter("_Twist", "Twist", 0f, 6.28f, 0f);
            AddParameter("_CenterX", "CenterX", 0f, 1f, 0.5f);
            AddParameter("_CenterY", "CenterY", 0f, 1f, 0.5f);
        }
    }
}
