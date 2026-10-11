using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MuralAR.Tests
{
    public sealed class M8InterfaceTests
    {
        MuralTracker _tracker;
        ExperienceController _experience;
        MuralSimulator _simulator;
        FlyingCarsUI _ui;
        CarsDirector _cars;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            yield return Sandbox.Load();
            _simulator = Sandbox.Simulator;
            _tracker = Object.FindAnyObjectByType<MuralTracker>();
            _experience = Object.FindAnyObjectByType<ExperienceController>();
            _ui = Object.FindAnyObjectByType<FlyingCarsUI>();
            _cars = Object.FindAnyObjectByType<CarsDirector>();
            yield return WaitUntil(() => _tracker.IsFound, 5f, "mural was never found");
            yield return null;
        }

        [UnityTest]
        public IEnumerator ThemedBar_Gallery_CallButton_AndNoLeftoverText()
        {
            Assert.IsTrue(_ui.BarVisible, "the FlyingCars buttons should show while the mural is found");
            Assert.IsTrue(_ui.CallButton.gameObject.activeSelf, "How to call AMANIRENAS should show before she emerges");
            yield return Shot("m9-01-bar");

            _ui.MuralsButton.onClick.Invoke();
            Assert.IsTrue(_ui.GalleryOpen, "Murals should open the gallery");
            Assert.GreaterOrEqual(_ui.CardButtons.Count, 1, "the gallery should list the murals");
            yield return Shot("m9-02-gallery");

            _ui.CardButtons[0].onClick.Invoke();
            yield return null;
            var detail = _ui.GetComponentsInChildren<TMPro.TMP_Text>(true).First(t => t.name == "Text" && t.transform.parent.name == "Content");
            StringAssert.Contains("The story", detail.text, "the card should tell the mural's short story");
            StringAssert.Contains("Amanirenas", detail.text, "the story should be about Amanirenas");
            StringAssert.Contains("About", detail.text, "the card should show the mural's about text");
            StringAssert.Contains("Learning Commons", detail.text, "the card should show where the mural is");
            yield return Shot("m9-03-card");

            Button(_ui, "BackButton").onClick.Invoke();
            Button(_ui, "CloseButton").onClick.Invoke();
            Assert.IsFalse(_ui.GalleryOpen, "Close should shut the gallery");

            _simulator.ForceLost = true;
            yield return new WaitForSeconds(0.8f);
            Assert.IsFalse(_ui.BarVisible, "the FlyingCars buttons should hide when the mural is lost");
            var panel = Object.FindObjectsByType<MonoBehaviour>().First(b => b.GetType().Name == "MuralInfoPanel");
            panel.GetType().GetMethod("ShowInfo").Invoke(panel, new object[] { "Another mural", "Text another mural left behind." });
            var info = panel.GetComponentsInChildren<Transform>(true).First(t => t.name == "Info").gameObject;
            Assert.IsTrue(info.activeSelf);
            _simulator.ForceLost = false;
            yield return null;
            yield return null;
            Assert.IsFalse(info.activeSelf, "finding Flying Cars should clear text another mural left on screen");
            Assert.IsTrue(_ui.BarVisible, "the FlyingCars buttons should come back with the mural");

            yield return WaitUntil(() => _cars.Cars.Count == 2, 5f, "cars never spawned");
            var before = _experience.State;
            _ui.CallButton.onClick.Invoke();
            Assert.IsTrue(_ui.HowToCallOpen, "the button should explain how to call her");
            Assert.AreEqual(before, _experience.State, "the button should only explain, not call her");
            yield return Shot("m9-04-how-to-call");
            Button(_ui, "GotItButton").onClick.Invoke();
            Assert.IsFalse(_ui.HowToCallOpen, "Got it should close the explanation");

            Object.FindAnyObjectByType<WomanController>().Emerge();
            yield return null;
            Assert.IsFalse(_ui.CallButton.gameObject.activeSelf, "the button should hide once she is coming");
        }

        static Button Button(Component root, string name) =>
            root.GetComponentsInChildren<Button>(true).First(b => b.name == name && b.gameObject.activeInHierarchy);

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
            var canvases = Object.FindObjectsByType<Canvas>().Where(c => c.isRootCanvas).ToArray();
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
