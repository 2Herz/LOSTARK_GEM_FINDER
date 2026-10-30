using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using LOSTARK_GEM_FINDER.Core;

namespace LOSTARK_GEM_FINDER.Services
{
    public class AuctionAPI : IAuctionAPI
    {
        private const string AUCTION_SEARCH_PATH = "auctions/items";
        private const string SORT_BY_BUY_PRICE = "BUY_PRICE";
        private const string SORT_ASCENDING = "ASC";
        private const int FIRST_PAGE = 1;
        private const int HTTP_TOO_MANY_REQUESTS = 429;

        private readonly HttpClient _client;

        public AuctionAPI(string apiKey)
        {
            _client = new HttpClient
            {
                BaseAddress = new Uri(AppConstants.API_BASE_URL + "/"),
                Timeout = TimeSpan.FromSeconds(AppConstants.API_TIMEOUT_SECONDS)
            };
            SetApiKey(apiKey);
        }

        // 요청이 진행 중이지 않을 때만 호출됨 (MainForm에서 보장)
        public void SetApiKey(string apiKey)
        {
            _client.DefaultRequestHeaders.Clear();
            _client.DefaultRequestHeaders.Add("accept", "application/json");
            _client.DefaultRequestHeaders.Add("authorization", "Bearer " + apiKey);
        }

        public async Task<long?> GetLowestBuyPriceAsync(string itemName, int itemTier)
        {
            int lastPage = FIRST_PAGE + AppConstants.MAX_SEARCH_PAGES - 1;

            for (int page = FIRST_PAGE; page <= lastPage; page++)
            {
                JObject json = await SearchAsync(itemName, itemTier, page);
                var items = json["Items"] as JArray;
                if (items == null || items.Count == 0) return null;

                // 🌟 "1레벨 겁화의 보석" 검색에 "10레벨"도 섞여 나오므로 이름이 정확히 같은 매물만 사용
                //    즉시 구매가가 없는 입찰 전용 매물도 제외
                long? lowest = items
                    .Where(i => (string)i["Name"] == itemName)
                    .Select(i => i["AuctionInfo"]?["BuyPrice"]?.Value<long?>())
                    .Where(p => p.HasValue && p.Value > 0)
                    .Min();

                if (lowest.HasValue) return lowest;

                int pageSize = json["PageSize"]?.Value<int?>() ?? 0;
                if (pageSize > 0 && items.Count < pageSize) return null;   // 마지막 페이지
            }
            return null;
        }

        private async Task<JObject> SearchAsync(string itemName, int itemTier, int page)
        {
            var payload = new JObject
            {
                { "CategoryCode", AppConstants.GEM_CATEGORY_CODE },
                { "ItemTier", itemTier },
                { "ItemName", itemName },
                { "Sort", SORT_BY_BUY_PRICE },
                { "SortCondition", SORT_ASCENDING },
                { "PageNo", page }
            };

            using (var content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json"))
            using (var res = await SendAsync(() => _client.PostAsync(AUCTION_SEARCH_PATH, content)))
            {
                EnsureSuccess(res);
                return JObject.Parse(await res.Content.ReadAsStringAsync());
            }
        }

        private static async Task<HttpResponseMessage> SendAsync(Func<Task<HttpResponseMessage>> request)
        {
            try
            {
                return await request();
            }
            catch (TaskCanceledException)
            {
                throw new TimeoutException("API 응답 시간이 초과되었습니다.");
            }
        }

        private static void EnsureSuccess(HttpResponseMessage res)
        {
            if (res.IsSuccessStatusCode) return;
            if (res.StatusCode == HttpStatusCode.Unauthorized) throw new ApiKeyInvalidException();
            if (res.StatusCode == HttpStatusCode.ServiceUnavailable) throw new ApiMaintenanceException();
            if ((int)res.StatusCode == HTTP_TOO_MANY_REQUESTS) throw new ApiRateLimitException();
            throw new Exception($"경매장 API 요청 실패 (HTTP {(int)res.StatusCode} {res.ReasonPhrase})");
        }
    }
}