using System.Collections.Generic;
using TMPro;
using Unfoundry;
using UnityEngine.UI;
using UnityEngine;
using System.IO;
using TinyJSON;
using System.Diagnostics;

namespace PlanIt
{
    internal class PlannerFrame : UIFrame
    {
        [SerializeField] private UIHeaderBar _heading = null;
        [SerializeField] private GameObject _planList = null;
        [SerializeField] private GameObject _planContainer = null;
        [SerializeField] private GameObject _planContent = null;
        [SerializeField] private GameObject _outputList = null;
        [SerializeField] private GameObject _inputList = null;
        [SerializeField] private GameObject _extraOutputList = null;
        [SerializeField] private GameObject _extraInputList = null;
        [SerializeField] private TMP_InputField _renamePlanInput;
        [SerializeField] private GameObject _editPlanRow;
        [SerializeField] private TMP_InputField _newPlanInput;

        [SerializeField] private IconToggle[] _conveyorOptionButtons = new IconToggle[3];
        [SerializeField] private IconToggle[] _metallurgyOptionButtons = new IconToggle[3];
        [SerializeField] private IconToggle[] _cementOptionButtons = new IconToggle[2];
        [SerializeField] private Image _blastFurnaceOptionImage;
        [SerializeField] private Image _stoveOptionImage;
        [SerializeField] private Image _airVentOptionImage;
        [SerializeField] private TooltipTrigger _blastFurnaceOptionTooltip;
        [SerializeField] private TooltipTrigger _stoveOptionTooltip;
        [SerializeField] private TooltipTrigger _airVentOptionTooltip;
        [SerializeField] private TextMeshProUGUI _blastFurnaceTowerCounterLabel;
        [SerializeField] private TextMeshProUGUI _stoveTowerCounterLabel;
        [SerializeField] private TextMeshProUGUI _airVentVentCounterLabel;

        [Header("Prefabs")]
        [SerializeField] private ItemSelectFrame _itemSelectFramePrefab;
        [SerializeField] private ItemPanelWithInput _outputPanelPrefab;
        [SerializeField] private ItemPanel _inputPanelPrefab;
        [SerializeField] private RecipeRow _recipeRowPrefab;
        [SerializeField] private ItemPanelWithConveyor _recipeOutputPanelPrefab;
        [SerializeField] private ItemPanelWithPower _recipeMachinePanelPrefab;
        [SerializeField] private ItemPanelWithConveyor _recipeInputPanelPrefab;

        [Header("Icons")]
        [SerializeField] private string[] _metallurgyIcons = new string[3];
        [SerializeField] private string[] _cementIcons = new string[2];
        [SerializeField] private string _blastFurnaceIcon = string.Empty;
        [SerializeField] private string _stoveIcon = string.Empty;
        [SerializeField] private string _airVentIcon = string.Empty;

        [Header("Tooltips")]
        [SerializeField] private string[] _cementTooltip = new string[2];
        [SerializeField] private string _blastFurnaceTooltip = string.Empty;
        [SerializeField] private string _stoveTooltip = string.Empty;
        [SerializeField] private string _airVentTooltip = string.Empty;

        private string _currentPlanPath = string.Empty;
        private PlanData _currentPlan = default;

        private string _planFolder = string.Empty;

        private Dictionary<ItemElementTemplate, ItemPanel> _inputPanels = new();

        public bool IsOpen => gameObject.activeSelf;

        private ItemSelectFrame _itemSelectFrame;

        public void Setup(string planFolder)
        {
            _planFolder = planFolder;

            _planContainer.SetActive(false);

            ItemElementRecipe.Init(1, 1, 1);

            _itemSelectFrame = Instantiate(_itemSelectFramePrefab, transform.parent);
            _itemSelectFrame.BuildContent();
            _itemSelectFrame.gameObject.SetActive(false);

            var conveyorIndex = 0;
            foreach (var conveyorSpeed in ItemElementRecipe.ConveyorSpeeds)
            {
                var (conveyor, speed) = conveyorSpeed;

                if (conveyor.identifier != "_base_conveyor_i" && conveyor.identifier != "_base_conveyor_ii" && conveyor.identifier != "_base_conveyor_iii" && conveyor.identifier != "_base_conveyor_iv") continue;

                _conveyorOptionButtons[conveyorIndex].Setup(conveyor.icon, $"Use {conveyor.name}\nSpeed: {Mathf.RoundToInt((float)speed)}/m");
                if (conveyorIndex++ >= _conveyorOptionButtons.Length) break;
            }

            var metallurgyIndex = 0;
            foreach (var metallurgyIcon in _metallurgyIcons)
            {
                var icon = ResourceDB.getIcon(metallurgyIcon);

                _metallurgyOptionButtons[metallurgyIndex].Setup(icon, $"Use Metallurgy Tier {metallurgyIndex + 1}");
                if (metallurgyIndex++ >= _metallurgyOptionButtons.Length) break;
            }

            var cementIndex = 0;
            foreach (var cementIcon in _cementIcons)
            {
                var icon = ResourceDB.getIcon(cementIcon);

                _cementOptionButtons[cementIndex].Setup(icon, _cementTooltip[cementIndex]);
                if (cementIndex++ >= _cementOptionButtons.Length) break;
            }

            _blastFurnaceOptionImage.sprite = ResourceDB.getIcon(_blastFurnaceIcon);
            _blastFurnaceOptionTooltip.tooltipText = _blastFurnaceTooltip;

            _stoveOptionImage.sprite = ResourceDB.getIcon(_stoveIcon);
            _stoveOptionTooltip.tooltipText = _stoveTooltip;

            _airVentOptionImage.sprite = ResourceDB.getIcon(_airVentIcon);
            _airVentOptionTooltip.tooltipText = _airVentTooltip;
        }

        public void Show()
        {
            gameObject.SetActive(true);
            FillPlanList();
            AudioManager.playUISoundEffect(ResourceDB.resourceLinker.audioClip_UIOpen);
            GlobalStateManager.addCursorRequirement();
        }

        public void Hide()
        {
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
                AudioManager.playUISoundEffect(ResourceDB.resourceLinker.audioClip_UIClose);
                GlobalStateManager.removeCursorRequirement();
            }
        }

        private void FillPlanList()
        {
            _planList.transform.DestroyAllChildren();

            var builder = UIBuilder.BeginWith(_planList);

            foreach (var filePath in Directory.GetFiles(_planFolder, "*.json"))
            {
                var name = Path.GetFileNameWithoutExtension(filePath);
                builder = builder.Element_TextButton_AutoSize($"Plan {name}", name)
                        .SetOnClick(() =>
                        {
                            LoadPlan(filePath);
                            AudioManager.playUISoundEffect(ResourceDB.resourceLinker.audioClip_UIButtonClick);
                        })
                    .Done;
            }

            builder.End();
        }

        public void OnToggleConveyorTier1(bool state)
        {
            if (!state || _currentPlan.conveyorTier == 0) return;
            _currentPlan.conveyorTier = 0;

            UpdateSolution();
        }

        public void OnToggleConveyorTier2(bool state)
        {
            if (!state || _currentPlan.conveyorTier == 1) return;
            _currentPlan.conveyorTier = 1;

            UpdateSolution();
        }

        public void OnToggleConveyorTier3(bool state)
        {
            if (!state || _currentPlan.conveyorTier == 2) return;
            _currentPlan.conveyorTier = 2;

            UpdateSolution();
        }

        public void OnToggleConveyorTier4(bool state)
        {
            if (!state || _currentPlan.conveyorTier == 3) return;
            _currentPlan.conveyorTier = 3;

            UpdateSolution();
        }

        public void OnToggleMetallurgyTier1(bool state)
        {
            if (!state || _currentPlan.metallurgyTier == 0) return;
            _currentPlan.metallurgyTier = 0;

            UpdateSolution();
        }

        public void OnToggleMetallurgyTier2(bool state)
        {
            if (!state || _currentPlan.metallurgyTier == 1) return;
            _currentPlan.metallurgyTier = 1;

            UpdateSolution();
        }

        public void OnToggleMetallurgyTier3(bool state)
        {
            if (!state || _currentPlan.metallurgyTier == 2) return;
            _currentPlan.metallurgyTier = 2;

            UpdateSolution();
        }

        public void OnToggleCementTier1(bool state)
        {
            if (!state || _currentPlan.cementTier == 0) return;
            _currentPlan.cementTier = 0;

            UpdateSolution();
        }

        public void OnToggleCementTier2(bool state)
        {
            if (!state || _currentPlan.cementTier == 1) return;
            _currentPlan.cementTier = 1;

            UpdateSolution();
        }

        public void OnClickDecrementBlastFurnaceTowerCount()
        {
            if (_currentPlan.blastFurnaceTowers > 1)
            {
                _currentPlan.blastFurnaceTowers--;
                UpdateOptionButtons();
            }
        }

        public void OnClickIncrementBlastFurnaceTowerCount()
        {
            if (_currentPlan.blastFurnaceTowers < ItemElementRecipe.BlastFurnaceMaxTowers)
            {
                _currentPlan.blastFurnaceTowers++;
                UpdateOptionButtons();
            }
        }

        public void OnClickDecrementStoveTowerCount()
        {
            if (_currentPlan.stoveTowers > ItemElementRecipe.StoveMinTowers)
            {
                _currentPlan.stoveTowers--;
                UpdateOptionButtons();
            }
        }

        public void OnClickIncrementStoveTowerCount()
        {
            if (_currentPlan.stoveTowers < ItemElementRecipe.StoveMaxTowers)
            {
                _currentPlan.stoveTowers++;
                UpdateOptionButtons();
            }
        }

        public void OnClickDecrementAirVentVentCount()
        {
            if (_currentPlan.airVentVents > 1)
            {
                _currentPlan.airVentVents--;
                UpdateOptionButtons();
            }
        }

        public void OnClickIncrementAirVentVentCount()
        {
            if (_currentPlan.airVentVents < ItemElementRecipe.AirVentMaxVents)
            {
                _currentPlan.airVentVents++;
                UpdateOptionButtons();
            }
        }

        public void OnClickRenamePlan()
        {
            if (string.IsNullOrEmpty(_currentPlanPath)) return;

            var name = PathHelpers.MakeValidFileName(_renamePlanInput?.text ?? "");
            if (string.IsNullOrEmpty(name)) return;

            var filePath = Path.Combine(_planFolder, $"{name}.json");
            if (File.Exists(filePath))
            {
                ConfirmationFrame.Show($"Overwrite '{name}'?", "Confirm", () =>
                {
                    RenamePlan(_currentPlanPath, filePath);
                });
            }
            else
            {
                RenamePlan(_currentPlanPath, filePath);
            }
        }

        public void OnClickDeletePlan()
        {
            if (string.IsNullOrEmpty(_currentPlanPath)) return;

            ConfirmationFrame.Show($"Delete '{Path.GetFileNameWithoutExtension(_currentPlanPath)}'?", "Confirm", () =>
            {
                DeletePlan(_currentPlanPath);
            });
        }

        public void OnClickNewPlan()
        {
            var name = PathHelpers.MakeValidFileName(_newPlanInput?.text ?? "");
            if (string.IsNullOrEmpty(name)) return;

            var filePath = Path.Combine(_planFolder, $"{name}.json");
            if (File.Exists(filePath))
            {
                ConfirmationFrame.Show($"Overwrite '{name}'?", "Confirm", () =>
                {
                    CreateNewPlan(filePath);
                });
            }
            else
            {
                CreateNewPlan(filePath);
            }
        }

        private void RenamePlan(string currentPlanPath, string filePath)
        {
            try
            {
                if (File.Exists(currentPlanPath)) File.Move(currentPlanPath, filePath);
            }
            catch { }

            FillPlanList();
            LoadPlan(filePath);
        }

        private void DeletePlan(string currentPlanPath)
        {
            try
            {
                if (File.Exists(currentPlanPath)) File.Delete(currentPlanPath);
            }
            catch { }

            FillPlanList();
            _planContainer.SetActive(false);
            _planContent.transform.DestroyAllChildren();
            _heading.setText("PlanIt - Planner");
            _editPlanRow.SetActive(false);
        }

        private void CreateNewPlan(string filePath)
        {
            var newPlan = PlanData.Create();
            var json = JSON.Dump(newPlan, EncodeOptions.PrettyPrint | EncodeOptions.NoTypeHints);
            File.WriteAllText(filePath, json);
            FillPlanList();
        }

        private void LoadPlan(string filePath)
        {
            if (!File.Exists(filePath)) return;

            var name = Path.GetFileNameWithoutExtension(filePath);

            _editPlanRow.SetActive(true);
            _renamePlanInput.text = name;

            _heading.setText($"PlanIt - {name}");

            ItemElementRecipe.Init(_currentPlan.blastFurnaceTowers, _currentPlan.stoveTowers, _currentPlan.airVentVents);

            _currentPlanPath = filePath;

            _currentPlan = PlanData.Load(filePath);
            _currentPlan.blastFurnaceTowers = Mathf.Clamp(_currentPlan.blastFurnaceTowers, 1, ItemElementRecipe.BlastFurnaceMaxTowers);
            _currentPlan.stoveTowers = Mathf.Clamp(_currentPlan.stoveTowers, ItemElementRecipe.StoveMinTowers, ItemElementRecipe.StoveMaxTowers);
            _currentPlan.airVentVents = Mathf.Clamp(_currentPlan.airVentVents, ItemElementRecipe.AirVentMinVents, ItemElementRecipe.AirVentMaxVents);

            _planContainer.SetActive(true);
            _planContent.transform.DestroyAllChildren();

            UpdateOutputs();
            UpdateInputs();
            UpdateOptionButtons();
        }

        public void AddOutput()
        {
            _itemSelectFrame.Show(itemElement =>
            {
                foreach (var output in _currentPlan.outputs) if (output == itemElement.fullIdentifier) return;
                foreach (var input in _currentPlan.inputs) if (input == itemElement.fullIdentifier) return;

                _currentPlan.outputs.Add(itemElement.fullIdentifier);
                _currentPlan.outputAmounts.Add(0);
                SavePlan();
                UpdateOutputs();
                UpdateSolution();
            });
        }

        public void AddInput()
        {
            _itemSelectFrame.Show(itemElement =>
            {
                foreach (var output in _currentPlan.outputs) if (output == itemElement.fullIdentifier) return;
                foreach (var input in _currentPlan.inputs) if (input == itemElement.fullIdentifier) return;

                _currentPlan.inputs.Add(itemElement.fullIdentifier);
                SavePlan();
                UpdateInputs();
                UpdateSolution();
            });
        }

        public void RemoveLastOutput()
        {
            if (_currentPlan.outputs.Count == 0) return;
            _currentPlan.outputs.RemoveAt(_currentPlan.outputs.Count - 1);
            _currentPlan.outputAmounts.RemoveAt(_currentPlan.outputAmounts.Count - 1);
            SavePlan();
            UpdateOutputs();
            UpdateSolution();
            AudioManager.playUISoundEffect(ResourceDB.resourceLinker.audioClip_UIButtonClick);
        }

        public void RemoveLastInput()
        {
            if (_currentPlan.inputs.Count == 0) return;
            _currentPlan.inputs.RemoveAt(_currentPlan.inputs.Count - 1);
            SavePlan();
            UpdateInputs();
            UpdateSolution();
            AudioManager.playUISoundEffect(ResourceDB.resourceLinker.audioClip_UIButtonClick);
        }

        private void RemoveOutput(string fullIdentifier)
        {
            if (_currentPlan.outputs.Count == 0) return;
            var index = _currentPlan.outputs.IndexOf(fullIdentifier);
            if (index < 0) return;
            _currentPlan.outputs.RemoveAt(index);
            _currentPlan.outputAmounts.RemoveAt(index);
            SavePlan();
            UpdateOutputs();
            UpdateSolution();
            AudioManager.playUISoundEffect(ResourceDB.resourceLinker.audioClip_UIButtonClick);
        }

        private void RemoveInput(string fullIdentifier)
        {
            if (_currentPlan.inputs.Count == 0) return;
            if (!_currentPlan.inputs.Remove(fullIdentifier)) return;
            SavePlan();
            UpdateInputs();
            UpdateSolution();
            AudioManager.playUISoundEffect(ResourceDB.resourceLinker.audioClip_UIButtonClick);
        }

        private void ToggleInput(string fullIdentifier)
        {
            if (_currentPlan.inputs.Contains(fullIdentifier))
            {
                _currentPlan.inputs.Remove(fullIdentifier);
            }
            else if (!_currentPlan.outputs.Contains(fullIdentifier))
            {
                _currentPlan.inputs.Add(fullIdentifier);
            }

            SavePlan();
            UpdateInputs();
            UpdateSolution();
            AudioManager.playUISoundEffect(ResourceDB.resourceLinker.audioClip_UIButtonClick);
        }

        private void SetOutputAmount(int outputIndex, double outputAmount)
        {
            if (outputIndex < 0 || outputIndex >= _currentPlan.outputAmounts.Count) return;

            _currentPlan.outputAmounts[outputIndex] = outputAmount;
            SavePlan();
            UpdateSolution();
        }

        private void SavePlan()
        {
            if (string.IsNullOrEmpty(_currentPlanPath)) return;

            try
            {
                _currentPlan.Save(_currentPlanPath);
            }
            catch { }
        }

        private void UpdateOptionButtons()
        {
            UpdateSolution();

            UpdateOptionButtons(_currentPlan.conveyorTier, _conveyorOptionButtons);
            UpdateOptionButtons(_currentPlan.metallurgyTier, _metallurgyOptionButtons);
            UpdateOptionButtons(_currentPlan.cementTier, _cementOptionButtons);

            _blastFurnaceTowerCounterLabel.text = _currentPlan.blastFurnaceTowers.ToString();
            _stoveTowerCounterLabel.text = _currentPlan.stoveTowers.ToString();
            _airVentVentCounterLabel.text = _currentPlan.airVentVents.ToString();

            SavePlan();
        }

        private void UpdateOptionButtons(int tier, IconToggle[] buttons)
        {
            buttons[tier].isOn = true;
        }

        private void UpdateOutputs()
        {
            if (_currentPlan.outputs.Count != _currentPlan.outputAmounts.Count)
            {
                PlanItSystem.log.LogWarning($"Output count ({_currentPlan.outputs.Count}) does not match output amount count ({_currentPlan.outputAmounts.Count})");
                return;
            }

            _outputList.transform.DestroyAllChildren();
            for (int i = 0; i < _currentPlan.outputs.Count; i++)
            {
                var output = _currentPlan.outputs[i];
                var outputAmount = _currentPlan.outputAmounts[i];
                var outputElement = ItemElementTemplate.Get(output);
                if (outputElement.isValid)
                {
                    var outputIndex = i;
                    var outputPanel = Instantiate(_outputPanelPrefab, _outputList.transform);
                    outputPanel.Setup($"Output Amount - {outputElement.name}", outputElement.icon, outputAmount);
                    outputPanel.onClicked += () => RemoveOutput(outputElement.fullIdentifier);
                    outputPanel.onRateChanged += value => SetOutputAmount(outputIndex, value);
                }
            }

            _extraOutputList.SetActive(false);
        }

        private void UpdateInputs()
        {
            _inputPanels.Clear();
            _inputList.transform.DestroyAllChildren();
            var builder = UIBuilder.BeginWith(_inputList);
            foreach (var input in _currentPlan.inputs)
            {
                var inputElement = ItemElementTemplate.Get(input);
                if (inputElement.isValid)
                {
                    var inputPanel = Instantiate(_inputPanelPrefab, _inputList.transform);
                    inputPanel.Setup($"Input - {inputElement.name}", inputElement.icon, 0.0);
                    inputPanel.onClicked += () => RemoveInput(inputElement.fullIdentifier);
                    _inputPanels[inputElement] = inputPanel;
                }
            }

            _extraInputList.SetActive(false);
        }

        private static readonly string[][] _metallurgyTierRecipes = new string[][]
        {
            new string[]
            {
                "CR:_base_xf_plates_t1",
                "CR:_base_technum_rods_t1",
                "CR:_base_steel_t1"
            },
            new string[]
            {
                "CR:_base_ore_xenoferrite",
                "CR:_base_ore_technum",
                "CR:_base_xf_plates_t2",
                "CR:_base_technum_rods_t2",
                "CR:_base_steel_t2"
            },
            new string[]
            {
                "CR:_base_ore_xenoferrite",
                "CR:_base_ore_technum",
                "BFM:_base_bfm_te",
                "BFM:_base_bfm_xf",
                "CR:_base_xf_plates_t3",
                "CR:_base_technum_rods_t3",
                "CR:_base_steel_t2"
            },
            new string[]
            {
                "CR:_base_technum_rods_t1_primitive",
                "CR:_base_xf_plates_t1_primitive"
            }
        };

        private void UpdateSolution()
        {
            ItemElementRecipe.Init(_currentPlan.blastFurnaceTowers, _currentPlan.stoveTowers, _currentPlan.airVentVents);

            _planContent.SetActive(false);

            var disabledRecipes = new HashSet<ulong>();
            for (int metallurgyTier = 0; metallurgyTier < _metallurgyTierRecipes.Length; metallurgyTier++)
            {
                if (metallurgyTier == _currentPlan.metallurgyTier) continue;
                foreach (var recipeIdentifier in _metallurgyTierRecipes[metallurgyTier])
                {
                    var itemElementRecipe = ItemElementRecipe.Get(recipeIdentifier);
                    if (itemElementRecipe == null)
                    {
                        PlanItSystem.log.LogWarning($"Invalid recipe {recipeIdentifier}");
                        continue;
                    }
                    disabledRecipes.Add(itemElementRecipe.id);
                }
            }
            foreach (var recipeIdentifier in _metallurgyTierRecipes[_currentPlan.metallurgyTier])
            {
                var itemElementRecipe = ItemElementRecipe.Get(recipeIdentifier);
                if (itemElementRecipe == null)
                {
                    PlanItSystem.log.LogWarning($"Invalid recipe {recipeIdentifier}");
                    continue;
                }
                disabledRecipes.Remove(itemElementRecipe.id);
            }

            if (_currentPlan.cementTier == 0)
            {
                disabledRecipes.Add(ItemElementRecipe.Get("CR:_base_cement_reprocessed").id);
            }
            else
            {
                disabledRecipes.Add(ItemElementRecipe.Get("CR:_base_cement").id);
            }

            var sw = new Stopwatch();
            sw.Restart();
            sw.Start();
            var solver = new Solver(disabledRecipes);
            solver.FindSubGraphs();
            sw.Stop();
            PlanItSystem.log.Log($"FindSubGraphs: {sw.ElapsedMilliseconds}ms");

            var targets = new Dictionary<ItemElementTemplate, double>();
            var ignore = new HashSet<ItemElementTemplate>();
            foreach (var item in _currentPlan.inputs)
            {
                var itemElement = ItemElementTemplate.Get(item);
                if (itemElement.isValid)
                {
                    ignore.Add(itemElement);
                }
            }

            for (int outputIndex = 0; outputIndex < _currentPlan.outputs.Count; outputIndex++)
            {
                var outputElement = ItemElementTemplate.Get(_currentPlan.outputs[outputIndex]);
                if (outputElement.isValid)
                {
                    targets[outputElement] = _currentPlan.outputAmounts[outputIndex];
                }
            }

            sw.Restart();
            sw.Start();
            var result = solver.Solve(targets, ignore);
            sw.Stop();
            PlanItSystem.log.Log($"Solve: {sw.ElapsedMilliseconds}ms");
            //result.Dump();

            sw.Restart();
            sw.Start();
            result.SortRecipes();
            var (conveyor, conveyorSpeed) = ItemElementRecipe.ConveyorSpeeds[Mathf.Clamp(_currentPlan.conveyorTier, 0, ItemElementRecipe.ConveyorSpeeds.Count - 1)];
            _planContent.transform.DestroyAllChildren();
            var inputAmounts = new Dictionary<ItemElementTemplate, double>();
            foreach (var recipeAmount in result.EachRecipe())
            {
                var recipe = ItemElementRecipe.Get(recipeAmount.Key);

                //PlanItSystem.log.Log($"Recipe: {recipe.name} - {recipeAmount.Value}");

                if (recipe.producers.Count == 0)
                {
                    PlanItSystem.log.LogWarning($"Recipe {recipe.name} has no producers");
                    continue;
                }

                foreach (var input in recipe.inputs)
                {
                    var itemElement = input.itemElement;
                    if (itemElement.isValid)
                    {
                        var amount = 0.0;
                        if (inputAmounts.TryGetValue(itemElement, out var _amount))
                        {
                            amount = _amount;
                        }
                        inputAmounts[itemElement] = amount + recipeAmount.Value * input.amount;
                    }
                }

                if (recipe.inputs.Length > 0)
                {
                    foreach (var output in recipe.outputs)
                    {
                        var itemElement = output.itemElement;
                        if (itemElement.isValid)
                        {
                            var amount = 0.0;
                            if (inputAmounts.TryGetValue(itemElement, out var _amount))
                            {
                                amount = _amount;
                            }
                            inputAmounts[itemElement] = amount - recipeAmount.Value * output.amount;
                        }
                    }
                }

                var recipeRow = Instantiate(_recipeRowPrefab, _planContent.transform);

                foreach (var output in recipe.outputs)
                {
                    var itemElement = output.itemElement;

                    var icon = conveyor.icon;
                    var speed = conveyorSpeed;
                    if (itemElement.isElement)
                    {
                        icon = ItemElementRecipe.PipeItem.icon;
                        speed = 36000.0;
                    }

                    var outputPanel = Instantiate(_recipeOutputPanelPrefab, recipeRow.OutputsTransform);
                    outputPanel.Setup(itemElement.name, itemElement.icon, output.amount * recipeAmount.Value, icon, output.amount * recipeAmount.Value / speed);
                    outputPanel.onClicked += () => ToggleInput(itemElement.fullIdentifier);
                }

                foreach (var producer in recipe.producers)
                {
                    var producerAmount = (double)(recipeAmount.Value * recipe.time / (producer.speed * 60.0));

                    var producerPanel = Instantiate(_recipeMachinePanelPrefab, recipeRow.MachinesTransform);
                    producerPanel.Setup(producer.name, producer.icon, producerAmount, producer.powerUsage * producerAmount);
                }

                foreach (var input in recipe.inputs)
                {
                    var itemElement = input.itemElement;

                    var icon = conveyor.icon;
                    var speed = conveyorSpeed;
                    if (itemElement.isElement)
                    {
                        icon = ItemElementRecipe.PipeItem.icon;
                        speed = 36000.0;
                    }

                    var inputPanel = Instantiate(_recipeInputPanelPrefab, recipeRow.InputsTransform);
                    inputPanel.Setup(itemElement.name, itemElement.icon, input.amount * recipeAmount.Value, icon, input.amount * recipeAmount.Value / speed);
                    inputPanel.onClicked += () => { ToggleInput(itemElement.fullIdentifier); };
                }
            }
            sw.Stop();
            PlanItSystem.log.Log($"Update UI: {sw.ElapsedMilliseconds}ms");

            sw.Restart();
            sw.Start();
            foreach (var inputLabel in _inputPanels.Values) inputLabel.SetRate(0.0);
            _extraInputList.transform.DestroyAllChildren(2);
            _extraInputList.SetActive(false);
            foreach (var input in inputAmounts)
            {
                if (_inputPanels.TryGetValue(input.Key, out var inputLabel))
                {
                    inputLabel.SetRate(input.Value);
                }
                else if (input.Value > 0.001)
                {
                    var inputElement = input.Key;

                    var inputPanel = Instantiate(_inputPanelPrefab, _extraInputList.transform);
                    inputPanel.Setup($"Input - {inputElement.name}", inputElement.icon, input.Value);
                    inputPanel.onClicked += () => ToggleInput(inputElement.fullIdentifier);

                    _extraInputList.SetActive(true);
                }
            }

            _extraOutputList.transform.DestroyAllChildren(2);
            _extraOutputList.SetActive(false);
            foreach (var output in result.wasteAmounts)
            {
                if (output.Value > 0.001)
                {
                    var outputElement = output.Key;

                    var outputPanel = Instantiate(_inputPanelPrefab, _extraOutputList.transform);
                    outputPanel.Setup($"Output - {outputElement.name}", outputElement.icon, output.Value);

                    _extraOutputList.SetActive(true);
                }
            }
            sw.Stop();
            PlanItSystem.log.Log($"Update UI 2: {sw.ElapsedMilliseconds}ms");

            _planContent.SetActive(true);
        }

        public override void iec_triggerFrameClose()
        {
            Hide();
        }

        public override bool IsModal()
        {
            return true;
        }
    }
}
