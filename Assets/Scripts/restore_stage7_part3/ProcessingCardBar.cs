using System.Collections.Generic;
using UnityEngine;

public class ProcessingCardBar : MonoBehaviour
{
    public static ProcessingCardBar Instance;

    [Header("UI")]
    public GameObject root;              // 0件の時はOFFにする親（ProcessingCardBarRoot）
    public Transform content;            // ScrollView/Viewport/Content
    public GameObject uiCardPrefab;      // UICard.Prefab

    [Header("Managers")]
    public DeckManager deckManager;      // カードID→CardData変換用

    // processId -> spawned ui
    private readonly Dictionary<int, GameObject> spawned = new Dictionary<int, GameObject>();

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        RefreshRoot();
    }

    private void RefreshRoot()
    {
        if (root != null)
            root.SetActive(spawned.Count > 0);
    }

    public void AddProcessingCard(int processId, int cardId)
    {
        if (spawned.ContainsKey(processId)) return;
        if (content == null || uiCardPrefab == null) return;

        if (deckManager == null) deckManager = FindAnyObjectByType<DeckManager>();
        if (deckManager == null) return;

        var data = deckManager.GetCardDataById(cardId);
        if (data == null) return;

        var go = Instantiate(uiCardPrefab, content, false);
        go.name = $"Processing_{processId}_{data.name}";

        // 新しいものほど左（先頭）
        go.transform.SetAsFirstSibling();

        var ui = go.GetComponent<CardUI>();
        if (ui != null)
        {
            // discardManagerは不要（回収モード移動をさせない）
            ui.SetCard(data, 1, null, CardUISource.HandZone);
        }

        spawned.Add(processId, go);
        RefreshRoot();
    }

    public void RemoveProcessingCard(int processId)
    {
        if (!spawned.TryGetValue(processId, out var go)) return;

        spawned.Remove(processId);
        if (go != null) Destroy(go);

        RefreshRoot();
    }
}
