using System;
using System.Collections.Generic;
using LogistiCore.Map;
using UnityEngine;

namespace LogistiCore.EditorTools
{
    /// <summary>
    /// Offline ear clipping for Natural Earth country rings.
    /// The land silhouette is this mesh. A low-resolution raster is not the coastline.
    /// </summary>
    static class MapPolygonFill
    {
        const double Epsilon = 1e-8;

        sealed class Node
        {
            public double X;
            public double Y;
            public double U;
            public double V;
            public Node Prev;
            public Node Next;
        }

        public static bool TryTriangulate(
            IList<double> outerLongitude,
            IList<double> outerLatitude,
            List<List<double>> holeLongitudes,
            List<List<double>> holeLatitudes,
            List<Vector3> vertices,
            List<Vector2> uv,
            List<int> triangles)
        {
            if (!TryBuild(outerLongitude, outerLatitude, wantCounterClockwise: true, out var outer))
                return false;

            var bridged = true;
            if (holeLongitudes != null)
            {
                for (var h = 0; h < holeLongitudes.Count; h++)
                {
                    if (!TryBuild(holeLongitudes[h], holeLatitudes[h], wantCounterClockwise: false, out var hole))
                        continue;
                    if (!Bridge(outer, hole))
                        bridged = false;
                }
            }

            if (!bridged || !EarClip(outer, vertices, uv, triangles))
            {
                if (!TryBuild(outerLongitude, outerLatitude, wantCounterClockwise: true, out outer))
                    return false;
                return EarClip(outer, vertices, uv, triangles);
            }

            return true;
        }

        static bool TryBuild(IList<double> longitude, IList<double> latitude, bool wantCounterClockwise, out Node ring)
        {
            ring = null;
            if (longitude == null || latitude == null)
                return false;

            var count = Math.Min(longitude.Count, latitude.Count);
            if (count >= 2 &&
                Nearly(longitude[0], longitude[count - 1]) &&
                Nearly(latitude[0], latitude[count - 1]))
                count--;

            Node first = null;
            Node previous = null;
            var built = 0;
            for (var i = 0; i < count; i++)
            {
                var lon = longitude[i];
                if (lon > 180d)
                    lon = 180d;
                if (lon < -180d)
                    lon = -180d;
                if (!MapProjection.TryProjectUnwrapped(latitude[i], lon, out var world))
                    continue;
                if (previous != null && Nearly(previous.X, world.x) && Nearly(previous.Y, world.y))
                    continue;

                var node = new Node
                {
                    X = world.x,
                    Y = world.y,
                    U = (lon + 180d) / 360d,
                    V = (latitude[i] + 90d) / 180d
                };
                if (first == null)
                    first = node;
                else
                {
                    previous.Next = node;
                    node.Prev = previous;
                }

                previous = node;
                built++;
            }

            if (built < 3)
                return false;

            previous.Next = first;
            first.Prev = previous;
            if (wantCounterClockwise && SignedArea(first) < 0d)
                Reverse(first);
            else if (!wantCounterClockwise && SignedArea(first) > 0d)
                Reverse(first);

            ring = Filter(first);
            return ring != null && Count(ring) >= 3;
        }

        static bool EarClip(Node start, List<Vector3> vertices, List<Vector2> uv, List<int> triangles, int depth = 0)
        {
            var remaining = Count(start);
            if (remaining < 3)
                return false;

            var queue = new Queue<Node>(remaining);
            var queued = new HashSet<Node>();
            var cursor = start;
            do
            {
                if (IsEar(cursor))
                {
                    queue.Enqueue(cursor);
                    queued.Add(cursor);
                }

                cursor = cursor.Next;
            }
            while (cursor != start);

            var ears = new List<Node>(remaining * 3);
            var live = start;
            var guard = remaining * remaining;
            while (remaining > 3 && queue.Count > 0 && guard-- > 0)
            {
                var ear = queue.Dequeue();
                queued.Remove(ear);
                if (ear.Prev == null || ear.Next == null || ear.Prev.Next != ear || ear.Next.Prev != ear)
                    continue;
                if (!IsEar(ear))
                    continue;

                var previous = ear.Prev;
                var next = ear.Next;
                ears.Add(previous);
                ears.Add(ear);
                ears.Add(next);
                previous.Next = next;
                next.Prev = previous;
                ear.Prev = null;
                ear.Next = null;
                remaining--;
                live = next;
                Offer(previous, queue, queued);
                Offer(next, queue, queued);
            }

            if (remaining == 3 && live != null && live.Prev != null && live.Next != null)
            {
                ears.Add(live.Prev);
                ears.Add(live);
                ears.Add(live.Next);
                Append(ears, vertices, uv, triangles);
                return true;
            }

            if (depth > 12 || live == null || live.Next == null || !TrySplit(live, out var left, out var right))
                return false;

            if (ears.Count > 0)
                Append(ears, vertices, uv, triangles);

            return EarClip(left, vertices, uv, triangles, depth + 1) &&
                   EarClip(right, vertices, uv, triangles, depth + 1);
        }

        static bool TrySplit(Node start, out Node left, out Node right)
        {
            left = null;
            right = null;
            var cursor = start;
            var checks = 0;
            do
            {
                var other = cursor.Next != null ? cursor.Next.Next : null;
                while (other != null && other != cursor && other != cursor.Prev && checks++ < 250000)
                {
                    if (IsDiagonal(cursor, other))
                    {
                        var cursorCopy = new Node { X = cursor.X, Y = cursor.Y, U = cursor.U, V = cursor.V };
                        var otherCopy = new Node { X = other.X, Y = other.Y, U = other.U, V = other.V };
                        var cursorNext = cursor.Next;
                        var otherPrev = other.Prev;
                        cursor.Next = other;
                        other.Prev = cursor;
                        cursorCopy.Next = cursorNext;
                        cursorNext.Prev = cursorCopy;
                        otherCopy.Next = cursorCopy;
                        cursorCopy.Prev = otherCopy;
                        otherPrev.Next = otherCopy;
                        otherCopy.Prev = otherPrev;
                        left = cursor;
                        right = cursorCopy;
                        return true;
                    }

                    other = other.Next;
                    if (other == cursor)
                        break;
                }

                cursor = cursor.Next;
            }
            while (cursor != start && checks < 250000);

            return false;
        }

        static bool IsDiagonal(Node a, Node b)
        {
            if (a == null || b == null || a == b || a.Next == b || a.Prev == b)
                return false;
            if (!LocallyInside(a, b) || !LocallyInside(b, a))
                return false;

            var midpointX = (a.X + b.X) * 0.5d;
            var midpointY = (a.Y + b.Y) * 0.5d;
            if (!PointInRing(midpointX, midpointY, a))
                return false;

            var cursor = a.Next;
            while (cursor != a && cursor != null)
            {
                var next = cursor.Next;
                if (next != null && cursor != b && next != b && cursor != a && next != a &&
                    SegmentsCross(a, b, cursor, next))
                    return false;

                cursor = next;
            }

            return true;
        }

        static bool PointInRing(double x, double y, Node start)
        {
            var inside = false;
            var cursor = start;
            var guard = 0;
            do
            {
                var next = cursor.Next;
                if (next == null)
                    return false;
                if ((cursor.Y > y) != (next.Y > y))
                {
                    var crossX = (next.X - cursor.X) * (y - cursor.Y) / (next.Y - cursor.Y) + cursor.X;
                    if (x < crossX)
                        inside = !inside;
                }

                cursor = next;
            }
            while (cursor != start && ++guard < 1000000);

            return inside;
        }

        static bool SegmentsCross(Node a, Node b, Node c, Node d)
        {
            var abC = Cross(a, b, c);
            var abD = Cross(a, b, d);
            var cdA = Cross(c, d, a);
            var cdB = Cross(c, d, b);
            return ((abC > Epsilon && abD < -Epsilon) || (abC < -Epsilon && abD > Epsilon)) &&
                   ((cdA > Epsilon && cdB < -Epsilon) || (cdA < -Epsilon && cdB > Epsilon));
        }

        static void Offer(Node node, Queue<Node> queue, HashSet<Node> queued)
        {
            if (node == null || queued.Contains(node) || node.Prev == null || node.Next == null)
                return;
            if (!IsEar(node))
                return;

            queue.Enqueue(node);
            queued.Add(node);
        }

        static bool IsEar(Node node)
        {
            var previous = node.Prev;
            var next = node.Next;
            if (previous == null || next == null || previous == next)
                return false;
            if (Cross(previous, node, next) <= Epsilon)
                return false;

            return !ContainsAnotherPoint(previous, node, next);
        }

        static void Append(List<Node> ears, List<Vector3> vertices, List<Vector2> uv, List<int> triangles)
        {
            var indexOf = new Dictionary<Node, int>(ears.Count);
            for (var i = 0; i < ears.Count; i++)
            {
                var node = ears[i];
                if (!indexOf.TryGetValue(node, out var index))
                {
                    index = vertices.Count;
                    indexOf.Add(node, index);
                    vertices.Add(new Vector3((float)node.X, (float)node.Y, 0f));
                    uv.Add(new Vector2((float)node.U, (float)node.V));
                }

                triangles.Add(index);
            }
        }

        static bool Bridge(Node outer, Node hole)
        {
            var left = hole;
            var cursor = hole.Next;
            while (cursor != hole)
            {
                if (cursor.X < left.X)
                    left = cursor;
                cursor = cursor.Next;
            }

            var bridge = FindBridge(left, outer);
            if (bridge == null || !LocallyInside(bridge, left))
                return false;

            var outerCopy = new Node { X = bridge.X, Y = bridge.Y, U = bridge.U, V = bridge.V };
            var holeCopy = new Node { X = left.X, Y = left.Y, U = left.U, V = left.V };
            var outerNext = bridge.Next;
            var holePrev = left.Prev;
            bridge.Next = left;
            left.Prev = bridge;
            outerCopy.Next = outerNext;
            outerNext.Prev = outerCopy;
            holeCopy.Next = outerCopy;
            outerCopy.Prev = holeCopy;
            holePrev.Next = holeCopy;
            holeCopy.Prev = holePrev;
            return true;
        }

        static Node FindBridge(Node hole, Node outer)
        {
            var hx = hole.X;
            var hy = hole.Y;
            var closest = double.NegativeInfinity;
            Node chosen = null;
            var cursor = outer;
            do
            {
                var next = cursor.Next;
                var straddles = (hy <= cursor.Y && hy >= next.Y) || (hy >= cursor.Y && hy <= next.Y);
                var dy = next.Y - cursor.Y;
                if (straddles && Math.Abs(dy) > 1e-15)
                {
                    var x = cursor.X + (hy - cursor.Y) * (next.X - cursor.X) / dy;
                    if (x <= hx && x > closest)
                    {
                        closest = x;
                        chosen = cursor.X < next.X ? cursor : next;
                    }
                }

                cursor = cursor.Next;
            }
            while (cursor != outer);

            if (chosen == null)
                return null;

            var originX = chosen.X;
            var originY = chosen.Y;
            var bestSlope = double.PositiveInfinity;
            var stop = chosen;
            cursor = chosen;
            do
            {
                if (hx >= cursor.X && cursor.X >= originX && Math.Abs(hx - cursor.X) > 1e-9 &&
                    Inside(cursor.X, cursor.Y, hx, hy, originX, originY, closest, hy))
                {
                    var slope = Math.Abs(hy - cursor.Y) / (hx - cursor.X);
                    if (slope < bestSlope && LocallyInside(cursor, hole))
                    {
                        chosen = cursor;
                        bestSlope = slope;
                    }
                }

                cursor = cursor.Next;
            }
            while (cursor != stop);

            return chosen;
        }

        static bool LocallyInside(Node corner, Node other)
        {
            if (Cross(corner.Prev, corner, corner.Next) < 0d)
                return Cross(corner, other, corner.Next) >= 0d && Cross(corner, corner.Prev, other) >= 0d;

            return Cross(corner, other, corner.Prev) < 0d || Cross(corner, corner.Next, other) < 0d;
        }

        static bool ContainsAnotherPoint(Node a, Node b, Node c)
        {
            var cursor = c.Next;
            while (cursor != a)
            {
                if (cursor != b && StrictInside(cursor.X, cursor.Y, a.X, a.Y, b.X, b.Y, c.X, c.Y))
                    return true;
                cursor = cursor.Next;
            }

            return false;
        }

        static bool StrictInside(double px, double py, double ax, double ay, double bx, double by, double cx, double cy)
        {
            var ab = (bx - ax) * (py - ay) - (by - ay) * (px - ax);
            var bc = (cx - bx) * (py - by) - (cy - by) * (px - bx);
            var ca = (ax - cx) * (py - cy) - (ay - cy) * (px - cx);
            var positive = ab > Epsilon && bc > Epsilon && ca > Epsilon;
            var negative = ab < -Epsilon && bc < -Epsilon && ca < -Epsilon;
            return positive || negative;
        }

        static bool Inside(double px, double py, double ax, double ay, double bx, double by, double cx, double cy)
        {
            var ab = (bx - ax) * (py - ay) - (by - ay) * (px - ax);
            var bc = (cx - bx) * (py - by) - (cy - by) * (px - bx);
            var ca = (ax - cx) * (py - cy) - (ay - cy) * (px - cx);
            var hasNegative = ab < -Epsilon || bc < -Epsilon || ca < -Epsilon;
            var hasPositive = ab > Epsilon || bc > Epsilon || ca > Epsilon;
            return !(hasNegative && hasPositive);
        }

        static Node Filter(Node start)
        {
            var removed = true;
            var guard = 0;
            while (removed && guard++ < 1000000)
            {
                removed = false;
                if (start == null || start.Prev == start.Next)
                    return null;

                var cursor = start;
                do
                {
                    var previous = cursor.Prev;
                    var next = cursor.Next;
                    if (Math.Abs(Cross(previous, cursor, next)) <= Epsilon)
                    {
                        previous.Next = next;
                        next.Prev = previous;
                        if (cursor == start)
                            start = next;
                        removed = true;
                        cursor = next;
                        continue;
                    }

                    cursor = next;
                }
                while (cursor != start);
            }

            return start != null && Count(start) >= 3 ? start : null;
        }

        static void Reverse(Node start)
        {
            var cursor = start;
            do
            {
                var next = cursor.Next;
                cursor.Next = cursor.Prev;
                cursor.Prev = next;
                cursor = next;
            }
            while (cursor != start);
        }

        static int Count(Node start)
        {
            var count = 0;
            var cursor = start;
            do
            {
                count++;
                cursor = cursor.Next;
            }
            while (cursor != start && count < 1000000);

            return count;
        }

        static double SignedArea(Node start)
        {
            var sum = 0d;
            var cursor = start;
            do
            {
                sum += cursor.X * cursor.Next.Y - cursor.Next.X * cursor.Y;
                cursor = cursor.Next;
            }
            while (cursor != start);

            return sum * 0.5d;
        }

        static double Cross(Node a, Node b, Node c)
        {
            return (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        }

        static bool Nearly(double a, double b)
        {
            return Math.Abs(a - b) <= 1e-9;
        }
    }
}
