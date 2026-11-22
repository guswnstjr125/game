using DarkLegion.Core;
using UnityEngine;

namespace DarkLegion.Data
{
    public sealed class KeywordData : ScriptableObject
    {
        [SerializeField] private string keywordId;
        [SerializeField] private string term;
        [SerializeField] private string displayName;
        [SerializeField] private string tooltip;

        public string Id => keywordId;
        public string Term => term;
        public string DisplayName => displayName;
        public string Tooltip => tooltip;

        public void Initialize(KeywordRecord record)
        {
            keywordId = record.Id;
            term = record.Term;
            displayName = record.Name;
            tooltip = record.Tooltip;
        }
    }
}
