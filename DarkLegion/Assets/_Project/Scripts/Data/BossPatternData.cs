using System;
using DarkLegion.Core;
using UnityEngine;

namespace DarkLegion.Data
{
    public sealed class BossPatternData : ScriptableObject
    {
        [Serializable]
        public struct Phase
        {
            [SerializeField] private int phase;
            [SerializeField] private int hpPctMin;
            [SerializeField] private int hpPctMax;

            public int Index => phase;
            public int HpPctMin => hpPctMin;
            public int HpPctMax => hpPctMax;

            public Phase(BossPhaseRange range)
            {
                phase = range.Phase;
                hpPctMin = range.HpPctMin;
                hpPctMax = range.HpPctMax;
            }
        }

        [Serializable]
        public struct Step
        {
            [SerializeField] private int phase;
            [SerializeField] private int turnIndex;
            [SerializeField] private string action;
            [SerializeField] private int damage;
            [SerializeField] private string statusEffect;
            [SerializeField] private string targetRule;
            [SerializeField] private bool enqueueTwice;
            [SerializeField] private string note;
            [SerializeField] private bool onceOnPhaseEnter;
            [SerializeField] private int thresholdPct;
            [SerializeField] private string curseId;

            public int Phase => phase;
            public int TurnIndex => turnIndex;
            public string Action => action;
            public int Damage => damage;
            public string StatusEffect => statusEffect;
            public string TargetRule => targetRule;
            public bool EnqueueTwice => enqueueTwice;
            public string Note => note;
            public bool OnceOnPhaseEnter => onceOnPhaseEnter;
            public int ThresholdPct => thresholdPct;
            public string CurseId => curseId;

            public Step(BossStep source)
            {
                phase = source.Phase;
                turnIndex = source.TurnIndex;
                action = source.Action;
                damage = source.Damage;
                statusEffect = source.StatusEffect;
                targetRule = source.TargetRule;
                enqueueTwice = source.EnqueueTwice;
                note = source.Note;
                onceOnPhaseEnter = source.OnceOnPhaseEnter;
                thresholdPct = source.ThresholdPct;
                curseId = source.CurseId;
            }

            public BossStep ToStep() => new BossStep(phase, turnIndex, action, damage, statusEffect, targetRule, enqueueTwice, note, onceOnPhaseEnter, thresholdPct, curseId);
        }

        [SerializeField] private string bossId;
        [SerializeField] private string enemyId;
        [SerializeField] private string displayName;
        [SerializeField] private int floor;
        [SerializeField] private int hp;
        [SerializeField] private int atk;
        [SerializeField] private int def;
        [SerializeField] private int phaseCount;
        [SerializeField] private string bossRelicId;
        [SerializeField] private string cardRewardPool;
        [SerializeField] private bool hasGoldReward;
        [SerializeField] private int goldRewardMin;
        [SerializeField] private int goldRewardMax;
        [SerializeField] private Phase[] phases = Array.Empty<Phase>();
        [SerializeField] private Step[] steps = Array.Empty<Step>();

        public string Id => bossId;
        public string EnemyId => enemyId;
        public string DisplayName => displayName;
        public int Floor => floor;
        public int Hp => hp;
        public int Atk => atk;
        public int Def => def;
        public string BossRelicId => bossRelicId;
        public bool HasGoldReward => hasGoldReward;
        public int GoldRewardMin => goldRewardMin;
        public int GoldRewardMax => goldRewardMax;
        public int StepCount => steps.Length;
        public Step StepAt(int index) => steps[index];
        public Phase PhaseAt(int index) => phases[index];

        public void Initialize(BossPatternRecord record)
        {
            bossId = record.BossId;
            enemyId = record.EnemyId;
            displayName = record.Name;
            floor = record.Floor;
            hp = record.Hp;
            atk = record.Atk;
            def = record.Def;
            phaseCount = record.PhaseCount;
            bossRelicId = record.BossRelicId;
            cardRewardPool = record.CardRewardPool;
            hasGoldReward = record.HasGoldReward;
            goldRewardMin = record.GoldRewardMin;
            goldRewardMax = record.GoldRewardMax;
            phases = new Phase[record.Phases.Count];
            for (int i = 0; i < phases.Length; i++) phases[i] = new Phase(record.Phases[i]);
            steps = new Step[record.Steps.Count];
            for (int i = 0; i < steps.Length; i++) steps[i] = new Step(record.Steps[i]);
        }

        public void AddTo(BossPatternCatalog catalog)
        {
            var built = new BossStep[steps.Length];
            for (int i = 0; i < built.Length; i++) built[i] = steps[i].ToStep();
            catalog.Add(enemyId, built);
        }
    }
}
