using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VeinTools
{
    public class ItemSelectFrame : UIFrame
    {
        [Header("Item Select Frame")]
        [SerializeField] private IconButton _itemSelectButtonPrefab;
        [SerializeField] private Transform _itemListTransform;
        [SerializeField] private TMP_InputField _filterInputField;

        public delegate void OnConfirmDelegate(ItemTemplate result);
        public delegate void OnCancelDelegate();

        private OnConfirmDelegate _onConfirm;
        private OnCancelDelegate _onCancel;
        private ItemTemplate _result = null;
        private Dictionary<ItemTemplate, IconButton> _itemButtons = new();

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

        private static readonly string[] _allowedFillItems = new[]
        {
            "_base_building_part",
            "_base_concrete",
            "_base_construction_hazard_block",
            "_base_dirt",
            "_base_stone",
            "_base_sand",
            "_base_rocky_desert_dirt",
            "_base_tropical_rainforest_dirt",
            "_base_tundra_dirt",
            "_base_decor_base",
            "_base_office_blocks",
            "_base_office_blocks_deluxe",
            "_base_office_marble_black_deluxe",
            "_base_office_marble_white_deluxe"
        };
        public void BuildContent()
        {
            var categories = ItemTemplateManager.getCraftingRecipeCategoryDictionary();
            foreach (var itemIdentifier in _allowedFillItems)
            {
                ItemTemplate item = ItemTemplateManager.getItemTemplate(itemIdentifier);
                if (item == null)
                    continue;

                if (!item.flags.HasFlagNonAlloc(ItemTemplate.ItemTemplateFlags.BUILDABLE_OBJECT))
                    continue;

                if (item.buildableObjectTemplate == null)
                    continue;

                if (item.isHiddenItem)
                    continue;

                if (System.Array.IndexOf(_allowedFillItems, item.identifier) < 0)
                    continue;

                if (item.buildableObjectTemplate.size != Vector3Int.one)
                    continue;

                var itemSelectButton = Instantiate(_itemSelectButtonPrefab, _itemListTransform);
                itemSelectButton.Setup(item.icon, item.name);
                itemSelectButton.onClick += () =>
                {
                    _result = item;
                    Hide(true);
                };
                _itemButtons[item] = itemSelectButton;
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
