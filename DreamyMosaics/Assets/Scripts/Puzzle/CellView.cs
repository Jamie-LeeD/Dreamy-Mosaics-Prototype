using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace DreamyMosaics.Puzzle
{
    public sealed class CellView : MonoBehaviour, IPointerClickHandler
    {
        public int Index;
        public Image Frame;
        public Image Fill;
        public Image Wash;
        public Image CatImage;
        public TextMeshProUGUI Mark;
        public Action<CellView, PointerEventData> Clicked;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (Clicked != null)
                Clicked(this, eventData);
        }

        public void Apply(Color frame, Color fill, Color wash, bool showCat, Color catTint, string mark, Color markColor, float markSize)
        {
            if (Frame != null)
                Frame.color = frame;
            if (Fill != null)
                Fill.color = fill;
            if (Wash != null)
            {
                Wash.color = wash;
                Wash.enabled = wash.a > 0.01f;
            }

            if (CatImage != null)
            {
                CatImage.enabled = showCat;
                CatImage.color = catTint;
            }

            if (Mark != null)
            {
                Mark.text = mark;
                Mark.color = markColor;
                Mark.fontSize = markSize;
                Mark.enabled = !string.IsNullOrEmpty(mark);
            }
        }
    }
}
