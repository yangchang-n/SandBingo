// Steamworks.NET 과 같은 조건으로 Steam 코드를 끈다
#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || UNITY_ANDROID || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

public enum Achievement
{
    Stage1Clear,
    Stage2Clear,
    Stage3Clear,
    CustomStagePlayed,
    VersusPlayed
}

// Steam 도전과제 해금
// Steam 이 연결되지 않았거나 Steam 코드가 빠진 빌드에서는 아무것도 하지 않으므로 호출하는 쪽에서 따로 확인할 필요 없음
// 이미 해금된 도전과제를 다시 해금해도 Steam 이 무시하므로 중복 확인도 하지 않음
public static class SteamAchievements
{
    public static void Unlock(Achievement achievement)
    {
#if !DISABLESTEAMWORKS
        if (!SteamManager.IsInitialized) return;

        string apiName = GetApiName(achievement);

        // 실패하는 경우는 대부분 Steamworks 에 등록한 API 이름과 코드의 이름이 다를 때
        if (!SteamUserStats.SetAchievement(apiName))
        {
            Debug.LogWarning($"[Steam] Failed to unlock achievement: {apiName}");
            return;
        }

        SteamUserStats.StoreStats();
        Debug.Log($"[Steam] Achievement unlocked: {apiName}");
#endif
    }

    // 스테이지 1, 2, 3 외의 번호(커스텀 0 등)는 무시
    public static void UnlockStageClear(int stageNumber)
    {
        switch (stageNumber)
        {
            case 1: Unlock(Achievement.Stage1Clear); break;
            case 2: Unlock(Achievement.Stage2Clear); break;
            case 3: Unlock(Achievement.Stage3Clear); break;
        }
    }

#if !DISABLESTEAMWORKS
    // Steamworks 에 등록한 API 이름과 정확히 같아야 한다
    static string GetApiName(Achievement achievement)
    {
        switch (achievement)
        {
            case Achievement.Stage1Clear:       return "STAGE_1_CLEAR";
            case Achievement.Stage2Clear:       return "STAGE_2_CLEAR";
            case Achievement.Stage3Clear:       return "STAGE_3_CLEAR";
            case Achievement.CustomStagePlayed: return "CUSTOM_STAGE_PLAYED";
            case Achievement.VersusPlayed:      return "VERSUS_PLAYED";
        }
        return "";
    }
#endif
}
