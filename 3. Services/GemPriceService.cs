using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LOSTARK_GEM_FINDER.Core;
using LOSTARK_GEM_FINDER.Models;

namespace LOSTARK_GEM_FINDER.Services
{
    public class GemPriceService : IGemPriceService
    {
        private static readonly IReadOnlyDictionary<string, long> EmptyHistory = new Dictionary<string, long>();

        private readonly IAuctionAPI _api;
        private readonly IGemDataRepository _db;
        private readonly Dictionary<string, GemQuote> _quotes = new Dictionary<string, GemQuote>();

        public event Action OnPricesUpdated;

        public IReadOnlyDictionary<string, GemQuote> Quotes => _quotes;
        public IReadOnlyList<PriceAlarm> Alarms => _db.Data.Alarms;
        public DateTime? LastUpdated { get; private set; }

        public GemPriceService(IAuctionAPI api, IGemDataRepository db)
        {
            _api = api;
            _db = db;

            foreach (var type in AppConstants.GemTypes)
            {
                for (int level = AppConstants.MIN_GEM_LEVEL; level <= AppConstants.MAX_GEM_LEVEL; level++)
                    _quotes[GemKeys.Make(type.ShortName, level)] = new GemQuote { TypeName = type.ShortName, Level = level };
            }
        }

        public async Task<List<PriceAlarm>> RefreshAsync()
        {
            Dictionary<string, long?> newPrices;

            // 🌟 동시에 보내는 요청 수를 제한해서 호출 한도를 보호
            using (var limiter = new SemaphoreSlim(AppConstants.MAX_CONCURRENT_REQUESTS))
            {
                var tasks = new List<Task<KeyValuePair<string, long?>>>();
                foreach (var type in AppConstants.GemTypes)
                {
                    for (int level = AppConstants.MIN_GEM_LEVEL; level <= AppConstants.MAX_GEM_LEVEL; level++)
                        tasks.Add(FetchAsync(type, level, limiter));
                }

                // 하나라도 실패하면 여기서 예외 → 아래 반영 단계로 가지 않음 (부분 갱신 방지)
                var results = await Task.WhenAll(tasks);
                newPrices = results.ToDictionary(r => r.Key, r => r.Value);
            }

            ApplyPrices(newPrices);
            var newlyTriggered = EvaluateAlarms();
            LastUpdated = DateTime.Now;

            try
            {
                _db.Save();
            }
            finally
            {
                OnPricesUpdated?.Invoke();
            }
            return newlyTriggered;
        }

        private async Task<KeyValuePair<string, long?>> FetchAsync(GemType type, int level, SemaphoreSlim limiter)
        {
            await limiter.WaitAsync();
            try
            {
                long? price = await _api.GetLowestBuyPriceAsync(type.GetItemName(level), type.Tier);
                return new KeyValuePair<string, long?>(GemKeys.Make(type.ShortName, level), price);
            }
            finally
            {
                limiter.Release();
            }
        }

        private void ApplyPrices(Dictionary<string, long?> newPrices)
        {
            string today = DateTime.Now.ToString(AppConstants.HISTORY_DATE_FORMAT, CultureInfo.InvariantCulture);

            foreach (var kvp in newPrices)
            {
                var quote = _quotes[kvp.Key];

                // 첫 갱신이면 지난 실행 때 저장한 가격과 비교
                long? previous = quote.Price;
                if (!previous.HasValue && _db.Data.LastPrices.TryGetValue(kvp.Key, out long saved)) previous = saved;

                quote.PreviousPrice = previous;
                quote.Price = kvp.Value;

                if (!kvp.Value.HasValue) continue;
                long price = kvp.Value.Value;

                _db.Data.LastPrices[kvp.Key] = price;
                RecordDailyLowest(kvp.Key, today, price);
            }
        }

        // 하루에 여러 번 갱신해도 그날의 최저가 하나만 기록
        private void RecordDailyLowest(string gemKey, string day, long price)
        {
            if (!_db.Data.History.TryGetValue(gemKey, out SortedDictionary<string, long> days))
            {
                days = new SortedDictionary<string, long>(StringComparer.Ordinal);
                _db.Data.History[gemKey] = days;
            }

            if (!days.TryGetValue(day, out long recorded) || price < recorded) days[day] = price;
        }

        private List<PriceAlarm> EvaluateAlarms()
        {
            var newlyTriggered = new List<PriceAlarm>();
            foreach (var alarm in _db.Data.Alarms)
            {
                if (UpdateAlarmState(alarm)) newlyTriggered.Add(alarm);
            }
            return newlyTriggered;
        }

        // 🌟 목표가 이하로 "새로" 내려왔을 때만 true → 같은 알림이 5분마다 반복되지 않음
        //    가격이 목표가 위로 다시 오르면 해제돼서 다음 하락 때 또 알려줌
        private bool UpdateAlarmState(PriceAlarm alarm)
        {
            if (!_quotes.TryGetValue(alarm.GemKey, out GemQuote quote) || !quote.Price.HasValue) return false;

            bool isReached = quote.Price.Value <= alarm.TargetPrice;
            bool isNew = isReached && !alarm.IsTriggered;
            alarm.IsTriggered = isReached;
            return isNew;
        }

        // 같은 보석에 알림이 이미 있으면 목표가만 변경
        public PriceAlarm SetAlarm(GemSelection gem, long targetPrice)
        {
            var alarm = _db.Data.Alarms.FirstOrDefault(a => a.GemKey == gem.Key);
            if (alarm == null)
            {
                alarm = new PriceAlarm { TypeName = gem.TypeName, Level = gem.Level };
                _db.Data.Alarms.Add(alarm);
            }

            alarm.TargetPrice = targetPrice;
            alarm.IsTriggered = false;
            UpdateAlarmState(alarm);   // 이미 목표가 이하라면 표시만 하고 풍선 알림은 생략

            _db.Save();
            return alarm;
        }

        public void RemoveAlarm(PriceAlarm alarm)
        {
            if (_db.Data.Alarms.Remove(alarm)) _db.Save();
        }

        public IReadOnlyDictionary<string, long> GetHistory(string gemKey)
            => _db.Data.History.TryGetValue(gemKey, out SortedDictionary<string, long> days) ? (IReadOnlyDictionary<string, long>)days : EmptyHistory;
    }
}