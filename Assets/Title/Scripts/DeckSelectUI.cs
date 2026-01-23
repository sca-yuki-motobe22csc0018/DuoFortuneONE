using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

public class DeckSelectUI : MonoBehaviour
{
    public TMP_Text messageText; // 「デッキがありません」表示用
    public GameObject SelectPanel;

    public void OnSelectDeck(int deckIndex)
    {
        // デッキ存在チェック
        if (!DeckSaveManager.Instance.HasDeckData(deckIndex))
        {
            messageText.text = "デッキデータがないので選択できません";
            StartCoroutine(HideMessageAfterSeconds(1f));
            return;
        }

        // 選択
        DeckSaveManager.Instance.SetSelectedDeck(deckIndex);
        messageText.text = "デッキ" + (deckIndex+1) + "を選択しました";
        StartCoroutine(HideMessageAfterSeconds(1f));
    }

    public void Open()
    {
        SelectPanel.SetActive(true);
    }

    public void Close()
    {
        SelectPanel.SetActive(false);
    }
    IEnumerator HideMessageAfterSeconds(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        messageText.text = "";

    }
}
