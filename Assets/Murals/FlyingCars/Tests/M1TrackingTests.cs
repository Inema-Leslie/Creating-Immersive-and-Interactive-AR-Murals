using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MuralAR.Tests
{
    public sealed class M1TrackingTests
    {
        MuralTracker _tracker;
        ExperienceController _experience;
        MuralSimulator _simulator;
        MuralConfig _config;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            yield return Sandbox.Load();
            _simulator = Sandbox.Simulator;
            _tracker = Object.FindAnyObjectByType<MuralTracker>();
            _experience = Object.FindAnyObjectByType<ExperienceController>();
            _config = _tracker.Config;
            yield return WaitUntil(() => _tracker.IsFound, 5f, "mural was never found");
        }

        [UnityTest]
        public IEnumerator MuralFound_AnchorFrameMatchesTheWall()
        {
            yield return null;
            Assert.AreEqual(ExperienceState.Awakening, _experience.State, "finding the mural starts the awakening");

            var anchor = _tracker.Anchor;
            Assert.Less(Vector3.Distance(anchor.position, _simulator.ImagePose.position), 0.001f, "anchor should sit at the image centre");
            Assert.Greater(Vector3.Dot(anchor.forward, Vector3.forward), 0.999f, "+Z should point into the wall");
            Assert.Greater(Vector3.Dot(anchor.up, Vector3.up), 0.999f, "+Y should point up the mural");
            Assert.Greater(Vector3.Dot(anchor.right, Vector3.right), 0.999f, "+X should point right along the mural");

            Assert.AreEqual(_simulator.MuralSize.x, _tracker.MuralSizeMeters.x, 0.01f);
            Assert.AreEqual(_simulator.MuralSize.y, _tracker.MuralSizeMeters.y, 0.05f);
        }

        [UnityTest]
        public IEnumerator TrackingLoss_GoesThroughHoldHintPause_ThenRelocalizes()
        {
            int lost = 0, relocalized = 0;
            _tracker.OnMuralLost += () => lost++;
            _tracker.OnMuralRelocalized += () => relocalized++;

            _simulator.ForceLost = true;
            yield return new WaitForSeconds(0.8f);
            Assert.AreEqual(1, lost);
            Assert.AreEqual(TrackingLoss.Holding, _tracker.Loss);

            yield return new WaitForSeconds(_config.lossHoldSeconds);
            Assert.AreEqual(TrackingLoss.Hinting, _tracker.Loss);

            yield return new WaitForSeconds(_config.lossPauseSeconds - _config.lossHoldSeconds);
            Assert.AreEqual(TrackingLoss.Paused, _tracker.Loss);

            _simulator.ForceLost = false;
            yield return null;
            yield return null;
            Assert.AreEqual(1, relocalized);
            Assert.AreEqual(TrackingLoss.None, _tracker.Loss);
            Assert.IsTrue(_tracker.IsTracking);
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
    }
}
