using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CardDisplayImageOnly : MonoBehaviour, IPointerClickHandler
{
    Image image;
    CardInfo card;
    DeckEditorUI editor;

    void Awake()
    {
        image = GetComponent<Image>();
    }

    public void SetCard(CardInfo info, DeckEditorUI ui = null)
    {
        card = info;
        editor = ui;

        if (image != null)
            image.sprite = info.sprite;
    }

    // ★右クリック検知
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            // 右クリックは「詳細表示のみ」
            if (editor != null && card != null)
            {
                editor.ShowDetail(card);
            }
        }
    }
}
