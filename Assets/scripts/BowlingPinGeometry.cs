using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Geometría de un pin de bolos reglamentario (USBC), construida como sólido de revolución.
/// Todas las medidas de entrada están en pulgadas; las mallas salen en metros.
///
/// Perfil (altura sobre la base -> diámetro):
///   base plana de 2.030 in, 2.250 in a 0.2 in, y los puntos reglamentarios
///   0.75 -> 2.828, 2.25 -> 3.906, 3.375 -> 4.510, 4.5 -> 4.766 (vientre máximo),
///   5.875 -> 4.563, 7.25 -> 3.703, 8.625 -> 2.472, 9.375 -> 1.965, 10 -> 1.797 (cuello mínimo),
///   10.875 -> 1.870, 11.75 -> 2.094, 12.625 -> 2.406, 13.5 -> 2.547 (cabeza máxima).
///   Sobre 13.5 in la corona es un cuarto de elipse que cierra en 15 in (altura total).
/// Entre puntos la curva se interpola de forma suave y monótona (Hermite).
/// Masa: 1.54 a 1.64 kg. Centro de gravedad: 5.625 a 5.937 in sobre la base.
/// </summary>
public static class BowlingPinGeometry
{
    public const float Inch = 0.0254f;
    public const float HeightIn = 15f;
    public const float HeadMaxIn = 13.5f;
    public const float MassKg = 1.6f;
    public const float CenterOfGravityM = 5.78f * Inch;

    static readonly float[] ProfH = { 0f, 0.2f, 0.75f, 2.25f, 3.375f, 4.5f, 5.875f, 7.25f, 8.625f, 9.375f, 10f, 10.875f, 11.75f, 12.625f, 13.5f };
    static readonly float[] ProfD = { 2.030f, 2.250f, 2.828f, 3.906f, 4.510f, 4.766f, 4.563f, 3.703f, 2.472f, 1.965f, 1.797f, 1.870f, 2.094f, 2.406f, 2.547f };

    // Cortes (in) para dividir el pin en piezas convexas. Cada pieza es convexa o casi,
    // así el cuello y la cintura quedan huecos como en el pin real.
    static readonly float[] ColliderCuts = { 0f, 4.5f, 7.25f, 8.625f, 10f, 11.75f, 13.5f, 15f };

    // Franjas rojas del cuello (in sobre la base). Solo estético; edítalas si quieres moverlas.
    static readonly float[] StripeLow = { 9.25f, 10.35f };
    static readonly float[] StripeHigh = { 9.65f, 10.75f };

    public static int ColliderPieces { get { return ColliderCuts.Length - 1; } }

    // ------------------------------------------------------------
    // Perfil
    // ------------------------------------------------------------
    static float R(int i) { return ProfD[i] * 0.5f; }

    static float Secant(int i, int j) { return (R(j) - R(i)) / (ProfH[j] - ProfH[i]); }

    static float Tangent(int k)
    {
        int n = ProfH.Length;
        if (k == 0) return Secant(0, 1);
        if (k == n - 1) return 0f;
        float d0 = Secant(k - 1, k);
        float d1 = Secant(k, k + 1);
        if (d0 * d1 <= 0f) return 0f;
        return 2f * d0 * d1 / (d0 + d1);
    }

    /// <summary>Radio (in) del pin a una altura (in) sobre la base.</summary>
    public static float RadiusAt(float hIn)
    {
        if (hIn <= 0f) return R(0);
        if (hIn >= HeightIn) return 0f;

        if (hIn > HeadMaxIn)
        {
            float a = R(ProfH.Length - 1);
            float t = (hIn - HeadMaxIn) / (HeightIn - HeadMaxIn);
            return a * Mathf.Sqrt(Mathf.Max(0f, 1f - t * t));
        }

        int n = ProfH.Length;
        int k = 0;
        while (k < n - 2 && hIn > ProfH[k + 1]) k++;

        float h0 = ProfH[k];
        float h1 = ProfH[k + 1];
        float dh = h1 - h0;
        float u = (hIn - h0) / dh;
        float m0 = Tangent(k) * dh;
        float m1 = Tangent(k + 1) * dh;
        float u2 = u * u;
        float u3 = u2 * u;
        return (2f * u3 - 3f * u2 + 1f) * R(k)
             + (u3 - 2f * u2 + u) * m0
             + (-2f * u3 + 3f * u2) * R(k + 1)
             + (u3 - u2) * m1;
    }

    static bool InStripe(float hIn)
    {
        for (int b = 0; b < StripeLow.Length; b++)
            if (hIn > StripeLow[b] && hIn < StripeHigh[b]) return true;
        return false;
    }

    // ------------------------------------------------------------
    // Malla visual: submalla 0 = cuerpo, submalla 1 = franjas rojas
    // ------------------------------------------------------------
    public static Mesh BuildVisualMesh(int radial)
    {
        List<float> raw = new List<float>();
        for (float h = 0f; h < HeadMaxIn - 0.001f; h += 0.25f) raw.Add(h);
        raw.Add(HeadMaxIn);
        for (int b = 0; b < StripeLow.Length; b++)
        {
            raw.Add(StripeLow[b]);
            raw.Add(StripeHigh[b]);
        }
        raw.Sort();

        List<float> hs = new List<float>();
        for (int i = 0; i < raw.Count; i++)
            if (hs.Count == 0 || raw[i] - hs[hs.Count - 1] > 0.001f) hs.Add(raw[i]);

        int dome = 16;
        for (int s = 1; s <= dome; s++)
        {
            float ang = (float)s / dome * Mathf.PI * 0.5f;
            hs.Add(HeadMaxIn + (HeightIn - HeadMaxIn) * Mathf.Sin(ang));
        }
        hs[hs.Count - 1] = HeightIn;

        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> body = new List<int>();
        List<int> stripes = new List<int>();

        int stride = radial + 1;
        for (int j = 0; j < hs.Count; j++)
        {
            float h = hs[j];
            float r = RadiusAt(h);

            float e = 0.02f;
            float hp = Mathf.Min(h + e, HeightIn);
            float hm = Mathf.Max(h - e, 0f);
            float drdh = (RadiusAt(hp) - RadiusAt(hm)) / (hp - hm);
            Vector2 n2 = new Vector2(1f, -drdh).normalized;

            for (int i = 0; i <= radial; i++)
            {
                float th = 2f * Mathf.PI * i / radial;
                float c = Mathf.Cos(th);
                float s = Mathf.Sin(th);
                verts.Add(new Vector3(r * c, h, r * s) * Inch);
                norms.Add(new Vector3(n2.x * c, n2.y, n2.x * s).normalized);
                uvs.Add(new Vector2((float)i / radial, h / HeightIn));
            }
        }

        for (int j = 0; j < hs.Count - 1; j++)
        {
            List<int> target = InStripe(0.5f * (hs[j] + hs[j + 1])) ? stripes : body;
            for (int i = 0; i < radial; i++)
            {
                int a = j * stride + i;
                int b = a + 1;
                int c = a + stride;
                int d = c + 1;
                target.Add(a); target.Add(c); target.Add(d);
                target.Add(a); target.Add(d); target.Add(b);
            }
        }

        // tapa inferior plana
        int centerIdx = verts.Count;
        verts.Add(Vector3.zero);
        norms.Add(Vector3.down);
        uvs.Add(new Vector2(0.5f, 0f));
        int ringStart = verts.Count;
        float r0 = RadiusAt(0f);
        for (int i = 0; i <= radial; i++)
        {
            float th = 2f * Mathf.PI * i / radial;
            verts.Add(new Vector3(r0 * Mathf.Cos(th), 0f, r0 * Mathf.Sin(th)) * Inch);
            norms.Add(Vector3.down);
            uvs.Add(new Vector2((float)i / radial, 0f));
        }
        for (int i = 0; i < radial; i++)
        {
            body.Add(centerIdx);
            body.Add(ringStart + i);
            body.Add(ringStart + i + 1);
        }

        Mesh mesh = new Mesh();
        mesh.name = "Pin_Visual";
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.subMeshCount = 2;
        mesh.SetTriangles(body, 0);
        mesh.SetTriangles(stripes, 1);
        mesh.RecalculateBounds();
        return mesh;
    }

    // ------------------------------------------------------------
    // Malla de colisión: una pieza convexa del pin (0 .. ColliderPieces-1)
    // ------------------------------------------------------------
    public static Mesh BuildColliderMesh(int piece)
    {
        float h0 = ColliderCuts[piece];
        float h1 = ColliderCuts[piece + 1];
        bool isDome = h1 >= HeightIn - 0.001f;
        int steps = isDome ? 6 : 4;
        int radial = 20;

        List<float> hs = new List<float>();
        for (int s = 0; s <= steps; s++)
        {
            float t = (float)s / steps;
            float h = isDome ? h0 + (h1 - h0) * Mathf.Sin(t * Mathf.PI * 0.5f) : Mathf.Lerp(h0, h1, t);
            hs.Add(h);
        }
        hs[hs.Count - 1] = h1;

        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();
        int stride = radial + 1;

        for (int j = 0; j < hs.Count; j++)
        {
            float r = RadiusAt(hs[j]);
            for (int i = 0; i <= radial; i++)
            {
                float th = 2f * Mathf.PI * i / radial;
                verts.Add(new Vector3(r * Mathf.Cos(th), hs[j], r * Mathf.Sin(th)) * Inch);
            }
        }
        for (int j = 0; j < hs.Count - 1; j++)
        {
            for (int i = 0; i < radial; i++)
            {
                int a = j * stride + i;
                int b = a + 1;
                int c = a + stride;
                int d = c + 1;
                tris.Add(a); tris.Add(c); tris.Add(d);
                tris.Add(a); tris.Add(d); tris.Add(b);
            }
        }

        Mesh mesh = new Mesh();
        mesh.name = "Pin_Col_" + piece;
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
