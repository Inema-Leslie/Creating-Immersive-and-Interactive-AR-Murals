using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MuralAR.Tests
{
    public sealed class M2M3CarTests
    {
        MuralTracker _tracker;
        ExperienceController _experience;
        CarsDirector _director;
        TouchInput _touch;
        CarInfoPanel _panel;
        MuralConfig _config;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            yield return Sandbox.Load();
            _tracker = Object.FindAnyObjectByType<MuralTracker>();
            _experience = Object.FindAnyObjectByType<ExperienceController>();
            _director = Object.FindAnyObjectByType<CarsDirector>();
            _touch = Object.FindAnyObjectByType<TouchInput>();
            _panel = Object.FindAnyObjectByType<CarInfoPanel>();
            _config = _tracker.Config;
            yield return WaitUntil(() => _director.Cars.Count == 2, 5f, "cars were not spawned when the mural was found");
        }

        [UnityTest]
        public IEnumerator Cars_WakeLiftOff_RespondToGestures_AndReturnHome()
        {
            var a = _director.Cars[0];
            var b = _director.Cars[1];
            Assert.AreEqual(CarState.Dormant, a.State);
            float minGap = float.PositiveInfinity;
            string minGapWhen = "";
            var watcher = new GameObject("Clearance watcher").AddComponent<Watcher>();
            watcher.Tick = () =>
            {
                if (_director.Clearance >= minGap) return;
                minGap = _director.Clearance;
                minGapWhen = $"A {a.State}/{a.Maneuver}, B {b.State}/{b.Maneuver}, t={Time.time:F1}";
            };

            yield return WaitUntil(() => a.State == CarState.Waking, _config.carsWakeAfterSeconds + 1f, "Car A never woke");
            yield return new WaitForSeconds(2.2f);
            yield return Shot("m2-01-car-a-peeling-off");
            yield return WaitUntil(() => a.State == CarState.Idle && b.State == CarState.Idle, _config.carLiftOffSeconds + _config.carB.wakeDelaySeconds + 6f, "cars never reached their hover spots");
            yield return WaitUntil(() => _experience.State == ExperienceState.CarsActive, 1f, "experience should move to CarsActive");
            float lengthA = Length(a);
            Assert.AreEqual(a.Settings.flyingLengthMeters, lengthA, a.Settings.flyingLengthMeters * 0.15f, "Car A should be full size");
            yield return Shot("m2-02-both-hovering");

            _touch.SimulateHold(a.gameObject, true);
            Assert.AreEqual(CarState.Revving, a.State);
            yield return new WaitForSeconds(_config.holdToFullRevSeconds + 0.1f);
            Assert.Greater(a.Throttle, 0.95f, "throttle should reach full while held");
            yield return Shot("m2-03-revving");
            _touch.SimulateHold(a.gameObject, false);
            Assert.AreEqual(CarState.Idle, a.State);

            _touch.SimulateSwipe(b.gameObject, new Vector2(400f, 120f));
            Assert.AreEqual(CarState.Flying, b.State);
            yield return new WaitForSeconds(1.2f);
            yield return Shot("m3-01-swipe-launch");
            yield return WaitUntil(() => b.State == CarState.Idle, 20f, "Car B never came back from its launch");

            var cam = Camera.main.transform;
            _touch.SimulateTap(a.gameObject);
            _touch.SimulateTap(a.gameObject);
            Assert.AreEqual(CarState.Flying, a.State, "double tap should start a fly-by");
            float closest = float.MaxValue;
            while (a.State == CarState.Flying)
            {
                closest = Mathf.Min(closest, Vector3.Distance(a.transform.position, cam.position));
                yield return null;
            }
            Assert.GreaterOrEqual(closest, _config.carMinCameraDistance - 0.05f, "fly-by came too close to the camera");

            yield return WaitUntil(() => a.State == CarState.Idle, 10f, "Car A didn't settle after the fly-by");
            var icon = a.transform.Find(CarInteraction.InfoIconName).gameObject;
            Assert.IsTrue(icon.activeSelf, "info icon should show while idle");
            _touch.SimulateTap(icon);
            Assert.AreEqual(CarState.Inspect, a.State);
            Assert.IsTrue(_panel.IsOpen);
            var texts = _panel.GetComponentsInChildren<TMPro.TMP_Text>(true);
            Assert.IsTrue(System.Array.Exists(texts, t => t.text == a.Settings.displayName), "panel should show the car's name");
            Assert.IsTrue(System.Array.Exists(texts, t => t.text.Contains(a.Settings.role)), "panel should show the car's role");
            yield return new WaitForSeconds(3f);
            Assert.GreaterOrEqual(Vector3.Distance(a.transform.position, cam.position), _config.carMinCameraDistance - 0.05f);
            yield return Shot("m3-02-inspect-panel");
            _panel.Close();
            Assert.AreEqual(CarState.Returning, a.State);
            yield return WaitUntil(() => a.State == CarState.Idle, 10f, "Car A never returned after inspect");
            Object.Destroy(watcher.gameObject);
            TestContext.WriteLine($"Smallest gap between the cars: {minGap:F2} m ({minGapWhen})");
            Assert.Greater(minGap, 0f, $"the cars touched ({minGapWhen})");
        }

        [DefaultExecutionOrder(10000)]
        sealed class Watcher : MonoBehaviour
        {
            public Action Tick;
            void LateUpdate() => Tick?.Invoke();
        }

        static float Length(CarController car)
        {
            var renderers = car.GetComponentsInChildren<MeshRenderer>().Where(r => r.name != "Underglow" && !r.name.Contains("Glow") && !r.name.Contains("Beam") && r.GetComponentInParent<Billboard>() == null).ToArray();
            var localBounds = new Bounds();
            bool first = true;
            foreach (var r in renderers)
            {
                foreach (var corner in Corners(r.localBounds))
                {
                    var p = car.transform.InverseTransformPoint(r.transform.TransformPoint(corner));
                    if (first) { localBounds = new Bounds(p, Vector3.zero); first = false; }
                    else localBounds.Encapsulate(p);
                }
            }
            return localBounds.size.z * car.transform.lossyScale.z;
        }

        static Vector3[] Corners(Bounds b) => new[]
        {
            b.min, b.max, new Vector3(b.min.x, b.min.y, b.max.z), new Vector3(b.min.x, b.max.y, b.min.z),
            new Vector3(b.max.x, b.min.y, b.min.z), new Vector3(b.min.x, b.max.y, b.max.z),
            new Vector3(b.max.x, b.min.y, b.max.z), new Vector3(b.max.x, b.max.y, b.min.z),
        };

        static IEnumerator WaitUntil(Func<bool> condition, float timeout, string failure)
        {
            float end = Time.time + timeout;
            while (!condition())
            {
                if (Time.time > end) Assert.Fail(failure);
                yield return null;
            }
        }

        static IEnumerator Shot(string name)
        {
            string folder = Environment.GetEnvironmentVariable("INTARE_SHOTS");
            if (string.IsNullOrEmpty(folder)) yield break;
            yield return null;
            Directory.CreateDirectory(folder);
            var cam = Camera.main;
            var canvases = Object.FindObjectsByType<Canvas>();
            var modes = canvases.Select(c => c.renderMode).ToArray();
            foreach (var c in canvases)
            {
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = 1f - c.sortingOrder * 0.001f;
            }
            Canvas.ForceUpdateCanvases();
            var rt = RenderTexture.GetTemporary(540, 960, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;
            for (int i = 0; i < canvases.Length; i++) canvases[i].renderMode = modes[i];
            var active = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(rt);
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), tex.EncodeToPNG());
            Object.Destroy(tex);
        }
    }
}
