using System;
using System.IO;
using System.Threading;
using ColdCaseOS.Data;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace ColdCaseOS.Core
{
    /// <summary>
    /// UniTask 기반 비동기 에피소드 JSON 로더 및 미디어 경로 해석기.
    /// 단일 씬에서 LoadScene 없이 에피소드 데이터를 핫스왑할 때 사용합니다.
    /// </summary>
    public static class EpisodeLoader
    {
        public static async UniTask<EpisodeData> LoadEpisodeAsync(string episodeId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(episodeId))
                throw new ArgumentException("episodeId는 비어 있을 수 없습니다.");

            string normalized = episodeId.ToUpperInvariant();
            string fileName = $"{normalized.ToLowerInvariant()}.json";

            string streamingPath = Path.Combine(Application.streamingAssetsPath, "Episodes", normalized, fileName);
            string assetsDataPath = Path.Combine(Application.dataPath, "Data", "Episodes", normalized, fileName);
            string projectDataPath = Path.Combine(Application.dataPath, "Project", "Data", "Episodes", normalized, fileName);

            string targetPath = File.Exists(streamingPath) ? streamingPath
                : File.Exists(assetsDataPath) ? assetsDataPath
                : projectDataPath;

            if (!File.Exists(targetPath))
            {
                Debug.LogError($"[EpisodeLoader] 에피소드 JSON을 찾을 수 없습니다: {targetPath}");
                return null;
            }

            string json = await UniTask.RunOnThreadPool(() => File.ReadAllText(targetPath), cancellationToken: ct);
            var episode = await UniTask.RunOnThreadPool(() => JsonConvert.DeserializeObject<EpisodeData>(json), cancellationToken: ct);
            episode?.ResolveAllTextTagIndices();
            return episode;
        }

        public static string GetMediaFullPath(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return string.Empty;
            string streamingPath = Path.Combine(Application.streamingAssetsPath, "Episodes", relativePath);
            if (File.Exists(streamingPath)) return streamingPath;

            string assetsDataPath = Path.Combine(Application.dataPath, "Data", "Episodes", relativePath);
            if (File.Exists(assetsDataPath)) return assetsDataPath;

            return Path.Combine(Application.dataPath, "Project", "Data", "Episodes", relativePath);
        }

        public static async UniTask<AudioClip> LoadAudioClipAsync(string relativePath, CancellationToken ct = default)
        {
            string fullPath = GetMediaFullPath(relativePath);
            if (!File.Exists(fullPath))
            {
                Debug.LogWarning($"[EpisodeLoader] 오디오 파일 없음: {fullPath}");
                return null;
            }

            string ext = Path.GetExtension(fullPath).ToLowerInvariant();
            AudioType audioType = ext switch
            {
                ".ogg" => AudioType.OGGVORBIS,
                ".mp3" => AudioType.MPEG,
                _ => AudioType.WAV
            };

            string uri = new Uri(fullPath).AbsoluteUri;
            using var req = UnityWebRequestMultimedia.GetAudioClip(uri, audioType);
            await req.SendWebRequest().WithCancellation(ct);

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[EpisodeLoader] 오디오 로드 실패 ({fullPath}): {req.error}");
                return null;
            }

            return DownloadHandlerAudioClip.GetContent(req);
        }

        public static async UniTask<Texture2D> LoadTextureAsync(string relativePath, CancellationToken ct = default)
        {
            string fullPath = GetMediaFullPath(relativePath);
            if (!File.Exists(fullPath))
            {
                Debug.LogWarning($"[EpisodeLoader] 이미지 파일 없음: {fullPath}");
                return null;
            }

            byte[] bytes = await UniTask.RunOnThreadPool(() => File.ReadAllBytes(fullPath), cancellationToken: ct);
            await UniTask.SwitchToMainThread(ct);

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            return tex.LoadImage(bytes) ? tex : null;
        }
    }
}
