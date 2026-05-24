using TMPro;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine;
using System.IO;
using UnityEngine.EventSystems;

namespace Duplicationer
{
    internal class SaveFrame : DuplicationerFrame
    {
        [Header("Save Frame")]
        [SerializeField] private TMP_InputField _searchInputField = null;
        [SerializeField] private GameObject _saveGridObject = null;
        [SerializeField] private GameObject _saveFramePreviewContainer = null;
        [SerializeField] private Image[] _saveFrameIconImages = new Image[4] { null, null, null, null };
        [SerializeField] private TMP_InputField _saveFrameNameInputField = null;
        [SerializeField] private TextMeshProUGUI _saveFrameMaterialReportText = null;
        [SerializeField] private Button _buttonSave = null;

        private TextMeshProUGUI _saveFramePreviewLabel = null;
        private Image[] _saveFramePreviewIconImages = new Image[4] { null, null, null, null };
        private ItemElementTemplate[] _saveFrameIconItemTemplates = new ItemElementTemplate[4] {
            ItemElementTemplate.Empty,
            ItemElementTemplate.Empty,
            ItemElementTemplate.Empty,
            ItemElementTemplate.Empty
        };
        private int _saveFrameIconCount = 0;

        private string _searchFilter = string.Empty;

        public string BlueprintName
        {
            get => _saveFrameNameInputField?.text ?? string.Empty;
            set {
                if (_saveFrameNameInputField != null) _saveFrameNameInputField.text = value;
            }
        }
        public ItemElementTemplate[] IconItemTemplates
        {
            get => _saveFrameIconItemTemplates;
            set => _saveFrameIconItemTemplates = value;
        }
        public int IconCount
        {
            get => _saveFrameIconCount;
            set => _saveFrameIconCount = value;
        }

        public void Show()
        {
            if (IsOpen) return;

            _searchFilter = string.Empty;
            _searchInputField.SetTextWithoutNotify(string.Empty);
            FilterSaveGrid();

            _tool.HideBlueprintFrame(true);
            _tool.HideLibraryFrame(true);
            _tool.HideFolderFrame(true);

            if (_tool.CurrentBlueprint != null)
            {
                if (_saveFrameNameInputField != null) _saveFrameNameInputField.text = _tool.CurrentBlueprint.Name;

                for (int i = 0; i < 4; i++) _saveFrameIconItemTemplates[i] = ItemElementTemplate.Empty;
                _tool.CurrentBlueprint.IconItemTemplates.CopyTo(_saveFrameIconItemTemplates, 0);
                _saveFrameIconCount = _tool.CurrentBlueprint.IconItemTemplates.Length;
            }

            FillSavePreview();
            FillSaveFrameIcons();
            FillSaveMaterialReport();

            Shown();

            EventSystem.current.SetSelectedGameObject(_saveFrameNameInputField.gameObject, null);
        }

        private void FillSaveMaterialReport()
        {
            int totalItemCount = 0;
            var materialReportBuilder = new System.Text.StringBuilder();
            foreach (var kv in _tool.CurrentBlueprint.ShoppingList)
            {
                var itemCount = kv.Value.count;
                if (itemCount > 0)
                {
                    totalItemCount += itemCount;
                    var name = kv.Value.name;
                    materialReportBuilder.AppendLine($"<color=#CCCCCC>{name}:</color> {itemCount}");
                }
            }

            if (totalItemCount > 0)
            {
                materialReportBuilder.AppendLine($"<color=#CCCCCC>Total:</color> {totalItemCount}");
            }

            _saveFrameMaterialReportText.text = materialReportBuilder.ToString();
        }

        internal void FillSavePreview()
        {
            _saveFramePreviewIconImages[0] = _saveFramePreviewIconImages[1] = _saveFramePreviewIconImages[2] = _saveFramePreviewIconImages[3] = null;

            switch (_saveFrameIconCount)
            {
                case 0:
                    {
                        _saveFramePreviewContainer.transform.DestroyAllChildren();
                        var gameObject = UnityEngine.Object.Instantiate(_tool.prefabBlueprintButtonDefaultIcon.Prefab, _saveFramePreviewContainer.transform);
                        var deleteButton = gameObject.transform.Find("DeleteButton")?.gameObject;
                        if (deleteButton != null) deleteButton.SetActive(false);
                        var renameButton = gameObject.transform.Find("RenameButton")?.gameObject;
                        if (renameButton != null) renameButton.SetActive(false);
                        _saveFramePreviewLabel = gameObject.GetComponentInChildren<TextMeshProUGUI>();
                        if (_saveFramePreviewLabel == null) throw new System.ArgumentNullException(nameof(_saveFramePreviewLabel));
                    }
                    break;

                case 1:
                    {
                        _saveFramePreviewContainer.transform.DestroyAllChildren();
                        var gameObject = UnityEngine.Object.Instantiate(_tool.prefabBlueprintButton1Icon.Prefab, _saveFramePreviewContainer.transform);
                        var deleteButton = gameObject.transform.Find("DeleteButton")?.gameObject;
                        if (deleteButton != null) deleteButton.SetActive(false);
                        var renameButton = gameObject.transform.Find("RenameButton")?.gameObject;
                        if (renameButton != null) renameButton.SetActive(false);
                        _saveFramePreviewLabel = gameObject.GetComponentInChildren<TextMeshProUGUI>();
                        if (_saveFramePreviewLabel == null) throw new System.ArgumentNullException(nameof(_saveFramePreviewLabel));
                        _saveFramePreviewIconImages[0] = gameObject.transform.Find("Icon1")?.GetComponent<Image>();
                    }
                    break;

                case 2:
                    {
                        _saveFramePreviewContainer.transform.DestroyAllChildren();
                        var gameObject = UnityEngine.Object.Instantiate(_tool.prefabBlueprintButton2Icon.Prefab, _saveFramePreviewContainer.transform);
                        var deleteButton = gameObject.transform.Find("DeleteButton")?.gameObject;
                        if (deleteButton != null) deleteButton.SetActive(false);
                        var renameButton = gameObject.transform.Find("RenameButton")?.gameObject;
                        if (renameButton != null) renameButton.SetActive(false);
                        _saveFramePreviewLabel = gameObject.GetComponentInChildren<TextMeshProUGUI>();
                        if (_saveFramePreviewLabel == null) throw new System.ArgumentNullException(nameof(_saveFramePreviewLabel));
                        _saveFramePreviewIconImages[0] = gameObject.transform.Find("Icon1")?.GetComponent<Image>();
                        _saveFramePreviewIconImages[1] = gameObject.transform.Find("Icon2")?.GetComponent<Image>();
                    }
                    break;

                case 3:
                    {
                        _saveFramePreviewContainer.transform.DestroyAllChildren();
                        var gameObject = UnityEngine.Object.Instantiate(_tool.prefabBlueprintButton3Icon.Prefab, _saveFramePreviewContainer.transform);
                        var deleteButton = gameObject.transform.Find("DeleteButton")?.gameObject;
                        if (deleteButton != null) deleteButton.SetActive(false);
                        var renameButton = gameObject.transform.Find("RenameButton")?.gameObject;
                        if (renameButton != null) renameButton.SetActive(false);
                        _saveFramePreviewLabel = gameObject.GetComponentInChildren<TextMeshProUGUI>();
                        if (_saveFramePreviewLabel == null) throw new System.ArgumentNullException(nameof(_saveFramePreviewLabel));
                        _saveFramePreviewIconImages[0] = gameObject.transform.Find("Icon1")?.GetComponent<Image>();
                        _saveFramePreviewIconImages[1] = gameObject.transform.Find("Icon2")?.GetComponent<Image>();
                        _saveFramePreviewIconImages[2] = gameObject.transform.Find("Icon3")?.GetComponent<Image>();
                    }
                    break;

                case 4:
                    {
                        _saveFramePreviewContainer.transform.DestroyAllChildren();
                        var gameObject = UnityEngine.Object.Instantiate(_tool.prefabBlueprintButton4Icon.Prefab, _saveFramePreviewContainer.transform);
                        _saveFramePreviewLabel = gameObject.GetComponentInChildren<TextMeshProUGUI>();
                        if (_saveFramePreviewLabel == null) throw new System.ArgumentNullException(nameof(_saveFramePreviewLabel));
                        var renameButton = gameObject.transform.Find("RenameButton")?.gameObject;
                        if (renameButton != null) renameButton.SetActive(false);
                        _saveFramePreviewIconImages[0] = gameObject.transform.Find("Icon1")?.GetComponent<Image>();
                        _saveFramePreviewIconImages[1] = gameObject.transform.Find("Icon2")?.GetComponent<Image>();
                        _saveFramePreviewIconImages[2] = gameObject.transform.Find("Icon3")?.GetComponent<Image>();
                        _saveFramePreviewIconImages[3] = gameObject.transform.Find("Icon4")?.GetComponent<Image>();
                    }
                    break;

                default:
                    break;
            }

            if (_saveFramePreviewLabel != null && _saveFrameNameInputField != null)
            {
                _saveFramePreviewLabel.text = Path.GetFileName(_saveFrameNameInputField.text);
            }

            for (int i = 0; i < _saveFrameIconCount; i++)
            {
                if (_saveFramePreviewIconImages[i] != null)
                {
                    _saveFramePreviewIconImages[i].sprite = _saveFrameIconItemTemplates[i].icon ?? _tool.iconEmpty.Sprite;
                }
            }
        }

        internal void FillSaveGrid()
        {
            if (_saveGridObject == null) return;

            _saveGridObject.transform.DestroyAllChildren();

            foreach (var itemTemplate in ItemElementTemplate.GatherAll())
            {
                if (itemTemplate.isHiddenItem) continue;

                var gameObject = Object.Instantiate(_tool.prefabBlueprintButtonIcon.Prefab, _saveGridObject.transform);
                gameObject.name = itemTemplate.fullIdentifier;

                var iconImage = gameObject.transform.Find("Icon1")?.GetComponent<Image>();
                if (iconImage != null) iconImage.sprite = itemTemplate.icon;

                var button = gameObject.GetComponentInChildren<Button>();
                if (button != null)
                {
                    button.onClick.AddListener(new UnityAction(() => SaveFrameAddIcon(itemTemplate)));
                }

                var panel = gameObject.GetComponent<Image>();
                if (panel != null) panel.color = Color.clear;
            }
        }

        internal void FilterSaveGrid()
        {
            if (_saveGridObject == null) return;

            foreach (Transform child in _saveGridObject.transform)
            {
                var itemTemplate = ItemElementTemplate.Get(child.gameObject.name);
                if (itemTemplate.isValid)
                {
                    child.gameObject.SetActive(string.IsNullOrWhiteSpace(_searchFilter) || itemTemplate.name.IndexOf(_searchFilter, System.StringComparison.OrdinalIgnoreCase) >= 0);
                }
            }
        }

        internal void FillSaveFrameIcons()
        {
            for (int i = 0; i < _saveFrameIconCount; i++)
            {
                if (_saveFrameIconImages[i] != null)
                {
                    _saveFrameIconImages[i].sprite = _saveFrameIconItemTemplates[i].icon ?? _tool.iconEmpty.Sprite;
                }
            }
            for (int i = _saveFrameIconCount; i < 4; i++)
            {
                if (_saveFrameIconImages[i] != null)
                {
                    _saveFrameIconImages[i].sprite = _tool.iconEmpty.Sprite;
                }
            }
        }

        private void SaveFrameAddIcon(ItemElementTemplate itemTemplate)
        {
            if (itemTemplate == null) throw new System.ArgumentNullException(nameof(itemTemplate));
            if (_saveFrameIconCount >= 4) return;

            AudioManager.playUISoundEffect(ResourceDB.resourceLinker.audioClip_UIButtonClick);

            _saveFrameIconItemTemplates[_saveFrameIconCount] = itemTemplate;
            _saveFrameIconCount++;

            FillSavePreview();
            FillSaveFrameIcons();
        }

        public void SaveFrameRemoveIcon(int iconIndex)
        {
            if (iconIndex >= _saveFrameIconCount) return;

            AudioManager.playUISoundEffect(ResourceDB.resourceLinker.audioClip_UIButtonClick);

            for (int i = iconIndex; i < 3; i++) _saveFrameIconItemTemplates[i] = _saveFrameIconItemTemplates[i + 1];
            _saveFrameIconItemTemplates[3] = ItemElementTemplate.Empty;
            _saveFrameIconCount--;

            FillSavePreview();
            FillSaveFrameIcons();
        }

        public void OnClick_GetInfoFromExistingBlueprint()
        {
            _tool.ShowLibraryFrame(this);
        }

        public void OnClick_Save()
        {
            _tool.FinishSaveBlueprint();
        }

        public void OnChange_SaveName(string value)
        {
            if (_saveFramePreviewLabel != null) _saveFramePreviewLabel.text = Path.GetFileName(value);
            _buttonSave.interactable = !string.IsNullOrWhiteSpace(value);
        }

        public void OnChange_Search(string value)
        {
            _searchFilter = value ?? string.Empty;
            FilterSaveGrid();
        }
    }
}
