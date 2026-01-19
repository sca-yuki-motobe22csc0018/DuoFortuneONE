using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CardDragHandler : MonoBehaviour//, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    //public CardInfo card;
    //public DeckEditorUI editor;

    //private Image image;
    //private Transform originalParent;

    //void Start()
    //{
    //    image = GetComponent<Image>();
    //}

    //public void OnBeginDrag(PointerEventData eventData)
    //{
    //    originalParent = transform.parent;
    //    transform.SetParent(editor.transform, true);
    //    image.raycastTarget = false;
    //}

    //public void OnDrag(PointerEventData eventData)
    //{
    //    transform.position = Input.mousePosition;
    //}

    //public void OnEndDrag(PointerEventData eventData)
    //{
    //    image.raycastTarget = true;
    //    transform.SetParent(originalParent, true);
    //}
}
