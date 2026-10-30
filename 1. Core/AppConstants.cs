using System;
using System.IO;
using LOSTARK_GEM_FINDER.Models;

namespace LOSTARK_GEM_FINDER.Core
{
    public static class AppConstants
    {
        public const string APP_NAME = "보석 최저가 검색기";

        // 🌟 exe 폴더가 아닌 사용자 AppData에 저장 → 새 버전을 다른 폴더에 풀어도 데이터가 그대로 유지됨
        public static readonly string DataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), APP_NAME);
        public static readonly string DataFilePath = Path.Combine(DataDirectory, "GemData.json");
        public static readonly string KeyFilePath = Path.Combine(DataDirectory, "ApiKey.dat");

        public const string API_BASE_URL = "https://developer-lostark.game.onstove.com";
        public const string API_KEY_PAGE_URL = "https://developer-lostark.game.onstove.com/";

        // 경매장 검색 조건
        public const int GEM_CATEGORY_CODE = 210000;
        public const int MIN_GEM_LEVEL = 7;
        public const int MAX_GEM_LEVEL = 10;
        public const string GEM_NAME_FORMAT = "{0}레벨 {1}";   // 예: 10레벨 겁화의 보석

        // 🌟 보석 종류는 여기만 고치면 표·알림·그래프에 모두 반영됨
        private const int TIER_4 = 4;
        public static readonly GemType[] GemTypes =
        {
            new GemType("겁화", "겁화의 보석", TIER_4),
            new GemType("작열", "작열의 보석", TIER_4)
        };

        // 통신
        public const int API_TIMEOUT_SECONDS = 15;
        public const int MAX_CONCURRENT_REQUESTS = 3;   // 동시에 보내는 요청 수 (호출 한도 보호)
        public const int MAX_SEARCH_PAGES = 3;          // 정확한 이름의 매물을 찾을 때까지 넘겨볼 최대 페이지

        // 자동 갱신 / 알림
        public const int MS_PER_MINUTE = 60 * 1000;
        public const int AUTO_REFRESH_MINUTES = 1;
        public const int AUTO_REFRESH_INTERVAL_MS = AUTO_REFRESH_MINUTES * MS_PER_MINUTE;
        public const int BALLOON_TIP_MS = 3000;

        public const string HISTORY_DATE_FORMAT = "yyyy-MM-dd";
    }
}