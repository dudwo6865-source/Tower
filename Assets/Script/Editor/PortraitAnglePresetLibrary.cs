using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 초상화 내보내기 창에서 저장한 카메라 프리셋 모음입니다.
// 에셋으로 저장하므로 EditorPrefs와 달리 프로젝트를 옮기거나 다른 PC에서 열어도 남습니다.
// 에디터 전용 타입이라 반드시 Editor 폴더 안에 두어야 빌드에 포함되지 않습니다.
public class PortraitAnglePresetLibrary : ScriptableObject
{
    public const string DefaultAssetPath = "Assets/Script/Editor/PortraitAnglePresets.asset";

    [System.Serializable]
    public class Preset
    {
        [Tooltip("프리셋 버튼에 표시되는 이름입니다.")]
        public string name = "새 프리셋";

        [Tooltip("좌우 회전(도)입니다.")]
        public float yaw;

        [Tooltip("상하 회전(도)입니다.")]
        public float pitch;

        [Tooltip("대상 높이 대비 시점의 상하 이동 비율입니다.")]
        public float heightOffset;

        [Tooltip("1보다 크면 대상을 크게 잡습니다.")]
        public float zoom = 1f;

        [Tooltip("대상 주위에 남길 여백 비율입니다.")]
        public float padding = 0.15f;

        [Tooltip("켜면 원근이 없는 Orthographic 카메라를 씁니다.")]
        public bool orthographic = true;

        [Tooltip("Orthographic이 꺼져 있을 때 쓰는 시야각입니다.")]
        public float fieldOfView = 30f;
    }

    [SerializeField]
    [Tooltip("저장한 프리셋 목록입니다. 순서를 바꾸면 창의 버튼 순서도 바뀝니다.")]
    List<Preset> presets = new List<Preset>();

    public List<Preset> Presets => presets;

    // 프로젝트 어디에 있든 찾아옵니다. (폴더를 옮겨도 동작하도록)
    public static PortraitAnglePresetLibrary Find()
    {
        string[] guids = AssetDatabase.FindAssets("t:PortraitAnglePresetLibrary");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            PortraitAnglePresetLibrary library =
                AssetDatabase.LoadAssetAtPath<PortraitAnglePresetLibrary>(path);

            if (library != null)
                return library;
        }

        return null;
    }

    // 없으면 만들어 줍니다. 프리셋을 처음 저장할 때만 호출해 빈 에셋이 생기지 않게 합니다.
    public static PortraitAnglePresetLibrary FindOrCreate()
    {
        PortraitAnglePresetLibrary library = Find();

        if (library != null)
            return library;

        library = CreateInstance<PortraitAnglePresetLibrary>();

        string folder = System.IO.Path.GetDirectoryName(DefaultAssetPath).Replace('\\', '/');

        if (!AssetDatabase.IsValidFolder(folder))
        {
            System.IO.Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
        }

        AssetDatabase.CreateAsset(library, AssetDatabase.GenerateUniqueAssetPath(DefaultAssetPath));
        AssetDatabase.SaveAssets();

        return library;
    }

    public Preset FindByName(string presetName)
    {
        if (string.IsNullOrWhiteSpace(presetName))
            return null;

        foreach (Preset preset in presets)
        {
            if (preset != null && preset.name == presetName)
                return preset;
        }

        return null;
    }
}
