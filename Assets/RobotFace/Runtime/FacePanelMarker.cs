using UnityEngine;

/// <summary>
/// Marker for the generated FacePanel object. Holds parameters for future rebuilds if needed.
/// </summary>
public class FacePanelMarker : MonoBehaviour
{
    [Header("Generated Panel Params")]
    public float width = 0.25f;
    public float height = 0.18f;
    public float curvature = 0.10f; // 0 = flat
    public int segmentsX = 24;
    public int segmentsY = 12;
    public float surfaceOffset = 0.003f; // lift above surface
}
