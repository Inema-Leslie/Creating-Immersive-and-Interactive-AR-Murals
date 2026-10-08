using UnityEngine;
using UnityEngine.InputSystem;

public class MuralDebugTester : MonoBehaviour
{
    [SerializeField] MuralExperience experience;

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null || experience == null) return;

        if (keyboard.fKey.wasPressedThisFrame) experience.OnTrackingFound();
        if (keyboard.lKey.wasPressedThisFrame) experience.OnTrackingLost();
    }
}