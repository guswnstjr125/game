using DarkLegion.Core;
using UnityEngine;

namespace DarkLegion.Data
{
    public sealed class ClassStartData : ScriptableObject
    {
        [SerializeField] private string className;
        [SerializeField] private int startHp;
        [SerializeField] private int maxHp;
        [SerializeField] private int startGold;
        [SerializeField] private int startCrystal;

        public string ClassName => className;
        public int StartHp => startHp;
        public int MaxHp => maxHp;
        public int StartGold => startGold;
        public int StartCrystal => startCrystal;

        public void Initialize(ClassStartRecord record)
        {
            className = record.ClassName;
            startHp = record.StartHp;
            maxHp = record.MaxHp;
            startGold = record.StartGold;
            startCrystal = record.StartCrystal;
        }
    }
}
