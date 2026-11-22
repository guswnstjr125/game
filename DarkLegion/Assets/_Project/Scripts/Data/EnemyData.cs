using System;
using System.Collections.Generic;
using System.Linq;
using DarkLegion.Core;
using UnityEngine;

namespace DarkLegion.Data
{
    public sealed class EnemyData : ScriptableObject
    {
        [Serializable]
        private struct EnemyAction
        {
            public string id;
            public string condition;
            public double chance;
            public string type;
            public double value;
            public string status;
            public int duration;
            public string target;
            public string spawnId;
            public double drainRatio;
            public string durationSpec;

            public EnemyAction(EnemyActionDef definition, string durationSpec)
            {
                id = definition.ActionId;
                condition = definition.Condition;
                chance = definition.Chance;
                type = definition.ActionType;
                value = definition.Value;
                status = definition.Status;
                duration = definition.Duration;
                target = definition.Target;
                spawnId = definition.SpawnId;
                drainRatio = definition.DrainRatio;
                this.durationSpec = durationSpec;
            }

            public EnemyActionDef ToDefinition() => new EnemyActionDef
            {
                ActionId = id, Condition = condition, Chance = chance, ActionType = type,
                Value = value, Status = status, Duration = duration, Target = target,
                SpawnId = spawnId, DrainRatio = drainRatio
            };
        }

        [SerializeField] private string enemyId;
        [SerializeField] private string displayName;
        [SerializeField] private string category;
        [SerializeField] private string role;
        [SerializeField] private string ai;
        [SerializeField] private int hp;
        [SerializeField] private int atk;
        [SerializeField] private int def;
        [SerializeField] private string drop;
        [SerializeField] private EnemyAction[] actions = Array.Empty<EnemyAction>();

        public string Id => enemyId;
        public string DisplayName => displayName;
        public string Category => category;
        public string Role => role;
        public string Ai => ai;
        public string Drop => drop;
        public string DurationSpecAt(int actionIndex) => actions[actionIndex].durationSpec;

        public void Initialize(DataRow row, EnemyKitCatalog catalog, IReadOnlyList<DataRow> actionRows)
        {
            enemyId = row.Str("ENE_ID");
            displayName = row.Str("Name_KO");
            category = row.Str("Cat");
            role = row.Str("Role");
            ai = row.Str("AI");
            drop = row.Str("Drop");
            (hp, atk, def) = catalog.SpawnStats(enemyId).Value;
            var kit = catalog.Get(enemyId);
            actions = kit == null ? Array.Empty<EnemyAction>() :
                kit.Select((action, index) => new EnemyAction(action, actionRows[index].Str("Duration"))).ToArray();
        }

        public void AddTo(EnemyKitCatalog catalog)
        {
            catalog.SetSpawnStats(enemyId, hp, atk, def);
            if (actions.Length != 0)
                catalog.Add(enemyId, actions.Select(action => action.ToDefinition()).ToArray());
        }
    }
}
