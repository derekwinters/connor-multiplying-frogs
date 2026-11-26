using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Frogs.Unity.Views
{
    /// <summary>RED STUB (#520): the START sign's plank. Implemented in the next commit.</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class TitleSignButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public event Action Clicked;

        public RectTransform RectTransform => null;
        public Image TapTarget => null;
        public RectTransform VisualRoot => null;
        public Image OutlineImage => null;
        public Image WoodImage => null;
        public Text Label => null;
        public bool IsPressed => false;

        public void OnPointerDown(PointerEventData eventData) { }

        public void OnPointerUp(PointerEventData eventData) { Clicked?.Invoke(); }

        public void OnPointerExit(PointerEventData eventData) { }
    }
}
