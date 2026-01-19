using UnityEngine;
using UnityEngine.EventSystems;

public class CardDropArea : MonoBehaviour, IDropHandler
{
    public enum DropType { AddToDeck, RemoveFromDeck }
    public DropType dropType;
    public DeckEditorUI editor;

    public void OnDrop(PointerEventData eventData)
    {
        //var drag = eventData.pointerDrag?.GetComponent<CardDragHandler>();
        //if (drag == null) return;

        //if (dropType == DropType.AddToDeck)
        //    editor.AddCardToDeck(drag.card);
        //else
        //    editor.RemoveCardFromDeck(drag.card);
    }
}
