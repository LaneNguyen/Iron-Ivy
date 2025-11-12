using UnityEngine;
using System.Linq;

[RequireComponent(typeof(SkinnedMeshRenderer))]
public class FacePanelFixer : MonoBehaviour
{
    public Transform headBone;       // drag Head in scene
    public Transform armatureRoot;   // drag Hips/Root (optional)

    void Awake()
    {
        var smr = GetComponent<SkinnedMeshRenderer>();
        if (!smr || headBone == null) return;

        // 1) convert verts to head-local
        var mesh = Instantiate(smr.sharedMesh);
        var verts = mesh.vertices; // these are currently in world (prob)
        for (int i = 0; i < verts.Length; i++)
        {
            // treat stored verts as world points and move into head local
            verts[i] = headBone.worldToLocalMatrix.MultiplyPoint3x4(transform.TransformPoint(verts[i]));
        }
        mesh.vertices = verts;
        mesh.bindposes = new Matrix4x4[] { headBone.worldToLocalMatrix };
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        smr.sharedMesh = mesh;

        // 2) proper bones & root
        smr.bones = new Transform[] { headBone };
        smr.rootBone = armatureRoot ? armatureRoot : headBone;
        smr.updateWhenOffscreen = true;
        smr.localBounds = new Bounds(Vector3.zero, new Vector3(0.3f, 0.3f, 0.1f));

        // 3) parent under head with clean local TRS
        transform.SetParent(headBone, worldPositionStays: false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
    }
}
