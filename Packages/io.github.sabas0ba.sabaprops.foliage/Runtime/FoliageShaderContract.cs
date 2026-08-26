namespace SabaProps.Foliage
{
    /// <summary>
    /// Public contract shared by procedural mesh packages that use the
    /// SabaProps foliage shader. Lightmap UVs occupy channels 1 and 2, so wind
    /// data deliberately lives in UV3.
    /// </summary>
    public static class FoliageShaderContract
    {
        public const string ShaderName = "SabaProps/Foliage";
        public const int WindDataUvChannel = 3;

        public const string DistanceFadeProperty = "_DistanceFade";
        public const string DistanceFadeKeyword = "_DISTANCEFADE_ON";

        /// <summary>
        /// UV3.xyz is an element root in object space; UV3.w is wind
        /// stiffness. UV0.y is the 0..1 bend coordinate from that root.
        /// </summary>
        public const string WindDataDescription =
            "UV3.xyz=rootOS, UV3.w=stiffness, UV0.y=bend";
    }
}
