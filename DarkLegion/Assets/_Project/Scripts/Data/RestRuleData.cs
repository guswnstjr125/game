using UnityEngine;

namespace DarkLegion.Data
{
    public sealed class RestRuleData : ScriptableObject
    {
        [SerializeField] private string optionId;
        [SerializeField] private string optionType;
        [SerializeField] private double healPercent;

        public string OptionId => optionId;
        public string OptionType => optionType;
        public double HealPercent => healPercent;

        public void Initialize(string id, string type, double percent)
        {
            optionId = id;
            optionType = type;
            healPercent = percent;
        }
    }
}
