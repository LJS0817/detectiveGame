using System;
using System.IO;
using ColdCaseOS.Data;
using Newtonsoft.Json;
using UnityEngine;

namespace ColdCaseOS.Core
{
    /// <summary>
    /// Application.persistentDataPath/save_data.json 기반 자동 저장 및 로드 매니저.
    /// </summary>
    public static class SaveManager
    {
        private const string SaveFileName = "save_data.json";
        private static string SaveFilePath => Path.Combine(Application.persistentDataPath, SaveFileName);

        private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore
        };

        public static bool HasSaveFile() => File.Exists(SaveFilePath);

        public static void Save(CampaignSaveData data)
        {
            if (data == null) return;
            try
            {
                string json = JsonConvert.SerializeObject(data, SerializerSettings);
                string tempPath = SaveFilePath + ".tmp";
                File.WriteAllText(tempPath, json);
                if (File.Exists(SaveFilePath)) File.Delete(SaveFilePath);
                File.Move(tempPath, SaveFilePath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveManager] 저장 실패: {ex.Message}");
            }
        }

        public static CampaignSaveData LoadOrCreate()
        {
            if (!File.Exists(SaveFilePath))
            {
                return new CampaignSaveData();
            }

            try
            {
                string json = File.ReadAllText(SaveFilePath);
                var data = JsonConvert.DeserializeObject<CampaignSaveData>(json, SerializerSettings);
                return data ?? new CampaignSaveData();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveManager] 로드 실패, 신규 세이브 생성: {ex.Message}");
                return new CampaignSaveData();
            }
        }

        public static void DeleteSave()
        {
            if (File.Exists(SaveFilePath)) File.Delete(SaveFilePath);
        }
    }
}
