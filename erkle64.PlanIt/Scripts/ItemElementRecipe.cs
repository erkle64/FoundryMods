using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PlanIt
{
    internal class ItemElementRecipe
    {
        public readonly ulong id;
        public readonly string identifier;
        public readonly string name;
        public readonly Sprite icon;
        public readonly double time;
        public readonly bool isResource;
        public readonly ItemElementTemplate.Amount[] outputs;
        public readonly ItemElementTemplate.Amount[] inputs;
        public readonly List<ItemElementProducer> producers = new List<ItemElementProducer>();

        public static IEnumerable<ItemElementRecipe> AllRecipes => _recipesById.Values;
        public static IEnumerable<ItemElementRecipe> RecipesByTag(string tag) => _recipesByTag.TryGetValue(tag, out var recipes) ? recipes : null;

        public static int BlastFurnaceMaxTowers => _blastFurnaceMaxTowers;
        public static int StoveMinTowers => _stoveMinTowers;
        public static int StoveMaxTowers => _stoveMaxTowers;
        public static int AirVentMinVents => _airVentMinVents;
        public static int AirVentMaxVents => _airVentMaxVents;
        public static List<(ItemElementTemplate, double)> ConveyorSpeeds => _conveyorSpeeds;
        public static ItemElementTemplate PipeItem => _pipeItem;

        public static double BlastFurnaceHotAirPerCraft => _blastFurnaceHotAirPerCraft;
        public static ItemElementTemplate HotAirTemplate => _hotAirTemplate;

        private static readonly Dictionary<string, List<ItemElementProducer>> _producersByTag = new Dictionary<string, List<ItemElementProducer>>();
        private static readonly Dictionary<string, ItemElementProducer> _producersByIdentifier = new Dictionary<string, ItemElementProducer>();

        private static ulong _nextId = 0;
        private static Dictionary<string, ItemElementRecipe> _recipesByIdentifier;
        private static Dictionary<ulong, ItemElementRecipe> _recipesById;
        private static Dictionary<string, List<ItemElementRecipe>> _recipesByTag;
        private static int _blastFurnaceMaxTowers = 1;
        private static int _stoveMinTowers = 1;
        private static int _stoveMaxTowers = 1;
        private static int _airVentMinVents = 1;
        private static int _airVentMaxVents = 1;

        private static double _blastFurnaceHotAirPerCraft;
        private static ItemElementTemplate _hotAirTemplate;

        private static List<(ItemElementTemplate, double)> _conveyorSpeeds = new List<(ItemElementTemplate, double)>();

        private static ItemElementTemplate _pipeItem;

        public static ItemElementRecipe Get(string identifier)
        {
            if (_recipesByIdentifier.TryGetValue(identifier, out var recipe)) return recipe;
            PlanItSystem.log.LogWarning($"Missing recipe '{identifier}'!");
            return null;
        }

        public static ItemElementRecipe Get(ulong id)
        {
            if (_recipesById.TryGetValue(id, out var recipe)) return recipe;
            PlanItSystem.log.LogWarning($"Missing recipe '{id}'!");
            return null;
        }

        public static bool TryGet(string identifier, out ItemElementRecipe recipe)
        {
            if (_recipesByIdentifier.TryGetValue(identifier, out recipe)) return true;
            PlanItSystem.log.LogWarning($"Missing recipe '{identifier}'!");
            return false;
        }

        public static bool TryGet(ulong id, out ItemElementRecipe recipe)
        {
            if (_recipesById.TryGetValue(id, out recipe)) return true;
            PlanItSystem.log.LogWarning($"Missing recipe '{id}'!");
            return false;
        }

        public static ItemElementRecipe Get(CraftingRecipe recipe) => Get($"CR:{recipe.identifier}");
        public static ItemElementRecipe Get(BlastFurnaceModeTemplate recipe) => Get($"BFM:{recipe.identifier}");

        private ItemElementRecipe(string identifier, string name, Sprite icon, double time, string[] tags, ItemTemplate output, double outputAmount)
        {
            id = _nextId++;
            this.identifier = $"RR:{identifier}";
            this.name = name;
            this.icon = icon;
            this.time = time;
            isResource = true;
            outputs = new ItemElementTemplate.Amount[] { new ItemElementTemplate.Amount(output, outputAmount) };
            inputs = new ItemElementTemplate.Amount[0];

            _recipesByIdentifier.Add(this.identifier, this);
            _recipesById.Add(id, this);

            AddRecipeByTags(tags);
            AddProducers(tags);

            //PlanItSystem.log.Log($"Added resource item recipe '{identifier}' with outputs[{string.Join(", ", outputs.Select(x => x.itemElement.name))}] and inputs[{string.Join(", ", inputs.Select(x => x.itemElement.name))}]");
        }

        private ItemElementRecipe(string identifier, string name, Sprite icon, double time, string[] tags, ElementTemplate output, double outputAmount)
        {
            id = _nextId++;
            this.identifier = $"RR:{identifier}";
            this.name = name;
            this.icon = icon;
            this.time = time;
            isResource = true;
            outputs = new ItemElementTemplate.Amount[] { new ItemElementTemplate.Amount(output, outputAmount) };
            inputs = new ItemElementTemplate.Amount[0];

            _recipesByIdentifier.Add(this.identifier, this);
            _recipesById.Add(id, this);

            AddRecipeByTags(tags);
            AddProducers(tags);

            //PlanItSystem.log.Log($"Added resource element recipe '{identifier}' with outputs[{string.Join(", ", outputs.Select(x => x.itemElement.name))}] and inputs[{string.Join(", ", inputs.Select(x => x.itemElement.name))}]");
        }

        private ItemElementRecipe(string identifier, string name, Sprite icon, double time, string[] tags, ItemElementTemplate.Amount[] outputs, ItemElementTemplate.Amount[] inputs)
        {
            id = _nextId++;
            this.identifier = $"RC:{identifier}";
            this.name = name;
            this.icon = icon;
            this.time = time;
            isResource = true;
            this.outputs = outputs;
            this.inputs = inputs;

            _recipesByIdentifier.Add(this.identifier, this);
            _recipesById.Add(id, this);

            AddRecipeByTags(tags);
            AddProducers(tags);

            //PlanItSystem.log.Log($"Added resource conversion '{identifier}' with outputs[{string.Join(", ", outputs.Select(x => x.itemElement.name))}] and inputs[{string.Join(", ", inputs.Select(x => x.itemElement.name))}]");
        }

        private ItemElementRecipe(CraftingRecipe recipe)
        {
            id = _nextId++;
            identifier = $"CR:{recipe.identifier}";
            name = recipe.name;
            icon = recipe.icon;
            time = recipe.timeMs / 1000.0;
            isResource = false;

            outputs = new ItemElementTemplate.Amount[recipe.output.Length + recipe.output_elemental.Length];
            var index = 0;
            foreach (var output in recipe.output) outputs[index++] = new ItemElementTemplate.Amount(output.itemTemplate, output.amount * output.percentage_fpm / 10000.0);
            foreach (var output in recipe.output_elemental) outputs[index++] = new ItemElementTemplate.Amount(output.Key, output.Value / 10000.0);

            inputs = new ItemElementTemplate.Amount[recipe.input.Length + recipe.input_elemental.Length];
            index = 0;
            foreach (var input in recipe.input) inputs[index++] = new ItemElementTemplate.Amount(input.itemTemplate, input.amount);
            foreach (var input in recipe.input_elemental) inputs[index++] = new ItemElementTemplate.Amount(input.Key, input.Value / 10000.0);

            _recipesByIdentifier.Add(identifier, this);
            _recipesById.Add(id, this);

            AddRecipeByTags(recipe.tags);
            AddProducers(recipe.tags);

            //PlanItSystem.log.Log($"Added recipe '{identifier}' with outputs[{string.Join(", ", outputs.Select(x => x.itemElement.name))}] and inputs[{string.Join(", ", inputs.Select(x => x.itemElement.name))}]");
        }

        private ItemElementRecipe(BlastFurnaceModeTemplate recipe, ItemElementTemplate.Amount hotAir)
        {
            id = _nextId++;
            identifier = $"BFM:{recipe.identifier}";
            name = recipe.name;
            icon = recipe.icon;
            time = 60.0;
            isResource = false;

            outputs = new ItemElementTemplate.Amount[recipe.output_elemental.Length];
            var index = 0;
            foreach (var output in recipe.output_elemental) outputs[index++] = new ItemElementTemplate.Amount(output.Key, output.Value / 10000.0);

            inputs = new ItemElementTemplate.Amount[recipe.input.Length + 1];
            index = 0;
            foreach (var input in recipe.input) inputs[index++] = new ItemElementTemplate.Amount(input.Key, input.Value);
            inputs[index++] = hotAir;

            _recipesByIdentifier.Add(identifier, this);
            _recipesById.Add(id, this);

            AddRecipeByTags("blast_furnace");
            AddProducers("blast_furnace");

            //PlanItSystem.log.Log($"Added blast furnace recipe '{identifier}' with outputs[{string.Join(", ", outputs.Select(x => x.itemElement.name))}] and inputs[{string.Join(", ", inputs.Select(x => x.itemElement.name))}]");
        }

        public double GetOutputAmount(ItemElementTemplate itemElement)
        {
            foreach (var output in outputs)
            {
                if (output.itemElement.Equals(itemElement)) return output.amount;
            }

            return 0.0;
        }

        public bool HasOutput(ItemElementTemplate itemElement)
        {
            foreach (var output in outputs)
            {
                if (output.itemElement.Equals(itemElement)) return true;
            }

            return false;
        }

        public bool HasInput(ItemElementTemplate itemElement)
        {
            foreach (var input in inputs)
            {
                if (input.itemElement.Equals(itemElement)) return true;
            }

            return false;
        }

        private void AddRecipeByTags(params string[] tags)
        {
            foreach (var tag in tags)
            {
                if (!_recipesByTag.TryGetValue(tag, out var tagRecipes))
                {
                    _recipesByTag[tag] = tagRecipes = new List<ItemElementRecipe>();
                }

                tagRecipes.Add(this);
            }
        }

        private void AddProducers(params string[] tags)
        {
            foreach (var tag in tags)
            {
                if (!_producersByTag.TryGetValue(tag, out var tagProducers)) continue;

                producers.AddRange(tagProducers);
            }
        }

        private static void AddProducer(string tag, ItemElementProducer producer)
        {
            if (!_producersByTag.ContainsKey(tag)) _producersByTag[tag] = new List<ItemElementProducer>();
            _producersByTag[tag].Add(producer);
            _producersByIdentifier[producer.identifier] = producer;
        }

        private static readonly (string, string)[] _vanillaItemResources =
        {
            ("_base_rubble_ignium", "miner"),
            ("_base_rubble_technum", "miner"),
            ("_base_rubble_telluxite", "miner"),
            ("_base_rubble_xenoferrite", "miner"),
            ("_base_ore_mineral_rock", "miner")
        };
        private static readonly (string, string)[] _vanillaElementResources =
        {
            ("_base_water", "pipe_intake"),
            ("_base_olumite", "pumpjack")
        };
        public static void Init(int blastFurnaceTowers, int stoveTowers, int airVentVents)
        {
            if (_recipesById != null)
            {
                UpdateModules(blastFurnaceTowers, stoveTowers, airVentVents);
                return;
            }

            _recipesByIdentifier = new Dictionary<string, ItemElementRecipe>();
            _recipesById = new Dictionary<ulong, ItemElementRecipe>();
            _recipesByTag = new Dictionary<string, List<ItemElementRecipe>>();

            _pipeItem = new ItemElementTemplate(ItemTemplateManager.getItemTemplate("_base_pipe"));

            _blastFurnaceHotAirPerCraft = 0.0;
            _producersByTag.Clear();
            _conveyorSpeeds.Clear();
            foreach (var building in ItemTemplateManager.getAllBuildableObjectTemplates().Values)
            {
                switch (building.type)
                {
                    case BuildableObjectTemplate.BuildableObjectType.Conveyor:
                        {
                            if (building.parentItemTemplate != null && !building.conveyor_isSlope)
                            {
                                var conveyorItem = new ItemElementTemplate(building.parentItemTemplate);
                                _conveyorSpeeds.Add((conveyorItem, building.conveyor_speed_slotsPerTick * 80.0));
                            }
                        }
                        break;

                    case BuildableObjectTemplate.BuildableObjectType.Producer:
                        {
                            var producer = new ItemElementProducer(
                                building.identifier,
                                building.parentItemTemplate.name,
                                building.parentItemTemplate.icon,
                                building.producer_recipeTimeModifier_fpm / 10000.0,
                                building.energyConsumptionKW_fpm / 10000.0);

                            foreach (var tag in building.producer_recipeType_tags)
                            {
                                AddProducer(tag, producer);
                            }
                        }
                        break;

                    case BuildableObjectTemplate.BuildableObjectType.AutoProducer:
                        {
                            var producer = new ItemElementProducer(
                                building.identifier,
                                building.parentItemTemplate.name,
                                building.parentItemTemplate.icon,
                                building.autoProducer_recipeTimeModifier_fpm / 10000.0,
                                building.energyConsumptionKW_fpm / 10000.0);

                            if (building.autoProducer_craftingTag != null)
                            {
                                AddProducer(building.autoProducer_craftingTag.identifier, producer);
                            }
                        }
                        break;

                    case BuildableObjectTemplate.BuildableObjectType.ResourceConverter:
                        {
                            var speed = GetResourceConverterSpeed(building, stoveTowers, airVentVents);

                            var producer = new ItemElementProducer(
                                building.identifier,
                                building.parentItemTemplate.name,
                                building.parentItemTemplate.icon,
                                speed,
                                building.resourceConverter_powerConsumption_kjPerSec_fpm / 10000.0);
                            AddProducer($"rc_{building.identifier}", producer);

                            var outputs = new ItemElementTemplate.Amount[building.resourceConverter_output_elemental_templates.Length];
                            var index = 0;
                            foreach (var output in building.resourceConverter_output_elemental_templates)
                            {
                                outputs[index++] = new ItemElementTemplate.Amount(output.Key, output.Value / 10000.0);
                            }

                            var inputs = new ItemElementTemplate.Amount[building.resourceConverter_input_elemental_templates.Length];
                            index = 0;
                            foreach (var input in building.resourceConverter_input_elemental_templates)
                            {
                                inputs[index++] = new ItemElementTemplate.Amount(input.Key, input.Value / 10000.0);
                            }

                            new ItemElementRecipe(building.identifier, outputs[0].itemElement.name, outputs[0].itemElement.icon, 1.0, new string[] { $"rc_{building.identifier}" }, outputs, inputs);
                        }
                        break;

                    case BuildableObjectTemplate.BuildableObjectType.Pumpjack:
                        {
                            var producer = new ItemElementProducer(
                                building.identifier,
                                building.parentItemTemplate.name,
                                building.parentItemTemplate.icon,
                                building.pumpjack_amountPerSec_fpm / 10000.0,
                                building.energyConsumptionKW_fpm / 10000.0);

                            AddProducer("pumpjack", producer);
                        }
                        break;

                    case BuildableObjectTemplate.BuildableObjectType.DroneMiner:
                        {
                            var producer = new ItemElementProducer(
                                building.identifier,
                                building.parentItemTemplate.name,
                                building.parentItemTemplate.icon,
                                600000.0 / building.droneMiner_miningSpeed_fpm,
                                building.energyConsumptionKW_fpm / 10000.0);

                            AddProducer("miner", producer);
                        }
                        break;

                    case BuildableObjectTemplate.BuildableObjectType.OreVeinMiner:
                        {
                            var producer = new ItemElementProducer(
                                building.identifier,
                                building.parentItemTemplate.name,
                                building.parentItemTemplate.icon,
                                60.0 / (building.oreVeinMiner_ticksPerOre_fpm / 10000.0),
                                building.oreVeinMiner_powerConsumptionMining_kjPerSec + building.oreVeinMiner_powerConsumptionBase_kjPerSec);

                            AddProducer("miner", producer);
                        }
                        break;

                    case BuildableObjectTemplate.BuildableObjectType.BlastFurnace:
                        {
                            var speed = GetBlastFurnaceSpeed(building, blastFurnaceTowers);

                            var producer = new ItemElementProducer(
                                building.identifier,
                                building.parentItemTemplate.name,
                                building.parentItemTemplate.icon,
                                speed,
                                building.energyConsumptionKW_fpm / 10000.0);
                            AddProducer($"blast_furnace", producer);
                        }
                        break;

                    case BuildableObjectTemplate.BuildableObjectType.PipeIntake:
                        {
                            if (building.fbm_ioFluidBoxes.Length > 0)
                            {
                                var waterPerCraft = (float)(building.fbm_ioFluidBoxes[0].transferRatePerSecond_liter / (double)FixedPointMath.FPM_BASE);

                                var producer = new ItemElementProducer(
                                    building.identifier,
                                    building.parentItemTemplate.name,
                                    building.parentItemTemplate.icon,
                                    (building.fbm_ioFluidBoxes != null && building.fbm_ioFluidBoxes.Length >= 1) ? building.fbm_ioFluidBoxes[0].capacity_liter : 1.0,
                                    0.0);
                                AddProducer($"pipe_intake", producer);

                                var water = ItemTemplateManager.getElementTemplate("_base_water");

                                new ItemElementRecipe(
                                    building.identifier,
                                    water.name,
                                    water.icon,
                                    1.0,
                                    new string[] { "pipe_intake" },
                                    water,
                                    waterPerCraft);
                            }
                        }
                        break;

                    case BuildableObjectTemplate.BuildableObjectType.AL_Start:
                        {
                            var producer = new ItemElementProducer(
                                building.identifier,
                                "Assembly Line",
                                building.parentItemTemplate.icon,
                                1.0,
                                0.0);
                            AddProducer($"assembly_line", producer);
                        }
                        break;
                }
            }

            foreach (var alot in ItemTemplateManager.getAllAssemblyLineObjectTemplates().Values)
            {
                var alRequirements = new Dictionary<ItemElementTemplate, double>();
                alRequirements[new ItemElementTemplate(alot.parentItemTemplate)] = 1.0;
                foreach (var stage in alot.stages)
                {
                    foreach (var subAction in stage.stageSubActions)
                    {
                        foreach (var required in subAction.relatedProducerActionTemplate.requiredItems)
                        {
                            var itemElement = new ItemElementTemplate(required.itemTemplate);
                            if (!alRequirements.TryGetValue(itemElement, out var amount)) amount = 0.0;
                            alRequirements[itemElement] = amount + required.amount;
                        }
                        foreach (var required in subAction.relatedProducerActionTemplate.requiredElements)
                        {
                            var itemElement = new ItemElementTemplate(required.elementTemplate);
                            if (!alRequirements.TryGetValue(itemElement, out var amount)) amount = 0.0;
                            alRequirements[itemElement] = amount + required.amount;
                        }
                    }
                }
                new ItemElementRecipe(
                    $"al_{alot.identifier}",
                    alot.name,
                    alot.icon,
                    60.0 / 32.0,
                    new string[] { "assembly_line" },
                    new ItemElementTemplate.Amount[] { new ItemElementTemplate.Amount(new ItemElementTemplate(alot.parentItemTemplate.alStarter_sellItemTemplate), 1.0) },
                    alRequirements.Select(x => new ItemElementTemplate.Amount(x.Key, x.Value)).ToArray());
            }

            _conveyorSpeeds.Sort((a, b) =>
            {
                var (aItem, aSpeed) = a;
                var (bItem, bSpeed) = b;
                return aSpeed.CompareTo(bSpeed);
            });

            foreach (var recipe in ItemTemplateManager.getAllCraftingRecipes().Values)
            {
                if (recipe.name.Contains("Empty Barreled")) continue;
                if (recipe.name.Contains("Unbarrel")) continue;
                if (recipe.tags.Contains("recycler")) continue;
                if (recipe.tags.Contains("creative_chest")) continue;
                if (recipe.tags.Contains("void_chest")) continue;
                if (recipe.tags.Contains("creative_tank")) continue;
                if (recipe.tags.Contains("void_tank")) continue;
                new ItemElementRecipe(recipe);
            }

            _hotAirTemplate = new ItemElementTemplate(ItemTemplateManager.getElementTemplate("_base_hot_air"));
            foreach (var recipe in ItemTemplateManager.getAllBlastFurnaceModeTemplates().Values)
            {
                new ItemElementRecipe(recipe, new ItemElementTemplate.Amount(_hotAirTemplate, _blastFurnaceHotAirPerCraft));
            }

            foreach (var itemResource in _vanillaItemResources)
            {
                var (itemIdentifier, tag) = itemResource;
                var hash = ItemTemplate.generateStringHash(itemIdentifier);
                var item = ItemTemplateManager.getItemTemplate(hash);
                if (item == null) continue;

                new ItemElementRecipe(itemIdentifier, item.name, item.icon, 1.0, new string[] { tag }, item, 1.0);
            }

            foreach (var elementResource in _vanillaElementResources)
            {
                var (elementIdentifier, tag) = elementResource;
                var hash = ElementTemplate.generateStringHash(elementIdentifier);
                var element = ItemTemplateManager.getElementTemplate(hash);
                if (element == null)
                {
                    PlanItSystem.log.LogWarning($"Missing resource element template '{elementIdentifier}'!");
                    continue;
                }

                new ItemElementRecipe(elementIdentifier, element.name, element.icon, 1.0, new string[] { tag }, element, 1.0);
            }

            foreach (var item in ItemTemplateManager.getAllItemTemplates())
            {
                if (_recipesById.Any(x => x.Value.HasOutput(new ItemElementTemplate(item.Value)))) continue;

                var resourceIdentifier = $"RR:{item.Value.identifier}";
                if (_recipesByIdentifier.ContainsKey(resourceIdentifier)) continue;

                new ItemElementRecipe(item.Value.identifier, item.Value.name, item.Value.icon, 1.0, new string[0], item.Value, 1.0);
            }

            foreach (var element in ItemTemplateManager.getAllElementTemplates())
            {
                if (_recipesById.Any(x => x.Value.HasOutput(new ItemElementTemplate(element.Value)))) continue;

                var resourceIdentifier = $"RR:{element.Value.identifier}";
                if (_recipesByIdentifier.ContainsKey(resourceIdentifier)) continue;

                new ItemElementRecipe(element.Value.identifier, element.Value.name, element.Value.icon, 1.0, new string[0], element.Value, 1.0);
            }
        }

        private static void UpdateModules(int blastFurnaceTowers, int stoveTowers, int airVentVents)
        {
            foreach (var building in ItemTemplateManager.getAllBuildableObjectTemplates().Values)
            {
                switch (building.type)
                {
                    case BuildableObjectTemplate.BuildableObjectType.ResourceConverter:
                        {
                            SetProducerSpeed(building.identifier, GetResourceConverterSpeed(building, stoveTowers, airVentVents));
                        }
                        break;

                    case BuildableObjectTemplate.BuildableObjectType.BlastFurnace:
                        {
                            SetProducerSpeed(building.identifier, GetBlastFurnaceSpeed(building, blastFurnaceTowers));
                        }
                        break;
                }
            }

            foreach (var recipe in _recipesByTag["blast_furnace"])
            {
                for (int i = 0; i < recipe.inputs.Length; i++)
                {
                    var input = recipe.inputs[i];
                    if (input.itemElement.Equals(_hotAirTemplate))
                    {
                        recipe.inputs[i] = new ItemElementTemplate.Amount(_hotAirTemplate, _blastFurnaceHotAirPerCraft);
                    }
                }
            }
        }

        private static void SetProducerSpeed(string identifier, double speed)
        {
            if (_producersByIdentifier.TryGetValue(identifier, out var producer)) producer.speed = speed;
        }

        private static double GetBlastFurnaceSpeed(BuildableObjectTemplate building, int blastFurnaceTowers)
        {
            var hotAirPerTick = building.blastFurnace_baseHotAirConsumptionPerTick_fpm / 10000.0;
            var hotAirPerMinute = hotAirPerTick * GameRoot.LOCKSTEP_TICKS_PER_SECOND * 60;

            var towerIdentifier = building.blastFurnace_towerModuleBotIdentifier;
            _blastFurnaceMaxTowers = 1;
            foreach (var limit in building.modularBuildingLimits)
            {
                if (limit.bot_identifier == towerIdentifier)
                {
                    _blastFurnaceMaxTowers = limit.maxAmount;
                    break;
                }
            }

            var modules = Math.Clamp(blastFurnaceTowers, 1, _blastFurnaceMaxTowers);
            var speed = (building.blastFurnace_speedModifier_fpm + (modules - 1) * building.blastFurnace_towerModule_speedIncrease_fpm) / 10000.0;
            _blastFurnaceHotAirPerCraft = hotAirPerMinute
                * (1 + (modules - 1) * building.blastFurnace_towerModule_hotAirConsumptionPercentIncrease_fpm / 10000.0) / speed;
            return speed;
        }

        private static double GetResourceConverterSpeed(BuildableObjectTemplate building, int stoveTowers, int airVentVents)
        {
            var speed = 1.0;
            foreach (var speedBonusModule in building.resourceConverter_speedBonusModules)
            {
                var moduleIdentifier = speedBonusModule.bot_identifier;
                var maxModules = 1;
                foreach (var limit in building.modularBuildingLimits)
                {
                    if (limit.bot_identifier == moduleIdentifier)
                    {
                        maxModules = limit.maxAmount;
                        break;
                    }
                }

                var modules = maxModules;

                if (moduleIdentifier == "_base_hot_air_stove_tower_1")
                {
                    _stoveMinTowers = speedBonusModule.numberOfIgnoredModules;
                    _stoveMaxTowers = maxModules;
                    modules = Mathf.Clamp(stoveTowers, _stoveMinTowers, _stoveMaxTowers);
                }
                else if (moduleIdentifier == "_base_air_intake_vent_1")
                {
                    _airVentMinVents = speedBonusModule.numberOfIgnoredModules;
                    _airVentMaxVents = maxModules;
                    modules = Mathf.Clamp(airVentVents, _airVentMinVents, _airVentMaxVents);
                }

                speed += speedBonusModule.speedBonus_fpm * (modules - speedBonusModule.numberOfIgnoredModules) / 10000.0;
            }

            return speed;
        }

        public static List<ItemElementRecipe> GatherRecipesFor(ItemElementTemplate itemElement)
        {
            var recipes = new List<ItemElementRecipe>();
            foreach (var recipe in _recipesById)
            {
                if (recipe.Value.HasOutput(itemElement)) recipes.Add(recipe.Value);
            }
            return recipes;
        }

        public static List<ItemElementRecipe> GatherUsesFor(ItemElementTemplate itemElement)
        {
            var recipes = new List<ItemElementRecipe>();
            foreach (var recipe in _recipesById)
            {
                if (recipe.Value.HasInput(itemElement)) recipes.Add(recipe.Value);
            }
            return recipes;
        }
    }
}
