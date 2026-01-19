using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class CardDetailUI : MonoBehaviour
{
    public TMP_Text rubyText;
    public TMP_Text nameText;
    public Image cardImage;
    public TMP_Text costText;
    public TMP_Text typeText;
    //public TMP_Text rarityText;
    public TMP_Text textText;

    public void Show(CardInfo card)
    {
        rubyText.text = card.ruby;
        nameText.text = card.name;
        cardImage.sprite = card.sprite;
        costText.text = card.cost.ToString();
        typeText.text = card.type;
        //rarityText.text = card.rarity;
        textText.text = card.text;
    }
}
