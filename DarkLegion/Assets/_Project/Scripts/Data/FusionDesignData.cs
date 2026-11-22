using DarkLegion.Core;
using UnityEngine;

namespace DarkLegion.Data
{
    public sealed class FusionRecipeData : ScriptableObject
    {
        [SerializeField] private string recipeId;
        [SerializeField] private string materialA;
        [SerializeField] private string materialB;
        [SerializeField] private string resultMonsterId;
        [SerializeField] private string recipeType;
        [SerializeField] private int weight;

        public string Id => recipeId;
        public string MaterialA => materialA;
        public string MaterialB => materialB;
        public string ResultMonsterId => resultMonsterId;
        public string RecipeType => recipeType;
        public int Weight => weight;
        public bool IsFixedRecipe => recipeType == "Fixed" && weight > 0;

        public void Initialize(FusionRecipeRecord record)
        {
            recipeId = record.Id;
            materialA = record.MaterialA;
            materialB = record.MaterialB;
            resultMonsterId = record.ResultMonsterId;
            recipeType = record.Type;
            weight = record.Weight;
        }
    }

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
