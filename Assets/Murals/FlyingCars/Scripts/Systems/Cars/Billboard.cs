using UnityEngine;

namespace MuralAR
{
    public sealed class Billboard : MonoBehaviour
    {
        [SerializeField] Camera viewCamera;
        [SerializeField, Tooltip("Size in metres regardless of the parent's scale.")] float worldSize = 0.35f;

        public void SetCamera(Camera cam) => viewCamera = cam;

        void LateUpdate()
        {
            if (viewCamera == null) viewCamera = Camera.main;
            if (viewCamera == null) return;
            transform.rotation = Quaternion.LookRotation(transform.position - viewCamera.transform.position, Vector3.up);
            var parentScale = transform.parent != null ? transform.parent.lossyScale.x : 1f;
            transform.localScale = Vector3.one * (worldSize / Mathf.Max(0.001f, parentScale));
        }
    }
}
