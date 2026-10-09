using System.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace MuralAR
{
    public sealed class ARBootstrap : MonoBehaviour
    {
        [SerializeField] MuralSimulator simulator;
        [SerializeField, Tooltip("Use the simulator when pressing Play in the editor.")]
        bool simulateInEditor = true;
        [SerializeField, Tooltip("AR session, camera and tracking components: off in the scene, switched on for real AR.")]
        Behaviour[] realAR = System.Array.Empty<Behaviour>();

        public bool IsReady { get; private set; }
        public bool IsSimulated { get; private set; }

        IEnumerator Start()
        {
            bool simulate = Application.isEditor && simulateInEditor;
            if (!simulate)
            {
                yield return ARSession.CheckAvailability();
                if (ARSession.state == ARSessionState.NeedsInstall) yield return ARSession.Install();
                simulate = ARSession.state is ARSessionState.Unsupported or ARSessionState.NeedsInstall;
            }

            if (simulate) simulator.Activate();
            else
            {
                simulator.enabled = false;
                foreach (var b in realAR)
                    if (b != null) b.enabled = true;
            }

            IsSimulated = simulate;
            IsReady = true;
        }
    }
}
