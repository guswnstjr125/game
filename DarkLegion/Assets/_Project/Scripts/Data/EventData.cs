using System;
using DarkLegion.Core;
using UnityEngine;

namespace DarkLegion.Data
{
    public sealed class EventData : ScriptableObject
    {
        [Serializable]
        public struct Option
        {
            [SerializeField] private int option;
            [SerializeField] private string condition;
            [SerializeField] private string result;

            public int Index => option;
            public string Condition => condition;
            public string Result => result;

            public Option(EventOptionRecord record)
            {
                option = record.Option;
                condition = record.Condition;
                result = record.Result;
            }
        }

        [SerializeField] private string eventId;
        [SerializeField] private string displayName;
        [SerializeField] private double floor1;
        [SerializeField] private double floor2;
        [SerializeField] private double floor3;
        [SerializeField] private double floor4;
        [SerializeField] private double floor5;
        [SerializeField] private Option[] options = Array.Empty<Option>();

        public string Id => eventId;
        public string DisplayName => displayName;
        public double Floor1 => floor1;
        public int OptionCount => options.Length;
        public Option OptionAt(int index) => options[index];

        public void Initialize(EventRecord record)
        {
            eventId = record.Id;
            displayName = record.Name;
            floor1 = record.Floor1;
            floor2 = record.Floor2;
            floor3 = record.Floor3;
            floor4 = record.Floor4;
            floor5 = record.Floor5;
            options = new Option[record.Options.Count];
            for (int i = 0; i < options.Length; i++) options[i] = new Option(record.Options[i]);
        }

        public void AddTo(EventPool pool)
        {
            if (floor1 > 0) pool.Add(1, eventId, floor1);
            if (floor2 > 0) pool.Add(2, eventId, floor2);
            if (floor3 > 0) pool.Add(3, eventId, floor3);
            if (floor4 > 0) pool.Add(4, eventId, floor4);
            if (floor5 > 0) pool.Add(5, eventId, floor5);
        }
    }
}
