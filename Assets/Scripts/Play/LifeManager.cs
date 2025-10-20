using System.Collections.Generic;
using UnityEngine;

public class LifeManager : MonoBehaviour
{
    [Header("Life Settings")]
    public GameObject lifeCardPrefab; // 裏向きライフカード
    public Transform lifeParent;
    public float maxWidth = 6.0f;     // 配置エリアの最大幅（手札のような中心揃え）
    public float minSpacing = 0.6f;   // 詰まりすぎ防止の最小間隔

    private List<GameObject> lifeCards = new List<GameObject>();
    private Dictionary<GameObject, CardGenerator.CardData> lifeDataDict = new Dictionary<GameObject, CardGenerator.CardData>();
    private CardGenerator.CardData lastDestroyedCard = null;

    /// <summary>
    /// 初期ライフを山札からセットアップ（データのみドローして配置）
    /// </summary>
    public void SetupInitialLife(int count, DeckManager deckManager)
    {
        if (deckManager == null)
        {
            Debug.LogError("LifeManager.SetupInitialLife: deckManagerが未設定です。");
            return;
        }

        // 既存をクリア
        foreach (var card in lifeCards)
            Destroy(card);
        lifeCards.Clear();
        lifeDataDict.Clear();

        // 指定枚数ぶんデータのみ引いて裏向きライフとして配置
        for (int i = 0; i < count; i++)
        {
            var data = deckManager.DrawCardDataOnly();
            if (data == null) break;
            AddLife(data);
        }

        RearrangeLife();
    }

    /// <summary>
    /// 指定のカードデータでライフを1枚追加（外部からデータが渡される場合に使用）
    /// </summary>
    public void AddLife(CardGenerator.CardData data)
    {
        if (data == null) return;

        GameObject card = Instantiate(lifeCardPrefab, lifeParent);
        card.transform.localScale = Vector3.one;
        card.name = "LifeCard_" + data.id;

        lifeCards.Add(card);
        lifeDataDict[card] = data;

        RearrangeLife();
        // ★ 修正: 以前はここで AddLife(null) を呼んでいたが削除（無限再帰防止）
    }

    /// <summary>
    /// 山札からデータのみ引いてライフを1枚追加（Block効果のLifeAdd等で使用）
    /// </summary>
    public void AddLife()
    {
        // ★ 修正: DeckManagerをFindして自動取得する形に変更（外部参照維持）
        var deckManager = FindAnyObjectByType<DeckManager>();
        if (deckManager == null)
        {
            Debug.LogWarning("LifeManager.AddLife(): DeckManager が見つかりません。ライフ追加をスキップします。");
            return;
        }

        var data = deckManager.DrawCardDataOnly();
        if (data == null)
        {
            Debug.LogWarning("LifeManager.AddLife(): 山札が空のためライフを追加できません。");
            return;
        }

        AddLife(data);
    }

    /// <summary>
    /// 末尾のライフを1枚破壊し、そのデータを返す
    /// </summary>
    public CardGenerator.CardData RemoveLife()
    {
        if (lifeCards.Count == 0) return null;

        GameObject last = lifeCards[lifeCards.Count - 1];
        lifeCards.RemoveAt(lifeCards.Count - 1);

        CardGenerator.CardData destroyedData = null;
        if (lifeDataDict.ContainsKey(last))
        {
            destroyedData = lifeDataDict[last];
            lifeDataDict.Remove(last);
        }

        Destroy(last);
        RearrangeLife();

        lastDestroyedCard = destroyedData;
        return destroyedData;
    }

    /// <summary>
    /// 直近に破壊されたライフカードのデータを取得
    /// </summary>
    public CardGenerator.CardData GetDestroyedCard()
    {
        return lastDestroyedCard;
    }

    /// <summary>
    /// ライフの横一列配置を自動調整（中央寄せ）
    /// </summary>
    private void RearrangeLife()
    {
        int count = lifeCards.Count;
        if (count == 0) return;

        // 自動spacing計算
        float spacing = (count > 1)
            ? Mathf.Max(maxWidth / (count - 1), minSpacing)
            : 0f;

        // 合計幅を計算して中央寄せ
        float totalWidth = spacing * (count - 1);
        float startX = -totalWidth / 2f;

        for (int i = 0; i < count; i++)
        {
            Vector3 pos = new Vector3(startX + spacing * i, 0, 0);
            lifeCards[i].transform.localPosition = pos;
        }
    }
}
