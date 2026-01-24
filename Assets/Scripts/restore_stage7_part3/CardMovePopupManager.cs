using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CardMovePopupManager : MonoBehaviour
{
    public static CardMovePopupManager Instance;

    [Header("Common")]
    public GameObject uiCardPrefab; // UICard.Prefab
    public float drawShowSeconds = 1.2f;
    public float recoverShowSeconds = 1.5f;
    public float discardShowSeconds = 1.8f;

    [Header("Draw (local only)")]
    public GameObject drawRoot;
    public Transform drawContent;
    public float drawCardScale = 1.15f;

    [Header("Recover (both)")]
    public GameObject recoverRoot;
    public Transform recoverContent;
    public float recoverCardScale = 1.15f;

    [Header("Discard (both)")]
    public GameObject discardRoot;
    public Transform discardContent; // ScrollRectのContent想定（GridLayoutGroup等）
    public float discardCardScale = 0.65f;

    [Header("Layout Spacing (optional)")]
    public float drawSpacing = 40f;
    public float recoverSpacing = 40f;
    public float discardGridSpacingX = 10f;
    public float discardGridSpacingY = 10f;

    private Coroutine drawCo;
    private Coroutine recoverCo;
    private Coroutine discardCo;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        HideAllImmediate();
    }

    public void HideAllImmediate()
    {
        if (drawRoot) drawRoot.SetActive(false);
        if (recoverRoot) recoverRoot.SetActive(false);
        if (discardRoot) discardRoot.SetActive(false);

        ClearChildren(drawContent);
        ClearChildren(recoverContent);
        ClearChildren(discardContent);
    }

    // ----------------------------
    // Public API
    // ----------------------------

    // 引いたカード（自分だけ表示したいので、呼ぶ側でHasInputAuthorityチェックする）
    public void ShowDrawCards(int[] cardIds)
    {
        if (cardIds == null || cardIds.Length == 0) return;
        if (uiCardPrefab == null) return;
        if (drawRoot == null || drawContent == null) return;

        if (drawCo != null) StopCoroutine(drawCo);
        drawCo = StartCoroutine(ShowRoutine_Simple(drawRoot, drawContent, cardIds, drawCardScale, drawShowSeconds, groupCounts: false));
    }

    // 捨て札から回収（両者に見せたい）
    public void ShowRecoverCards(int[] cardIds)
    {
        if (cardIds == null || cardIds.Length == 0) return;
        if (uiCardPrefab == null) return;
        if (recoverRoot == null || recoverContent == null) return;

        if (recoverCo != null) StopCoroutine(recoverCo);
        recoverCo = StartCoroutine(ShowRoutine_Simple(recoverRoot, recoverContent, cardIds, recoverCardScale, recoverShowSeconds, groupCounts: false));
    }

    // 手札から捨てる（大量になり得るので small + 表(グリッド)想定、同IDは枚数表示でまとめる）
    public void ShowDiscardCards(int[] cardIds)
    {
        if (cardIds == null || cardIds.Length == 0) return;
        if (uiCardPrefab == null) return;
        if (discardRoot == null || discardContent == null) return;

        if (discardCo != null) StopCoroutine(discardCo);
        discardCo = StartCoroutine(ShowRoutine_Simple(discardRoot, discardContent, cardIds, discardCardScale, discardShowSeconds, groupCounts: true));
    }

    // ----------------------------
    // Internal
    // ----------------------------

    private IEnumerator ShowRoutine_Simple(GameObject root, Transform content, int[] cardIds, float scale, float seconds, bool groupCounts)
    {
        // 表示
        root.SetActive(true);

        // ▼追加：Contentのレイアウト間隔をスクリプトから反映（任意）
        var hlg = content.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        if (hlg != null)
        {
            if (root == drawRoot) hlg.spacing = drawSpacing;
            if (root == recoverRoot) hlg.spacing = recoverSpacing;
        }

        var glg = content.GetComponent<UnityEngine.UI.GridLayoutGroup>();
        if (glg != null)
        {
            glg.spacing = new Vector2(discardGridSpacingX, discardGridSpacingY);
        }


        // 既存クリア
        ClearChildren(content);

        if (groupCounts)
        {
            // 同IDはまとめて count 表示（順序は「最初に出た順」）
            var orderedUnique = new List<int>();
            var countMap = new Dictionary<int, int>();

            foreach (var id in cardIds)
            {
                if (!countMap.ContainsKey(id))
                {
                    countMap[id] = 0;
                    orderedUnique.Add(id);
                }
                countMap[id]++;
            }

            foreach (var id in orderedUnique)
            {
                int count = countMap[id];
                SpawnUICard(content, id, count, scale);
            }
        }
        else
        {
            // そのまま並べる（最大3枚想定）
            foreach (var id in cardIds)
            {
                SpawnUICard(content, id, 1, scale);
            }
        }

        // 一定時間待って消す（Time.timeScaleの影響を受けない）
        yield return new WaitForSecondsRealtime(seconds);

        // 消す
        root.SetActive(false);
        ClearChildren(content);
    }

    private void SpawnUICard(Transform parent, int cardId, int count, float scale)
    {
        var data = GetCardData(cardId);
        if (data == null) return;

        var go = Instantiate(uiCardPrefab, parent);
        go.transform.localScale = Vector3.one * scale;

        // UICard.Prefabに CardUI が付いている前提
        var ui = go.GetComponent<CardUI>();
        if (ui != null)
        {
            // 第3引数(DiscardManager等)はここでは不要なのでnull
            ui.SetCard(data, count, null, CardUISource.HandZone);
        }
    }

    private CardGenerator.CardData GetCardData(int id)
    {
        if (GameManager.Instance == null) return null;
        if (GameManager.Instance.deckManager == null) return null;
        return GameManager.Instance.deckManager.GetCardDataById(id);
    }

    private void ClearChildren(Transform t)
    {
        if (t == null) return;
        for (int i = t.childCount - 1; i >= 0; i--)
        {
            Destroy(t.GetChild(i).gameObject);
        }
    }
}
