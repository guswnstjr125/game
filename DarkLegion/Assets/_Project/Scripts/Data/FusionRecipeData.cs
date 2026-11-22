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
}
