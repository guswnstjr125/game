using DarkLegion.Core;
using UnityEngine;

namespace DarkLegion.Data
{
    public sealed class RelicData : ScriptableObject
    {
        [SerializeField] private string relicId;
        [SerializeField] private string displayName;
        [SerializeField] private string rarity;
        [SerializeField] private string trigger;
        [SerializeField] private string effect;
        [SerializeField] private string text;

        public string Id => relicId;
        public string DisplayName => displayName;
        public string Rarity => rarity;
        public string Trigger => trigger;
        public string Effect => effect;
        public string Text => text;

        public void Initialize(RelicRecord record)
        {
            relicId = record.Id;
            displayName = record.Name;
            rarity = record.Rarity;
            trigger = record.Trigger;
            effect = record.Effect;
            text = record.Text;
        }
    }
}
