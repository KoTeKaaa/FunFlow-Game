using NUnit.Framework;
using UnityEngine;

public class GuardVisionTests
{
    private static readonly Vector3 Origin = new Vector3(2f, 0.5f, -3f);

    private static bool InSector(Vector3 forward, float angle, float range, Vector3 offset) =>
        GuardVision.IsPointInSector(Origin, forward, angle, range, Origin + offset);

    [Test]
    public void PointStraightAhead_WithinRange_IsInside()
    {
        Assert.IsTrue(InSector(Vector3.forward, 60f, 6f, new Vector3(0f, 0f, 3f)));
    }

    [Test]
    public void PointOnRangeBoundary_IsInside_AndJustBeyond_IsOutside()
    {
        Assert.IsTrue(InSector(Vector3.forward, 60f, 6f, new Vector3(0f, 0f, 6f)));
        Assert.IsFalse(InSector(Vector3.forward, 60f, 6f, new Vector3(0f, 0f, 6.01f)));
    }

    [Test]
    public void PointOnHalfAngleEdge_IsInside_AndJustOutsideAngle_IsOutside()
    {
        Assert.IsTrue(InSector(Vector3.forward, 60f, 6f, GuardVision.Direction(30f) * 4f));
        Assert.IsTrue(InSector(Vector3.forward, 60f, 6f, GuardVision.Direction(-30f) * 4f));
        Assert.IsFalse(InSector(Vector3.forward, 60f, 6f, GuardVision.Direction(31f) * 4f));
        Assert.IsFalse(InSector(Vector3.forward, 60f, 6f, GuardVision.Direction(-31f) * 4f));
    }

    [Test]
    public void PointBehindOrToTheSide_OfNarrowSector_IsOutside()
    {
        Assert.IsFalse(InSector(Vector3.forward, 60f, 6f, new Vector3(0f, 0f, -2f)));
        Assert.IsFalse(InSector(Vector3.forward, 60f, 6f, new Vector3(2f, 0f, 0f)));
    }

    [Test]
    public void WideSector_SeesSides_ButNotBehind()
    {
        Assert.IsTrue(InSector(Vector3.forward, 180f, 6f, new Vector3(3f, 0f, 0f)));
        Assert.IsTrue(InSector(Vector3.forward, 180f, 6f, new Vector3(-3f, 0f, 0f)));
        Assert.IsFalse(InSector(Vector3.forward, 180f, 6f, new Vector3(0f, 0f, -0.5f)));
    }

    [Test]
    public void RotatedForward_UsesItsDirection()
    {
        Vector3 forward = GuardVision.Direction(90f);
        Assert.IsTrue(InSector(forward, 60f, 6f, new Vector3(4f, 0f, 1f)));
        Assert.IsFalse(InSector(forward, 60f, 6f, new Vector3(0f, 0f, 4f)));
        Assert.IsFalse(InSector(forward, 60f, 6f, new Vector3(-4f, 0f, 0f)));
    }

    [Test]
    public void Height_IsIgnored_OnlyFloorDistanceCounts()
    {
        Assert.IsTrue(InSector(Vector3.forward, 60f, 6f, new Vector3(0f, 10f, 5f)));
        Assert.IsTrue(InSector(new Vector3(0f, -0.3f, 1f), 60f, 6f, new Vector3(0f, -2f, 5f)));
    }

    [Test]
    public void ObserverPoint_IsInside_AndDegenerateSector_SeesNothing()
    {
        Assert.IsTrue(InSector(Vector3.forward, 60f, 6f, Vector3.zero));
        Assert.IsFalse(InSector(Vector3.forward, 60f, 0f, new Vector3(0f, 0f, 0.1f)));
        Assert.IsFalse(InSector(Vector3.forward, 0f, 6f, new Vector3(0f, 0f, 1f)));
        Assert.IsFalse(InSector(Vector3.up, 60f, 6f, new Vector3(0f, 0f, 1f)));
    }

    [Test]
    public void DirectionAndYaw_AreInverse_AndClockwiseFromAbove()
    {
        Vector3 east = GuardVision.Direction(90f);
        Assert.That(east.x, Is.EqualTo(1f).Within(1e-5f));
        Assert.That(east.z, Is.EqualTo(0f).Within(1e-5f));
        Assert.That(GuardVision.Yaw(GuardVision.Direction(-135f)), Is.EqualTo(-135f).Within(1e-3f));
    }
}
