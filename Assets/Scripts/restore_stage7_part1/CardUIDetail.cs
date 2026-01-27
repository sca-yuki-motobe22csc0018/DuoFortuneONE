using UnityEngine;
using UnityEngine.EventSystems;

public class CardUIDetail : MonoBehaviour, IPointerClickHandler
{
    private CardGenerator.CardData cardData;

    public void Init(CardGenerator.CardData data)
    {
        if (data == null)
        {
            return;
        }

        cardData = data;
    }
    public void OnPointerClick(PointerEventData eventData)
    {
        // ★ 回収モード中は短押しで詳細を開かない
        // ★ 回収モード中は左クリック短押しで詳細を開かない（右クリックはOK）
        var dm = FindAnyObjectByType<DiscardManager>();
        if (eventData.button == PointerEventData.InputButton.Left && dm != null && dm.IsRecoverMode) return;


        if (cardData == null)
        {
            Debug.LogError("cardData is NULL!");
            return;
        }
        CardDetailPanel.Instance?.Show(cardData);
    }

}
