using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class CorridorCameraTests
{
    private GameObject _root;
    private Transform _target;
    private Camera _camera;
    private CorridorCamera _follow;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        bool reload = !EditorSettings.enterPlayModeOptionsEnabled
            || (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) == 0;
        yield return new EnterPlayMode(reload);
        _target = new GameObject("Camera target test").transform;
        _root = new GameObject("Camera test");
        _root.SetActive(false);
        _camera = _root.AddComponent<Camera>();
        _camera.enabled = false;
        _camera.orthographic = true;
        _camera.orthographicSize = 8f;
        _root.transform.rotation = Quaternion.Euler(60f, 0f, 0f);
        _follow = _root.AddComponent<CorridorCamera>();
        var serialized = new SerializedObject(_follow);
        serialized.FindProperty("_target").objectReferenceValue = _target;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (_target != null) Object.DestroyImmediate(_target.gameObject);
        Object.DestroyImmediate(_root);
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator Follow_ShowsFullWidthAndForwardMotionAndReturnsToStart()
    {
        _root.SetActive(true);
        foreach (float aspect in new[] { 16f / 9f, 4f / 3f })
        {
            _camera.aspect = aspect;
            foreach (float x in new[] { 0f, 30f, 60f, 0f })
            {
                _target.position = new Vector3(x, 3f, 5f);
                _target.rotation = Quaternion.Euler(20f, x, 45f);
                yield return null;
                Assert.That(_root.transform.position, Is.EqualTo(new Vector3(x + 6f, 14f, -8f)));
                Assert.That(Quaternion.Angle(_root.transform.rotation, Quaternion.Euler(60f, 0f, 0f)), Is.LessThan(0.001f));
                foreach (float z in new[] { -6f, 6f })
                {
                    var point = _camera.WorldToViewportPoint(new Vector3(x, 0f, z));
                    Assert.That(point.x, Is.InRange(0f, 1f));
                    Assert.That(point.y, Is.InRange(0f, 1f));
                    Assert.That(point.z, Is.GreaterThan(0f));
                }
                Assert.That(_camera.WorldToViewportPoint(new Vector3(x + 1f, 0f, 0f)).x,
                    Is.GreaterThan(_camera.WorldToViewportPoint(new Vector3(x, 0f, 0f)).x));
            }
        }
    }

    [UnityTest]
    public IEnumerator DestroyedTarget_ReportsErrorAndDisablesFollow()
    {
        _root.SetActive(true);
        Object.DestroyImmediate(_target.gameObject);
        LogAssert.Expect(LogType.Error, "CorridorCamera: assign Target in Inspector.");
        yield return null;
        Assert.That(_follow.enabled, Is.False);
    }

    [Test]
    public void MissingTarget_ReportsErrorOnActivation()
    {
        var serialized = new SerializedObject(_follow);
        serialized.FindProperty("_target").objectReferenceValue = null;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        LogAssert.Expect(LogType.Error, "CorridorCamera: assign Target in Inspector.");
        _root.SetActive(true);
        Assert.That(_follow.enabled, Is.False);
    }
}
