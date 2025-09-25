using UnityEngine;
using UnityEngine.EventSystems;

public class CardUIDetail : MonoBehaviour, IPointerClickHandler
{
    private CardGenerator.CardData cardData;

    public void Init(CardGenerator.CardData data)
    {
        cardData = data;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // �� ������[�h���͒Z�����ŏڍׂ��J���Ȃ�
        var dm = FindAnyObjectByType<DiscardManager>();
        if (dm != null && dm.IsRecoverMode) return;

        if (cardData == null)
        {
            Debug.LogError("cardData is NULL!");
            return;
        }
        CardDetailPanel.Instance?.Show(cardData);
    }

}
