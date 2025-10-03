using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class DeckManager : MonoBehaviour
{
    [Header("Card Prefab")]
    public GameObject cardPrefab;

    [Header("Deck CSV Files")]
    public string defaultDeckCSV = "DefaultDeck.csv";
    public string player1DeckCSV = "Player1Deck.csv";
    public string player2DeckCSV = "Player2Deck.csv";

    private Stack<int> deckStack = new Stack<int>();

    [Header("Draw Settings")]
    public Vector3 drawPosition = Vector3.zero;
    public float cardScale = 0.33f;

    void Start()
    {
        InitializeDeck();
    }

    /// <summary>
    /// CSVを読み込んでデッキを作成、シャッフル
    /// </summary>
    public void InitializeDeck()
    {
        deckStack.Clear();

        // CSVからIDを読み込む
        List<int> allIDs = new List<int>();
        allIDs.AddRange(LoadDeckCSV(defaultDeckCSV));
        allIDs.AddRange(LoadDeckCSV(player1DeckCSV));
        allIDs.AddRange(LoadDeckCSV(player2DeckCSV));

        // Fisher–Yatesシャッフル
        for (int i = 0; i < allIDs.Count; i++)
        {
            int rnd = Random.Range(i, allIDs.Count);
            int tmp = allIDs[i];
            allIDs[i] = allIDs[rnd];
            allIDs[rnd] = tmp;
        }

        // スタックに積む（上から引けるように逆順）
        for (int i = allIDs.Count - 1; i >= 0; i--)
        {
            deckStack.Push(allIDs[i]);
        }
    }

    /// <summary>
    /// CSVからカードIDリストを読み込む
    /// </summary>
    private List<int> LoadDeckCSV(string fileName)
    {
        List<int> ids = new List<int>();
        string path = Path.Combine(Application.streamingAssetsPath, fileName);
        if (!File.Exists(path))
        {
            Debug.LogWarning($"Deck CSV not found: {path}");
            return ids;
        }

        string[] lines = File.ReadAllLines(path);
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;
            if (int.TryParse(line, out int id))
            {
                ids.Add(id);
            }
        }

        return ids;
    }

    /// <summary>
    /// 指定プレイヤーの手札に1枚ドロー
    /// </summary>
    public void DrawCardToHand(PlayerManager player)
    {
        if (player == null || player.handManager == null) return;
        if (deckStack.Count == 0) return;

        int cardID = deckStack.Pop();

        // カード生成
        GameObject cardGO = Instantiate(cardPrefab, drawPosition, Quaternion.identity);
        cardGO.transform.localScale = Vector3.one;

        // カード情報をセット
        CardGenerator cg = cardGO.GetComponent<CardGenerator>();
        if (cg != null)
        {
            cg.cardID = cardID;
            cg.player = player; // ★ ここでプレイヤーを設定 ★

            cg.ApplyCardData(new CardGenerator.CardData
            {
                id = cardID,
                name = "Card" + cardID,
                ruby = "Card" + cardID,
                cost = cardID % 5 + 1,
                text = "効果テキスト",
                type = "0",
                rarity = "Common",
                image = ""
            });
        }

        // プレイヤーの手札に追加
        player.handManager.AddCard(cardGO);
    }
}
