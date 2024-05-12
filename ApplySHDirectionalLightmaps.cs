using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;

public class ApplySHDirectionalLightmaps : EditorWindow
{
    [MenuItem("Tools/Apply SH Directional Lightmaps")]
    static void ApplySpecialLightmapsToSelected()
    {
        GameObject[] selectedObjects = Selection.gameObjects;
        Dictionary<Material, List<GameObject>> materialObjectMap = new Dictionary<Material, List<GameObject>>(); // Which objects are using which material.

        // Add all objects to the materialObjectMap
        foreach (GameObject obj in selectedObjects)
        {
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer != null && renderer.sharedMaterial != null)
            {
                Material material = renderer.sharedMaterial;
                if (!materialObjectMap.ContainsKey(material))
                {
                    materialObjectMap.Add(material, new List<GameObject>());
                }
                materialObjectMap[material].Add(obj);
            }
        }

        // For each material entry from the selected Objects
        foreach (var entry in materialObjectMap)
        {
            Material material = entry.Key;
            List<GameObject> objectsUsingMaterial = entry.Value;

            if (!material.HasProperty("_L1x")) { continue; } // If the material doesn't support custom lighting it doesn't need to be processed.

            HashSet<int> uniqueLightmaps = new HashSet<int>(objectsUsingMaterial.Select(obj => obj.GetComponent<Renderer>().lightmapIndex));
            if (uniqueLightmaps.Count > 1)
            {
                string objectList = string.Join(", ", objectsUsingMaterial.Select(obj => obj.name).ToArray());
                bool splitMaterial = EditorUtility.DisplayDialog(
                    "Multiple Lightmaps Detected",
                    "Material '" + material.name + "' is used by multiple objects with different lightmaps:\n" + objectList + "\nDo you want to split this material into multiple materials?",
                    "Split Material",
                    "Cancel"
                );

                if (splitMaterial)
                {
                    // Split material into the same amount of uniqueLightmaps it was used.
                    foreach (int lightmapIndex in uniqueLightmaps)
                    {
                        List<GameObject> objectsUsingMateralOnSameLightmap = objectsUsingMaterial.Where(obj => obj.GetComponent<Renderer>().lightmapIndex == lightmapIndex).ToList();
                        Material lightmapSpecificMaterial = SplitMaterialForGameObject(objectsUsingMateralOnSameLightmap[0]);
                        objectsUsingMateralOnSameLightmap.RemoveAt(0);
                        ApplyMaterialToObjects(objectsUsingMateralOnSameLightmap, lightmapSpecificMaterial);
                    }
                }
            }
            else
            {
                ApplySpecialLightmapsToMaterial(objectsUsingMaterial[0]);
            }
        }
    }

    static void ApplyMaterialToObjects(List<GameObject> Objects, Material material)
    {
        List<Renderer> renderers = new List<Renderer>();
        foreach (GameObject obj in Objects)
        {
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderers.Add(renderer);
            }
        }

        foreach (Renderer renderer in renderers)
        {
            renderer.sharedMaterial = material;
        }
    }

    static Material SplitMaterialForGameObject(GameObject obj)
    {
        Renderer renderer = obj.GetComponent<Renderer>();
        Material originalMaterial = renderer.sharedMaterial;

        string oldMaterialPath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(originalMaterial));
        string newMaterialPath = AssetDatabase.GenerateUniqueAssetPath(oldMaterialPath + "/" + originalMaterial.name + "_" + GetLightmapName(renderer.lightmapIndex) + ".mat");
        Debug.Log(newMaterialPath);
        Material newMaterial = new Material(originalMaterial);
        AssetDatabase.CreateAsset(newMaterial, newMaterialPath);

        renderer.sharedMaterial = newMaterial;
        ApplySpecialLightmapsToMaterial(obj);
        return newMaterial;
    }

    static void ApplySpecialLightmapsToMaterial(GameObject obj)
    {
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer != null && renderer.sharedMaterial != null)
        {
            string lightmapName = GetLightmapName(renderer.lightmapIndex);
            Material material = renderer.sharedMaterial;

            string lightmapFolderPath = GetLightmapFolderPath(renderer.lightmapIndex);
            if (!string.IsNullOrEmpty(lightmapFolderPath))
            {
                string[] lightmapSuffixes = { "_L1x", "_L1y", "_L1z" };
                foreach (string suffix in lightmapSuffixes)
                {
                    Texture lightmap = FindTextureBySuffix(lightmapFolderPath, lightmapName + suffix);
                    if (lightmap != null)
                    {
                        material.SetTexture(suffix, lightmap);
                    }
                }
            }
            else
            {
                Debug.LogWarning("Lightmap folder path not found for object: " + obj.name);
            }
        }
    }


    static string GetLightmapFolderPath(int lightmapIndex)
    {
        LightmapData[] lightmaps = LightmapSettings.lightmaps;
        if (lightmapIndex >= 0 && lightmapIndex < lightmaps.Length)
        {
            string lightmapPath = AssetDatabase.GetAssetPath(lightmaps[lightmapIndex].lightmapColor);
            return Path.GetDirectoryName(lightmapPath);
        }
        return null;
    }

    static string GetLightmapName(int lightmapIndex)
    {
        LightmapData[] lightmaps = LightmapSettings.lightmaps;
        if (lightmapIndex >= 0 && lightmapIndex < lightmaps.Length)
        {
            string lightmapPath = AssetDatabase.GetAssetPath(lightmaps[lightmapIndex].lightmapColor);
            string lightmapName = Path.GetFileNameWithoutExtension(lightmapPath);
            return lightmapName.Replace("_L0", "");
        }
        return null;
    }

    static Texture FindTextureBySuffix(string folderPath, string suffix)
    {
        string[] texturePaths = Directory.GetFiles(folderPath, "*" + suffix + ".*", SearchOption.TopDirectoryOnly);
        foreach (string texturePath in texturePaths)
        {
            Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(texturePath);
            if (texture.name.EndsWith(suffix))
            {
                return texture;
            }
        }
        return null;
    }
}
