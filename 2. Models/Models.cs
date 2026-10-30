using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using LOSTARK_GEM_FINDER.Core;

namespace LOSTARK_GEM_FINDER.Models
{
    public static class GemKeys
    {
        public static string Make(string typeName, int level) => $"{typeName}_{level}";
    }

    public class GemType
    {
        public string ShortName { get; }    // 겁화
        public string ItemSuffix { get; }   // 겁화의 보석
        public int Tier { get; }

        public GemType(string shortName, string itemSuffix, int tier)
        {
            ShortName = shortName;
            ItemSuffix = itemSuffix;
            Tier = tier;
        }

        public string GetItemName(int level) => string.Format(AppConstants.GEM_NAME_FORMAT, level, ItemSuffix);
    }

    // 콤보박스 표시용 (보석 종류 + 레벨)
    public class GemSelection
    {
        public string TypeName { get; }
        public int Level { get; }
        public string Key => GemKeys.Make(TypeName, Level);

        public GemSelection(string typeName, int level)
        {
            TypeName = typeName;
            Level = level;
        }

        public override string ToString() => $"{Level}레벨 {TypeName}";
    }

    public class GemQuote
    {
        public string TypeName { get; set; }
        public int Level { get; set; }
        public long? Price { get; set; }           // null = 즉시 구매 가능한 매물 없음
        public long? PreviousPrice { get; set; }

        public long? Diff => Price.HasValue && PreviousPrice.HasValue ? (long?)(Price.Value - PreviousPrice.Value) : null;
    }

    public class PriceAlarm
    {
        public string TypeName { get; set; }
        public int Level { get; set; }
        public long TargetPrice { get; set; }
        public bool IsTriggered { get; set; }      // 도달 알림을 이미 보냈는지 (가격이 다시 오르면 해제)

        [JsonIgnore] public string GemKey => GemKeys.Make(TypeName, Level);
        [JsonIgnore] public string DisplayName => $"{Level}레벨 {TypeName}";
    }

    public class GemData
    {
        public List<PriceAlarm> Alarms { get; set; } = new List<PriceAlarm>();

        // 보석 키 → 마지막 최저가 (재시작 후에도 등락 비교용)
        public Dictionary<string, long> LastPrices { get; set; } = new Dictionary<string, long>();

        // 보석 키 → (날짜 → 그날 관측된 최저가)
        public Dictionary<string, SortedDictionary<string, long>> History { get; set; } = new Dictionary<string, SortedDictionary<string, long>>();

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            if (Alarms == null) Alarms = new List<PriceAlarm>();
            if (LastPrices == null) LastPrices = new Dictionary<string, long>();
            if (History == null) History = new Dictionary<string, SortedDictionary<string, long>>();

            Alarms.RemoveAll(a => a == null || string.IsNullOrEmpty(a.TypeName));

            // 날짜 문자열은 Ordinal 비교가 곧 날짜 순서
            foreach (var key in History.Keys.ToList())
            {
                var days = History[key];
                History[key] = days == null
                    ? new SortedDictionary<string, long>(StringComparer.Ordinal)
                    : new SortedDictionary<string, long>(days, StringComparer.Ordinal);
            }
        }
    }
}