using DarkLegion.Core;
using UnityEngine;

namespace DarkLegion.Data
{
    public sealed class StatusData : ScriptableObject
    {
        [SerializeField] private string statusId;
        [SerializeField] private string displayName;
        [SerializeField] private string stackType;
        [SerializeField] private string tickTiming;
        [SerializeField] private string effect;
        [SerializeField] private bool hasMaxStack;
        [SerializeField] private int maxStack;
        [SerializeField] private bool hasDuration;
        [SerializeField] private int durationTurns;
        [SerializeField] private bool durationUnspecified;

        public string Id => statusId;
        public string DisplayName => displayName;
        public string StackType => stackType;
        public string TickTiming => tickTiming;
        public bool HasMaxStack => hasMaxStack;
        public int MaxStack => maxStack;
        public bool HasDuration => hasDuration;
        public int DurationTurns => durationTurns;
        public bool DurationUnspecified => durationUnspecified;

        public void Initialize(StatusRecord record)
        {
            statusId = record.Id;
            displayName = record.Name;
            stackType = record.StackType;
            tickTiming = record.TickTiming;
            effect = record.Effect;
            hasMaxStack = record.HasMaxStack;
            maxStack = record.MaxStack;
            hasDuration = record.HasDuration;
            durationTurns = record.DurationTurns;
            durationUnspecified = record.DurationUnspecified;
        }
    }
}
