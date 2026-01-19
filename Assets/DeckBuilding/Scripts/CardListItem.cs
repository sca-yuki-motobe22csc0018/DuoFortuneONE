using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CardListItem : MonoBehaviour
{
    public Image icon;
    public TMP_Text nameText;
    public TMP_Text costText;

    CardInfo card;
    DeckEditorUI editor;

    public void Setup(CardInfo info, DeckEditorUI ed)
    {
        card = info;
        editor = ed;

        icon.sprite = info.sprite;
        nameText.text = info.name;
        costText.text = info.cost.ToString();
    }

    public void OnClick()
    {
        editor.AddCardToDeck(card);
    }
}
