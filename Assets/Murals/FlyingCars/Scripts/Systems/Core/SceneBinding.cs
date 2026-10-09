using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace MuralAR
{
    public readonly struct SceneRefs
    {
        public readonly Camera Camera;
        public readonly Light Sun;
        public readonly ARCameraBackground CameraBackground;

        public SceneRefs(Camera camera, Light sun, ARCameraBackground cameraBackground)
        {
            Camera = camera;
            Sun = sun;
            CameraBackground = cameraBackground;
        }
    }

    public interface ISceneBound
    {
        void Bind(SceneRefs refs);
    }

    public interface IMuralHost
    {
        void SimulateFound();

        void SimulateLost();

        void SimulateTap(GameObject target);
    }
}
