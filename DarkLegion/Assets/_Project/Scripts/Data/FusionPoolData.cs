using DarkLegion.Core;
using UnityEngine;

namespace DarkLegion.Data
{
    public sealed class FusionPoolData : ScriptableObject
    {
        [SerializeField] private string poolId;
        [SerializeField] private string inputClass;
        [SerializeField] private int inputLevel;
        [SerializeField] private string outputClass;
        [SerializeField] private int outputLevel;
        [SerializeField] private string resultMonsterId;
        [SerializeField] private int weight;

        public string Id => poolId;
        public string InputClass => inputClass;
        public int InputLevel => inputLevel;
        public string OutputClass => outputClass;
        public int OutputLevel => outputLevel;
        public string ResultMonsterId => resultMonsterId;
        public int Weight => weight;

        public void Initialize(FusionPoolRecord record)
        {
            poolId = record.Id;
            inputClass = record.InputClass;
            inputLevel = record.InputLevel;
            outputClass = record.OutputClass;
            outputLevel = record.OutputLevel;
            resultMonsterId = record.ResultMonsterId;
            weight = record.Weight;
        }
    }
}
