using System.Collections.Generic;
using System.IO;
using System.Text; // ★ 追加（CSVパースで使用）
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

    // ★ 追加: カードID -> CardData のデータベース（Card_Data.csv を読み込む）
    private Dictionary<int, CardGenerator.CardData> cardDatabase = new Dictionary<int, CardGenerator.CardData>();

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

        // ★ 追加: カード定義DBをロード（Card_Data.csv）
        LoadCardDatabase();

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
    /// 指定プレイヤーの手札に1枚ドロー（従来処理を維持）
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

            // ★ 追加: DBからCardDataを引く（なければ従来の簡易ダミーでフォールバック）
            var data = CreateCardDataById(cardID);
            cg.ApplyCardData(data);
        }

        // プレイヤーの手札に追加
        player.handManager.AddCard(cardGO);
    }

    /// <summary>
    /// ★ 追加: 山札から「データだけ」引く（Life用）
    /// </summary>
    public CardGenerator.CardData DrawCardDataOnly()
    {
        if (deckStack.Count == 0)
        {
            Debug.LogWarning("デッキが空です。");
            return null;
        }

        int cardID = deckStack.Pop();
        return CreateCardDataById(cardID);
    }

    // ===============================
    // ★ 追加: カード定義DB関連
    // ===============================

    /// <summary>
    /// Card_Data.csv を読み込んで cardDatabase を構築
    /// </summary>
    private void LoadCardDatabase()
    {
        cardDatabase.Clear();

        string path = Path.Combine(Application.streamingAssetsPath, "Card_Data.csv");
        if (!File.Exists(path))
        {
            Debug.LogWarning($"Card_Data.csv not found: {path}");
            return;
        }

        string csvText = File.ReadAllText(path, Encoding.UTF8);
        var rows = ParseCsv(csvText);
        if (rows == null || rows.Count <= 1) return;

        for (int i = 1; i < rows.Count; i++)
        {
            var values = rows[i];
            if (values.Length < 8) continue;
            if (!int.TryParse(values[0], out int id)) continue;

            var data = new CardGenerator.CardData
            {
                id = id,
                name = values[1],
                ruby = (values.Length > 2) ? values[2] : "",
                type = (values.Length > 3) ? values[3] : "",
                rarity = (values.Length > 4) ? values[4] : "",
                cost = (values.Length > 5 && int.TryParse(values[5], out int c)) ? c : 0,
                text = (values.Length > 6) ? values[6] : "",
                image = (values.Length > 7) ? values[7] : "",

                effectType1 = (values.Length > 8) ? values[8] : "",
                effectValue1 = (values.Length > 9) ? values[9] : "0",
                effectType2 = (values.Length > 10) ? values[10] : "",
                effectValue2 = (values.Length > 11) ? values[11] : "0",
                effectType3 = (values.Length > 12) ? values[12] : "",
                effectValue3 = (values.Length > 13) ? values[13] : "0",
                effectType4 = (values.Length > 14) ? values[14] : "",
                effectValue4 = (values.Length > 15) ? values[15] : "0",
                effectType5 = (values.Length > 16) ? values[16] : "",
                effectValue5 = (values.Length > 17) ? values[17] : "0",
                effectType6 = (values.Length > 18) ? values[18] : "",
                effectValue6 = (values.Length > 19) ? values[19] : "0",
            };

            if (!cardDatabase.ContainsKey(data.id))
                cardDatabase.Add(data.id, data);
        }
    }

    /// <summary>
    /// CSVのクォート対応パーサ（CardGenerator と同等の挙動）
    /// </summary>
    private List<string[]> ParseCsv(string csvText)
    {
        var rows = new List<string[]>();
        var currentRow = new List<string>();
        var currentField = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < csvText.Length; i++)
        {
            char c = csvText[i];
            char next = (i + 1 < csvText.Length) ? csvText[i + 1] : '\0';

            if (inQuotes)
            {
                if (c == '"' && next == '"') { currentField.Append('"'); i++; }
                else if (c == '"') inQuotes = false;
                else currentField.Append(c);
            }
            else
            {
                if (c == '"') inQuotes = true;
                else if (c == ',')
                {
                    currentRow.Add(currentField.ToString());
                    currentField.Clear();
                }
                else if (c == '\r' && next == '\n')
                {
                    currentRow.Add(currentField.ToString());
                    rows.Add(currentRow.ToArray());
                    currentRow = new List<string>();
                    currentField.Clear();
                    i++;
                }
                else if (c == '\n' || c == '\r')
                {
                    currentRow.Add(currentField.ToString());
                    rows.Add(currentRow.ToArray());
                    currentRow = new List<string>();
                    currentField.Clear();
                }
                else currentField.Append(c);
            }
        }

        if (currentField.Length > 0 || currentRow.Count > 0)
        {
            currentRow.Add(currentField.ToString());
            rows.Add(currentRow.ToArray());
        }

        return rows;
    }

    /// <summary>
    /// IDから CardData を作る。DBが無ければ従来の簡易ダミーで補完。
    /// </summary>
    private CardGenerator.CardData CreateCardDataById(int cardID)
    {
        if (cardDatabase != null && cardDatabase.TryGetValue(cardID, out var dataFromDb))
        {
            // DBから取得（本物のデータ）
            return dataFromDb;
        }

        // フォールバック：従来の簡易ダミー
        return new CardGenerator.CardData
        {
            id = cardID,
            name = "Card" + cardID,
            ruby = "Card" + cardID,
            cost = cardID % 5 + 1,
            text = "効果テキスト",
            type = "0",
            rarity = "Common",
            image = ""
        };
    }
}
