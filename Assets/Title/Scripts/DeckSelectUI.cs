using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

public class DeckSelectUI : MonoBehaviour
{
    public TMP_Text messageText; // 「デッキがありません」表示用

    public void OnSelectDeck(int deckIndex)
    {
        // デッキ存在チェック
        if (!DeckSaveManager.Instance.HasDeckData(deckIndex))
        {
            messageText.text = "デッキデータがないので選択できません";
            return;
        }

        // 選択
        DeckSaveManager.Instance.SetSelectedDeck(deckIndex);
        StartCoroutine(HideMessageAfterSeconds(1f));
    }

    IEnumerator HideMessageAfterSeconds(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        messageText.text = "";

    }
}
