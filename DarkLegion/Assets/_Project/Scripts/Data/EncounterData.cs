using System;
using System.Collections.Generic;
using System.Linq;
using DarkLegion.Core;
using UnityEngine;

namespace DarkLegion.Data
{
    public sealed class EncounterData : ScriptableObject
    {
        [Serializable]
        public struct EnemyEntry
        {
            [SerializeField] private string id;
            [SerializeField] private int count;

            public string Id => id;
            public int Count => count;

            public EnemyEntry(string id, int count)
            {
                this.id = id;
                this.count = count;
            }
        }

        [SerializeField] private int floor;
        [SerializeField] private int node;
        [SerializeField] private string nodeType;
        [SerializeField] private string variant;
        [SerializeField] private int wave;
        [SerializeField] private EnemyEntry[] enemies = Array.Empty<EnemyEntry>();

        public int Floor => floor;
        public int Node => node;
        public string NodeType => nodeType;
        public string Variant => variant;
        public int Wave => wave;
        public IReadOnlyList<EnemyEntry> Enemies => enemies;

        public void Initialize(Encounter encounter, int waveNumber)
        {
            floor = encounter.Floor;
            node = encounter.Node;
            nodeType = encounter.NodeType;
            variant = encounter.Variant;
            wave = waveNumber;
            enemies = encounter.Waves[waveNumber].Select(entry => new EnemyEntry(entry.id, entry.count)).ToArray();
        }

        public void AddTo(EncounterCatalog catalog)
        {
            foreach (var enemy in enemies)
                catalog.Add(floor, node, nodeType, variant, wave, enemy.Id, enemy.Count);
        }
    }
}
