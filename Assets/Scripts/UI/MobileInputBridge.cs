using UnityEngine;
using UnityEngine.EventSystems;

// Attach ke setiap on-screen button.
// Implement IPointerDownHandler dan IPointerUpHandler
// agar input hold (tahan gas/rem) tetap terdeteksi.
public class MobileInputBridge : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler
{
    public enum InputAction { MoveLeft, MoveRight, Accelerate, Slow }

    [SerializeField] private InputAction action;

    // PlayerController membaca flag ini setiap frame
    public static bool IsLeftHeld;
    public static bool IsRightHeld;
    public static bool IsAccelHeld;
    public static bool IsSlowHeld;

    public void OnPointerDown(PointerEventData e)
    {
        switch (action)
        {
            case InputAction.MoveLeft:   IsLeftHeld  = true; break;
            case InputAction.MoveRight:  IsRightHeld = true; break;
            case InputAction.Accelerate: IsAccelHeld = true; break;
            case InputAction.Slow:       IsSlowHeld  = true; break;
        }
    }

    public void OnPointerUp(PointerEventData e)
    {
        switch (action)
        {
            case InputAction.MoveLeft:   IsLeftHeld  = false; break;
            case InputAction.MoveRight:  IsRightHeld = false; break;
            case InputAction.Accelerate: IsAccelHeld = false; break;
            case InputAction.Slow:       IsSlowHeld  = false; break;
        }
    }
}