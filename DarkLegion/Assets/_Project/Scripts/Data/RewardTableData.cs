using System.Collections.Generic;
using DarkLegion.Core;
using UnityEngine;

namespace DarkLegion.Data
{
    public sealed class RewardTableData : ScriptableObject
    {
        [SerializeField] private string battleType;
        [SerializeField] private int floor;
        [SerializeField] private int goldMin;
        [SerializeField] private int goldMax;
        [SerializeField] private double common;
        [SerializeField] private double uncommon;
        [SerializeField] private double rare;
        [SerializeField] private double legendary;

        public string BattleType => battleType;
        public int Floor => floor;
        public int GoldMin => goldMin;
        public int GoldMax => goldMax;
        public double Common => common;
        public double Uncommon => uncommon;
        public double Rare => rare;
        public double Legendary => legendary;

        public void Initialize(RewardBattleRecord record)
        {
            battleType = record.BattleType;
            floor = record.Floor;
            goldMin = record.GoldMin;
            goldMax = record.GoldMax;
            common = record.Common;
            uncommon = record.Uncommon;
            rare = record.Rare;
            legendary = record.Legendary;
        }

        public void AddTo(RewardTables tables)
        {
            tables.AddRow(new RewardRow
            {
                BattleType = battleType, Floor = floor, GoldMin = goldMin, GoldMax = goldMax,
                Rarity = new Dictionary<string, double> { ["C"] = common, ["U"] = uncommon, ["R"] = rare, ["L"] = legendary }
            });
        }
    }
}
