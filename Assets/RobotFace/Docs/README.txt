Face Panel Builder (Skinned, fitted to MeshBody)

This Editor tool builds a curved face mesh that conforms to your MeshBody (from Armature.fbx)
and skins it 100% to the Head bone.

Use:
1) Import Armature.fbx into your project/scene.
2) Import the 'Assets/RobotFace' folder into your Unity project.
3) Open menu: Tools > Robot Face > Face Panel Builder.
4) Set:
   - Mesh Body (SkinnedMeshRenderer) = your MeshBody
   - Head Bone (Transform) = the head bone under your rig
5) Adjust width/height/curvature and click "Generate Face Panel".
6) A SkinnedMeshRenderer 'FacePanel' will appear under the Head; assign your material (atlas).

Tip: If forward of the head faces inward, rotate head or adjust Local Offset (+Z should point outward).
