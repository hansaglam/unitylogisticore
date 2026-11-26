using UnityEngine;

namespace LogistiCore.Depot
{
    /// <summary>
    /// Read-only 2D hit zones for the authored fixed camera. Selection changes
    /// the inspection tab only; never activates a facility or dispatches cargo.
    /// </summary>
    public enum DepotYardInspectTarget : byte
    {
        None = 0,
        Parcel = 1,
        Storage = 2
    }

    public static class DepotYardInspectionPolicy
    {
        public static DepotYardInspectTarget Resolve(DepotVisualStage stage,
            Vector2 localPoint)
        {
            if (stage == DepotVisualStage.Unverified ||
                !IsFinite(localPoint))
                return DepotYardInspectTarget.None;

            if (stage == DepotVisualStage.StorageOperational &&
                (Inside(localPoint,
                    new Vector2(-3.15f, 1.35f),
                    new Vector2(0.55f, 2.42f),
                    new Vector2(4.18f, 0.91f),
                    new Vector2(0.45f, -0.22f)) ||
                 Inside(localPoint,
                    new Vector2(-3.15f, 1.35f),
                    new Vector2(0.45f, -0.22f),
                    new Vector2(0.45f, -2.32f),
                    new Vector2(-3.15f, -0.75f)) ||
                 Inside(localPoint,
                    new Vector2(0.45f, -0.22f),
                    new Vector2(4.18f, 0.91f),
                    new Vector2(4.18f, -1.19f),
                    new Vector2(0.45f, -2.32f))))
                return DepotYardInspectTarget.Storage;

            if (Inside(localPoint,
                new Vector2(-3.7f, -0.3f),
                new Vector2(0.4f, 1.15f),
                new Vector2(4.7f, -0.35f),
                new Vector2(0.6f, -1.9f)))
                return DepotYardInspectTarget.Parcel;

            return DepotYardInspectTarget.None;
        }

        static bool IsFinite(Vector2 point) =>
            !float.IsNaN(point.x) && !float.IsInfinity(point.x) &&
            !float.IsNaN(point.y) && !float.IsInfinity(point.y);

        static bool Inside(Vector2 p, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            // No allocation on pointer taps; evaluate the four edges directly.
            var ab = Cross(a, b, p);
            var bc = Cross(b, c, p);
            var cd = Cross(c, d, p);
            var da = Cross(d, a, p);
            const float epsilon = 0.0001f;
            var positive = ab > epsilon || bc > epsilon ||
                cd > epsilon || da > epsilon;
            var negative = ab < -epsilon || bc < -epsilon ||
                cd < -epsilon || da < -epsilon;
            return !(positive && negative);
        }

        static float Cross(Vector2 a, Vector2 b, Vector2 p)
        {
            return (b.x - a.x) * (p.y - a.y) -
                (b.y - a.y) * (p.x - a.x);
        }
    }
}
