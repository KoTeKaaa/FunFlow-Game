using UnityEngine;

/// <summary>
/// Чистая геометрия зрения охранника на полу XZ: высота (Y) не учитывается.
/// Используется и для обнаружения вора, и для построения конуса, чтобы они совпадали.
/// </summary>
public static class GuardVision
{
    // Допуск в градусах на краю сектора: точка ровно на границе считается видимой.
    private const float AngleTolerance = 0.01f;

    /// <summary>
    /// Точка внутри сектора: не дальше <paramref name="range"/> от <paramref name="origin"/>
    /// и не дальше половины <paramref name="viewAngle"/> от <paramref name="forward"/>.
    /// Граница включается. Сама точка наблюдателя считается видимой.
    /// </summary>
    public static bool IsPointInSector(Vector3 origin, Vector3 forward, float viewAngle, float range, Vector3 point)
    {
        if (!(range > 0f) || !(viewAngle > 0f))
            return false;

        Vector2 offset = new Vector2(point.x - origin.x, point.z - origin.z);
        float sqrDistance = offset.sqrMagnitude;
        if (sqrDistance > range * range)
            return false;
        if (sqrDistance < 1e-8f || viewAngle >= 360f)
            return true;

        Vector2 facing = new Vector2(forward.x, forward.z);
        if (facing.sqrMagnitude < 1e-8f)
            return false;

        return Vector2.Angle(facing, offset) <= viewAngle * 0.5f + AngleTolerance;
    }

    /// <summary>Направление на полу по углу поворота вокруг Y: 0 — +Z, 90 — +X (по часовой, вид сверху).</summary>
    public static Vector3 Direction(float yawDegrees)
    {
        float radians = yawDegrees * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));
    }

    /// <summary>Угол поворота вокруг Y для направления на полу (обратное к <see cref="Direction"/>).</summary>
    public static float Yaw(Vector3 direction) => Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
}
