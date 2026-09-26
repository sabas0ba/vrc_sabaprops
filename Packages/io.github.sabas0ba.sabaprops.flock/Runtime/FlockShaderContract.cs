namespace SabaProps.Flock
{
    /// <summary>
    /// Vertex channel layout shared by the mesh builder and
    /// <c>SabaFlock.shader</c>. Every value the shader needs is written into
    /// the mesh, so one material serves every species and every swarm.
    /// <list type="table">
    /// <item><term>POSITION</term><description>Body-local position in metres. +Z is forward, +Y is up.</description></item>
    /// <item><term>NORMAL</term><description>Body-local normal.</description></item>
    /// <item><term>COLOR</term><description>rgb = albedo, a = specular sheen (silver fish scales).</description></item>
    /// <item><term>UV0</term><description>x = signed span coordinate of wings and pectoral fins (-1 left tip, +1 right tip), y = axial coordinate (0 head, 1 tail), z = <see cref="FlockBodyPart"/>, w = shoulder offset of the wing root from the body axis.</description></item>
    /// <item><term>UV1</term><description>x = individual index, yzw = three random values in [0, 1).</description></item>
    /// <item><term>UV2</term><description>x = <see cref="FlockPattern"/>, y = speed (m/s), z = time offset (s), w = cluster radius (m).</description></item>
    /// <item><term>UV3</term><description>xyz = area half extents (m), w = body length (m).</description></item>
    /// <item><term>UV4</term><description>x = <see cref="FlockAnimation"/>, y = beat frequency (Hz), z = beat amplitude, w = glide fraction.</description></item>
    /// <item><term>UV5</term><description>x = individual count, y = bank gain, z = maximum pitch, w = size variance.</description></item>
    /// </list>
    /// </summary>
    public static class FlockShaderContract
    {
        public const string ShaderName = "SabaProps/Flock/Swarm";

        public const int BodyChannel = 0;
        public const int IndividualChannel = 1;
        public const int SwarmChannel = 2;
        public const int AreaChannel = 3;
        public const int AnimationChannel = 4;
        public const int ExtraChannel = 5;

        public const string SilhouetteColorProperty = "_SilhouetteColor";
        public const string SilhouetteStartProperty = "_SilhouetteStart";
        public const string SilhouetteEndProperty = "_SilhouetteEnd";
        public const string MediumColorProperty = "_MediumColor";
        public const string MediumDensityProperty = "_MediumDensity";
        public const string TimeScaleProperty = "_TimeScale";
    }

    /// <summary>Body part id stored in UV0.z. Selects the animation applied to the vertex.</summary>
    public enum FlockBodyPart
    {
        Body = 0,
        /// <summary>Bird wing or ray pectoral fin. Rotated or waved by span.</summary>
        Wing = 1,
        Tail = 2,
        Fin = 3,
        Legs = 4,
    }
}
