using System;
using System.IO;
using Newtonsoft.Json;

namespace LOSTARK_GEM_FINDER.Services
{
    // 원자적 저장 + 손상 파일 보관을 공통으로 처리하는 도우미
    public static class JsonFileStore
    {
        private const string BACKUP_EXTENSION = ".bak";
        private const string TEMP_EXTENSION = ".tmp";
        private const string CORRUPT_SUFFIX_FORMAT = "yyyyMMdd_HHmmss";

        public static string GetBackupPath(string path) => path + BACKUP_EXTENSION;

        public static T Load<T>(string path, out bool isCorrupted) where T : class
        {
            isCorrupted = false;
            if (!File.Exists(path)) return null;

            try
            {
                var data = JsonConvert.DeserializeObject<T>(File.ReadAllText(path));
                if (data == null) isCorrupted = true;
                return data;
            }
            catch (Exception)
            {
                isCorrupted = true;
                return null;
            }
        }

        // 🌟 임시 파일에 다 쓴 뒤 교체 → 쓰는 도중 종료돼도 원본이 깨지지 않음
        public static void Save<T>(string path, T data)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            string tempPath = path + TEMP_EXTENSION;
            File.WriteAllText(tempPath, JsonConvert.SerializeObject(data, Formatting.Indented));

            if (!File.Exists(path))
            {
                File.Move(tempPath, path);
                return;
            }

            try
            {
                File.Replace(tempPath, path, GetBackupPath(path));
            }
            catch (IOException)
            {
                File.Copy(tempPath, path, true);
                File.Delete(tempPath);
            }
        }

        // 🌟 손상 파일을 옮겨서 보관 → 본 파일 자리가 비어 다음 저장 때 .bak이 덮어써지지 않음
        public static string PreserveCorrupt(string path)
        {
            string corruptPath = $"{path}.corrupt_{DateTime.Now.ToString(CORRUPT_SUFFIX_FORMAT)}";
            try
            {
                File.Move(path, corruptPath);
                return corruptPath;
            }
            catch (Exception ex)
            {
                return $"(보관 실패: {ex.Message})";
            }
        }
    }
}