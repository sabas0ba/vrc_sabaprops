namespace SabaProps.Flock
{
    /// <summary>Body plan family. Selects the mesh generator and the animation mode.</summary>
    public enum FlockCategory
    {
        Bird = 0,
        Fish = 1,
    }

    /// <summary>
    /// Group motion evaluated in the vertex shader. The numeric values are
    /// written into the mesh and read by <c>SabaFlockMotion.cginc</c>; do not
    /// renumber them.
    /// </summary>
    public enum FlockPattern
    {
        /// <summary>A loose cloud that follows a figure-eight path.</summary>
        Cruise = 0,
        /// <summary>A dense cloud whose outline stretches and folds over time.</summary>
        Murmuration = 1,
        /// <summary>A V formation behind a leader.</summary>
        VFormation = 2,
        /// <summary>Individuals circle in a rising and sinking column.</summary>
        Thermal = 3,
        /// <summary>A school stretched along the direction of travel.</summary>
        Stream = 4,
        /// <summary>Individuals orbit a centre on tilted spheres.</summary>
        BaitBall = 5,
        /// <summary>Individuals orbit a vertical axis at stacked heights.</summary>
        Tornado = 6,
        /// <summary>Each individual follows its own path inside the area, as in an aquarium tank.</summary>
        Wander = 7,
        /// <summary>Fixed positions for sessile animals; appendages may still move.</summary>
        Anchored = 8,
        /// <summary>Movement accelerates with mantle contraction, for cephalopods.</summary>
        Jet = 9,
        /// <summary>Slow smooth random drift in three dimensions, for jellyfish.</summary>
        Float = 10,
        /// <summary>Independent broad flight paths instead of a shared formation.</summary>
        FreeFlight = 11,
        /// <summary>Normal wandering with a preference for the lower part of the area.</summary>
        FloorGlide = 13,
    }

    /// <summary>Mesh detail tier.</summary>
    public enum FlockDetail
    {
        /// <summary>Flat outline for far distance and sky silhouettes.</summary>
        Silhouette = 0,
        /// <summary>Low-poly body with the main colour regions.</summary>
        Low = 1,
        /// <summary>Full body with fins, markings and colour patterns.</summary>
        High = 2,
    }

    /// <summary>How the swarm is split into renderers.</summary>
    public enum FlockLodMode
    {
        /// <summary>One renderer at the chosen detail tier.</summary>
        Single = 0,
        /// <summary>Three renderers under a LODGroup, High to Silhouette.</summary>
        LodGroup = 1,
    }

    /// <summary>
    /// Per-vertex animation mode. The numeric values are written into the mesh
    /// and read by the shader; do not renumber them.
    /// </summary>
    public enum FlockAnimation
    {
        /// <summary>Wings rotate about the body axis, with optional gliding.</summary>
        Flap = 0,
        /// <summary>Lateral body wave that grows toward the tail.</summary>
        Undulate = 1,
        /// <summary>Pectoral fins travel a wave from front to back, as in rays.</summary>
        RayWave = 2,
        Walk = 3,
        Tentacles = 4,
        Pulse = 5,
        Static = 6,
        Jet = 7,
    }

    /// <summary>Special anatomy beyond the ordinary bird and fish layouts.</summary>
    public enum FlockBodyShape
    {
        Default = 0,
        Squid = 1, Jellyfish = 3, GardenEel = 4, Crab = 5,
        Eel = 6, Urchin = 7, Anemone = 8, Oyster = 9, FlyingFish = 10,
        Seahorse = 11, Chicken = 12, Chick = 13,
    }

    public enum FlockWingShape
    {
        /// <summary>Narrow tapered tip, swept back.</summary>
        Pointed = 0,
        /// <summary>Short and rounded, for small woodland birds.</summary>
        Rounded = 1,
        /// <summary>Broad with separated primaries at the tip, for soaring raptors and storks.</summary>
        Slotted = 2,
        /// <summary>Long and narrow, for seabirds.</summary>
        Long = 3,
        /// <summary>Strongly swept sickle shape, for swifts and swallows.</summary>
        Sickle = 4,
        /// <summary>Stretched membrane with a scalloped trailing edge.</summary>
        Membrane = 5,
    }

    public enum FlockTailShape
    {
        Short = 0,
        Square = 1,
        Forked = 2,
        DeepFork = 3,
        Wedge = 4,
        Fan = 5,
        Long = 6,
    }

    public enum FlockFishBody
    {
        /// <summary>Spindle shape of most schooling fish.</summary>
        Fusiform = 0,
        /// <summary>Thick spindle with a narrow tail stock, as in tuna and sharks.</summary>
        Torpedo = 1,
        /// <summary>Long and slender, as in barracuda.</summary>
        Elongated = 2,
        /// <summary>Tall and laterally flattened, as in butterflyfish and angelfish.</summary>
        Disc = 3,
        /// <summary>Flattened top to bottom with wide pectoral fins, as in rays.</summary>
        Ray = 4,
    }

    public enum FlockCaudalFin
    {
        Forked = 0,
        Lunate = 1,
        Truncate = 2,
        Rounded = 3,
        /// <summary>Long flowing double tail of fancy goldfish and guppies.</summary>
        Veil = 4,
        /// <summary>Upper lobe longer than the lower, as in sharks.</summary>
        Heterocercal = 5,
        /// <summary>Thin whip, as in rays.</summary>
        Whip = 6,
    }

    public enum FlockColorPattern
    {
        Plain = 0,
        /// <summary>Vertical bars over the whole flank.</summary>
        VerticalBars = 1,
        /// <summary>Wavy bars on the upper flank only.</summary>
        BackBars = 2,
        /// <summary>One horizontal stripe along the flank.</summary>
        LateralStripe = 3,
        /// <summary>Several horizontal stripes.</summary>
        HorizontalStripes = 4,
        /// <summary>Horizontal stripes on the lower flank only.</summary>
        BellyStripes = 5,
        /// <summary>Scattered spots.</summary>
        Spots = 6,
        /// <summary>Large irregular patches.</summary>
        Patches = 7,
        /// <summary>One dark bar through the eye.</summary>
        EyeBar = 8,
    }
}
