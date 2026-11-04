using UnityEngine;

/// <summary>
/// Una sola cámara que se mueve entre dos poses con una transición suave:
/// Pose_Lanzamiento (vista grande de la bola y el ángulo) y Pose_Pines (closeup del impacto).
/// El controlador llama a ShowPins() al lanzar y a ShowThrow() cuando termina de acomodar los pines.
/// </summary>
public class CameraRig : MonoBehaviour
{
    public Camera cam;
    public Transform poseThrow;
    public Transform posePins;
    public Transform ball;

    public Vector3 pinsFocus = new Vector3(0f, 0.15f, 18.55f);
    public float fovThrow = 55f;
    public float fovPines = 40f;
    public float focusSmooth = 6f;

    float transitionTime = 2f;
    float blend;      // 0 = pose de lanzamiento, 1 = pose de pines
    bool toPins;
    Vector3 focusSmoothed;

    public bool AtThrow
    {
        get { return !toPins && blend <= 0.001f; }
    }

    public void ShowPins(float duration)
    {
        transitionTime = Mathf.Max(0.1f, duration);
        toPins = true;
    }

    public void ShowThrow(float duration)
    {
        transitionTime = Mathf.Max(0.1f, duration);
        toPins = false;
    }

    void Start()
    {
        focusSmoothed = pinsFocus;
        toPins = false;
        blend = 0f;
        Apply();
    }

    void LateUpdate()
    {
        float target = toPins ? 1f : 0f;
        blend = Mathf.MoveTowards(blend, target, Time.unscaledDeltaTime / transitionTime);
        Apply();
    }

    void Apply()
    {
        if (cam == null || poseThrow == null || posePins == null) return;

        // smootherstep: arranque y llegada muy suaves
        float s = blend * blend * blend * (blend * (blend * 6f - 15f) + 10f);

        Vector3 pos = Vector3.Lerp(poseThrow.position, posePins.position, s);

        Vector3 focus = pinsFocus;
        if (ball != null)
        {
            float z = ball.position.z;
            float w = Mathf.InverseLerp(14.5f, 18f, z) * 0.6f;
            if (z > 19.3f) w = 0f;
            focus = Vector3.Lerp(pinsFocus, ball.position, w);
        }
        focusSmoothed = Vector3.Lerp(focusSmoothed, focus, 1f - Mathf.Exp(-focusSmooth * Time.unscaledDeltaTime));

        Quaternion rotPins = Quaternion.LookRotation(focusSmoothed - pos);
        Quaternion rot = Quaternion.Slerp(poseThrow.rotation, rotPins, s);

        cam.transform.SetPositionAndRotation(pos, rot);
        cam.fieldOfView = Mathf.Lerp(fovThrow, fovPines, s);
    }
}
