using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class CardDatabase : MonoBehaviour
{
    public static CardDatabase Instance { get; private set; }

    public List<CardInfo> cards = new List<CardInfo>();
    public string csvFileName = "Card_Data_beta.csv";

    //追加 カード枚数用
    public int numOfCards;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadCsv();
    }

    public void LoadCsv()
    {
        cards.Clear();

        string path = Path.Combine(Application.streamingAssetsPath, csvFileName);
        if (!File.Exists(path))
        {
            Debug.LogError("CSV not found: " + path);
            return;
        }

        var csvLines = ParseCsv(path).Skip(1).Take(numOfCards).ToList();

        bool first = true;
        foreach (var cells in csvLines)
        {
            if (first && cells.Length > 0 && cells[0].Contains("Number"))
            {
                first = false;
                Debug.Log($"ヘッダー読み込み");
                continue;
            }

            first = false;

            if (cells.Length < 8)
            {
                Debug.LogWarning($"列数不足: {cells.Length}");
                continue;
            }

            CardInfo card = new CardInfo();
            card.number = cells[0].Trim();
            card.name = cells[1].Trim();
            card.ruby = cells[2].Trim();
            card.type = cells[3].Trim();
            card.rarity = cells[4].Trim();
            int.TryParse(cells[5].Trim(), out card.cost);
            card.text = cells[6].Trim();
            card.imageName = cells[7].Trim();

            card.sprite = Resources.Load<Sprite>("cardPNG/" + card.imageName+"card");

            cards.Add(card);

            Debug.Log($"[{cards.Count}] 番号:{card.number} | 名前:{card.name} | ルビ:{card.ruby} | " +
                      $"タイプ:{card.type} | レアリティ:{card.rarity} | コスト:{card.cost} | " +
                      $"テキスト:{card.text.Replace("\n", " ")} | 画像:{card.imageName}");
        }

        Debug.Log($"✓ 合計 {cards.Count} 枚のカードを読み込みました。");
    }

    private List<string[]> ParseCsv(string path)
    {
        var result = new List<string[]>();
        using (var reader = new StreamReader(path))
        {
            var currentLine = new List<string>();
            var currentCell = new System.Text.StringBuilder();
            bool inQuotes = false;

            while (reader.Peek() > -1)
            {
                char c = (char)reader.Read();

                if (c == '"')
                {
                    if (inQuotes && reader.Peek() == '"')
                    {
                        currentCell.Append('"');
                        reader.Read();
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    currentLine.Add(currentCell.ToString());
                    currentCell.Clear();
                }
                else if ((c == '\n' || c == '\r') && !inQuotes)
                {
                    if (c == '\r' && reader.Peek() == '\n')
                        reader.Read();

                    if (currentCell.Length > 0 || currentLine.Count > 0)
                    {
                        currentLine.Add(currentCell.ToString());
                        result.Add(currentLine.ToArray());
                        currentLine.Clear();
                        currentCell.Clear();
                    }
                }
                else
                {
                    currentCell.Append(c);
                }
            }

            if (currentCell.Length > 0 || currentLine.Count > 0)
            {
                currentLine.Add(currentCell.ToString());
                result.Add(currentLine.ToArray());
            }
        }

        return result;
    }

    public CardInfo GetCard(string number)
        => cards.FirstOrDefault(c => c.number == number);
}
