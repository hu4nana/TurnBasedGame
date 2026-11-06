using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Shared two-triangle ground markers; no colliders, lighting or shadow passes.</summary>
public static class CharacterMarkerTools
{
    private const string Folder = "Assets/Prototype/";

    public static GameObject CreateMarker(Transform parent, bool ring)
    {
        GameObject marker = new(ring ? "Tactical State Ring" : "Blob Shadow");
        marker.transform.SetParent(parent, false);
        marker.AddComponent<MeshFilter>();
        marker.AddComponent<MeshRenderer>();
        Configure(marker, ring);
        return marker;
    }

    private static void Configure(GameObject marker, bool ring)
    {
        MeshFilter filter = marker.GetComponent<MeshFilter>();
        MeshRenderer renderer = marker.GetComponent<MeshRenderer>();
        if (filter == null || renderer == null)
            throw new InvalidOperationException("Marker requires MeshFilter and MeshRenderer: " + marker.name);
        Undo.RecordObjects(new UnityEngine.Object[] { marker.transform, filter, renderer }, "Optimize character marker");
        filter.sharedMesh = GetQuad();
        renderer.sharedMaterial = GetMaterial(ring);
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        renderer.sortingOrder = short.MinValue + 1;
        marker.transform.localPosition = new Vector3(0f, ring ? 0.025f : 0.015f, 0f);
        marker.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        marker.transform.localScale = ring ? new Vector3(0.72f, 0.72f, 1f) : new Vector3(0.6f, 0.35f, 1f);
        foreach (Collider collider in marker.GetComponents<Collider>()) Undo.DestroyObjectImmediate(collider);
        EditorUtility.SetDirty(filter);
        EditorUtility.SetDirty(renderer);
    }

    private static Mesh GetQuad()
    {
        const string path = Folder + "CharacterMarkerQuad.asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh != null) return mesh;
        mesh = new Mesh
        {
            name = "Shared Character Marker Quad",
            vertices = new[] { new Vector3(-0.5f,-0.5f,0), new Vector3(-0.5f,0.5f,0),
                new Vector3(0.5f,0.5f,0), new Vector3(0.5f,-0.5f,0) },
            uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right },
            triangles = new[] { 0, 1, 2, 0, 2, 3 }
        };
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static Material GetMaterial(bool ring)
    {
        string path = Folder + (ring ? "TacticalRing.mat" : "BlobShadow.mat");
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(Folder + "CharacterMarker.shader");
        if (shader == null) throw new InvalidOperationException("CharacterMarker shader missing.");
        material = new Material(shader);
        material.SetFloat("_InnerRadius", ring ? 0.67f : 0f);
        material.SetFloat("_Softness", ring ? 0.07f : 0.3f);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    public static void ValidateActiveScene()
    {
        int count = 0;
        foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        foreach (var unit in root.GetComponentsInChildren<GridOccupant>(true))
        foreach (var renderer in unit.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (renderer.name != "Blob Shadow" && renderer.name != "Tactical State Ring") continue;
            Mesh mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            if (mesh != GetQuad() || mesh.vertexCount != 4 || mesh.triangles.Length != 6 ||
                renderer.shadowCastingMode != ShadowCastingMode.Off || renderer.receiveShadows ||
                renderer.sharedMaterial != GetMaterial(renderer.name == "Tactical State Ring") ||
                ShaderUtil.ShaderHasError(renderer.sharedMaterial.shader) || renderer.GetComponent<Collider>() != null)
                throw new InvalidOperationException("Marker validation failed: " + unit.name + "/" + renderer.name);
            count++;
        }
        if (count == 0) throw new InvalidOperationException("No character markers found.");
    }
}
