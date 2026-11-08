using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// 거대 괴물이 무너뜨릴 주요 건물(Request1009Plan.md §66 GD2)을 Blender로 조각내기 위해, 게임 속 모양 그대로를 OBJ로 내보낸다.
// - 건물 뿌리 기준(로컬 좌표)으로 모든 MeshRenderer를 합친다. 재질마다 usemtl로 나누고, MTL에 색·텍스처(절대 경로)를 적는다.
// - 문짝(InteractableDoor 아래)은 따로 "Door_n" 오브젝트로 적어 Blender가 통째로 떨어지는 조각으로 쓴다.
// - 남겨 둘 것(재료 받침·빛·수리 자리 등, GiantFractureKeep)과 충돌체 전용·조명은 내보내지 않는다.
// Unity(왼손, Y 위) → OBJ(오른손): x를 뒤집고 삼각형 순서를 바꾼다. Blender OBJ 가져오기 기본(-Z 앞, Y 위)이 Z 위로 바꾼다.
public static class FractureSourceExporter
{
    public static string Export(Transform root, string objPath, List<string> skipped = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(objPath));
        string mtlPath = Path.ChangeExtension(objPath, ".mtl");
        var obj = new StringBuilder();
        var mtl = new StringBuilder();
        var writtenMats = new HashSet<string>();
        obj.AppendLine("mtllib " + Path.GetFileName(mtlPath));
        // v / vt / vn 번호는 따로 센다(UV나 법선이 없는 메시가 끼면 뒤 메시의 번호가 밀린다)
        int vBase = 1, tBase = 1, nBase = 1, doors = 0, meshes = 0, verts = 0;
        Matrix4x4 toRoot = root.worldToLocalMatrix;

        foreach (MeshRenderer r in root.GetComponentsInChildren<MeshRenderer>(false))
        {
            if (!r.enabled) continue;
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            if (GiantFractureKeep.IsKept(r.transform, root))
            {
                skipped?.Add(r.name);
                continue;
            }
            Mesh mesh = mf.sharedMesh; // 에디터에서는 Read/Write가 꺼진 메시도 읽을 수 있다
            bool door = r.GetComponentInParent<InteractableDoor>() != null;
            obj.AppendLine("o " + (door ? "Door_" + (doors++) : Clean(r.name)));
            Matrix4x4 m = toRoot * r.localToWorldMatrix;
            Vector3[] vs = mesh.vertices;
            Vector3[] ns = mesh.normals;
            Vector2[] uv = mesh.uv;
            foreach (Vector3 v in vs)
            {
                Vector3 p = m.MultiplyPoint3x4(v);
                obj.Append("v ").Append(F(-p.x)).Append(' ').Append(F(p.y)).Append(' ').Append(F(p.z)).Append('\n');
            }
            bool hasUv = uv != null && uv.Length == vs.Length;
            bool hasN = ns != null && ns.Length == vs.Length;
            if (hasUv) foreach (Vector2 t in uv) obj.Append("vt ").Append(F(t.x)).Append(' ').Append(F(t.y)).Append('\n');
            if (hasN)
                foreach (Vector3 n in ns)
                {
                    Vector3 q = m.MultiplyVector(n).normalized;
                    obj.Append("vn ").Append(F(-q.x)).Append(' ').Append(F(q.y)).Append(' ').Append(F(q.z)).Append('\n');
                }
            Material[] mats = r.sharedMaterials;
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                Material mat = s < mats.Length ? mats[s] : null;
                string matName = mat != null ? Clean(mat.name) : "None";
                obj.AppendLine("usemtl " + matName);
                if (writtenMats.Add(matName)) WriteMaterial(mtl, matName, mat);
                int[] tris = mesh.GetTriangles(s);
                for (int i = 0; i < tris.Length; i += 3)
                {
                    obj.Append('f');
                    // 왼손 → 오른손: 순서를 바꾼다(0, 2, 1)
                    foreach (int k in new[] { tris[i], tris[i + 2], tris[i + 1] })
                    {
                        obj.Append(' ').Append(k + vBase);
                        if (hasUv || hasN) obj.Append('/').Append(hasUv ? (k + tBase).ToString() : "");
                        if (hasN) obj.Append('/').Append(k + nBase);
                    }
                    obj.Append('\n');
                }
            }
            vBase += vs.Length;
            if (hasUv) tBase += vs.Length;
            if (hasN) nBase += vs.Length;
            verts += vs.Length;
            meshes++;
        }
        File.WriteAllText(objPath, obj.ToString());
        File.WriteAllText(mtlPath, mtl.ToString());
        return $"{root.name}: meshes {meshes}, doors {doors}, verts {verts}, materials {writtenMats.Count}";
    }

    private static void WriteMaterial(StringBuilder mtl, string name, Material mat)
    {
        mtl.AppendLine("newmtl " + name);
        Color c = mat != null && mat.HasProperty("_Color") ? mat.color : Color.white;
        mtl.AppendLine($"Kd {F(c.r)} {F(c.g)} {F(c.b)}");
        if (mat == null) return;
        string albedo = TexturePath(mat, "_MainTex");
        if (albedo != null) mtl.AppendLine("map_Kd " + albedo);
        string normal = TexturePath(mat, "_BumpMap");
        if (normal != null) mtl.AppendLine("map_Bump " + normal);
    }

    private static string TexturePath(Material mat, string prop)
    {
        if (!mat.HasProperty(prop)) return null;
        Texture t = mat.GetTexture(prop);
        if (t == null) return null;
        string p = AssetDatabase.GetAssetPath(t);
        return string.IsNullOrEmpty(p) ? null : Path.GetFullPath(p);
    }

    private static string Clean(string s) => s.Replace(' ', '_');
    private static string F(float f) => f.ToString("0.#####", CultureInfo.InvariantCulture);
}
