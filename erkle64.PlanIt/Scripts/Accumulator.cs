using Planit;
using System.Collections.Generic;
using System.Linq;

namespace PlanIt
{
    internal struct Accumulator
    {
        public readonly Required required;
        public readonly Dictionary<ulong, double> recipeAmounts;
        public readonly Dictionary<ItemElementTemplate, double> itemAmounts;
        public readonly Dictionary<ItemElementTemplate, double> wasteAmounts;
        public readonly List<ulong> recipeOrder;

        public bool HasItems => itemAmounts.Count > 0;

        public Accumulator(ItemElementTemplate itemElement, double amount)
        {
            required = new Required(itemElement, amount);
            recipeAmounts = new Dictionary<ulong, double>();
            itemAmounts = new Dictionary<ItemElementTemplate, double>();
            wasteAmounts = new Dictionary<ItemElementTemplate, double>();
            recipeOrder = new();
        }

        public void Merge(Accumulator other, bool addDependency)
        {
            //PlanItSystem.log.Log($"Accumulator Merge: {required.itemElement.name}:{required.amount} + {other.required.itemElement.name}:{other.required.amount}");
            //Dump();
            //other.Dump();

            if (required.itemElement.isValid && addDependency) required.AddDependency(other.required);

            foreach (var recipe in other.EachRecipe()) AddRecipe(recipe.Key, recipe.Value);
            foreach (var item in other.itemAmounts) AddItem(item.Key, item.Value);
            foreach (var item in other.wasteAmounts) AddWaste(item.Key, item.Value);
        }

        public void AddRecipe(ulong recipeId, double amount)
        {
            if (recipeAmounts.ContainsKey(recipeId))
            {
                recipeAmounts[recipeId] += amount;
            }
            else
            {
                recipeAmounts[recipeId] = amount;
                recipeOrder.Add(recipeId);
            }
        }

        public void AddItem(ItemElementTemplate itemElement, double amount)
        {
            if (itemAmounts.ContainsKey(itemElement)) itemAmounts[itemElement] += amount;
            else itemAmounts[itemElement] = amount;
        }

        public void AddWaste(ItemElementTemplate itemElement, double amount)
        {
            if (wasteAmounts.ContainsKey(itemElement)) wasteAmounts[itemElement] += amount;
            else wasteAmounts[itemElement] = amount;
        }

        public double GetRecipeAmount(ulong recipeId)
        {
            return recipeAmounts.ContainsKey(recipeId) ? recipeAmounts[recipeId] : 0.0;
        }

        public double GetWasteAmount(ItemElementTemplate itemElement)
        {
            return wasteAmounts.ContainsKey(itemElement) ? wasteAmounts[itemElement] : 0.0;
        }

        public void Dump()
        {
            PlanItSystem.log.Log("+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++");
            PlanItSystem.log.Log($"Accumulator Dump: {required.itemElement.name} - {required.amount}");
            foreach (var recipe in recipeAmounts) PlanItSystem.log.Log($"Recipe: {ItemElementRecipe.Get(recipe.Key).name} - {recipe.Value}");
            foreach (var item in itemAmounts) PlanItSystem.log.Log($"Item: {item.Key.name} - {item.Value}");
            foreach (var waste in wasteAmounts) PlanItSystem.log.Log($"Waste: {waste.Key.name} - {waste.Value}");
            PlanItSystem.log.Log("-----------------------------------------------------------------------");
        }

        public void SortRecipes()
        {
            if (recipeOrder.Count <= 1) return;

            ulong GetRecipeForItem(ItemElementTemplate item, IEnumerable<ulong> recipeIds)
            {
                foreach (var recipeId in recipeIds)
                {
                    if (ItemElementRecipe.TryGet(recipeId, out var recipe))
                    {
                        foreach (var output in recipe.outputs)
                        {
                            if (output.itemElement.Equals(item)) return recipeId;
                        }
                    }
                }

                return 0uL;
            }

            HashSet<(ulong, ulong)> edges = new();
            foreach (var recipeId in recipeAmounts.Keys)
            {
                if (ItemElementRecipe.TryGet(recipeId, out var recipe))
                {
                    foreach (var ingredient in recipe.inputs)
                    {
                        var recipeIdForItem = GetRecipeForItem(ingredient.itemElement, recipeAmounts.Keys);
                        //if (recipeIdForItem == 0uL) PlanItSystem.log.Log($"Recipe {recipe.name} has no recipe for {ingredient.itemElement.name}");
                        //else PlanItSystem.log.Log($"Recipe {recipe.name} has recipe {ItemElementRecipe.Get(recipeIdForItem).name} for {ingredient.itemElement.name}");

                        if (recipeIdForItem != 0uL && recipeIdForItem != recipeId)
                        {
                            edges.Add((recipeId, recipeIdForItem));
                        }
                    }
                }
            }

            var sortedRecipeIds = TopoSort<ulong>.CyclicTopoSort(edges);
            recipeOrder.Clear();
            recipeOrder.AddRange(sortedRecipeIds);
        }

        public IEnumerable<KeyValuePair<ulong, double>> EachRecipe()
        {
            PlanItSystem.log.Log($"Accumulator EachRecipe: {recipeOrder.Count} == {recipeAmounts.Count}");

            foreach (var recipeId in recipeOrder)
            {
                if (recipeAmounts.TryGetValue(recipeId, out var amount))
                {
                    yield return new KeyValuePair<ulong, double>(recipeId, amount);
                }
            }
        }

        internal class Required
        {
            public ItemElementTemplate itemElement;
            public double amount;
            public List<Required> dependencies;

            public Required()
            {
                itemElement = ItemElementTemplate.Empty;
                amount = 0.0;
                dependencies = new List<Required>();
            }

            public Required(ItemElementTemplate itemElement, double amount)
            {
                this.itemElement = itemElement;
                this.amount = amount;
                dependencies = new List<Required>();
            }

            public void AddDependency(Required dependency)
            {
                if (!dependency.itemElement.isValid) return;
                dependencies.Add(dependency);
            }
        }
    }
}
