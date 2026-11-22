using System;
using System.Linq;
using DarkLegion.Core;
using UnityEngine;

namespace DarkLegion.Data
{
    public sealed class CardData : ScriptableObject
    {
        [Serializable]
        private struct Parameter
        {
            public string Name;
            public double Value;
        }

        [Serializable]
        private struct OptionalInt
        {
            public bool HasValue;
            public int Value;

            public OptionalInt(int? value)
            {
                HasValue = value.HasValue;
                Value = value.GetValueOrDefault();
            }

            public int? Read() => HasValue ? Value : (int?)null;
        }

        [SerializeField] private string cardId;
        [SerializeField] private string displayName;
        [SerializeField] private string cardClass;
        [SerializeField] private string rarity;
        [SerializeField] private string keyword;
        [SerializeField] private string description;
        [SerializeField] private string upgradedDescription;
        [SerializeField] private OptionalInt unlockFloor;
        [SerializeField] private OptionalInt classUnlockFloor;
        [SerializeField] private bool normalDrop;
        [SerializeField] private bool eliteDrop;
        [SerializeField] private bool bossDrop;
        [SerializeField] private CardType type;
        [SerializeField] private int cost;
        [SerializeField] private int level;
        [SerializeField] private int hp;
        [SerializeField] private int atk;
        [SerializeField] private int def;
        [SerializeField] private bool exhaust;
        [SerializeField] private bool ethereal;
        [SerializeField] private bool soulbound;
        [SerializeField] private OptionalInt upCost;
        [SerializeField] private OptionalInt upHp;
        [SerializeField] private OptionalInt upAtk;
        [SerializeField] private OptionalInt upDef;
        [SerializeField] private OptionalInt sacrificeCrystal;
        [SerializeField] private Parameter[] parameters = Array.Empty<Parameter>();
        [SerializeField] private Parameter[] upgradedParameters = Array.Empty<Parameter>();

        public string Id => cardId;
        public string DisplayName => displayName;
        public string CardClass => cardClass;
        public string Rarity => rarity;
        public string Keyword => keyword;
        public string Description => description;
        public string UpgradedDescription => upgradedDescription;
        public int? UnlockFloor => unlockFloor.Read();
        public int? ClassUnlockFloor => classUnlockFloor.Read();
        public bool NormalDrop => normalDrop;
        public bool EliteDrop => eliteDrop;
        public bool BossDrop => bossDrop;

        public void Initialize(CardDefinition definition, DataRow master, DataRow unlock, DataRow text)
        {
            cardId = definition.Id;
            displayName = master.Str("카드명");
            cardClass = master.Str("클래스");
            rarity = master.Str("희귀도");
            keyword = text?.Str("Keyword") ?? "";
            description = text?.Str("Text_KO") ?? "";
            upgradedDescription = text?.Str("Text_Up_KO") ?? "";
            unlockFloor = new OptionalInt(unlock?.Has("UnlockFloor") == true ? unlock.Int("UnlockFloor") : (int?)null);
            classUnlockFloor = new OptionalInt(unlock?.Has("UnlockFloor_IfStartClass") == true ? unlock.Int("UnlockFloor_IfStartClass") : (int?)null);
            normalDrop = unlock?.Bool("DropPool_Normal") == true;
            eliteDrop = unlock?.Bool("DropPool_Elite") == true;
            bossDrop = unlock?.Bool("DropPool_Boss") == true;
            type = definition.Type;
            cost = definition.Cost;
            level = definition.Level;
            hp = definition.Hp;
            atk = definition.Atk;
            def = definition.Def;
            exhaust = definition.Exhaust;
            ethereal = definition.Ethereal;
            soulbound = definition.Soulbound;
            upCost = new OptionalInt(definition.UpCost);
            upHp = new OptionalInt(definition.UpHp);
            upAtk = new OptionalInt(definition.UpAtk);
            upDef = new OptionalInt(definition.UpDef);
            sacrificeCrystal = new OptionalInt(definition.SacrificeCrystal);
            parameters = definition.Params.Select(pair => new Parameter { Name = pair.Key, Value = pair.Value }).ToArray();
            upgradedParameters = definition.UpParams.Select(pair => new Parameter { Name = pair.Key, Value = pair.Value }).ToArray();
        }

        public CardDefinition ToDefinition() => new CardDefinition
        {
            Id = cardId, Type = type, Cost = cost, Level = level,
            Hp = hp, Atk = atk, Def = def, Exhaust = exhaust, Ethereal = ethereal, Soulbound = soulbound,
            UpCost = upCost.Read(), UpHp = upHp.Read(), UpAtk = upAtk.Read(), UpDef = upDef.Read(),
            SacrificeCrystal = sacrificeCrystal.Read(),
            Params = parameters.ToDictionary(value => value.Name, value => value.Value),
            UpParams = upgradedParameters.ToDictionary(value => value.Name, value => value.Value)
        };
    }
}
