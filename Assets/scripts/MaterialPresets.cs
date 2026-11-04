using UnityEngine;

/// <summary>
/// Cambia materiales en tiempo de ejecución: B = bola, N = pines, L = pista.
/// Reemplaza solo el primer material de cada renderer (el pin conserva sus franjas rojas).
/// Puedes arrastrar tus propios materiales a las listas desde el Inspector.
/// </summary>
public class MaterialPresets : MonoBehaviour
{
    public Renderer[] ballRenderers;
    public Material[] ballMaterials;

    public Renderer[] pinRenderers;
    public Material[] pinMaterials;

    public Renderer[] laneRenderers;
    public Material[] laneMaterials;

    int ballIndex;
    int pinIndex;
    int laneIndex;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.B)) Cycle(ballRenderers, ballMaterials, ref ballIndex);
        if (Input.GetKeyDown(KeyCode.N)) Cycle(pinRenderers, pinMaterials, ref pinIndex);
        if (Input.GetKeyDown(KeyCode.L)) Cycle(laneRenderers, laneMaterials, ref laneIndex);
    }

    static void Cycle(Renderer[] targets, Material[] mats, ref int index)
    {
        if (mats == null || mats.Length == 0 || targets == null) return;
        index = (index + 1) % mats.Length;
        if (mats[index] == null) return;

        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] == null) continue;
            Material[] current = targets[i].sharedMaterials;
            if (current.Length == 0) continue;
            current[0] = mats[index];
            targets[i].sharedMaterials = current;
        }
    }
}
