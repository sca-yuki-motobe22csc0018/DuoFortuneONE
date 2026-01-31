using System.Collections.Generic;
using System.IO;
using System.Text; // ★ 追加（CSVパースで使用）
using UnityEngine;
using System; // ★ 追加

public class DeckManager : MonoBehaviour
{
    [Header("Card Prefab")]
    public GameObject cardPrefab;

    [Header("Deck CSV Files")]
    public string defaultDeckCSV = "DefaultDeck.csv";
    public string player1DeckCSV = "Player1Deck.csv";
    public string player2DeckCSV = "Player2Deck.csv";

    private const string SELECTED_DECK_KEY_P1 = "SelectedDeckIndex_P1";
    private const string SELECTED_DECK_KEY_P2 = "SelectedDeckIndex_P2";


    private Stack<int> deckStack = new Stack<int>();

    private List<int> LoadSelectedDeckIDsForPlayer(int playerNo) // 1 or 2
    {
        // 1) GlobalDeckLocator：もし使うなら「P1/P2用に別保持」が必要
        // 今の GlobalDeckLocator は selectedDeck 1つだけなので、ここでは JSON 優先にするのが安全

        // 2) JSON + PlayerPrefs
        int index = PlayerPrefs.GetInt(playerNo == 1 ? SELECTED_DECK_KEY_P1 : SELECTED_DECK_KEY_P2, -1);
        if (index < 0) return new List<int>();

        string filePath = Path.Combine(Application.persistentDataPath, DECKS_JSON_NAME);
        if (!File.Exists(filePath)) return new List<int>();

        string json = File.ReadAllText(filePath);
        var save = JsonUtility.FromJson<DeckSaveFileLite>(json);
        if (save == null || save.decks == null) return new List<int>();
        if (index < 0 || index >= save.decks.Count) return new List<int>();

        var deck = save.decks[index];
        if (deck == null || deck.cardNumbers == null || deck.cardNumbers.Count == 0) return new List<int>();

        return ConvertCardNumbersToIds(deck.cardNumbers);
    }


    // ===============================
    // ★ 追加: JSONデッキ読み込み用（DeckSaveManager と同形式）
    // ===============================
    [Serializable]
    private class DeckSaveFileLite
    {
        public List<DeckDataLite> decks = new List<DeckDataLite>();
    }

    [Serializable]
    private class DeckDataLite
    {
        public List<string> cardNumbers = new List<string>();
    }

    private const string SELECTED_DECK_KEY = "SelectedDeckIndex"; // DeckSaveManager と同じ :contentReference[oaicite:5]{index=5}
    private const string DECKS_JSON_NAME = "decks.json";          // DeckSaveManager と同じ :contentReference[oaicite:6]{index=6}


    // ▼追加：山札の残り枚数
    public int GetRemainingCount()
    {
        return deckStack != null ? deckStack.Count : 0;
    }



    // ★ 追加: カードID -> CardData のデータベース（Card_Data.csv を読み込む）
    private Dictionary<int, CardGenerator.CardData> cardDatabase = new Dictionary<int, CardGenerator.CardData>();

    [Header("Draw Settings")]
    public Vector3 drawPosition = Vector3.zero;
    public float cardScale = 0.33f;

    void Start()
    {
        // ★ Hostだけが山札を初期化する（Clientは触らない）
        var gm = FindAnyObjectByType<GameManager>();
        if (gm != null && gm.Object != null && !gm.Object.HasStateAuthority)
            return;

        InitializeDeck();
    }

    /// <summary>
    /// ★ 追加：DefaultDeck(CSV) + HostDeck(ids) + ClientDeck(ids) で山札を作成、シャッフル
    /// </summary>
    public void InitializeDeckWithSelectedDecks(List<int> hostDeckIds, List<int> clientDeckIds)
    {
        deckStack.Clear();

        // Card_Data.csv DB
        LoadCardDatabase();

        List<int> allIDs = new List<int>();

        // DefaultDeckは固定（CSV）
        allIDs.AddRange(LoadDeckCSV(defaultDeckCSV));

        // Hostが選んだデッキ
        if (hostDeckIds != null && hostDeckIds.Count > 0)
            allIDs.AddRange(hostDeckIds);

        // Clientが選んだデッキ
        if (clientDeckIds != null && clientDeckIds.Count > 0)
            allIDs.AddRange(clientDeckIds);

        // Fisher–Yatesシャッフル
        for (int i = 0; i < allIDs.Count; i++)
        {
            int rnd = UnityEngine.Random.Range(i, allIDs.Count);
            int tmp = allIDs[i];
            allIDs[i] = allIDs[rnd];
            allIDs[rnd] = tmp;
        }

        // スタックに積む（上から引けるように逆順）
        for (int i = allIDs.Count - 1; i >= 0; i--)
        {
            deckStack.Push(allIDs[i]);
        }

        Debug.Log($"[DeckManager] InitializeDeckWithSelectedDecks: total={allIDs.Count} (default+host+client)");
    }



    /// <summary>
    /// CSVを読み込んでデッキを作成、シャッフル
    /// </summary>
    public void InitializeDeck()
    {
        deckStack.Clear();

        LoadCardDatabase();

        List<int> allIDs = new List<int>();

        // ①固定デフォルト
        allIDs.AddRange(LoadDeckCSV(defaultDeckCSV));

        // ②ホスト選択（P1）
        var p1 = LoadSelectedDeckIDsForPlayer(1);
        if (p1.Count > 0) allIDs.AddRange(p1);
        else allIDs.AddRange(LoadDeckCSV(player1DeckCSV)); // 保険

        // ③参加側選択（P2）
        var p2 = LoadSelectedDeckIDsForPlayer(2);
        if (p2.Count > 0) allIDs.AddRange(p2);
        else allIDs.AddRange(LoadDeckCSV(player2DeckCSV)); // 保険

        // シャッフル（UnityEngine.Randomを明示）
        for (int i = 0; i < allIDs.Count; i++)
        {
            int rnd = UnityEngine.Random.Range(i, allIDs.Count);
            int tmp = allIDs[i];
            allIDs[i] = allIDs[rnd];
            allIDs[rnd] = tmp;
        }

        for (int i = allIDs.Count - 1; i >= 0; i--)
            deckStack.Push(allIDs[i]);
    }


    // ===============================
    // ★ 追加: デッキ選択結果を取得（優先順位）
    //  1) GlobalDeckLocator.selectedDeck（シーン跨ぎ受け渡し用）
    //  2) persistentDataPath/decks.json + PlayerPrefs(SelectedDeckIndex)
    //  3) 取れなければ空（呼び出し側でCSVにフォールバック）
    // ===============================
    private List<int> LoadSelectedDeckIDs()
    {
        // 1) GlobalDeckLocator 優先
        if (GlobalDeckLocator.Instance != null &&
            GlobalDeckLocator.Instance.selectedDeck != null &&
            GlobalDeckLocator.Instance.selectedDeck.Count > 0)
        {
            return ConvertCardNumbersToIds(GlobalDeckLocator.Instance.selectedDeck);
        } // :contentReference[oaicite:8]{index=8}

        // 2) JSON + PlayerPrefs（DeckSaveManager と同仕様）
        int index = PlayerPrefs.GetInt(SELECTED_DECK_KEY, -1); // 未設定は -1 :contentReference[oaicite:9]{index=9}
        if (index < 0) return new List<int>();

        string filePath = Path.Combine(Application.persistentDataPath, DECKS_JSON_NAME); // :contentReference[oaicite:10]{index=10}
        if (!File.Exists(filePath)) return new List<int>();

        string json = File.ReadAllText(filePath);
        var save = JsonUtility.FromJson<DeckSaveFileLite>(json);
        if (save == null || save.decks == null) return new List<int>();
        if (index < 0 || index >= save.decks.Count) return new List<int>();

        var deck = save.decks[index];
        if (deck == null || deck.cardNumbers == null || deck.cardNumbers.Count == 0) return new List<int>();

        return ConvertCardNumbersToIds(deck.cardNumbers);
    }

    private List<int> ConvertCardNumbersToIds(List<string> cardNumbers)
    {
        List<int> ids = new List<int>();
        for (int i = 0; i < cardNumbers.Count; i++)
        {
            if (int.TryParse(cardNumbers[i], out int id))
                ids.Add(id);
        }
        return ids;
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

    // ★ 山札から「IDだけ」を複数枚まとめて引く（Host専用で使用）
    public int[] DrawCardIDs(int count)
    {
        if (count <= 0) return new int[0];

        int drawCount = Mathf.Min(count, deckStack.Count);
        int[] result = new int[drawCount];

        for (int i = 0; i < drawCount; i++)
        {
            result[i] = deckStack.Pop();
        }

        return result;
    }

    // ★ 外部から CardID → CardData を取るためのラッパー
    public CardGenerator.CardData GetCardDataById(int cardID)
    {
        return CreateCardDataById(cardID);
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

        string path = Path.Combine(Application.streamingAssetsPath, "Card_Data_Beta.csv");
        if (!File.Exists(path))
        {
            Debug.LogWarning($"Card_Data_Beta.csv not found: {path}");
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
                effectType7 = (values.Length > 20) ? values[20] : "",
                effectValue7 = (values.Length > 21) ? values[21] : "0",
                effectType8 = values.Length > 22 ? values[22] : "",
                effectValue8 = values.Length > 23 ? values[23] : "0",
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
