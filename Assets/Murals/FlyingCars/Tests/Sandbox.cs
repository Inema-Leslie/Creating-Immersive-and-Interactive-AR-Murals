using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MuralAR.Tests
{
    static class Sandbox
    {
        const string ScenePath = "Assets/Murals/FlyingCars/Sandbox_FlyingCars.unity";

        public static MuralSimulator Simulator { get; private set; }

        public static IEnumerator Load()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync("Sandbox_FlyingCars", LoadSceneMode.Single);
#endif
            Simulator = null;
            float end = Time.time + 5f;
            while (Simulator == null || Simulator.Mural == null)
            {
                if (Time.time > end) Assert.Fail("the simulator never spawned the mural");
                Simulator = Object.FindAnyObjectByType<MuralSimulator>();
                yield return null;
            }
        }
    }
}
