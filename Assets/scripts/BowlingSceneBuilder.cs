using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Menú Bolos > Crear escena de bolos. Arma toda la escena con primitivas de Unity
/// y pines con geometría reglamentaria (BowlingPinGeometry).
/// Colocar este archivo dentro de una carpeta llamada "Editor".
/// </summary>
public static class BowlingSceneBuilder
{
    const string MatDir = "Assets/Bowling/Materials";
    const string PhysDir = "Assets/Bowling/Physics";
    const string MeshDir = "Assets/Bowling/Meshes";
    const string SceneDir = "Assets/Scenes";

    const float BallRadius = 0.109f;
    const float HeadPinZ = 18.29f;
    const float PinSpacing = 0.3048f;   // 12 in entre centros
    const float RowSpacing = 0.2640f;   // 12 in * sen(60°)
    const float LaneEnd = 19.4f;
    const float PitEnd = 20.9f;

    [MenuItem("Bolos/Crear escena de bolos")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EnsureFolder("Assets", "Bowling");
        EnsureFolder("Assets/Bowling", "Materials");
        EnsureFolder("Assets/Bowling", "Physics");
        EnsureFolder("Assets/Bowling", "Meshes");
        EnsureFolder("Assets", "Scenes");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ---------- Materiales visuales (intercambiables) ----------
        Material[] laneMats =
        {
            MakeMat("Pista_MaderaClara", new Color(0.82f, 0.64f, 0.40f), 0f, 0.85f),
            MakeMat("Pista_MaderaOscura", new Color(0.35f, 0.20f, 0.10f), 0f, 0.80f),
            MakeMat("Pista_SinteticaAzul", new Color(0.15f, 0.30f, 0.60f), 0f, 0.70f)
        };
        Material[] ballMats =
        {
            MakeMat("Bola_AzulBrillante", new Color(0.10f, 0.20f, 0.85f), 0.3f, 0.95f),
            MakeMat("Bola_Roja", new Color(0.75f, 0.05f, 0.05f), 0.2f, 0.90f),
            MakeMat("Bola_Negra", new Color(0.03f, 0.03f, 0.03f), 0.4f, 0.95f),
            MakeMat("Bola_VerdeMetalica", new Color(0.05f, 0.55f, 0.20f), 0.8f, 0.85f)
        };
        Material[] pinMats =
        {
            MakeMat("Pin_Blanco", new Color(0.95f, 0.95f, 0.95f), 0f, 0.70f),
            MakeMat("Pin_Marfil", new Color(0.90f, 0.85f, 0.68f), 0f, 0.60f),
            MakeMat("Pin_MaderaNatural", new Color(0.70f, 0.50f, 0.30f), 0f, 0.50f)
        };

        // ---------- Materiales fijos ----------
        Material matGutter = MakeMat("Canal", new Color(0.10f, 0.10f, 0.12f), 0.2f, 0.4f);
        Material matWall = MakeMat("Pared", new Color(0.12f, 0.12f, 0.16f), 0f, 0.3f);
        Material matFloor = MakeMat("Suelo_Sala", new Color(0.07f, 0.07f, 0.09f), 0f, 0.2f);
        Material matApproach = MakeMat("Aproximacion", new Color(0.55f, 0.40f, 0.25f), 0f, 0.6f);
        Material matBackstop = MakeMat("Cortina_Fondo", new Color(0.18f, 0.05f, 0.08f), 0f, 0.1f);
        Material matMark = MakeMat("Marcas_Pista", new Color(0.10f, 0.06f, 0.04f), 0f, 0.3f);
        Material matFoul = MakeMat("Linea_Falta", new Color(0.70f, 0.08f, 0.08f), 0f, 0.3f);
        Material matStripe = MakeMat("Pin_Franja", new Color(0.85f, 0.05f, 0.05f), 0f, 0.6f);
        Material matHoles = MakeMat("Bola_Agujeros", new Color(0.02f, 0.02f, 0.02f), 0f, 0.1f);
        Material matRake = MakeMat("Rastrillo", new Color(0.55f, 0.56f, 0.60f), 0.8f, 0.6f);
        Material matMachine = MakeMat("Maquina_Pinos", new Color(0.10f, 0.10f, 0.12f), 0.5f, 0.5f);
        Material matTrail = MakeShaderMat("Estela", Shader.Find("Sprites/Default"), new Color(1f, 0.9f, 0.4f, 1f));

        // ---------- Materiales físicos ----------
        // La bola usa fricción "Minimum": en contacto con pines y pista manda su fricción baja,
        // así el giro de la bola no la hace trepar por los pines.
        PhysicMaterial pmLane = MakePhys("Pista", 0.15f, 0.20f, 0.0f, PhysicMaterialCombine.Average);
        PhysicMaterial pmBall = MakePhys("Bola", 0.12f, 0.15f, 0.1f, PhysicMaterialCombine.Minimum);
        PhysicMaterial pmPin = MakePhys("Pin", 0.30f, 0.35f, 0.30f, PhysicMaterialCombine.Average);
        PhysicMaterial pmWall = MakePhys("Pared", 0.50f, 0.50f, 0.2f, PhysicMaterialCombine.Average);

        // ---------- Mallas del pin reglamentario ----------
        Mesh pinVisual = SaveMesh(BowlingPinGeometry.BuildVisualMesh(48), "Pin_Visual");
        Mesh[] pinColliders = new Mesh[BowlingPinGeometry.ColliderPieces];
        for (int k = 0; k < pinColliders.Length; k++)
            pinColliders[k] = SaveMesh(BowlingPinGeometry.BuildColliderMesh(k), "Pin_Col_" + k);

        // ---------- Iluminación ambiente ----------
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.22f, 0.24f, 0.30f);
        RenderSettings.skybox = null;

        GameObject root = new GameObject("Bolera");

        // ---------- Geometría estática ----------
        var laneRenderers = new List<Renderer>();

        Block("Suelo_Sala", root.transform, new Vector3(0f, -0.65f, 9f), new Vector3(12f, 0.2f, 40f), matFloor, pmWall);
        var approach = Block("Aproximacion", root.transform, new Vector3(0f, -0.2f, -2f), new Vector3(2f, 0.4f, 4f), matApproach, pmLane);
        laneRenderers.Add(approach.GetComponent<Renderer>());
        var lane = Block("Pista", root.transform, new Vector3(0f, -0.2f, LaneEnd * 0.5f), new Vector3(1.05f, 0.4f, LaneEnd), laneMats[0], pmLane);
        laneRenderers.Add(lane.GetComponent<Renderer>());

        foreach (float side in new[] { -1f, 1f })
        {
            Block("Canal_" + (side < 0 ? "Izq" : "Der"), root.transform,
                new Vector3(side * 0.645f, -0.26f, LaneEnd * 0.5f), new Vector3(0.24f, 0.4f, LaneEnd), matGutter, pmWall);
            Block("Pared_" + (side < 0 ? "Izq" : "Der"), root.transform,
                new Vector3(side * 0.785f, 0.10f, PitEnd * 0.5f), new Vector3(0.04f, 0.4f, PitEnd), matWall, pmWall);
        }

        Block("Foso", root.transform, new Vector3(0f, -0.35f, (LaneEnd + PitEnd) * 0.5f), new Vector3(1.6f, 0.4f, PitEnd - LaneEnd), matWall, pmWall);
        Block("Cortina_Fondo", root.transform, new Vector3(0f, 0.45f, PitEnd + 0.05f), new Vector3(1.65f, 1.4f, 0.1f), matBackstop, pmWall);

        // Línea de falta y marcas de puntería (solo visuales)
        Prim(PrimitiveType.Cube, "Linea_Falta", root.transform, new Vector3(0f, 0.001f, 0f), new Vector3(1.05f, 0.002f, 0.05f), matFoul, false);
        for (int i = -3; i <= 3; i++)
        {
            Prim(PrimitiveType.Cylinder, "Marca_" + i, root.transform, new Vector3(i * 0.15f, 0.001f, 4.5f), new Vector3(0.03f, 0.0005f, 0.03f), matMark, false);
        }

        // ---------- Máquina de pines ----------
        // Carcasa visual sobre los pines (sin colisión ni sombra)
        GameObject hood = Prim(PrimitiveType.Cube, "Maquina_Pinos", root.transform, new Vector3(0f, 2.1f, 18.9f), new Vector3(1.7f, 0.4f, 1.7f), matMachine, false);
        hood.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

        // Rastrillo: barra cinemática que barre los pines caídos hacia el foso
        GameObject rakeGO = Prim(PrimitiveType.Cube, "Rastrillo", root.transform, new Vector3(0f, 1.6f, 17.3f), new Vector3(1.52f, 0.5f, 0.08f), matRake, true);
        rakeGO.GetComponent<Collider>().sharedMaterial = pmWall;
        Rigidbody rakeRb = rakeGO.AddComponent<Rigidbody>();
        rakeRb.isKinematic = true;
        rakeRb.interpolation = RigidbodyInterpolation.Interpolate;
        rakeRb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        // ---------- Pines ----------
        GameObject pinsRoot = new GameObject("Pines");
        pinsRoot.transform.SetParent(root.transform, false);
        var pinRenderers = new List<Renderer>();
        int id = 1;
        for (int r = 0; r < 4; r++)
        {
            for (int c = 0; c <= r; c++)
            {
                float x = (c - r * 0.5f) * PinSpacing;
                float z = HeadPinZ + r * RowSpacing;
                CreatePin("Pin_" + id, pinsRoot.transform, new Vector3(x, 0f, z), pinVisual, pinColliders,
                    pinMats[0], matStripe, pmPin, pinRenderers);
                id++;
            }
        }

        // ---------- Bola ----------
        GameObject ballGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ballGO.name = "Bola";
        ballGO.transform.SetParent(root.transform, false);
        ballGO.transform.position = new Vector3(-0.15f, BallRadius + 0.002f, -0.5f);
        ballGO.transform.localScale = Vector3.one * (BallRadius * 2f);
        ballGO.GetComponent<Renderer>().sharedMaterial = ballMats[0];
        ballGO.GetComponent<SphereCollider>().sharedMaterial = pmBall;

        Rigidbody ballRb = ballGO.AddComponent<Rigidbody>();
        ballRb.mass = 6.8f;
        ballRb.interpolation = RigidbodyInterpolation.Interpolate;
        ballRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        Vector3[] holePos =
        {
            new Vector3(-0.12f, 0.46f, 0.12f),
            new Vector3(0.12f, 0.46f, 0.12f),
            new Vector3(0f, 0.47f, -0.12f)
        };
        for (int i = 0; i < holePos.Length; i++)
        {
            Prim(PrimitiveType.Sphere, "Agujero_" + (i + 1), ballGO.transform, holePos[i], Vector3.one * 0.1f, matHoles, false);
        }

        TrailRenderer trail = ballGO.AddComponent<TrailRenderer>();
        trail.time = 3f;
        trail.widthMultiplier = 0.03f;
        trail.sharedMaterial = matTrail;
        trail.startColor = new Color(1f, 0.9f, 0.4f, 0.9f);
        trail.endColor = new Color(1f, 0.9f, 0.4f, 0f);
        trail.shadowCastingMode = ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.emitting = false;

        // ---------- Cámara única + dos poses ----------
        GameObject camsRoot = new GameObject("Camaras");
        Transform poseThrow = MakePose("Pose_Lanzamiento", camsRoot.transform, new Vector3(0.7f, 0.8f, -2.2f), new Vector3(0f, 0.1f, 2.0f));
        Transform posePins = MakePose("Pose_Pines", camsRoot.transform, new Vector3(-1.3f, 0.7f, 16.7f), new Vector3(0f, 0.15f, 18.5f));
        Camera cam = MakeCamera("Camara_Principal", poseThrow.position, poseThrow.rotation, 55f);

        // ---------- Luces ----------
        MakeLight("Luz_Ambiental", LightType.Directional, new Vector3(0f, 6f, 8f), new Vector3(60f, -20f, 0f),
            new Color(0.8f, 0.85f, 1f), 0.35f, 0f, 0f);
        MakeLight("Foco_Pines", LightType.Spot, new Vector3(0f, 3f, 17.8f), new Vector3(90f, 0f, 0f),
            new Color(1f, 0.95f, 0.85f), 3.5f, 7f, 80f);
        MakeLight("Foco_Pista", LightType.Spot, new Vector3(0f, 3f, 9f), new Vector3(90f, 0f, 0f),
            new Color(1f, 0.92f, 0.8f), 2f, 8f, 90f);
        MakeLight("Foco_Aproximacion", LightType.Spot, new Vector3(0f, 3f, -2f), new Vector3(90f, 0f, 0f),
            new Color(1f, 0.95f, 0.9f), 1.5f, 8f, 100f);
        MakeLight("Brillo_Foso", LightType.Point, new Vector3(0f, 1f, 20.2f), Vector3.zero,
            new Color(0.4f, 0.5f, 1f), 1f, 3f, 0f);

        // ---------- Sistema ----------
        GameObject sys = new GameObject("Sistema_Bolos");

        CameraRig rig = sys.AddComponent<CameraRig>();
        rig.cam = cam;
        rig.poseThrow = poseThrow;
        rig.posePins = posePins;
        rig.ball = ballGO.transform;

        BowlingController ctrl = sys.AddComponent<BowlingController>();
        ctrl.ball = ballRb;
        ctrl.pinsRoot = pinsRoot.transform;
        ctrl.rake = rakeRb;
        ctrl.cameraRig = rig;

        MaterialPresets presets = sys.AddComponent<MaterialPresets>();
        presets.ballRenderers = new[] { ballGO.GetComponent<Renderer>() };
        presets.ballMaterials = ballMats;
        presets.pinRenderers = pinRenderers.ToArray();
        presets.pinMaterials = pinMats;
        presets.laneRenderers = laneRenderers.ToArray();
        presets.laneMaterials = laneMats;

        BowlingHUD hud = sys.AddComponent<BowlingHUD>();
        hud.controller = ctrl;

        EditorSceneManager.SaveScene(scene, SceneDir + "/Bowling.unity");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeGameObject = sys;

        EditorUtility.DisplayDialog("Bolos", "Escena creada en Assets/Scenes/Bowling.unity. Presiona Play.", "OK");
    }

    // ------------------------------------------------------------
    // Pin reglamentario:
    //  - Visual: una malla de revolución con el perfil USBC (cuerpo + franjas rojas).
    //  - Colisión: 7 piezas convexas (base, vientre, cintura, cuello, cabeza...) para que el
    //    cuello quede hueco como en el pin real.
    //  - Masa 1.6 kg y centro de gravedad a ~5.8 in de la base.
    // ------------------------------------------------------------
    static void CreatePin(string pinName, Transform parent, Vector3 pos, Mesh visual, Mesh[] colliders,
        Material body, Material stripe, PhysicMaterial pm, List<Renderer> renderers)
    {
        GameObject pin = new GameObject(pinName);
        pin.transform.SetParent(parent, false);
        pin.transform.position = pos;

        Rigidbody rb = pin.AddComponent<Rigidbody>();
        rb.mass = BowlingPinGeometry.MassKg;
        rb.centerOfMass = new Vector3(0f, BowlingPinGeometry.CenterOfGravityM, 0f);
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        GameObject vis = new GameObject("Visual");
        vis.transform.SetParent(pin.transform, false);
        vis.AddComponent<MeshFilter>().sharedMesh = visual;
        MeshRenderer mr = vis.AddComponent<MeshRenderer>();
        mr.sharedMaterials = new[] { body, stripe };
        renderers.Add(mr);

        for (int k = 0; k < colliders.Length; k++)
        {
            GameObject c = new GameObject("Col_" + k);
            c.transform.SetParent(pin.transform, false);
            MeshCollider mc = c.AddComponent<MeshCollider>();
            mc.sharedMesh = colliders[k];
            mc.convex = true;
            mc.sharedMaterial = pm;
        }
    }

    // ------------------------------------------------------------
    // Utilidades
    // ------------------------------------------------------------
    static GameObject Prim(PrimitiveType type, string objName, Transform parent, Vector3 localPos, Vector3 scale,
        Material mat, bool keepCollider)
    {
        GameObject g = GameObject.CreatePrimitive(type);
        g.name = objName;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos;
        g.transform.localScale = scale;
        g.GetComponent<Renderer>().sharedMaterial = mat;
        if (!keepCollider)
        {
            Collider c = g.GetComponent<Collider>();
            if (c != null) Object.DestroyImmediate(c);
        }
        return g;
    }

    static GameObject Block(string objName, Transform parent, Vector3 pos, Vector3 scale, Material mat, PhysicMaterial pm)
    {
        GameObject g = Prim(PrimitiveType.Cube, objName, parent, pos, scale, mat, true);
        g.GetComponent<Collider>().sharedMaterial = pm;
        g.isStatic = true;
        return g;
    }

    static Transform MakePose(string poseName, Transform parent, Vector3 pos, Vector3 target)
    {
        GameObject go = new GameObject(poseName);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.LookAt(target);
        return go.transform;
    }

    static Camera MakeCamera(string camName, Vector3 pos, Quaternion rot, float fov)
    {
        GameObject go = new GameObject(camName);
        go.transform.SetPositionAndRotation(pos, rot);
        Camera cam = go.AddComponent<Camera>();
        cam.fieldOfView = fov;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 60f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.03f, 0.03f, 0.05f);
        go.AddComponent<AudioListener>();
        go.tag = "MainCamera";
        return cam;
    }

    static void MakeLight(string lightName, LightType type, Vector3 pos, Vector3 euler, Color color,
        float intensity, float range, float spotAngle)
    {
        GameObject go = new GameObject(lightName);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(euler);
        Light l = go.AddComponent<Light>();
        l.type = type;
        l.color = color;
        l.intensity = intensity;
        if (type != LightType.Directional) l.range = range;
        if (type == LightType.Spot) l.spotAngle = spotAngle;
        l.shadows = type == LightType.Point ? LightShadows.None : LightShadows.Soft;
    }

    static Shader LitShader()
    {
        if (GraphicsSettings.currentRenderPipeline != null)
        {
            Shader urp = Shader.Find("Universal Render Pipeline/Lit");
            if (urp != null) return urp;
        }
        return Shader.Find("Standard");
    }

    static Material MakeMat(string matName, Color color, float metallic, float smoothness)
    {
        Material m = MakeShaderMat(matName, LitShader(), color);
        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Glossiness", smoothness);
        m.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material MakeShaderMat(string matName, Shader shader, Color color)
    {
        string path = MatDir + "/" + matName + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(shader);
            AssetDatabase.CreateAsset(m, path);
        }
        m.shader = shader;
        m.color = color;
        EditorUtility.SetDirty(m);
        return m;
    }

    static PhysicMaterial MakePhys(string pmName, float dynamicFriction, float staticFriction, float bounciness,
        PhysicMaterialCombine frictionCombine)
    {
        string path = PhysDir + "/" + pmName + ".physicMaterial";
        PhysicMaterial pm = AssetDatabase.LoadAssetAtPath<PhysicMaterial>(path);
        if (pm == null)
        {
            pm = new PhysicMaterial(pmName);
            AssetDatabase.CreateAsset(pm, path);
        }
        pm.dynamicFriction = dynamicFriction;
        pm.staticFriction = staticFriction;
        pm.bounciness = bounciness;
        pm.frictionCombine = frictionCombine;
        pm.bounceCombine = PhysicMaterialCombine.Average;
        EditorUtility.SetDirty(pm);
        return pm;
    }

    static Mesh SaveMesh(Mesh mesh, string meshName)
    {
        string path = MeshDir + "/" + meshName + ".asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
        EditorUtility.CopySerialized(mesh, existing);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    static void EnsureFolder(string parent, string folder)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + folder))
            AssetDatabase.CreateFolder(parent, folder);
    }
}
