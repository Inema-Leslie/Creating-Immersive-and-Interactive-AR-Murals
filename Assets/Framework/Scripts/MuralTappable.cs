using UnityEngine;

// Marks an object inside a mural prefab as tappable. The object needs a Collider.
// When tapped, the mural script receives OnTapped(this) and can check id or index.
public class MuralTappable : MonoBehaviour
{
    [Tooltip("Optional name used by the mural script, for example Band3 or Pot1.")]
    public string id;

    [Tooltip("Optional number used by the mural script, for example the band or pot number.")]
    public int index;

    public void Tap()
    {
        MuralExperience owner = GetComponentInParent<MuralExperience>();
        if (owner != null && !owner.IsPaused)
        {
            owner.OnTapped(this);
        }
    }
}
