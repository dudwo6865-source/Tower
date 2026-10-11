using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "BuildableProductionData",
    menuName = "Tank/생산 건물 데이터")]
public class BuildableProductionData : ScriptableObject, IUpgradableBuildableData
{
    [Header("표시")]
    [Label("표시 이름")]
    public string displayName = "Barracks";

    [Label("아이콘")]
    [Tooltip("빌드 버튼에 표시할 아이콘입니다. 비워두면 프리팹의 SelectableEntity.portrait를 사용합니다.")]
    public Sprite icon;

    [Label("설명")]
    [TextArea]
    public string description;

    [Header("건설")]
    [Label("건물 프리팹")]
    public GameObject prefab;

    [Label("Watt 비용")]
    [Tooltip("배치 확정 시 소비되는 Watt입니다.")]
    public int wattCost = 75;

    [Label("소유자 ID")]
    [Tooltip("배치되는 건물의 소유자 ID입니다.")]
    public int ownerId = 1;

    [Label("타입 ID")]
    [Tooltip("더블클릭 시 같은 종류로 묶을 타입 ID입니다.")]
    public string entityTypeId;

    [Header("생산")]
    [Label("최대 인구수")]
    [Tooltip("이 건물 하나가 생산한 유닛들이 동시에 차지할 수 있는 인구수 합계입니다. 예: 20이고 마린 인구수가 1이면 마린 20기까지 생산합니다. 0이면 무제한입니다.")]
    [Min(0)]
    public int maxPopulation = 20;

    [Label("생산 레시피 목록")]
    [Tooltip("이 건물에서 고를 수 있는 생산 레시피입니다. 생산 패널 슬롯에 이 순서대로 표시됩니다.")]
    public List<ProductionRecipe> recipes = new List<ProductionRecipe>();

    [Label("기본 레시피 번호")]
    [Tooltip("건물이 완공됐을 때 처음 생산할 레시피의 목록 번호(0부터)입니다.")]
    [Min(0)]
    public int defaultRecipeIndex;

    [Header("업그레이드")]
    [Label("업그레이드 분기")]
    [Tooltip("업그레이드 분기 목록입니다. 여러 개를 넣으면 플레이어가 갈래를 고를 수 있습니다. " +
        "각 분기 프리팹의 칸 수는 이 건물과 같아야 합니다. 업그레이드해도 집결지·생산한 유닛·생산 중인 레시피는 이어집니다.")]
    public List<ProductionUpgradeOption> upgradeOptions = new List<ProductionUpgradeOption>();

    // 예전 단일 레시피 필드입니다. OnValidate에서 목록으로 옮긴다.
    [SerializeField, HideInInspector]
    ProductionRecipe recipe;

    public IReadOnlyList<ProductionRecipe> Recipes => recipes;

    public bool HasUpgradeOptions
    {
        get
        {
            if (upgradeOptions == null)
                return false;

            for (int i = 0; i < upgradeOptions.Count; i++)
            {
                if (IsUsableOption(upgradeOptions[i]))
                    return true;
            }

            return false;
        }
    }

    /// <summary>자기 자신으로의 순환은 무한 교체가 되므로 분기에서 제외합니다.</summary>
    public bool IsUsableOption(ProductionUpgradeOption option)
    {
        return option != null && option.IsValid && option.tier != this;
    }

    public void GetUpgradeOptions(List<BuildingUpgradeOption> results)
    {
        if (upgradeOptions == null)
            return;

        for (int i = 0; i < upgradeOptions.Count; i++)
        {
            if (IsUsableOption(upgradeOptions[i]))
                results.Add(upgradeOptions[i]);
        }
    }

    public bool HasRecipes => GetDefaultRecipe() != null;

    public ProductionRecipe GetDefaultRecipe()
    {
        if (recipes == null || recipes.Count == 0)
            return recipe;

        int index = Mathf.Clamp(defaultRecipeIndex, 0, recipes.Count - 1);

        if (recipes[index] != null)
            return recipes[index];

        for (int i = 0; i < recipes.Count; i++)
        {
            if (recipes[i] != null)
                return recipes[i];
        }

        return null;
    }

    public string BuildAssetName => name;
    public string DisplayName => displayName;
    public GameObject Prefab => prefab;
    // W03 절약형 건축법: 건설 Watt 비용 할인을 반영합니다.
    public int WattCost => RelicManager.Instance != null
        ? Mathf.Max(0, Mathf.RoundToInt(RelicManager.Instance.ApplyBonus(RelicEffectType.BuildWattCost, wattCost)))
        : wattCost;
    public int OwnerId => ownerId;
    public Sprite Icon => icon != null ? icon : BuildableIconResolver.ResolvePrefabPortrait(prefab);

    void OnValidate()
    {
        if (recipes == null)
            recipes = new List<ProductionRecipe>();

        if (recipe != null)
        {
            if (!recipes.Contains(recipe))
                recipes.Insert(0, recipe);

            recipe = null;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        if (recipes.Count > 0)
            defaultRecipeIndex = Mathf.Clamp(defaultRecipeIndex, 0, recipes.Count - 1);
        else
            defaultRecipeIndex = 0;
    }

    public Vector2Int GetFootprintCells()
    {
        return GridFootprint.ResolveFootprintCells(prefab);
    }

    public string GetEntityTypeId()
    {
        if (!string.IsNullOrWhiteSpace(entityTypeId))
            return entityTypeId;

        if (prefab == null)
            return displayName;

        SelectableEntity selectable = prefab.GetComponent<SelectableEntity>();

        if (selectable != null && !string.IsNullOrWhiteSpace(selectable.entityTypeId))
            return selectable.entityTypeId;

        return displayName;
    }
}
