using UnityEngine;

namespace SabaProps.Foliage
{
    /// <summary>
    /// Geometry shared by fields that scatter different procedural props over
    /// the same rectangle/circle model.
    /// </summary>
    public static class FoliageAreaUtility
    {
        public static Vector2 LocalExtents(
            FoliageAreaShape shape, Vector2 size, float radius)
        {
            return shape == FoliageAreaShape.Circle
                ? new Vector2(radius, radius)
                : new Vector2(Mathf.Abs(size.x) * 0.5f, Mathf.Abs(size.y) * 0.5f);
        }

        public static float AreaSquareMeters(
            FoliageAreaShape shape, Vector2 size, float radius)
        {
            return shape == FoliageAreaShape.Circle
                ? Mathf.PI * radius * radius
                : Mathf.Abs(size.x) * Mathf.Abs(size.y);
        }

        public static bool ContainsLocalPoint(
            FoliageAreaShape shape, Vector2 size, float radius,
            float x, float z)
        {
            if (shape == FoliageAreaShape.Circle)
            {
                return x * x + z * z <= radius * radius;
            }

            Vector2 extents = LocalExtents(shape, size, radius);
            return Mathf.Abs(x) <= extents.x && Mathf.Abs(z) <= extents.y;
        }

        public static Vector2 LocalPointToMaskUv(
            FoliageAreaShape shape, Vector2 size, float radius,
            float x, float z)
        {
            Vector2 extents = LocalExtents(shape, size, radius);
            return new Vector2(
                Mathf.InverseLerp(-extents.x, extents.x, x),
                Mathf.InverseLerp(-extents.y, extents.y, z));
        }
    }
}
