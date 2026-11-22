using System;
using DarkLegion.Core;
using UnityEngine;

namespace DarkLegion.Data
{
    public sealed class MonsterData : ScriptableObject
    {
        [Serializable]
        public struct Effect
        {
            [SerializeField] private string slot;
            [SerializeField] private string trigger;
            [SerializeField] private string condition;
            [SerializeField] private string effect;
            [SerializeField] private string target;
            [SerializeField] private bool hasValue;
            [SerializeField] private double value;
            [SerializeField] private string valueText;
            [SerializeField] private bool hasDuration;
            [SerializeField] private int duration;
            [SerializeField] private string durationText;
            [SerializeField] private string limit;

            public string Slot => slot;
            public string Trigger => trigger;
            public string EffectId => effect;
            public string Target => target;
            public bool HasValue => hasValue;
            public double Value => value;
            public string ValueText => valueText;
            public bool HasDuration => hasDuration;
            public int Duration => duration;
            public string DurationText => durationText;
            public string Limit => limit;

            public Effect(MonsterEffectRecord record)
            {
                slot = record.Slot;
                trigger = record.Trigger;
                condition = record.Condition;
                effect = record.Effect;
                target = record.Target;
                hasValue = record.HasValue;
                value = record.Value;
                valueText = record.ValueText ?? "";
                hasDuration = record.HasDuration;
                duration = record.Duration;
                durationText = record.DurationText ?? "";
                limit = record.Limit;
            }
        }

        [SerializeField] private string monsterId;
        [SerializeField] private string displayName;
        [SerializeField] private string monsterClass;
        [SerializeField] private int level;
        [SerializeField] private string cardId;
        [SerializeField] private int hp;
        [SerializeField] private int atk;
        [SerializeField] private int def;
        [SerializeField] private int crystal;
        [SerializeField] private string onSummon;
        [SerializeField] private string passive;
        [SerializeField] private string onDeath;
        [SerializeField] private string onAttack;
        [SerializeField] private string special;
        [SerializeField] private Effect[] effects = Array.Empty<Effect>();

        public string Id => monsterId;
        public string DisplayName => displayName;
        public string MonsterClass => monsterClass;
        public int Level => level;
        public string CardId => cardId;
        public int Hp => hp;
        public int Atk => atk;
        public int Def => def;
        public int Crystal => crystal;
        public string OnDeath => onDeath;
        public string Special => special;
        public int EffectCount => effects.Length;
        public Effect EffectAt(int index) => effects[index];

        public void Initialize(MonsterRecord record)
        {
            monsterId = record.Id;
            displayName = record.Name;
            monsterClass = record.Class;
            level = record.Level;
            cardId = record.CardId;
            hp = record.Hp;
            atk = record.Atk;
            def = record.Def;
            crystal = record.Crystal;
            onSummon = record.OnSummon;
            passive = record.Passive;
            onDeath = record.OnDeath;
            onAttack = record.OnAttack;
            special = record.Special;
            effects = new Effect[record.Effects.Count];
            for (int i = 0; i < effects.Length; i++) effects[i] = new Effect(record.Effects[i]);
        }
    }
}
