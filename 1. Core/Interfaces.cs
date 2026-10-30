using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LOSTARK_GEM_FINDER.Models;

namespace LOSTARK_GEM_FINDER.Core
{
    public interface IAPIKeyStore
    {
        string Load();
        void Save(string apiKey);
    }

    public interface IGemDataRepository
    {
        GemData Data { get; }
        string LoadWarning { get; }   // 손상 복구 안내 (없으면 null)
        void Save();
    }

    public interface IAuctionAPI
    {
        void SetApiKey(string apiKey);

        // 즉시 구매가 기준 최저가. 매물이 없으면 null
        Task<long?> GetLowestBuyPriceAsync(string itemName, int itemTier);
    }

    public interface IGemPriceService
    {
        event Action OnPricesUpdated;

        IReadOnlyDictionary<string, GemQuote> Quotes { get; }
        IReadOnlyList<PriceAlarm> Alarms { get; }
        DateTime? LastUpdated { get; }

        // 전체 시세 갱신 후, 이번에 새로 목표가에 도달한 알림 목록을 반환
        Task<List<PriceAlarm>> RefreshAsync();

        PriceAlarm SetAlarm(GemSelection gem, long targetPrice);
        void RemoveAlarm(PriceAlarm alarm);

        IReadOnlyDictionary<string, long> GetHistory(string gemKey);
    }
}