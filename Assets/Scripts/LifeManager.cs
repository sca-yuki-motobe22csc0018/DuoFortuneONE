using System.Collections.Generic;
using UnityEngine;

public class LifeManager : MonoBehaviour
{
    [Header("Life Settings")]
    public GameObject lifeCardPrefab; // カード裏面のプレハブ
    public Transform lifeParent;      // ライフを並べる親オブジェクト
    public float spacing = 1.0f;      // 横並びの間隔

    private List<GameObject> lifeCards = new List<GameObject>();

    /// <summary>
    /// ゲーム開始時に初期ライフを配置
    /// </summary>
    public void SetupInitialLife(int count)
    {
        // 既存のライフを削除
        foreach (var card in lifeCards)
        {
            Destroy(card);
        }
        lifeCards.Clear();

        // ライフを横並びで生成
        for (int i = 0; i < count; i++)
        {
            GameObject card = Instantiate(lifeCardPrefab, lifeParent);
            card.transform.localScale = Vector3.one;

            // 横に並べる
            card.transform.localPosition = new Vector3(i * spacing, 0, 0);

            lifeCards.Add(card);
        }

        // 中央ぞろえ
        float totalWidth = (count - 1) * spacing;
        foreach (var card in lifeCards)
        {
            card.transform.localPosition -= new Vector3(totalWidth / 2f, 0, 0);
        }
    }

    /// <summary>
    /// ライフを1増やす
    /// </summary>
    public void AddLife()
    {
        GameObject card = Instantiate(lifeCardPrefab, lifeParent);
        card.transform.localScale = Vector3.one;

        lifeCards.Add(card);
        RearrangeLife();
    }

    /// <summary>
    /// ライフを1減らす
    /// </summary>
    public void RemoveLife()
    {
        if (lifeCards.Count == 0) return;

        GameObject last = lifeCards[lifeCards.Count - 1];
        lifeCards.RemoveAt(lifeCards.Count - 1);
        Destroy(last);

        RearrangeLife();
    }

    /// <summary>
    /// 横並びに再配置
    /// </summary>
    private void RearrangeLife()
    {
        int count = lifeCards.Count;
        for (int i = 0; i < count; i++)
        {
            lifeCards[i].transform.localPosition = new Vector3(i * spacing, 0, 0);
        }

        float totalWidth = (count - 1) * spacing;
        foreach (var card in lifeCards)
        {
            card.transform.localPosition -= new Vector3(totalWidth / 2f, 0, 0);
        }
    }
}
