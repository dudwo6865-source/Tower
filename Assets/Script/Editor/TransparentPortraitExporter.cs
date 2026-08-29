using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class TransparentPortraitExporter
{
    public const string DefaultOutputFolder = "Assets/Art/Portraits";

    public struct ExportSettings
    {
        public int width;
        public int height;
        public float padding;
        public float yaw;
        public float pitch;
        public float fieldOfView;
        // 수직 프레이밍 오프셋(대상 높이 대비 비율). 양수면 시점이 위로 올라갑니다.
        public float heightOffset;
        // 대상 배율. 1보다 크면 크게(가깝게), 작으면 작게(멀리) 찍힙니다.
        public float zoom;
        public bool orthographic;
        public bool hideGameplayUi;
        public bool importAsSprite;
        public bool assignPortrait;
    }

    public struct ExportResult
    {
        public bool success;
        public string assetPath;
        public string message;
        public SelectableEntity assignedEntity;
    }

    public static ExportResult Export(Object source, string outputFolder, string fileName, ExportSettings settings)
    {
        if (source == null)
            return Fail("내보낼 대상이 없습니다.");

        GameObject prefabOrObject = ResolveSourceObject(source);

        if (prefabOrObject == null)
            return Fail("GameObject 또는 프리팹을 지정해 주세요.");

        if (string.IsNullOrWhiteSpace(outputFolder))
            outputFolder = DefaultOutputFolder;

        if (!outputFolder.StartsWith("Assets/", StringComparison.Ordinal))
            return Fail("출력 경로는 Assets/ 아래여야 합니다.");

        if (string.IsNullOrWhiteSpace(fileName))
            fileName = prefabOrObject.name;

        fileName = SanitizeFileName(fileName);

        if (settings.width <= 0 || settings.height <= 0)
            return Fail("해상도는 1 이상이어야 합니다.");

        Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), outputFolder));

        Texture2D texture = RenderToTexture(prefabOrObject, settings, settings.width, settings.height, out string renderError);

        if (texture == null)
            return Fail(renderError ?? "렌더링에 실패했습니다.");

        try
        {
            string assetPath = GetUniqueAssetPath(outputFolder, fileName);
            File.WriteAllBytes(assetPath, texture.EncodeToPNG());

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            if (settings.importAsSprite)
                ApplySpriteImportSettings(assetPath);

            SelectableEntity assignedEntity = null;

            if (settings.assignPortrait)
                assignedEntity = TryAssignPortrait(prefabOrObject, assetPath);

            AssetDatabase.SaveAssets();

            return new ExportResult
            {
                success = true,
                assetPath = assetPath,
                message = $"PNG 저장 완료: {assetPath}",
                assignedEntity = assignedEntity
            };
        }
        catch (Exception exception)
        {
            return Fail(exception.Message);
        }
        finally
        {
            Object.DestroyImmediate(texture);
        }
    }

    // 지정한 설정으로 대상을 렌더링해 알파 텍스처를 반환합니다.
    // 미리보기와 실제 내보내기 모두 이 코어를 사용합니다.
    // 반환된 Texture2D의 소유권은 호출자에게 있으므로 사용 후 DestroyImmediate 해야 합니다.
    public static Texture2D RenderPreview(Object source, ExportSettings settings, int width, int height, out string error)
    {
        error = null;

        if (source == null)
        {
            error = "대상이 없습니다.";
            return null;
        }

        GameObject prefabOrObject = ResolveSourceObject(source);

        if (prefabOrObject == null)
        {
            error = "GameObject 또는 프리팹을 지정해 주세요.";
            return null;
        }

        return RenderToTexture(prefabOrObject, settings, width, height, out error);
    }

    static Texture2D RenderToTexture(GameObject prefabOrObject, ExportSettings settings, int width, int height, out string error)
    {
        error = null;

        if (width <= 0 || height <= 0)
        {
            error = "해상도는 1 이상이어야 합니다.";
            return null;
        }

        PreviewRenderUtility preview = new PreviewRenderUtility(true);

        try
        {
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.backgroundColor = Color.clear;
            preview.camera.orthographic = settings.orthographic;
            preview.camera.aspect = (float)width / height;
            preview.camera.nearClipPlane = 0.01f;
            preview.camera.farClipPlane = 1000f;

            ConfigurePreviewLights(preview);

            GameObject instance = CreatePreviewInstance(preview, prefabOrObject);

            if (instance == null)
            {
                error = "미리보기 인스턴스를 만들지 못했습니다.";
                return null;
            }

            List<DisabledComponentState> disabledStates = new List<DisabledComponentState>();

            if (settings.hideGameplayUi)
                DisableGameplayVisuals(instance, disabledStates);

            Bounds bounds = CalculateRenderableBounds(instance);

            if (bounds.size.sqrMagnitude <= 0.0001f)
            {
                error = "렌더러가 없어 캡처할 수 없습니다.";
                RestoreDisabledComponents(disabledStates);
                return null;
            }

            FrameCamera(preview.camera, bounds, settings);

            RenderTexture renderTexture = RenderTexture.GetTemporary(
                width,
                height,
                24,
                RenderTextureFormat.ARGB32);

            RenderTexture previousTarget = preview.camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;

            try
            {
                preview.camera.targetTexture = renderTexture;
                preview.camera.Render();

                RenderTexture.active = renderTexture;

                Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();

                return texture;
            }
            finally
            {
                preview.camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(renderTexture);
                RestoreDisabledComponents(disabledStates);
            }
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return null;
        }
        finally
        {
            preview.Cleanup();
        }
    }

    public static IEnumerable<Object> GetExportSourcesFromSelection()
    {
        Object[] selection = Selection.objects;

        if (selection == null || selection.Length == 0)
            yield break;

        foreach (Object item in selection)
        {
            if (item == null)
                continue;

            if (item is GameObject || item is UnitData)
            {
                yield return item;
                continue;
            }

            if (item is Component component && component.gameObject != null)
                yield return component.gameObject;
        }
    }

    static GameObject ResolveSourceObject(Object source)
    {
        if (source is GameObject gameObject)
            return gameObject;

        if (source is UnitData unitData)
            return ResolveUnitDataSource(unitData);

        if (source is Component component)
            return component.gameObject;

        return null;
    }

    static GameObject ResolveUnitDataSource(UnitData unitData)
    {
        if (unitData == null)
            return null;

        string[] guids = AssetDatabase.FindAssets("t:Prefab");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab == null)
                continue;

            Unit unit = prefab.GetComponent<Unit>();

            if (unit != null && unit.data == unitData)
                return prefab;

            Building building = prefab.GetComponent<Building>();

            if (building != null && building.data == unitData)
                return prefab;
        }

        return null;
    }

    static GameObject CreatePreviewInstance(PreviewRenderUtility preview, GameObject source)
    {
        if (PrefabUtility.IsPartOfPrefabAsset(source))
            return preview.InstantiatePrefabInScene(source);

        GameObject instance = Object.Instantiate(source);
        instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        preview.AddSingleGO(instance);
        return instance;
    }

    static void ConfigurePreviewLights(PreviewRenderUtility preview)
    {
        if (preview.lights == null || preview.lights.Length == 0)
            return;

        preview.lights[0].intensity = 1.25f;
        preview.lights[0].color = Color.white;
        preview.lights[0].transform.rotation = Quaternion.Euler(40f, 40f, 0f);

        if (preview.lights.Length > 1)
        {
            preview.lights[1].intensity = 0.85f;
            preview.lights[1].color = new Color(0.85f, 0.9f, 1f, 1f);
            preview.lights[1].transform.rotation = Quaternion.Euler(335f, 220f, 0f);
        }
    }

    static void FrameCamera(Camera camera, Bounds bounds, ExportSettings settings)
    {
        Vector3 center = bounds.center;
        float maxExtent = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
        maxExtent = Mathf.Max(maxExtent, 0.1f);

        // 카메라 높낮이: 대상 높이에 비례해 시점(주시점)을 위/아래로 이동한다.
        // 양수면 주시점이 위로 올라가 대상이 프레임 아래쪽에 잡힌다.
        center.y += settings.heightOffset * bounds.size.y;

        // 여백(padding)으로 프레이밍 크기를 통일해 원근/직교 모두 같은 감각으로 조정한다.
        // 배율(zoom)로 대상을 더 크게(가깝게)/작게(멀리) 잡는다. zoom>1이면 프레임을 좁혀 크게 찍는다.
        float zoom = settings.zoom <= 0f ? 1f : settings.zoom;
        float frameExtent = maxExtent * (1f + settings.padding) / zoom;

        Quaternion rotation = Quaternion.Euler(settings.pitch, settings.yaw, 0f);
        camera.transform.rotation = rotation;

        if (camera.orthographic)
        {
            camera.orthographicSize = frameExtent;
            // 직교 카메라는 거리와 무관하지만 클립 평면 안에 들어오도록 충분히 뒤로 뺀다.
            camera.transform.position = center + rotation * (Vector3.back * (maxExtent * 4f));
            return;
        }

        float fov = Mathf.Clamp(settings.fieldOfView <= 0f ? 30f : settings.fieldOfView, 5f, 120f);
        camera.fieldOfView = fov;

        // 지정한 FOV에서 대상이 프레임에 꼭 맞도록 필요한 거리를 계산한다.
        float distance = frameExtent / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
        camera.transform.position = center + rotation * (Vector3.back * distance);
    }

    static Bounds CalculateRenderableBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

        if (renderers.Length == 0)
            return new Bounds(root.transform.position, Vector3.one * 0.1f);

        Bounds bounds = renderers[0].bounds;

        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        return bounds;
    }

    static void DisableGameplayVisuals(GameObject root, List<DisabledComponentState> disabledStates)
    {
        DisableBehaviours<WorldHealthBar>(root, disabledStates);
        DisableBehaviours<Canvas>(root, disabledStates);
        DisableBehaviours<SelectionRingIndicator>(root, disabledStates);
    }

    static void DisableBehaviours<T>(GameObject root, List<DisabledComponentState> disabledStates)
        where T : Behaviour
    {
        T[] components = root.GetComponentsInChildren<T>(true);

        foreach (T component in components)
        {
            if (component == null)
                continue;

            disabledStates.Add(new DisabledComponentState(component, component.enabled));
            component.enabled = false;
        }
    }

    static void RestoreDisabledComponents(List<DisabledComponentState> disabledStates)
    {
        for (int i = disabledStates.Count - 1; i >= 0; i--)
        {
            DisabledComponentState state = disabledStates[i];

            if (state.component != null)
                state.component.enabled = state.wasEnabled;
        }
    }

    static void ApplySpriteImportSettings(string assetPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;

        if (importer == null)
            return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    static SelectableEntity TryAssignPortrait(GameObject source, string assetPath)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);

        if (sprite == null || source == null)
            return null;

        GameObject prefabRoot = GetPrefabAssetRoot(source);

        if (prefabRoot == null)
            return null;

        SelectableEntity entity = prefabRoot.GetComponent<SelectableEntity>();

        if (entity == null)
            return null;

        Undo.RecordObject(entity, "Assign Portrait");
        entity.portrait = sprite;
        EditorUtility.SetDirty(entity);

        if (PrefabUtility.IsPartOfPrefabAsset(prefabRoot))
            PrefabUtility.SavePrefabAsset(prefabRoot);

        return entity;
    }

    static GameObject GetPrefabAssetRoot(GameObject source)
    {
        if (PrefabUtility.IsPartOfPrefabAsset(source))
            return source;

        GameObject correspondingObject = PrefabUtility.GetCorrespondingObjectFromSource(source);

        if (correspondingObject != null)
            return correspondingObject;

        return PrefabUtility.GetCorrespondingObjectFromOriginalSource(source);
    }

    static UnitData FindUnitData(GameObject gameObject)
    {
        if (gameObject == null)
            return null;

        Unit unit = gameObject.GetComponent<Unit>();

        if (unit != null && unit.data != null)
            return unit.data;

        Building building = gameObject.GetComponent<Building>();

        if (building != null && building.data != null)
            return building.data;

        return null;
    }

    static string GetUniqueAssetPath(string folder, string fileName)
    {
        string path = $"{folder}/{fileName}.png";
        int counter = 1;

        while (File.Exists(path))
        {
            path = $"{folder}/{fileName}_{counter}.png";
            counter++;
        }

        return path.Replace('\\', '/');
    }

    static string SanitizeFileName(string fileName)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars())
            fileName = fileName.Replace(invalid, '_');

        return fileName.Trim();
    }

    static ExportResult Fail(string message)
    {
        return new ExportResult
        {
            success = false,
            message = message
        };
    }

    readonly struct DisabledComponentState
    {
        public readonly Behaviour component;
        public readonly bool wasEnabled;

        public DisabledComponentState(Behaviour component, bool wasEnabled)
        {
            this.component = component;
            this.wasEnabled = wasEnabled;
        }
    }
}
