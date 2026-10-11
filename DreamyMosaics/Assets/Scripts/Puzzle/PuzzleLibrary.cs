using UnityEngine;

namespace DreamyMosaics.Puzzle
{
    public static class PuzzleLibrary
    {
        public static LevelData[] Load()
        {
            TextAsset asset = Resources.Load<TextAsset>("Puzzles/campaign");
            if (asset == null || string.IsNullOrEmpty(asset.text))
                return null;
            CampaignFile file = JsonUtility.FromJson<CampaignFile>(asset.text);
            if (file == null)
                return null;
            return file.levels;
        }
    }

    public static class ProgressionStore
    {
        const string UnlockKey = "dm.v2.unlock";

        public static int UnlockedCount(int levelCount)
        {
            int unlocked = PlayerPrefs.GetInt(UnlockKey, 1);
            if (unlocked < 1)
                unlocked = 1;
            if (unlocked > levelCount)
                unlocked = levelCount;
            return unlocked;
        }

        public static int Stars(int index)
        {
            return PlayerPrefs.GetInt(StarKey(index), 0);
        }

        public static void NoteWin(int index, int levelCount, int stars, float seconds)
        {
            int unlocked = UnlockedCount(levelCount);
            if (index + 1 >= unlocked && index + 1 < levelCount)
                PlayerPrefs.SetInt(UnlockKey, index + 2);
            if (stars > Stars(index))
                PlayerPrefs.SetInt(StarKey(index), stars);
            float previous = PlayerPrefs.GetFloat(TimeKey(index), -1f);
            if (previous < 0f || seconds < previous)
                PlayerPrefs.SetFloat(TimeKey(index), seconds);
            PlayerPrefs.Save();
        }

        public static int Rate(int mistakes, int hints)
        {
            if (mistakes <= 0 && hints <= 0)
                return 3;
            if (mistakes <= 1 && hints <= 1)
                return 2;
            return 1;
        }

        static string StarKey(int index)
        {
            return "dm.v2.stars." + index;
        }

        static string TimeKey(int index)
        {
            return "dm.v2.time." + index;
        }
    }
}
