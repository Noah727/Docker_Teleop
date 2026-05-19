#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ApplyRoomDecorPrefabs
{
    private const string RoomPrefabPath =
        "Packages/com.meta.xr.sdk.interaction/Runtime/Sample/Objects/Environment/Room/RoomEnvironment.prefab";
    private const string PlantPotPath =
        "Packages/com.meta.xr.sdk.interaction/Runtime/Sample/Objects/Props/Plants/PlantPot.fbx";
    private const string PlantUmbrellaPath =
        "Packages/com.meta.xr.sdk.interaction/Runtime/Sample/Objects/Props/Plants/PlantUmbrella.fbx";
    private const string WoodWallpaperTexturePath = "Assets/Textures/WoodWallPanels.png";
    private const string WoodWallpaperMaterialPath = "Assets/Materials/WoodWallPanels.mat";
    private static readonly Color WarmCeilingColor = new Color(0.97f, 0.92f, 0.86f, 1f);
    private static readonly Color PlantLeafColor = new Color(0.22f, 0.50f, 0.20f, 1f);
    private static readonly Color PlantPotColor = new Color(0.27f, 0.20f, 0.16f, 1f);
    private static readonly Color DarkWoodColor = new Color(0.18f, 0.11f, 0.07f, 1f);
    private static readonly Color BookAccentColor = new Color(0.70f, 0.48f, 0.30f, 1f);

    [MenuItem("Tools/Room Decor/Apply Sample Prefabs")]
    public static void Apply()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError("[ApplyRoomDecorPrefabs] No active loaded scene.");
            return;
        }

        var roomDecor = FindOrCreateRoot("RoomDecor", null);
        var plantsRoot = FindOrCreateRoot("Plants", roomDecor.transform);
        var shelvesRoot = FindOrCreateRoot("Shelves", roomDecor.transform);

        ToneDownCeilingLight();
        ApplyWallTreatment();

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RoomPrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[ApplyRoomDecorPrefabs] Could not load prefab at {RoomPrefabPath}");
            return;
        }

        var plantPotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlantPotPath);
        var plantUmbrellaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlantUmbrellaPath);

        ClearChildren(plantsRoot.transform);
        ClearChildren(shelvesRoot.transform);

        var tempRoot = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        if (tempRoot == null)
        {
            Debug.LogError("[ApplyRoomDecorPrefabs] Failed to instantiate room sample prefab.");
            return;
        }

        try
        {
            var shelfSource = FindFirstByName(tempRoot.transform, "ShelfSculpture")
                ?? FindFirstByName(tempRoot.transform, "Bookcase");
            var booksSource = FindFirstByName(tempRoot.transform, "Books");

            if (plantPotPrefab == null || plantUmbrellaPrefab == null)
            {
                Debug.LogWarning("[ApplyRoomDecorPrefabs] Plant FBX assets were not found in the package.");
            }
            else
            {
                BuildPlantAssembly(
                    plantsRoot.transform,
                    "ImportedPlant_Left",
                    plantPotPrefab,
                    plantUmbrellaPrefab,
                    new Vector3(-4.55f, 0f, -4.15f),
                    new Vector3(0f, 35f, 0f),
                    Vector3.one * 1.00f);

                BuildPlantAssembly(
                    plantsRoot.transform,
                    "ImportedPlant_Right",
                    plantPotPrefab,
                    plantUmbrellaPrefab,
                    new Vector3(4.55f, 0f, -4.15f),
                    new Vector3(0f, -35f, 0f),
                    Vector3.one * 1.00f);
            }

            if (shelfSource == null)
            {
                Debug.LogWarning("[ApplyRoomDecorPrefabs] No shelf object found in room sample prefab.");
            }
            else
            {
                BuildShelfSet(
                    shelvesRoot.transform,
                    "DarkShelf_Left",
                    shelfSource,
                    booksSource,
                    new Vector3(-4.35f, 0f, 2.8f),
                    new Vector3(0f, 90f, 0f),
                    Vector3.one * 1.05f);

                BuildShelfSet(
                    shelvesRoot.transform,
                    "DarkShelf_Right",
                    shelfSource,
                    booksSource,
                    new Vector3(4.35f, 0f, 2.8f),
                    new Vector3(0f, -90f, 0f),
                    Vector3.one * 1.05f);
            }
        }
        finally
        {
            Object.DestroyImmediate(tempRoot);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[ApplyRoomDecorPrefabs] Replaced room plants and shelves with imported package decor.");
    }

    private static void ToneDownCeilingLight()
    {
        var bulb = GameObject.Find("Light_Bulb");
        if (bulb == null || !bulb.TryGetComponent<Light>(out var light))
        {
            Debug.LogWarning("[ApplyRoomDecorPrefabs] Light_Bulb was not found. Skipped lighting adjustment.");
            return;
        }

        light.intensity = 2.15f;
        light.range = 8.0f;
    }

    private static void ApplyWallTreatment()
    {
        var wallMaterial = CreateOrUpdateWoodWallpaperMaterial();
        ApplyMaterialToNamedObject("Room_Wall_Back", wallMaterial);
        ApplyMaterialToNamedObject("Room_Wall_Front", wallMaterial);
        ApplyMaterialToNamedObject("Room_Wall_Left", wallMaterial);
        ApplyMaterialToNamedObject("Room_Wall_Right", wallMaterial);
        TintNamedObject("Room_Ceiling", WarmCeilingColor);
    }

    private static Material CreateOrUpdateWoodWallpaperMaterial()
    {
        EnsureAssetFolder("Assets/Textures");
        EnsureAssetFolder("Assets/Materials");
        WriteWoodWallpaperTexture(WoodWallpaperTexturePath);

        AssetDatabase.ImportAsset(WoodWallpaperTexturePath, ImportAssetOptions.ForceUpdate);
        var importer = AssetImporter.GetAtPath(WoodWallpaperTexturePath) as TextureImporter;
        if (importer != null)
        {
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.anisoLevel = 4;
            importer.mipmapEnabled = true;
            importer.sRGBTexture = true;
            importer.SaveAndReimport();
        }

        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(WoodWallpaperTexturePath);
        var material = AssetDatabase.LoadAssetAtPath<Material>(WoodWallpaperMaterialPath);
        if (material == null)
        {
            var shader = Shader.Find("Standard");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, WoodWallpaperMaterialPath);
        }

        material.name = "WoodWallPanels";
        if (material.shader == null)
        {
            material.shader = Shader.Find("Standard");
        }

        SetMaterialTexture(material, texture);
        SetMaterialColor(material, Color.white);
        SetMaterialFloat(material, "_Glossiness", 0.18f);
        SetMaterialFloat(material, "_Metallic", 0.0f);
        SetMaterialFloat(material, "_Smoothness", 0.18f);
        SetMaterialTextureScale(material, new Vector2(2.2f, 1.0f));
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        return material;
    }

    private static void WriteWoodWallpaperTexture(string assetPath)
    {
        const int width = 1024;
        const int height = 1024;
        const int plankCount = 9;

        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Bilinear;

        var palette = new[]
        {
            new Color(0.29f, 0.18f, 0.12f, 1f),
            new Color(0.34f, 0.21f, 0.14f, 1f),
            new Color(0.39f, 0.25f, 0.17f, 1f),
            new Color(0.31f, 0.20f, 0.13f, 1f)
        };

        var plankWidth = width / plankCount;
        for (var x = 0; x < width; x++)
        {
            var plankIndex = Mathf.Clamp(x / plankWidth, 0, plankCount - 1);
            var localX = x - (plankIndex * plankWidth);
            var seamDistance = Mathf.Min(localX, plankWidth - 1 - localX);

            var baseColor = palette[plankIndex % palette.Length];
            var plankVariation = 0.86f + (0.08f * Mathf.Sin((plankIndex + 1) * 1.7f));
            var edgeShade = Mathf.Lerp(0.65f, 1.0f, Mathf.Clamp01(seamDistance / 12f));

            for (var y = 0; y < height; y++)
            {
                var grainA = Mathf.PerlinNoise((x * 0.04f) + (plankIndex * 1.3f), y * 0.010f);
                var grainB = Mathf.PerlinNoise((x * 0.015f) + 11.0f, (y * 0.09f) + (plankIndex * 3.1f));
                var fiber = Mathf.PerlinNoise((plankIndex * 8.0f) + 2.0f, y * 0.18f);
                var plankWave = Mathf.Sin((y * 0.028f) + (plankIndex * 0.8f)) * 0.035f;

                var brightness = plankVariation;
                brightness *= Mathf.Lerp(0.82f, 1.12f, grainA);
                brightness *= Mathf.Lerp(0.90f, 1.05f, grainB);
                brightness *= Mathf.Lerp(0.92f, 1.06f, fiber);
                brightness *= edgeShade + plankWave;

                if (seamDistance <= 2)
                {
                    brightness *= 0.55f;
                }

                var knot = Mathf.PerlinNoise((x * 0.12f) + (plankIndex * 7.0f), (y * 0.035f) + 23.0f);
                if (knot > 0.73f)
                {
                    brightness *= 0.80f;
                }

                var color = baseColor * brightness;
                color.a = 1f;
                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();
        var absolutePath = AssetPathToAbsolute(assetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
        File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
    }

    private static string AssetPathToAbsolute(string assetPath)
    {
        var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
        return Path.Combine(projectRoot, assetPath);
    }

    private static void EnsureAssetFolder(string assetPath)
    {
        var absolutePath = AssetPathToAbsolute(assetPath);
        Directory.CreateDirectory(absolutePath);
    }

    private static GameObject FindOrCreateRoot(string name, Transform parent)
    {
        var existing = GameObject.Find(name);
        if (existing != null)
        {
            if (parent != null && existing.transform.parent != parent)
            {
                existing.transform.SetParent(parent, false);
            }

            return existing;
        }

        var created = new GameObject(name);
        created.transform.SetParent(parent, false);
        return created;
    }

    private static void ClearChildren(Transform parent)
    {
        for (var i = parent.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(parent.GetChild(i).gameObject);
        }
    }

    private static GameObject CloneDecor(
        GameObject source,
        Transform parent,
        string newName,
        Vector3 localPosition,
        Vector3 localEulerAngles,
        Vector3 localScale)
    {
        var clone = Object.Instantiate(source, parent);
        clone.name = newName;
        clone.transform.localPosition = localPosition;
        clone.transform.localRotation = Quaternion.Euler(localEulerAngles);
        clone.transform.localScale = localScale;
        clone.SetActive(true);
        return clone;
    }

    private static void BuildPlantAssembly(
        Transform parent,
        string rootName,
        GameObject plantPotPrefab,
        GameObject plantUmbrellaPrefab,
        Vector3 localPosition,
        Vector3 localEulerAngles,
        Vector3 localScale)
    {
        var root = new GameObject(rootName);
        root.transform.SetParent(parent, false);
        root.transform.localPosition = localPosition;
        root.transform.localRotation = Quaternion.Euler(localEulerAngles);
        root.transform.localScale = localScale;

        var pot = Object.Instantiate(plantPotPrefab, root.transform);
        pot.name = "PlantPot";
        pot.transform.localPosition = Vector3.zero;
        pot.transform.localRotation = Quaternion.identity;
        pot.transform.localScale = Vector3.one;
        TintHierarchy(pot, PlantPotColor);

        var leaves = Object.Instantiate(plantUmbrellaPrefab, root.transform);
        leaves.name = "PlantLeaves";
        leaves.transform.localPosition = Vector3.zero;
        leaves.transform.localRotation = Quaternion.identity;
        leaves.transform.localScale = Vector3.one;
        TintHierarchy(leaves, PlantLeafColor);
    }

    private static void BuildShelfSet(
        Transform parent,
        string rootName,
        Transform shelfSource,
        Transform booksSource,
        Vector3 localPosition,
        Vector3 localEulerAngles,
        Vector3 localScale)
    {
        var root = new GameObject(rootName);
        root.transform.SetParent(parent, false);
        root.transform.localPosition = localPosition;
        root.transform.localRotation = Quaternion.Euler(localEulerAngles);
        root.transform.localScale = localScale;

        var shelf = Object.Instantiate(shelfSource.gameObject, root.transform);
        shelf.name = "Shelf";
        shelf.transform.localPosition = Vector3.zero;
        shelf.transform.localRotation = Quaternion.identity;
        shelf.transform.localScale = Vector3.one;
        TintHierarchy(shelf, DarkWoodColor);

        if (booksSource != null)
        {
            AddBooks(root.transform, booksSource.gameObject, "Books_Left", new Vector3(-0.18f, 1.05f, -0.04f), new Vector3(0f, 0f, 0f));
            AddBooks(root.transform, booksSource.gameObject, "Books_Right", new Vector3(0.16f, 1.30f, -0.02f), new Vector3(0f, 90f, 0f));
        }
    }

    private static void AddBooks(
        Transform parent,
        GameObject booksSource,
        string name,
        Vector3 localPosition,
        Vector3 localEulerAngles)
    {
        var books = Object.Instantiate(booksSource, parent);
        books.name = name;
        books.transform.localPosition = localPosition;
        books.transform.localRotation = Quaternion.Euler(localEulerAngles);
        books.transform.localScale = Vector3.one * 0.55f;
        TintHierarchy(books, BookAccentColor);
    }

    private static void TintNamedObject(string objectName, Color color)
    {
        var go = GameObject.Find(objectName);
        if (go == null)
        {
            return;
        }

        TintHierarchy(go, color);
    }

    private static void ApplyMaterialToNamedObject(string objectName, Material material)
    {
        var go = GameObject.Find(objectName);
        if (go == null || material == null)
        {
            return;
        }

        foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
        {
            var count = Mathf.Max(1, renderer.sharedMaterials != null ? renderer.sharedMaterials.Length : 0);
            var materials = new Material[count];
            for (var i = 0; i < count; i++)
            {
                materials[i] = material;
            }

            renderer.sharedMaterials = materials;
        }
    }

    private static void TintHierarchy(GameObject root, Color color)
    {
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            var shared = renderer.sharedMaterials;
            if (shared == null || shared.Length == 0)
            {
                continue;
            }

            var tinted = new Material[shared.Length];
            for (var i = 0; i < shared.Length; i++)
            {
                var template = shared[i];
                var shader = template != null ? template.shader : Shader.Find("Standard");
                var material = template != null ? new Material(template) : new Material(shader);
                material.name = $"{root.name}_Tinted_{i}";
                SetMaterialColor(material, color);
                tinted[i] = material;
            }

            renderer.sharedMaterials = tinted;
        }
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        if (material.HasProperty("_EmissionColor"))
        {
            material.SetColor("_EmissionColor", color * 0.05f);
        }
    }

    private static void SetMaterialTexture(Material material, Texture texture)
    {
        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", texture);
        }

        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", texture);
        }
    }

    private static void SetMaterialTextureScale(Material material, Vector2 scale)
    {
        if (material.HasProperty("_BaseMap"))
        {
            material.SetTextureScale("_BaseMap", scale);
        }

        if (material.HasProperty("_MainTex"))
        {
            material.SetTextureScale("_MainTex", scale);
        }
    }

    private static void SetMaterialFloat(Material material, string propertyName, float value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
    }

    private static Transform FindFirstByName(Transform root, string name)
    {
        foreach (var child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == name)
            {
                return child;
            }
        }

        return null;
    }

    private static List<Transform> FindAllByName(Transform root, string name)
    {
        var matches = new List<Transform>();
        foreach (var child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == name)
            {
                matches.Add(child);
            }
        }

        return matches;
    }
}
#endif
