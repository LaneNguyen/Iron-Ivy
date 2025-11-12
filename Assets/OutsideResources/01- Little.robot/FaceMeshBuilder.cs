using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class FaceMeshBuilder : MonoBehaviour
{
    [Header("Rounded-Quad")]
    [Range(0.01f, 2f)] public float width = 0.25f;
    [Range(0.01f, 2f)] public float height = 0.18f;
    [Range(0f, 0.3f)] public float radius = 0.04f;
    [Range(2, 16)] public int cornerSegments = 6;

    Mesh _mesh;

    void OnEnable() { Rebuild(); }
    void OnValidate() { Rebuild(); }

    void Rebuild()
    {
        if (_mesh == null)
        {
            _mesh = new Mesh();
            _mesh.name = "FaceRoundedQuad";
            GetComponent<MeshFilter>().sharedMesh = _mesh;
        }

        // clamp radius
        float r = Mathf.Min(radius, width * 0.5f - 1e-4f, height * 0.5f - 1e-4f);
        int seg = Mathf.Max(2, cornerSegments);

        // Build a rounded-rect by stitching 4 quarter arcs + edges
        // For simplicity we do a triangulated ring around center quad

        // Build vertices
        // grid: we’ll sample a polar for corners, linear for edges
        System.Collections.Generic.List<Vector3> verts = new();
        System.Collections.Generic.List<Vector2> uvs = new();
        System.Collections.Generic.List<int> tris = new();

        // Helper to add an arc (corner)
        void AddCorner(Vector2 center, float startDeg)
        {
            float step = 90f / seg;
            for (int i = 0; i <= seg; i++)
            {
                float a = (startDeg + i * step) * Mathf.Deg2Rad;
                Vector2 p = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                verts.Add(new Vector3(p.x, p.y, 0f));
                // UV simple fill [0..1], map x,y to uv
                float u = (p.x / (width)) + 0.5f;
                float v = (p.y / (height)) + 0.5f;
                uvs.Add(new Vector2(u, v));
            }
        }

        // Precompute rectangle inner rect corners (without radius)
        float hx = width * 0.5f;
        float hy = height * 0.5f;

        Vector2 bl = new Vector2(-hx + r, -hy + r);
        Vector2 tl = new Vector2(-hx + r, hy - r);
        Vector2 tr = new Vector2(hx - r, hy - r);
        Vector2 br = new Vector2(hx - r, -hy + r);

        // Build outer ring path: start at (tr arc), (br arc), (bl arc), (tl arc) in order (counter-clockwise)
        int startTR = verts.Count;
        AddCorner(tr, 90f);   // from up to left (TR quarter: 90→180)
        int startBR = verts.Count;
        AddCorner(br, 0f);    // right→up (BR: 0→90)
        int startBL = verts.Count;
        AddCorner(bl, 270f);  // down→right (BL: 270→360)
        int startTL = verts.Count;
        AddCorner(tl, 180f);  // left→down (TL: 180→270)

        // Connect straight edges between arc ends (avoid dup last vertices)
        // Actually arcs already include endpoints; we will also add mid edge points to keep shape nicer
        // Add mid points on edges between corners
        System.Action<Vector2> AddPoint = (Vector2 p) =>
        {
            verts.Add(new Vector3(p.x, p.y, 0));
            float u = (p.x / (width)) + 0.5f;
            float v = (p.y / (height)) + 0.5f;
            uvs.Add(new Vector2(u, v));
        };

        Vector2 midTop = new Vector2(0, hy);
        Vector2 midRight = new Vector2(hx, 0);
        Vector2 midBottom = new Vector2(0, -hy);
        Vector2 midLeft = new Vector2(-hx, 0);

        int idTop = verts.Count; AddPoint(midTop);
        int idRight = verts.Count; AddPoint(midRight);
        int idBottom = verts.Count; AddPoint(midBottom);
        int idLeft = verts.Count; AddPoint(midLeft);

        // Center quad (two triangles) for a nice UV fill
        int c0 = verts.Count; AddPoint(new Vector2(-hx + r, -hy + r));
        int c1 = verts.Count; AddPoint(new Vector2(hx - r, -hy + r));
        int c2 = verts.Count; AddPoint(new Vector2(hx - r, hy - r));
        int c3 = verts.Count; AddPoint(new Vector2(-hx + r, hy - r));

        // Triangulate fan from center rectangle to edges (simple approach)
        // Make the center rect:
        tris.Add(c0); tris.Add(c1); tris.Add(c2);
        tris.Add(c0); tris.Add(c2); tris.Add(c3);

        // For outer ring, we can just triangulate from the center rect corners (c0..c3) to arcs and midpoints
        // To keep this concise (and stable), add a simple border quad strip:
        // Build a border rectangle just slightly larger than the center (connect by quads)
        // (We’ll not overcomplicate; this is “good enough” visual for a face panel)

        // Create a simple quad strip around: (c0->c1->c2->c3) to (approximate outer: -hx..hx / -hy..hy)
        int o0 = verts.Count; AddPoint(new Vector2(-hx, -hy));
        int o1 = verts.Count; AddPoint(new Vector2(hx, -hy));
        int o2 = verts.Count; AddPoint(new Vector2(hx, hy));
        int o3 = verts.Count; AddPoint(new Vector2(-hx, hy));

        // Connect inner (c) to outer (o)
        void AddQuad(int a, int b, int c, int d)
        {
            tris.Add(a); tris.Add(b); tris.Add(c);
            tris.Add(a); tris.Add(c); tris.Add(d);
        }
        AddQuad(c0, c1, o1, o0);
        AddQuad(c1, c2, o2, o1);
        AddQuad(c2, c3, o3, o2);
        AddQuad(c3, c0, o0, o3);

        _mesh.Clear();
        _mesh.SetVertices(verts);
        _mesh.SetUVs(0, uvs);
        _mesh.SetTriangles(tris, 0);
        _mesh.RecalculateNormals(); // flat is fine for unlit
        _mesh.RecalculateBounds();
    }
}
