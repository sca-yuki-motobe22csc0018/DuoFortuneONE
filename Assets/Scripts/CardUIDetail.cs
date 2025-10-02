using UnityEngine;
using TMPro;

public class CardUIDetail : MonoBehaviour
{
    private CardGenerator.CardData cardData;

    public TMP_Text nameText;
    public TMP_Text typeText;
    public TMP_Text costText;
    public TMP_Text rarityText;
    public TMP_Text descriptionText;

    public void Init(CardGenerator.CardData data)
    {
        cardData = data;
        if (nameText != null) nameText.text = data.name;
        if (typeText != null) typeText.text = data.type;
        if (costText != null) costText.text = data.cost.ToString();
        if (rarityText != null) rarityText.text = data.rarity;
        if (descriptionText != null) descriptionText.text = data.text;
    }

    public CardGenerator.CardData GetData()
    {
        return cardData;
    }
}
