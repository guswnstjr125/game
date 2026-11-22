using DarkLegion.Core;
using UnityEngine;

namespace DarkLegion.Data
{
    public sealed class CurseCardData : ScriptableObject
    {
        [SerializeField] private string curseId;
        [SerializeField] private string displayName;
        [SerializeField] private int cost;
        [SerializeField] private string effect;
        [SerializeField] private bool removable;
        [SerializeField] private string removeCost;
        [SerializeField] private double value1;
        [SerializeField] private bool hasDuration;
        [SerializeField] private int duration;
        [SerializeField] private string keyword;

        public string Id => curseId;
        public string DisplayName => displayName;
        public int Cost => cost;
        public string Effect => effect;
        public bool Removable => removable;
        public string RemoveCost => removeCost;
        public double Value1 => value1;
        public bool HasDuration => hasDuration;
        public int Duration => duration;
        public string Keyword => keyword;

        public void Initialize(CurseRecord record)
        {
            curseId = record.Id;
            displayName = record.Name;
            cost = record.Cost;
            effect = record.Effect;
            removable = record.Removable;
            removeCost = record.RemoveCost;
            value1 = record.Value1;
            hasDuration = record.HasDuration;
            duration = record.Duration;
            keyword = record.Keyword;
        }
    }
}
