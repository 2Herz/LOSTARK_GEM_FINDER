using LOSTARK_GEM_FINDER.Core;
using LOSTARK_GEM_FINDER.Models;

namespace LOSTARK_GEM_FINDER.Services
{
    public class GemDataRepository : IGemDataRepository
    {
        public GemData Data { get; private set; }
        public string LoadWarning { get; private set; }

        public GemDataRepository()
        {
            string path = AppConstants.DataFilePath;
            Data = JsonFileStore.Load<GemData>(path, out bool isCorrupted);

            if (isCorrupted)
            {
                string corruptPath = JsonFileStore.PreserveCorrupt(path);
                Data = JsonFileStore.Load<GemData>(JsonFileStore.GetBackupPath(path), out _);
                LoadWarning = Data != null
                    ? $"저장 파일이 손상되어 백업에서 복구했습니다.\n손상된 원본: {corruptPath}"
                    : $"저장 파일이 손상되어 새로 시작합니다.\n손상된 원본: {corruptPath}";
            }

            if (Data == null) Data = new GemData();
        }

        public void Save() => JsonFileStore.Save(AppConstants.DataFilePath, Data);
    }
}