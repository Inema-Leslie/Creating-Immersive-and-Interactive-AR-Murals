using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MuralAR.Tests
{
    public sealed class M5M6WomanSavannaTests
    {
        const float StoryTimeScale = 6f;

        MuralTracker _tracker;
        ExperienceController _experience;
        MuralSimulator _simulator;
        WomanController _woman;
        SavannaController _savanna;
        CarsDirector _cars;
        StoryPlayer _story;
        NightFall _night;
        CinematicCamera _cinematic;
        TouchInput _touch;
        MuralConfig _config;

        float _minClearance = float.PositiveInfinity;
        string _minClearanceWhen = "";
        bool _watching;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            yield return Sandbox.Load();
            _tracker = Object.FindAnyObjectByType<MuralTracker>();
            _experience = Object.FindAnyObjectByType<ExperienceController>();
            _simulator = Object.FindAnyObjectByType<MuralSimulator>();
            _woman = Object.FindAnyObjectByType<WomanController>();
            _savanna = Object.FindAnyObjectByType<SavannaController>();
            _cars = Object.FindAnyObjectByType<CarsDirector>();
            _story = Object.FindAnyObjectByType<StoryPlayer>();
            _night = Object.FindAnyObjectByType<NightFall>();
            _cinematic = Object.FindAnyObjectByType<CinematicCamera>();
            _touch = Object.FindAnyObjectByType<TouchInput>();
            _config = _tracker.Config;
            yield return WaitUntil(() => _tracker.IsFound && _woman.PaintedTapTarget != null, 5f, "mural was never found");
        }

        [UnityTearDown]
        public IEnumerator Restore()
        {
            Time.timeScale = 1f;
            _watching = false;
            yield return null;
        }

        [UnityTest]
        public IEnumerator TapWoman_SheEmerges_CarsFollow_StoryNightAndCinematic()
        {
            yield return WaitUntil(() => _cars.Cars.Count == 2 && _cars.Cars[0].State == CarState.Idle && _cars.Cars[1].State == CarState.Idle,
                _config.carsWakeAfterSeconds + _config.carB.wakeDelaySeconds + 12f, "cars never settled");
            _watching = true;
            var watcher = new GameObject("Clearance watcher").AddComponent<Watcher>();
            watcher.Tick = WatchClearance;
            yield return Shot("m5-01-before-tap");

            var target = _woman.PaintedTapTarget;
            var arCamera = Camera.main;
            Physics.SyncTransforms();
            Assert.IsTrue(Physics.Raycast(new Ray(arCamera.transform.position, (target.transform.position - arCamera.transform.position).normalized), out var hit, 100f, 1 << target.layer));
            Assert.AreSame(target, hit.collider.gameObject, "the tap ray hit something else");

            _simulator.Host.SimulateTap(target);
            Assert.AreEqual(ExperienceState.WomanEmerging, _experience.State);
            yield return new WaitForSeconds(_config.portalLeadSeconds * 0.8f);
            Assert.IsNotNull(GameObject.Find("Portal"), "the portal should open on the wall");
            yield return Shot("m5-01b-portal-cars-circling");

            yield return new WaitForSeconds(_config.emergenceSeconds * _config.emergenceDissolveShare * 0.5f);
            yield return Shot("m5-02-dissolving");
            yield return new WaitForSeconds(_config.emergenceSeconds * 0.45f);
            yield return Shot("m5-03-walking-out");

            float sheLanded = -1f, carADived = -1f, carAOut = -1f, carBDived = -1f;
            var a = _cars.Cars[0];
            var b = _cars.Cars[1];
            while (_experience.State != ExperienceState.Savanna)
            {
                if (sheLanded < 0f && _woman.HasEmerged) sheLanded = Time.time;
                if (carADived < 0f && !a.IsSolid) carADived = Time.time;
                if (carADived > 0f && carAOut < 0f && a.IsSolid)
                {
                    carAOut = Time.time;
                    yield return Shot("m5-04-car-a-out-of-portal");
                }
                if (carBDived < 0f && !b.IsSolid) carBDived = Time.time;
                if (Time.time - sheLanded > 40f && sheLanded > 0f) Assert.Fail("the savanna never started after she landed");
                yield return null;
            }
            Assert.Greater(sheLanded, 0f, "she never finished emerging");
            Assert.Greater(carADived, sheLanded, "Car A must go through the portal after her");
            Assert.Greater(carBDived, carAOut, "Car B must go through after Car A has come out");
            Assert.Less(Vector3.Dot(_woman.Woman.forward, _tracker.Anchor.forward), 0.3f, "she should face away from the wall");
            float height = Height(_woman.Woman.gameObject);
            Assert.AreEqual(_config.womanHeightMeters, height, 0.15f, "she should be life size");

            yield return null;
            Assert.IsTrue(_savanna.IsActive);
            Assert.IsTrue(_story.IsPlaying, "she should start speaking as the savanna appears");
            Assert.IsTrue(_cinematic.IsFilming, "the cinematic camera should film her story");
            Assert.AreNotSame(arCamera, Camera.main, "the story camera should be the one rendering");
            yield return new WaitForSeconds(_config.savannaFlashSeconds + 1f);
            yield return Shot("m6-01-savanna-opening");

            var terrain = Object.FindAnyObjectByType<Terrain>();
            var centre = terrain.transform.position + terrain.terrainData.size * 0.5f;
            var landing = _savanna.Root.position;
            Assert.Less(Vector2.Distance(new Vector2(centre.x, centre.z), new Vector2(landing.x, landing.z)), 0.5f, "the terrain should be centred where she landed");

            Time.timeScale = StoryTimeScale;
            int lastShot = _cinematic.ShotIndex;
            float furthestWalk = 0f;
            while (_experience.State != ExperienceState.Complete)
            {
                furthestWalk = Mathf.Max(furthestWalk, Vector3.Distance(_woman.Woman.position, landing));
                if (_cinematic.ShotIndex != lastShot)
                {
                    lastShot = _cinematic.ShotIndex;
                    Time.timeScale = 1f;
                    yield return new WaitForSeconds(1.6f);
                    yield return Shot($"m7-{lastShot:00}-{_config.shots[lastShot].name.Replace(' ', '-').Replace(",", "")}");
                    Time.timeScale = StoryTimeScale;
                }
                if (_story.Elapsed > _config.nightFallsAtSeconds + _config.nightTransitionSeconds + 1f && _story.Elapsed < _config.nightFallsAtSeconds + _config.nightTransitionSeconds + 3f)
                {
                    Assert.Greater(_night.Amount, 0.99f, "night should have fallen by the end of the first minute");
                    Assert.Greater(CountFireflies(), 0, "fireflies should be out");
                }
                if (_story.Elapsed > _story.Duration + 20f) Assert.Fail("the story never completed");
                yield return null;
            }
            Assert.Greater(furthestWalk, 1f, "she should walk around while she speaks");

            Time.timeScale = 1f;
            yield return WaitUntil(() => !_cinematic.IsFilming, _config.cinematicReturnSeconds + 2f, "the cinematic camera never handed back");
            Assert.AreSame(arCamera, Camera.main);

            _watching = false;
            Object.Destroy(watcher.gameObject);
            TestContext.WriteLine($"Smallest gap between the cars: {_minClearance:F2} m ({_minClearanceWhen})");
            Assert.Greater(_minClearance, 0f, $"the cars touched ({_minClearanceWhen})");

            var clearFlags = CameraClearFlags.SolidColor;
            yield return WaitUntil(() => !_savanna.IsActive, _config.cinematicReturnSeconds + _config.savannaFlashSeconds + 2f, "the savanna never ended after her story");
            yield return new WaitForSeconds(_config.savannaFlashSeconds);
            Assert.AreEqual(clearFlags, arCamera.clearFlags, "the AR camera should be restored");
            Assert.IsFalse(_savanna.Root.gameObject.activeInHierarchy);
            Assert.IsNotNull(_woman.Woman, "she stays with the viewer after the story");
            yield return WaitUntil(() => Vector3.Distance(_woman.Woman.position, landing) < 0.05f, 10f, "she should walk back to where she landed");
            yield return new WaitForSeconds(1.5f);
            yield return Shot("m8-01-back-in-the-courtyard");

            var herPosition = _woman.Woman.position;
            _simulator.ForceLost = true;
            yield return new WaitForSeconds(_config.lossPauseSeconds + 0.5f);
            Assert.Less(Vector3.Distance(herPosition, _woman.Woman.position), 0.001f);
        }

        int CountFireflies()
        {
            int count = 0;
            foreach (var ps in _savanna.Root.Find("Fireflies").GetComponentsInChildren<ParticleSystem>()) count += ps.particleCount;
            return count;
        }

        void WatchClearance()
        {
            if (!_watching || _cars.Clearance >= _minClearance) return;
            _minClearance = _cars.Clearance;
            var a = _cars.Cars[0];
            var b = _cars.Cars[1];
            _minClearanceWhen = $"{_experience.State}, A {a.State}/{a.Maneuver} at {a.transform.localPosition:F2} fwd {a.transform.localRotation * Vector3.forward:F2} path {a.IsOnPath}, B {b.State}/{b.Maneuver} at {b.transform.localPosition:F2} fwd {b.transform.localRotation * Vector3.forward:F2} path {b.IsOnPath}, t={Time.time:F1}";
        }

        [DefaultExecutionOrder(10000)]
        sealed class Watcher : MonoBehaviour
        {
            public Action Tick;
            void LateUpdate() => Tick?.Invoke();
        }

        static float Height(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b.size.y;
        }

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
            var modes = new RenderMode[canvases.Length];
            for (int i = 0; i < canvases.Length; i++)
            {
                modes[i] = canvases[i].renderMode;
                canvases[i].renderMode = RenderMode.ScreenSpaceCamera;
                canvases[i].worldCamera = cam;
                canvases[i].planeDistance = 1f - canvases[i].sortingOrder * 0.001f;
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
