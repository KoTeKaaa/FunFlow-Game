using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

// Test-only forward route over the saved level's current collider bounds.
internal static class LevelTestRoute
{
    internal static Vector3[] Build(Rigidbody player, Vector3 goal, bool avoidCutters)
    {
        Vector3 start = player.position; start.y = goal.y = 0;
        var colliders = player.gameObject.scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Collider>()).Where(c => c.enabled).ToArray();
        var floor = colliders.Where(c => c.gameObject.layer == LayerMask.NameToLayer("Ground"))
            .OrderByDescending(c => c.bounds.size.x).First().bounds;
        float radius = player.GetComponent<Collider>().bounds.extents.x + 0.08f;
        var blocked = new List<Bounds>();
        foreach (var collider in colliders)
        {
            bool cutter = collider.isTrigger && collider.gameObject.layer == LayerMask.NameToLayer("Cutter");
            if (collider.attachedRigidbody == player || (collider.isTrigger && !(cutter && avoidCutters))) continue;
            var bounds = collider.bounds;
            if (!cutter && (bounds.max.y <= 0.1f || bounds.min.y >= player.position.y + radius)) continue;
            if (cutter)
            {
                // Full bag: 0.6 m radius, following 0.8 m behind the player.
                bounds.center += Vector3.right * 0.8f;
                bounds.Expand(new Vector3(1.36f, 0, 1.36f));
            }
            else bounds.Expand(new Vector3(radius * 2, 0, radius * 2));
            blocked.Add(bounds);
        }
        bool Clear(Vector3 a, Vector3 b)
        {
            Vector3 delta = b - a;
            if (delta.x <= 0 || Mathf.Abs(delta.z) > delta.x * Mathf.Tan(77f * Mathf.Deg2Rad)) return false;
            foreach (var bounds in blocked)
            {
                float low = 0, high = 1;
                foreach (int axis in new[] { 0, 2 })
                {
                    if (Mathf.Abs(delta[axis]) < 0.0001f)
                    {
                        if (a[axis] < bounds.min[axis] || a[axis] > bounds.max[axis]) { low = 2; break; }
                    }
                    else
                    {
                        float first = (bounds.min[axis] - a[axis]) / delta[axis];
                        float last = (bounds.max[axis] - a[axis]) / delta[axis];
                        low = Mathf.Max(low, Mathf.Min(first, last)); high = Mathf.Min(high, Mathf.Max(first, last));
                    }
                }
                if (low <= high) return false;
            }
            return true;
        }
        const float stepX = 0.5f, stepZ = 0.25f;
        float minZ = floor.min.z + radius;
        int rows = Mathf.CeilToInt((goal.x - start.x) / stepX), columns = Mathf.FloorToInt((floor.size.z - radius * 2) / stepZ) + 1;
        var costs = new float[rows, columns]; var previous = new int[rows, columns];
        Vector3 Point(int row, int column) => new Vector3(start.x + row * stepX, 0, minZ + column * stepZ);
        for (int row = 0; row < rows; row++)
            for (int column = 0; column < columns; column++) { costs[row, column] = float.PositiveInfinity; previous[row, column] = -1; }
        for (int row = 1; row < rows; row++)
            for (int column = 0; column < columns; column++)
            {
                var point = Point(row, column);
                if (row == 1 && Clear(start, point)) { costs[row, column] = Vector3.Distance(start, point); continue; }
                for (int from = Mathf.Max(0, column - 8); from <= Mathf.Min(columns - 1, column + 8); from++)
                {
                    if (!float.IsFinite(costs[row - 1, from])) continue;
                    var source = Point(row - 1, from); float cost = costs[row - 1, from] + Vector3.Distance(source, point);
                    if (cost < costs[row, column] && Clear(source, point)) { costs[row, column] = cost; previous[row, column] = from; }
                }
            }
        int best = -1; float total = float.PositiveInfinity;
        for (int column = 0; column < columns; column++)
        {
            var point = Point(rows - 1, column); float cost = costs[rows - 1, column] + Vector3.Distance(point, goal);
            if (cost < total && Clear(point, goal)) { total = cost; best = column; }
        }
        Assert.That(best, Is.GreaterThanOrEqualTo(0), "No forward route through the current level collider bounds.");
        var points = new List<Vector3> { goal };
        for (int row = rows - 1, column = best; row > 0; column = previous[row, column], row--)
            points.Add(Point(row, column));
        points.Add(start); points.Reverse();
        var route = new List<Vector3>();
        for (int index = 0; index < points.Count - 1;)
        {
            int next = points.Count - 1;
            while (next > index + 1 && !Clear(points[index], points[next])) next--;
            route.Add(points[next]); index = next;
        }
        return route.ToArray();
    }
}
