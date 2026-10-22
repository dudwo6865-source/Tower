using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// 크레딧과 최초 별 달성 기록을 하나의 파일로 함께 저장한다.
public static class CreditWallet
{
    [Serializable]
    class StageRecord
    {
        public string stageId;
        public int bestStars;
        public string lastRunId;
        public long lastFirstBonus;
        public int lastRepeatReward;
    }

    [Serializable]
    class SaveData
    {
        public int version = 1;
        public long credits;
        public List<StageRecord> stages = new List<StageRecord>();
    }

    public sealed class ClearReward
    {
        public int Stars { get; internal set; }
        public long FirstBonus { get; internal set; }
        public int RepeatReward { get; internal set; }
        public long Total => FirstBonus + RepeatReward;
    }

    static SaveData data;
    public static event Action Changed;
    public static string LastError { get; private set; }
    public static string SavePath => Path.Combine(Application.persistentDataPath, "credit-progress.json");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        data = null;
        LastError = null;
        Changed = null;
    }

    public static bool TryGetBalance(out long balance)
    {
        bool loaded = EnsureLoaded();
        balance = loaded ? data.credits : 0;
        return loaded;
    }

    public static int GetBestStars(string stageId)
    {
        if (!EnsureLoaded()) return 0;
        return data.stages.Find(s => s.stageId == stageId)?.bestStars ?? 0;
    }

    static bool EnsureLoaded()
    {
        if (data != null) return true;
        try
        {
            if (!File.Exists(SavePath))
            {
                data = new SaveData();
                LastError = null;
                return true;
            }
            SaveData loaded = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
            if (loaded == null || loaded.version != 1 || loaded.credits < 0 || loaded.stages == null)
                throw new InvalidDataException("크레딧 저장 형식이 올바르지 않습니다.");
            var ids = new HashSet<string>();
            foreach (StageRecord stage in loaded.stages)
                if (stage == null || string.IsNullOrWhiteSpace(stage.stageId) || !ids.Add(stage.stageId)
                    || stage.bestStars < 1 || stage.bestStars > 3)
                    throw new InvalidDataException("스테이지 기록이 올바르지 않습니다.");
            data = loaded;
            LastError = null;
            return true;
        }
        catch (Exception exception)
        {
            return Fail(exception);
        }
    }

    public static bool TryAwardClear(string stageId, string runId, int stars,
        StageClearRewards settings, out ClearReward reward)
    {
        reward = null;
        if (string.IsNullOrWhiteSpace(stageId) || string.IsNullOrWhiteSpace(runId) || settings == null)
        {
            LastError = "스테이지 보상 설정 또는 저장 식별자가 없습니다.";
            return false;
        }
        if (!EnsureLoaded()) return false;
        stars = Mathf.Clamp(stars, 1, 3);
        try
        {
            // 저장 성공 전에는 메모리의 잔액과 기록도 변경하지 않는다.
            SaveData next = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));
            StageRecord stage = next.stages.Find(s => s.stageId == stageId);
            if (stage == null)
            {
                stage = new StageRecord { stageId = stageId };
                next.stages.Add(stage);
            }
            if (stage.lastRunId == runId)
            {
                reward = new ClearReward { Stars = stars, FirstBonus = stage.lastFirstBonus,
                    RepeatReward = stage.lastRepeatReward };
                return true;
            }
            long first = settings.GetFirstClearBonus(stage.bestStars, stars);
            int repeat = settings.GetRepeatReward(stars);
            next.credits = checked(next.credits + first + repeat);
            stage.bestStars = Math.Max(stage.bestStars, stars);
            stage.lastRunId = runId;
            stage.lastFirstBonus = first;
            stage.lastRepeatReward = repeat;

            Directory.CreateDirectory(Application.persistentDataPath);
            string temporary = SavePath + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(next, true));
            if (File.Exists(SavePath))
                File.Replace(temporary, SavePath, SavePath + ".bak");
            else
                File.Move(temporary, SavePath);
            data = next;
            LastError = null;
            reward = new ClearReward { Stars = stars, FirstBonus = first, RepeatReward = repeat };
        }
        catch (Exception exception)
        {
            return Fail(exception);
        }
        // 화면 갱신 실패가 저장 실패로 취급되지 않게 한다.
        try { Changed?.Invoke(); }
        catch (Exception exception) { Debug.LogException(exception); }
        return true;
    }

    static bool Fail(Exception exception)
    {
        LastError = "크레딧 기록을 저장하거나 불러오지 못했습니다.";
        Debug.LogError($"{LastError}\n{exception}");
        return false;
    }
}
