using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DeckPreviewUI : MonoBehaviour
{
    [Header("UI")]
    public Transform previewContent;          // ScrollView の Content
    public GameObject cardImagePrefab;         // CardDisplayImageOnly付きPrefab
    public TMP_Text messageText;               // エラーメッセージ表示用（任意）

    //--------------------------------------------------
    // デッキ1?3 ボタンから呼ばれる
    //--------------------------------------------------
    public void OnSelectDeck(int deckIndex)
    {
        // デッキが存在するかチェック
        if (!DeckSaveManager.Instance.HasDeckData(deckIndex))
        {
            ShowMessage("デッキデータが無いので選択できません");
            ClearPreview();
            return;
        }

        // 選択デッキを保存（バトル用）
        DeckSaveManager.Instance.SetSelectedDeck(deckIndex);

        // プレビュー表示
        ShowDeckPreview(deckIndex);
    }

    //--------------------------------------------------
    // デッキ内容表示
    //--------------------------------------------------
    void ShowDeckPreview(int deckIndex)
    {
        ClearPreview();

        var deck = DeckSaveManager.Instance.GetDeck(deckIndex);
        if (deck == null) return;

        foreach (var number in deck.cardNumbers)
        {
            var info = CardDatabase.Instance.GetCard(number);
            if (info == null) continue;

            var obj = Instantiate(cardImagePrefab, previewContent);
            obj.GetComponent<CardDisplayImageOnly>().SetCard(info);
        }
    }

    //--------------------------------------------------
    // 表示クリア
    //--------------------------------------------------
    void ClearPreview()
    {
        foreach (Transform t in previewContent)
            Destroy(t.gameObject);
    }

    //--------------------------------------------------
    // メッセージ表示（3秒で消える）
    //--------------------------------------------------
    void ShowMessage(string text)
    {
        if (messageText == null) return;

        messageText.text = text;
        CancelInvoke(nameof(ClearMessage));
        Invoke(nameof(ClearMessage), 3f);
    }

    void ClearMessage()
    {
        if (messageText != null)
            messageText.text = "";
    }
}
