using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PlanIt
{
    internal class ItemSelectFrame : UIFrame
    {
        [Header("Item Select Frame")]
        [SerializeField] private IconButton _itemSelectButtonPrefab;
        [SerializeField] private Transform _itemListTransform;
        [SerializeField] private TMP_InputField _filterInputField;

        public delegate void OnConfirmDelegate(ItemElementTemplate result);
        public delegate void OnCancelDelegate();

        private OnConfirmDelegate _onConfirm;
        private OnCancelDelegate _onCancel;
        private ItemElementTemplate _result = ItemElementTemplate.Empty;
        private Dictionary<ItemElementTemplate, IconButton> _itemButtons = new();

        public bool IsOpen => gameObject.activeSelf;

        bool _selectFilterInputField = true;
        void Update()
        {
            if (_selectFilterInputField == true)
            {
                // select input field
                _filterInputField.gameObject.GetComponent<Selectable>().select_advanced(EventSystem.current);
                _selectFilterInputField = false;
            }
        }

        public void Show(OnConfirmDelegate onConfirm, OnCancelDelegate onCancel = null)
        {
            _onConfirm = onConfirm;
            _onCancel = onCancel;
            _selectFilterInputField = true;
            _filterInputField.text = string.Empty;

            gameObject.SetActive(true);
            AudioManager.playUISoundEffect(ResourceDB.resourceLinker.audioClip_UIOpen);
            GlobalStateManager.addCursorRequirement();
        }

        public void Hide(bool result)
        {
            if (IsOpen)
            {
                gameObject.SetActive(false);
                AudioManager.playUISoundEffect(ResourceDB.resourceLinker.audioClip_UIClose);
                GlobalStateManager.removeCursorRequirement();

                if (result) _onConfirm?.Invoke(_result);
                else _onCancel?.Invoke();
            }
        }

        public void BuildContent()
        {
            var done = new HashSet<ItemElementTemplate>();

            var categories = ItemTemplateManager.getCraftingRecipeCategoryDictionary();
            foreach (var category in categories.Values)
            {
                foreach (var rowGroup in category.list_rowGroups)
                {
                    foreach (var recipe in rowGroup.list_recipes)
                    {
                        var itemElement = ItemElementTemplate.Empty;

                        foreach (var output in recipe.output_elemental)
                        {
                            itemElement = new ItemElementTemplate(output.Key);
                            break;
                        }

                        foreach (var output in recipe.output)
                        {
                            itemElement = new ItemElementTemplate(output.itemTemplate);
                            break;
                        }

                        if (itemElement.isValid && !done.Contains(itemElement))
                        {
                            done.Add(itemElement);

                            var itemSelectButton = Instantiate(_itemSelectButtonPrefab, _itemListTransform);
                            itemSelectButton.Setup(itemElement.icon, itemElement.name);
                            itemSelectButton.onClick += () =>
                            {
                                _result = itemElement;
                                Hide(true);
                            };
                            _itemButtons[itemElement] = itemSelectButton;
                        }
                    }
                }
            }

            foreach (var recipe in ItemElementRecipe.AllRecipes)
            {
                var itemElement = ItemElementTemplate.Empty;
                if (recipe.outputs.Length > 0 && recipe.inputs.Length > 0)
                {
                    itemElement = recipe.outputs[0].itemElement;

                    if (!done.Contains(itemElement))
                    {
                        done.Add(itemElement);

                        var itemSelectButton = Instantiate(_itemSelectButtonPrefab, _itemListTransform);
                        itemSelectButton.Setup(itemElement.icon, itemElement.name);
                        itemSelectButton.onClick += () =>
                        {
                            _result = itemElement;
                            Hide(true);
                        };
                        _itemButtons[itemElement] = itemSelectButton;
                    }
                }
            }
        }

        public void OnFilterChanged()
        {
            var filter = _filterInputField.text.ToLowerInvariant();

            FilterItems(filter);
        }

        private void FilterItems(string filter)
        {
            if (string.IsNullOrEmpty(filter))
            {
                // show all
                foreach (var kvp in _itemButtons)
                {
                    var button = kvp.Value;
                    button.gameObject.SetActive(true);
                }
                return;
            }

            foreach (var kvp in _itemButtons)
            {
                var itemElement = kvp.Key;
                var button = kvp.Value;
                if (string.IsNullOrEmpty(filter) ||
                    itemElement.name.ToLowerInvariant().Contains(filter) ||
                    itemElement.identifier.ToLowerInvariant().Contains(filter))
                {
                    button.gameObject.SetActive(true);
                }
                else
                {
                    button.gameObject.SetActive(false);
                }
            }
        }

        public override void iec_triggerFrameClose()
        {
            Hide(false);
        }

        public override bool IsModal()
        {
            return true;
        }
    }
}
