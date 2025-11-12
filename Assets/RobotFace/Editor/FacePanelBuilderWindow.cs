#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public class FacePanelBuilderWindow : EditorWindow
{
    [Header("Targets")]
    public SkinnedMeshRenderer meshBody;   // e.g., MeshBody from Armature.fbx
    public Transform headBone;             // Head bone
    public Animator rigAnimator;           // Animator on character (for bones map)

    [Header("Panel Shape")]
    public float width = 0.25f;
    public float height = 0.18f;
    [Tooltip("0 = flat, 0.05 light, 0.12 medium, 0.2 strong")]
    public float curvature = 0.12f;
    public int segmentsX = 32;
    public int segmentsY = 12;

    [Header("Fit to Surface")]
    [Tooltip("Projected from head forward onto baked MeshBody.")]
    public float projectDistance = 0.08f;
    [Tooltip("Lift above surface to avoid z-fighting.")]
    public float surfaceOffset = 0.003f;

    [Header("Placement")]
    public Vector3 localOffset = new Vector3(0, 0, 0.005f);

    [MenuItem("Tools/Robot Face/Face Panel Builder")]
    public static void ShowWindow()
    {
        var win = GetWindow<FacePanelBuilderWindow>("Face Panel Builder");
        win.minSize = new Vector2(350, 420);
        win.Show();
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Targets", EditorStyles.boldLabel);
        meshBody = (SkinnedMeshRenderer)EditorGUILayout.ObjectField("Mesh Body (Skinned)", meshBody, typeof(SkinnedMeshRenderer), true);
        headBone = (Transform)EditorGUILayout.ObjectField("Head Bone", headBone, typeof(Transform), true);
        rigAnimator = (Animator)EditorGUILayout.ObjectField("Animator (Rig)", rigAnimator, typeof(Animator), true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Panel Shape", EditorStyles.boldLabel);
        width = EditorGUILayout.Slider("Width (m)", width, 0.05f, 0.6f);
        height = EditorGUILayout.Slider("Height (m)", height, 0.05f, 0.6f);
        curvature = EditorGUILayout.Slider("Curvature", curvature, 0f, 0.3f);
        segmentsX = EditorGUILayout.IntSlider("Segments X", segmentsX, 6, 64);
        segmentsY = EditorGUILayout.IntSlider("Segments Y", segmentsY, 2, 32);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Fit to Surface", EditorStyles.boldLabel);
        projectDistance = EditorGUILayout.Slider("Project Distance", projectDistance, 0.02f, 0.3f);
        surfaceOffset = EditorGUILayout.Slider("Surface Offset", surfaceOffset, 0.0005f, 0.01f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Placement", EditorStyles.boldLabel);
        localOffset = EditorGUILayout.Vector3Field("Local Offset (+Z out)", localOffset);

        EditorGUILayout.Space();
        GUI.enabled = meshBody && headBone;
        if (GUILayout.Button("Generate Face Panel (Skinned to Head)"))
        {
            Generate();
        }
        GUI.enabled = true;
    }

    void Generate()
    {
        // Bake current pose of the body mesh for raycast fitting
        Mesh baked = new Mesh();
        meshBody.BakeMesh(baked, true);

        // Temp collider for raycasts
        GameObject temp = new GameObject("TEMP_BakedCollider");
        temp.hideFlags = HideFlags.HideAndDontSave;
        temp.transform.position = meshBody.transform.position;
        temp.transform.rotation = meshBody.transform.rotation;
        temp.transform.localScale = meshBody.transform.lossyScale;
        var mc = temp.AddComponent<MeshCollider>();
        mc.sharedMesh = baked;

        // Panel basis (at head, oriented by head)
        Vector3 cx = headBone.right;   // local +X
        Vector3 cy = headBone.up;      // local +Y
        Vector3 cz = headBone.forward; // local +Z (out of face)
        Vector3 origin = headBone.position + headBone.TransformVector(localOffset);

        int nx = Mathf.Max(2, segmentsX);
        int ny = Mathf.Max(2, segmentsY);
        int vxCount = (nx + 1) * (ny + 1);

        List<Vector3> verts = new List<Vector3>(vxCount);
        List<BoneWeight> weights = new List<BoneWeight>(vxCount);
        List<Vector2> uvs = new List<Vector2>(vxCount);
        List<int> tris = new List<int>(nx * ny * 6);

        // Curved grid in head local plane, then project toward surface
        for (int iy = 0; iy <= ny; iy++)
        {
            float ty = (float)iy / ny;       // 0..1
            float y = Mathf.Lerp(-height*0.5f, height*0.5f, ty);

            for (int ix = 0; ix <= nx; ix++)
            {
                float tx = (float)ix / nx;   // 0..1
                float x = Mathf.Lerp(-width*0.5f, width*0.5f, tx);

                // curvature along X: z_offset local
                float zOff = 0f;
                if (curvature > 0.0001f)
                {
                    float u = (tx * 2f - 1f);
                    zOff = curvature * (1f - (u*u)); // peak at center
                }

                Vector3 p = origin + cx * x + cy * y + cz * zOff;

                // Raycast from outside toward head to stick to surface
                Vector3 from = p + cz * projectDistance;
                Vector3 dir = -cz;
                RaycastHit hit;
                if (mc.Raycast(new Ray(from, dir), out hit, projectDistance * 2f))
                {
                    p = hit.point + cz * surfaceOffset;
                }

                verts.Add(p);
                uvs.Add(new Vector2(tx, ty));

                // bone weight 100% Head
                BoneWeight bw = new BoneWeight();
                bw.boneIndex0 = 0;
                bw.weight0 = 1f;
                weights.Add(bw);
            }
        }

        for (int iy = 0; iy < ny; iy++)
        {
            for (int ix = 0; ix < nx; ix++)
            {
                int i0 = iy * (nx + 1) + ix;
                int i1 = i0 + 1;
                int i2 = i0 + (nx + 1);
                int i3 = i2 + 1;
                tris.Add(i0); tris.Add(i2); tris.Add(i1);
                tris.Add(i1); tris.Add(i2); tris.Add(i3);
            }
        }

        // Build mesh
        Mesh panel = new Mesh();
        panel.name = "FacePanel_Generated";
        panel.SetVertices(verts);
        panel.SetUVs(0, uvs);
        panel.SetTriangles(tris, 0);
        panel.RecalculateNormals();
        panel.RecalculateBounds();
        panel.boneWeights = weights.ToArray();

        // Bindposes: single head bone
        Matrix4x4 bind = headBone.worldToLocalMatrix;
        panel.bindposes = new Matrix4x4[] { bind };

        // Create object under head
        GameObject go = new GameObject("FacePanel");
        Undo.RegisterCreatedObjectUndo(go, "Create FacePanel");
        go.transform.SetParent(headBone, worldPositionStays: true);
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        var smr = go.AddComponent<SkinnedMeshRenderer>();
        smr.sharedMesh = panel;
        smr.rootBone = headBone;
        smr.bones = new Transform[] { headBone };
        smr.updateWhenOffscreen = true;

        // Try assign a simple unlit material
        var mat = new Material(Shader.Find("Unlit/RobotFaceAtlas_Simple"));
        smr.sharedMaterial = mat;

        // Marker
        var marker = go.AddComponent<FacePanelMarker>();
        marker.width = width;
        marker.height = height;
        marker.curvature = curvature;
        marker.segmentsX = segmentsX;
        marker.segmentsY = segmentsY;
        marker.surfaceOffset = surfaceOffset;

        // Cleanup temp
        Object.DestroyImmediate(temp);

        Selection.activeGameObject = go;
        EditorGUIUtility.PingObject(go);
        Debug.Log("FacePanel generated under Head. Assign your atlas material and you're good.");
    }
}
#endif
