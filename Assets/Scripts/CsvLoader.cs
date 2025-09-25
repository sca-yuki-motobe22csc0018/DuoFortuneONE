using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public class CsvLoader : MonoBehaviour
{
    [System.Serializable]
    public class CardData
    {
        public int id;
        public string name;
        public int type;
        public string rarity;
        public int cost;
        public string text;
        public string image; // Resources内の画像ファイル名
    }

    public  List<CardData> cardList = new List<CardData>();

    void Start()
    {
        LoadCSV();
    }
    void LoadCSV()
    {
        string path = UnityEngine.Application.streamingAssetsPath + "/Card_Data.csv";
        string[] lines = File.ReadAllLines(path, Encoding.UTF8);

        for (int i = 1; i < lines.Length; i++) // 0行目はヘッダー
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            string[] values = lines[i].Split(',');

            CardData data = new CardData();
            data.id = int.Parse(values[0]);
            data.name = values[1];
            data.type = int.Parse(values[2]);
            data.rarity = values[3];
            data.cost = int.Parse(values[4]);
            data.text = values[5];
            //data.image = values[6];

            cardList.Add(data);
        }
    }
}
