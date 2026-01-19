using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DeckCardItem : MonoBehaviour
{
    public Image icon;
    public TMP_Text nameText;

    public CardInfo card;
    DeckEditorUI editor;

    public void Setup(CardInfo c, DeckEditorUI e)
    {
        card = c;
        editor = e;
        icon.sprite = c.sprite;
        nameText.text = c.name;
    }

    public void OnClick()
    {
        // ÉfÉbÉLÇ©ÇÁçÌèú
        editor.RemoveCardFromDeck(card);
    }
}
