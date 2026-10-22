#if UNITY_EDITOR
// Bar Layout Tools - helper menu used to inspect and arrange the bar scene.
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class BarLayoutTools
{
    static string OutDir => System.IO.Path.Combine(Application.dataPath, ".barlayout");

    static bool TryGetBounds(GameObject g, out Bounds b)
    {
        b = default;
        var rs = g.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return false;
        b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return true;
    }

    static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;
    static string V(Vector3 v) => $"({v.x:F2},{v.y:F2},{v.z:F2})";

    static void Dump(StringBuilder sb, Transform t, int depth, int maxDepth)
    {
        string src = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject);
        string b = TryGetBounds(t.gameObject, out var bb) ? $" bounds c{V(bb.center)} s{V(bb.size)}" : "";
        sb.AppendLine($"{new string(' ', depth * 2)}{t.name} | pos{V(t.position)} rot{V(t.eulerAngles)} scl{V(t.lossyScale)}{b}" + (string.IsNullOrEmpty(src) ? "" : $" | src {src}") + (t.gameObject.activeInHierarchy ? "" : " [inactive]"));
        if (depth < maxDepth)
            foreach (Transform c in t) Dump(sb, c, depth + 1, maxDepth);
    }

    [MenuItem("Tools/Bar Layout/Dump Scene Info")]
    public static void DumpScene()
    {
        var sb = new StringBuilder();
        var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (var r in roots) Dump(sb, r.transform, 0, 4);
        System.IO.Directory.CreateDirectory(OutDir);
        System.IO.File.WriteAllText(System.IO.Path.Combine(OutDir, "scene.txt"), sb.ToString());
        Debug.Log("[Bar Layout] Scene info written.");
    }
}
#endif
