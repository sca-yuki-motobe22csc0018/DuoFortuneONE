using System.Collections.Generic;
using UnityEngine;

public class Test : MonoBehaviour
{

    public void OnClick()
    {
        DeckData deck = DeckSaveManager.Instance.GetSelectedDeck();

        if (deck == null)
        {
            Debug.LogError("デッキが選択されていません");
            return;
        }

        foreach (var cardNumber in deck.cardNumbers)
        {
            Debug.Log("カード番号：" + cardNumber);
        }

    }
}
