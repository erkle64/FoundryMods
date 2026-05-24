using UnityEngine;
using UnityEngine.UI;

namespace VeinTools
{

    public class IconButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image iconImage;
        [SerializeField] private TooltipTrigger tooltipTrigger;

        public event System.Action onClick;

        public bool interactable
        {
            get => button.interactable;
            set => button.interactable = value;
        }

        public void Setup(Sprite icon, string tooltip)
        {
            iconImage.sprite = icon;
            tooltipTrigger.tooltipText = tooltip;
        }

        public void OnClick()
        {
            onClick?.Invoke();
        }
    }

}