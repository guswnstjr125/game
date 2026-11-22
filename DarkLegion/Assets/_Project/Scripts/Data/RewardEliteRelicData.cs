using System.Collections.Generic;
using DarkLegion.Core;
using UnityEngine;

namespace DarkLegion.Data
{
    public sealed class RewardEliteRelicData : ScriptableObject
    {
        [SerializeField] private int floor;
        [SerializeField] private string poolA;
        [SerializeField] private string poolB;
        [SerializeField] private string poolC;
        [SerializeField] private string rarityRule;

        public int Floor => floor;
        public string PoolA => poolA;
        public string PoolB => poolB;
        public string PoolC => poolC;
        public string RarityRule => rarityRule;

        public void Initialize(EliteRelicRecord record)
        {
            floor = record.Floor;
            poolA = record.PoolA;
            poolB = record.PoolB;
            poolC = record.PoolC;
            rarityRule = record.RarityRule;
        }

        public void AddTo(RewardTables tables)
        {
            var pool = new List<string>();
            if (!string.IsNullOrEmpty(poolA)) pool.Add(poolA);
            if (!string.IsNullOrEmpty(poolB)) pool.Add(poolB);
            if (!string.IsNullOrEmpty(poolC)) pool.Add(poolC);
            tables.SetEliteRelicPool(floor, pool);
        }
    }
}
