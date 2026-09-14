using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class TransparentPortraitExporter
{
    public const string DefaultOutputFolder = "Assets/Art/Portraits";

    // 슈퍼샘플링 시 렌더 타깃 한 변의 최대 픽셀 수입니다.
    const int MaxRenderSize = 4096;

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
        // 수평 프레이밍 오프셋(화면상 대상 폭 대비 비율). 양수면 시점이 오른쪽으로 갑니다.
        public float sideOffset;
        // 대상 배율. 1보다 크면 크게(가깝게), 작으면 작게(멀리) 찍힙니다.
        public float zoom;
        public bool orthographic;
        public bool hideGameplayUi;
        public bool importAsSprite;
        public bool assignPortrait;
        // 같은 이름의 PNG가 있으면 새 파일을 만들지 않고 덮어씁니다.
        public bool overwriteExisting;
        // 1보다 크면 그 배율로 크게 렌더한 뒤 줄여 외곽선 계단현상을 줄입니다. (1/2/4)
        public int supersample;
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
            string assetPath = GetTargetAssetPath(outputFolder, fileName, settings.overwriteExisting);
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

        // 슈퍼샘플링: 크게 렌더한 뒤 평균을 내어 줄이면 알파 외곽선이 부드러워집니다.
        int scale = Mathf.Clamp(settings.supersample <= 0 ? 1 : settings.supersample, 1, 4);

        while (scale > 1 && (width * scale > MaxRenderSize || height * scale > MaxRenderSize))
            scale /= 2;

        int renderWidth = width * scale;
        int renderHeight = height * scale;

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
                renderWidth,
                renderHeight,
                24,
                RenderTextureFormat.ARGB32);

            RenderTexture previousTarget = preview.camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;

            try
            {
                preview.camera.targetTexture = renderTexture;
                preview.camera.Render();

                RenderTexture.active = renderTexture;

                Texture2D rendered = new Texture2D(renderWidth, renderHeight, TextureFormat.RGBA32, false);
                rendered.hideFlags = HideFlags.HideAndDontSave;
                rendered.ReadPixels(new Rect(0, 0, renderWidth, renderHeight), 0, 0);
                rendered.Apply();

                if (scale == 1)
                    return rendered;

                Texture2D downsampled = Downsample(rendered, width, height, scale);
                Object.DestroyImmediate(rendered);

                return downsampled;
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

    // 프로젝트 전체 프리팹 스캔은 비싸므로 한 번 찾은 결과를 캐시합니다.
    // (미리보기는 값이 바뀔 때마다 렌더되므로 캐시가 없으면 슬라이더가 버벅입니다.)
    static readonly Dictionary<UnitData, GameObject> UnitDataPrefabCache = new Dictionary<UnitData, GameObject>();

    public static void ClearUnitDataCache()
    {
        UnitDataPrefabCache.Clear();
    }

    static GameObject ResolveUnitDataSource(UnitData unitData)
    {
        if (unitData == null)
            return null;

        if (UnitDataPrefabCache.TryGetValue(unitData, out GameObject cached) && cached != null)
            return cached;

        string[] guids = AssetDatabase.FindAssets("t:Prefab");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab == null)
                continue;

            Unit unit = prefab.GetComponent<Unit>();

            if (unit != null && unit.data == unitData)
            {
                UnitDataPrefabCache[unitData] = prefab;
                return prefab;
            }

            Building building = prefab.GetComponent<Building>();

            if (building != null && building.data == unitData)
            {
                UnitDataPrefabCache[unitData] = prefab;
                return prefab;
            }
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

        Quaternion rotation = Quaternion.Euler(settings.pitch, settings.yaw, 0f);
        camera.transform.rotation = rotation;

        // 바운딩 박스를 카메라 기준으로 돌려서 화면상 가로/세로 크기를 구한다.
        // (단순히 가장 긴 변만 쓰면 각도나 가로세로 비에 따라 잘리거나 너무 작게 나온다.)
        GetViewExtents(bounds.extents, rotation, out float viewExtentX, out float viewExtentY, out float viewExtentZ);

        float aspect = camera.aspect <= 0f ? 1f : camera.aspect;

        // 세로 기준 프레임 크기. 가로가 더 넓으면 비율로 나눠 가로도 들어오게 한다.
        float fitExtent = Mathf.Max(viewExtentY, viewExtentX / aspect);
        fitExtent = Mathf.Max(fitExtent, 0.05f);

        // 카메라 높낮이: 대상 높이에 비례해 시점(주시점)을 위/아래로 이동한다.
        // 양수면 주시점이 위로 올라가 대상이 프레임 아래쪽에 잡힌다.
        center.y += settings.heightOffset * bounds.size.y;

        // 카메라 좌우: 화면상 대상 폭에 비례해 시점을 카메라 기준 좌/우로 이동한다.
        // 월드 X가 아니라 카메라의 오른쪽 축을 쓰므로 Yaw를 돌려도 화면 기준으로 움직인다.
        // 양수면 주시점이 오른쪽으로 가서 대상이 프레임 왼쪽에 잡힌다.
        center += rotation * Vector3.right * (settings.sideOffset * viewExtentX * 2f);

        // 여백(padding)으로 프레이밍 크기를 통일해 원근/직교 모두 같은 감각으로 조정한다.
        // 배율(zoom)로 대상을 더 크게(가깝게)/작게(멀리) 잡는다. zoom>1이면 프레임을 좁혀 크게 찍는다.
        float zoom = settings.zoom <= 0f ? 1f : settings.zoom;
        float frameExtent = fitExtent * (1f + settings.padding) / zoom;

        // 대상이 근평면 앞으로 튀어나오지 않도록 깊이 절반만큼 더 물러난다.
        float backOff = viewExtentZ + 1f;

        if (camera.orthographic)
        {
            camera.orthographicSize = frameExtent;
            camera.transform.position = center + rotation * (Vector3.back * (backOff + frameExtent * 2f));
            return;
        }

        float fov = Mathf.Clamp(settings.fieldOfView <= 0f ? 30f : settings.fieldOfView, 5f, 120f);
        camera.fieldOfView = fov;

        // 지정한 FOV에서 대상이 프레임에 꼭 맞도록 필요한 거리를 계산한다.
        float distance = frameExtent / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
        camera.transform.position = center + rotation * (Vector3.back * (distance + backOff));
    }

    // 바운딩 박스 8개 꼭짓점을 카메라 로컬 축으로 옮겨 축별 최대 반지름을 구합니다.
    static void GetViewExtents(Vector3 extents, Quaternion rotation, out float x, out float y, out float z)
    {
        Quaternion inverse = Quaternion.Inverse(rotation);
        x = 0f;
        y = 0f;
        z = 0f;

        for (int corner = 0; corner < 8; corner++)
        {
            Vector3 point = new Vector3(
                (corner & 1) == 0 ? -extents.x : extents.x,
                (corner & 2) == 0 ? -extents.y : extents.y,
                (corner & 4) == 0 ? -extents.z : extents.z);

            Vector3 local = inverse * point;

            x = Mathf.Max(x, Mathf.Abs(local.x));
            y = Mathf.Max(y, Mathf.Abs(local.y));
            z = Mathf.Max(z, Mathf.Abs(local.z));
        }
    }

    // 실제로 화면에 보이는 렌더러만으로 프레이밍을 계산합니다.
    // 꺼져 있는 이펙트나 체력바까지 포함하면 대상이 필요 이상으로 작게 잡힙니다.
    static Bounds CalculateRenderableBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

        if (renderers.Length == 0)
            return new Bounds(root.transform.position, Vector3.one * 0.1f);

        bool hasBounds = false;
        Bounds bounds = new Bounds(root.transform.position, Vector3.zero);

        for (int pass = 0; pass < 2 && !hasBounds; pass++)
        {
            // 1차: 보이는 메시 렌더러만. 하나도 없으면 2차에서 조건 없이 전부 사용합니다.
            bool strict = pass == 0;

            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                    continue;

                if (strict && !IsFramingRenderer(renderer))
                    continue;

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                    continue;
                }

                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds ? bounds : new Bounds(root.transform.position, Vector3.one * 0.1f);
    }

    static bool IsFramingRenderer(Renderer renderer)
    {
        if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
            return false;

        // 파티클/트레일/라인/UI는 프레이밍 기준에서 제외합니다.
        if (renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer)
            return false;

        if (renderer.GetComponentInParent<Canvas>() != null)
            return false;

        return renderer.bounds.size.sqrMagnitude > 0.000001f;
    }

    // 알파를 고려해(프리멀티플라이) 평균을 내야 반투명 외곽에 검은 테두리가 생기지 않습니다.
    static Texture2D Downsample(Texture2D source, int width, int height, int scale)
    {
        Color[] src = source.GetPixels();
        Color[] dst = new Color[width * height];
        int srcWidth = source.width;
        float sampleCount = scale * scale;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float r = 0f, g = 0f, b = 0f, a = 0f;

                for (int sy = 0; sy < scale; sy++)
                {
                    int row = (y * scale + sy) * srcWidth;

                    for (int sx = 0; sx < scale; sx++)
                    {
                        Color c = src[row + x * scale + sx];
                        r += c.r * c.a;
                        g += c.g * c.a;
                        b += c.b * c.a;
                        a += c.a;
                    }
                }

                float alpha = a / sampleCount;

                dst[y * width + x] = alpha <= 0.0001f
                    ? new Color(0f, 0f, 0f, 0f)
                    : new Color(r / a, g / a, b / a, alpha);
            }
        }

        Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false);
        result.hideFlags = HideFlags.HideAndDontSave;
        result.SetPixels(dst);
        result.Apply();

        return result;
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

    static string GetTargetAssetPath(string folder, string fileName, bool overwriteExisting)
    {
        string path = $"{folder}/{fileName}.png";

        if (overwriteExisting)
            return path.Replace('\\', '/');

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
