// 유니티 밖에서 보상 계산과 파일 저장을 검증하기 위한 최소 대체 구현이다.
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Text;

public class LabelAttribute : Attribute { public LabelAttribute(string label) {} }
namespace UnityEngine
{
    public class MonoBehaviour
    {
        public object gameObject;
        public Transform transform = new Transform();
        protected T GetComponent<T>() where T : class
            => typeof(T) == typeof(SelectableEntity) ? new SelectableEntity() as T : null;
        protected T[] GetComponentsInChildren<T>() => new T[0];
        protected static void Destroy(object target) {}
    }
    public class HeaderAttribute : Attribute { public HeaderAttribute(string text) {} }
    public class DisallowMultipleComponent : Attribute {}
    public class RangeAttribute : Attribute { public RangeAttribute(float min, float max) {} }
    public class MinAttribute : Attribute { public MinAttribute(float min) {} }
    public class TooltipAttribute : Attribute { public TooltipAttribute(string text) {} }
    public enum RuntimeInitializeLoadType { SubsystemRegistration }
    public class RuntimeInitializeOnLoadMethodAttribute : Attribute
    { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType type) {} }
    public static class Application { public static string persistentDataPath; }
    public static class Debug
    {
        public static void LogError(object message) { Console.WriteLine(message); }
        public static void LogException(Exception exception) { throw exception; }
    }
    public static class Mathf
    {
        public static int Max(int a, int b) => Math.Max(a, b);
        public static float Max(float a, float b) => Math.Max(a, b);
        public static float Min(float a, float b) => Math.Min(a, b);
        public static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));
        public static float Clamp(float value, float min, float max) => Math.Max(min, Math.Min(max, value));
        public static bool Approximately(float a, float b) => Math.Abs(a - b) < 0.00001f;
        public static float Clamp01(float value) => Math.Max(0, Math.Min(1, value));
    }
    public static class JsonUtility
    {
        public static string ToJson(object value, bool prettyPrint = false)
        {
            using (var stream = new MemoryStream())
            {
                new DataContractJsonSerializer(value.GetType()).WriteObject(stream, value);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }
        public static T FromJson<T>(string json)
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
        }
    }
}

public enum RelicEffectType { ManaStoneGain }
public class RelicManager
{
    public static RelicManager Instance;
    public float GetModifiedValue(RelicEffectType effect, int owner, float amount) => amount * 2;
}

public static class CreditRewardsHarness
{
    static int checks;
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
    }
    static void Reload() => typeof(CreditWallet).GetMethod("ResetStatics",
        BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);

    public static void Main(string[] args)
    {
        UnityEngine.Application.persistentDataPath = args[0];
        var settings = new StageClearRewards();
        Check(settings.Evaluate(0, 0, 0).Stars == 0, "점수 없는 클리어는 0성");
        Check(settings.Evaluate(499, 0, 0).Stars == 0, "50퍼센트 미달");
        Check(settings.Evaluate(500, 0, 0).Stars == 1, "50퍼센트 경계");
        Check(settings.Evaluate(749, 0, 0).Stars == 1, "75퍼센트 미달");
        Check(settings.Evaluate(750, 0, 0).Stars == 2, "75퍼센트 경계");
        Check(settings.Evaluate(999, 0, 0).Stars == 2, "100퍼센트 미달");
        Check(settings.Evaluate(1000, 0, 0).Stars == 3, "100퍼센트 경계");
        Check(settings.Evaluate(9999, 0, 0).Stars == 3, "기준 초과도 최대 3성");
        var combined = settings.Evaluate(300, 4, 300);
        Check(combined.TimeScore == 300 && combined.SpawnerScore == 400 && combined.ManaStoneScore == 300
            && combined.TotalScore == 1000 && combined.Stars == 3, "세 항목 합산");
        Check(settings.Evaluate(0, 10, 0).Stars == 3, "스포너만으로 기준 달성");
        Check(settings.Evaluate(0, 0, 1000).Stars == 3, "마석만으로 기준 달성");
        Check(settings.Evaluate(100, 1, 100).TotalScore < settings.Evaluate(101, 1, 100).TotalScore, "긴 게임 시간은 점수 증가");
        Check(settings.Evaluate(100, 1, 100).TotalScore < settings.Evaluate(100, 2, 100).TotalScore, "스포너 추가 파괴는 점수 증가");
        Check(settings.Evaluate(100, 1, 100).TotalScore < settings.Evaluate(100, 1, 101).TotalScore, "마석 추가 획득은 점수 증가");
        var weighted = new StageClearRewards { pointsPerSecond = 2, pointsPerSpawner = 50, pointsPerManaStone = 3 };
        Check(weighted.Evaluate(10, 2, 20).TotalScore == 180, "스테이지별 점수 배율");
        Check(settings.Evaluate(499.9f, 0, 0.9f).TotalScore == 499, "항목별 소수점 버림");
        Check(settings.Evaluate(-1, -1, -1).TotalScore == 0, "음수 실적 보정");
        var invalid = new StageClearRewards { targetScore = 0, pointsPerSecond = -1, pointsPerSpawner = -1, pointsPerManaStone = -1 };
        Check(invalid.TargetScore == 1 && invalid.Evaluate(10, 10, 10).Stars == 0, "잘못된 기준과 음수 배율 보정");
        var odd = new StageClearRewards { targetScore = 1001 };
        Check(odd.OneStarScore == 501 && odd.TwoStarScore == 751 && odd.Evaluate(500, 0, 0).Stars == 0,
            "홀수 기준 점수의 경계 올림");
        Check(settings.Evaluate(float.NaN, 0, float.PositiveInfinity).TotalScore == 0, "잘못된 실적 값 보호");
        var health = new EntityHealth { armor = 10 };
        typeof(EntityHealth).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(health, null);
        health.TakeDamage(30);
        Check(health.TotalDamageTaken == 20, "방어력 적용 후 실제 피해 누적");
        health.Heal(100);
        Check(health.CurrentHealth == 100 && health.TotalDamageTaken == 20, "수리 후에도 피해 기록 유지");
        health.SetUpgradeMaxHealthBonus(100);
        Check(health.CurrentHealth == 200 && health.TotalDamageTaken == 20, "체력 업그레이드로 피해 기록 유지");
        health.TakeDamage(150);
        health.Heal(1000);
        Check(health.TotalDamageTaken == 160, "누적 피해 통계 유지");
        health.TakeDamage(1000);
        Check(health.TotalDamageTaken == 360, "남은 체력보다 큰 피해는 실제 잃은 체력만 집계");
        var mana = new ManaStoneManager { startingManaStone = 50 };
        typeof(ManaStoneManager).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(mana, null);
        Check(mana.CurrentManaStone == 50 && mana.TotalEarnedManaStone == 0, "시작 마석 제외");
        mana.Add(100);
        Check(mana.TotalEarnedManaStone == 100, "마석 획득 집계");
        Check(mana.TrySpend(100) && mana.TotalEarnedManaStone == 100, "소비해도 누적량 유지");
        mana.maxManaStone = 60;
        mana.Add(100);
        Check(mana.TotalEarnedManaStone == 110, "보유 상한 초과분 제외");
        mana.Add(100);
        Check(mana.TotalEarnedManaStone == 110, "가득 찬 상태 중복 집계 방지");
        mana.maxManaStone = 0;
        RelicManager.Instance = new RelicManager();
        mana.Add(10);
        Check(mana.TotalEarnedManaStone == 130, "유물 보너스 포함");
        mana.Add(-10);
        Check(mana.TotalEarnedManaStone == 130, "음수 획득 무시");
        Check(CreditWallet.TryGetBalance(out long balance) && balance == 0, "신규 저장");
        Check(CreditWallet.TryAwardClear("stage-a", "run-1", 1, settings, out var first)
            && first.FirstBonus == 100 && first.Total == 120, "첫 클리어");
        Check(CreditWallet.TryAwardClear("stage-a", "run-1", 1, settings, out var duplicate)
            && duplicate.Total == 120, "동일 판 재시도");
        Check(CreditWallet.TryGetBalance(out balance) && balance == 120, "중복 지급 방지");
        Reload();
        Check(CreditWallet.TryGetBalance(out balance) && balance == 120, "재실행 후 잔액");
        Check(CreditWallet.TryAwardClear("stage-a", "run-1", 1, settings, out duplicate)
            && CreditWallet.TryGetBalance(out balance) && balance == 120, "재실행 후 중복 방지");
        Check(CreditWallet.TryAwardClear("stage-a", "run-2", 3, settings, out var improved)
            && improved.FirstBonus == 100 && improved.Total == 140, "새 별 보너스 보충");
        Check(CreditWallet.TryAwardClear("stage-a", "run-3", 1, settings, out var repeat)
            && repeat.FirstBonus == 0 && repeat.Total == 20, "낮은 등급 재클리어");
        Check(CreditWallet.GetBestStars("stage-a") == 3, "최고 등급 유지");
        Check(CreditWallet.TryAwardClear("stage-b", "run-4", 3, settings, out var other)
            && other.FirstBonus == 200 && other.Total == 240, "다른 스테이지 독립 기록");
        Check(CreditWallet.TryGetBalance(out balance) && balance == 520, "누적 잔액");
        File.WriteAllText(CreditWallet.SavePath + ".tmp", "잠금");
        using (var locked = new FileStream(CreditWallet.SavePath + ".tmp", FileMode.Open,
            FileAccess.ReadWrite, FileShare.None))
            Check(!CreditWallet.TryAwardClear("stage-c", "run-5", 3, settings, out var failed), "저장 실패 반환");
        Check(CreditWallet.TryGetBalance(out balance) && balance == 520
            && CreditWallet.GetBestStars("stage-c") == 0, "실패 시 메모리 유지");
        Check(CreditWallet.TryAwardClear("stage-c", "run-5", 3, settings, out var retried)
            && retried.Total == 240, "저장 재시도");
        Reload();
        Check(CreditWallet.TryGetBalance(out balance) && balance == 760, "재시도 영속화");
        Check(CreditWallet.TryAwardClear("stage-zero", "run-zero", 0, settings, out var zero)
            && zero.Stars == 0 && zero.FirstBonus == 0 && zero.Total == 0, "0성 클리어 자동 별 지급 방지");
        Reload();
        Check(CreditWallet.TryGetBalance(out balance) && balance == 760
            && CreditWallet.GetBestStars("stage-zero") == 0, "0성 기록 다시 불러오기");
        Check(CreditWallet.TryAwardClear("stage-zero", "run-improved", 1, settings, out var newStar)
            && newStar.FirstBonus == 100 && newStar.Total == 120, "0성 이후 첫 별 보너스");
        File.WriteAllText(CreditWallet.SavePath, "손상된 데이터");
        Reload();
        Check(!CreditWallet.TryGetBalance(out balance), "손상된 저장 차단");
        Check(!CreditWallet.TryAwardClear("stage-d", "run-6", 3, settings, out var corrupt)
            && File.ReadAllText(CreditWallet.SavePath) == "손상된 데이터", "손상 파일 덮어쓰기 방지");
        Console.WriteLine("통과: " + checks + "개 검증");
    }
}
