using UnityEngine;
using UnityEngine.InputSystem;

// Converts screen pointer to world XY on the same Z plane as the given point (fixes 2D aim offset).
public static class AimPlaneUtil
{
    public static Vector3 ScreenToWorldOnPlane(Camera cam, Vector3 planePoint)
    {
        if (cam == null)
            return planePoint;

        Vector2 screen = Mouse.current != null
            ? Mouse.current.position.ReadValue()
            : (Vector2)Input.mousePosition;

        Vector3 mp = screen;
        mp.z = Mathf.Abs(cam.transform.position.z - planePoint.z);
        return cam.ScreenToWorldPoint(mp);
    }
}
