using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

public class DeckSelectUI : MonoBehaviour
{
    public TMP_Text messageText; // 「デッキがありません」表示用
    public GameObject SelectPanel;
    public TMP_Text selectedDeckText; // ★追加：「デッキ1」みたいに常時表示する用

    private void Start()
    {
        RefreshSelectedDeckLabel(); // ★追加
    }

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

        RefreshSelectedDeckLabel(); // ★追加：常時表示テキスト更新

        messageText.text = "デッキ" + (deckIndex+1) + "を選択しました";
        StartCoroutine(HideMessageAfterSeconds(1f));
    }

    public void Open()
    {
        SelectPanel.SetActive(true);
        RefreshSelectedDeckLabel(); // ★追加
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

    private void RefreshSelectedDeckLabel()
    {
        if (selectedDeckText == null) return;
        if (DeckSaveManager.Instance == null)
        {
            selectedDeckText.text = "";
            return;
        }

        int idx = DeckSaveManager.Instance.GetSelectedDeckIndex(); // -1なら未選択 :contentReference[oaicite:2]{index=2}
        if (idx < 0)
        {
            selectedDeckText.text = ""; // 未選択なら空表示（好みで「未選択」でもOK）
            return;
        }

        selectedDeckText.text = "デッキ" + (idx + 1); // ←要望どおり「デッキ1/2/3」
    }

}
