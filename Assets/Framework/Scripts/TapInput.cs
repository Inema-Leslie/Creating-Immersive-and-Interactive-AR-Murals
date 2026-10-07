using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Sends screen taps to MuralTappable objects. Works with touch on the phone and the mouse in the editor.
// Place one in the scene, for example on the XR Origin. Taps on UI buttons are ignored.
public class TapInput : MonoBehaviour
{
    [Tooltip("The AR camera. Uses Camera.main when empty.")]
    public Camera arCamera;

    [Tooltip("How far the tap ray reaches, in meters.")]
    public float maxDistance = 20f;

    private readonly List<RaycastResult> uiResults = new List<RaycastResult>();

    private void Update()
    {
        Pointer pointer = Pointer.current;
        if (pointer == null || !pointer.press.wasPressedThisFrame)
        {
            return;
        }

        Vector2 screenPosition = pointer.position.ReadValue();

        if (IsOverUI(screenPosition))
        {
            return;
        }

        Camera cam = arCamera != null ? arCamera : Camera.main;
        if (cam == null)
        {
            return;
        }

        Ray ray = cam.ScreenPointToRay(screenPosition);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, maxDistance))
        {
            MuralTappable tappable = hit.collider.GetComponentInParent<MuralTappable>();
            if (tappable != null)
            {
                tappable.Tap();
            }
        }
    }

    private bool IsOverUI(Vector2 screenPosition)
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        PointerEventData eventData = new PointerEventData(EventSystem.current);
        eventData.position = screenPosition;
        uiResults.Clear();
        EventSystem.current.RaycastAll(eventData, uiResults);
        return uiResults.Count > 0;
    }
}
