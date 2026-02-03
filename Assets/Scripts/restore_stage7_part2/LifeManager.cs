using System.Collections.Generic;
using UnityEngine;

public class LifeManager : MonoBehaviour
{
    [Header("Life Settings")]
    public GameObject lifeCardPrefab; // 裏向きライフカード
    public Transform lifeParent;
    public float maxWidth = 6.0f;     // 配置エリアの最大幅（手札のような中心揃え）
    public float minSpacing = 0.6f;   // 詰まりすぎ防止の最小間隔

    [Header("Life Seal (Defence Window Use Lock)")]
    public GameObject lifeSealStatusImage;      // ライフゾーン封印中の表示（任意）
    public GameObject lifeSealMarkPrefab;       // 各ライフカード上の封印マーク（任意）
    public string lifeSealMarkChildName = "SealMark";

    private bool _lifeDefenceSealed = false;

    public bool IsLifeDefenceSealed => _lifeDefenceSealed;


    private List<GameObject> lifeCards = new List<GameObject>();
    private Dictionary<GameObject, CardGenerator.CardData> lifeDataDict = new Dictionary<GameObject, CardGenerator.CardData>();
    private CardGenerator.CardData lastDestroyedCard = null;

    // 現在のライフ枚数を外から参照するためのプロパティ
    public int LifeCount => lifeCards.Count;


    /// <summary>
    /// 初期ライフを山札からセットアップ（データのみドローして配置）
    /// </summary>
    public void SetupInitialLife(int lifeCount)
    {
        foreach (var card in lifeCards)
        {
            Destroy(card);
        }
        lifeCards.Clear();
        lifeDataDict.Clear();

        for (int i = 0; i < lifeCount; i++)
        {
            // ★ CardGenerator.Instance は存在しないので使わない
            // 既存の AddLife() は DeckManager から DrawCardDataOnly() して AddLife(data) する
            AddLife();
        }

        RearrangeLife();

        // ステータス表示（任意）
        if (lifeSealStatusImage != null)
            lifeSealStatusImage.SetActive(_lifeDefenceSealed);
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

        // ★封印中ならマークも付ける（後から増えたライフにも適用）
        ApplyLifeSealMarkToCard(card, _lifeDefenceSealed);

        // ステータス表示（任意）
        if (lifeSealStatusImage != null)
            lifeSealStatusImage.SetActive(_lifeDefenceSealed);

        RearrangeLife();
        // 例: 以前はここで AddLife(null) していた等があるならそのままでもOK
    }
    public void SetLifeDefenceSealed(bool sealedOn)
    {
        _lifeDefenceSealed = sealedOn;

        if (lifeSealStatusImage != null)
            lifeSealStatusImage.SetActive(_lifeDefenceSealed);

        // 既存ライフへ反映
        for (int i = 0; i < lifeCards.Count; i++)
        {
            ApplyLifeSealMarkToCard(lifeCards[i], _lifeDefenceSealed);
        }
    }
    private void ApplyLifeSealMarkToCard(GameObject card, bool show)
    {
        if (card == null) return;

        Transform markTr = null;
        if (!string.IsNullOrEmpty(lifeSealMarkChildName))
        {
            markTr = card.transform.Find(lifeSealMarkChildName);
        }

        GameObject markObj = (markTr != null) ? markTr.gameObject : null;

        // 子が無い場合はプレハブから生成（任意）
        if (markObj == null && lifeSealMarkPrefab != null)
        {
            markObj = Instantiate(lifeSealMarkPrefab, card.transform);
            markObj.name = lifeSealMarkChildName;
            markObj.transform.localScale = Vector3.one;
            markObj.transform.localPosition = Vector3.zero;
        }

        if (markObj != null)
            markObj.SetActive(show);
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

        float spacing = (count > 1)
            ? Mathf.Max(maxWidth / (count - 1), minSpacing)
            : 0f;

        float totalWidth = spacing * (count - 1);
        float startX = -totalWidth / 2f;

        for (int i = 0; i < count; i++)
        {
            Vector3 pos = new Vector3(startX + spacing * i, 0, 0);
            lifeCards[i].transform.localPosition = pos;
        }

        // ★ ライフが増減したので、このクライアントのUIを更新
        var gm = GameManager.Instance;
        if (gm != null)
        {
            gm.UpdateAllLifeUIForLocal();
        }
    }

}
