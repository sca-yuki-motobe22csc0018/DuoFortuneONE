using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class DeckSaveManager : MonoBehaviour
{
    public static DeckSaveManager Instance;

    string fileName = "decks.json";
    const string SELECTED_DECK_KEY = "SelectedDeckIndex";

    [Serializable]
    public class DeckSaveFile
    {
        public List<DeckData> decks = new();
    }

    public DeckSaveFile saveFile = new();

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        Load();
    }

    string FilePath => Path.Combine(Application.persistentDataPath, fileName);

    // -----------------------------
    // 保存
    // -----------------------------
    public void Save()
    {
        string json = JsonUtility.ToJson(saveFile, true);
        File.WriteAllText(FilePath, json);
    }

    // -----------------------------
    // 読み込み
    // -----------------------------
    public void Load()
    {
        if (!File.Exists(FilePath))
        {
            saveFile = new DeckSaveFile();
            for (int i = 0; i < 3; i++)
                saveFile.decks.Add(new DeckData { cardNumbers = new List<string>() });

            Save();
            return;
        }

        string json = File.ReadAllText(FilePath);
        saveFile = JsonUtility.FromJson<DeckSaveFile>(json);
    }

    // -----------------------------
    // デッキ取得
    // -----------------------------
    public DeckData GetDeck(int index)
    {
        if (index < 0 || index >= saveFile.decks.Count)
            return null;

        return saveFile.decks[index];
    }

    // -----------------------------
    // デッキ保存
    // -----------------------------
    public void SetDeck(int index, DeckData data)
    {
        if (index < 0 || index >= saveFile.decks.Count)
            return;

        saveFile.decks[index] = data;
        Save();
    }

    //==================================================
    // ★ 使用するデッキを指定
    //==================================================
    public void SetSelectedDeck(int index)
    {
        PlayerPrefs.SetInt(SELECTED_DECK_KEY, index);
        PlayerPrefs.Save();

        Debug.Log($"[DeckSaveManager] Selected Deck = {index + 1}");
    }

    //==================================================
    // ★ 使用するデッキ番号を取得
    //==================================================
    public int GetSelectedDeckIndex()
    {
        // 未設定時は -1（安全設計）
        return PlayerPrefs.GetInt(SELECTED_DECK_KEY, -1);
    }

    //==================================================
    // ★ 使用中デッキデータを取得（超重要）
    //==================================================
    public DeckData GetSelectedDeck()
    {
        int index = GetSelectedDeckIndex();
        if (index < 0) return null;

        return GetDeck(index);
    }

    //==================================================
    // ★ デッキが存在するか
    //==================================================
    public bool HasDeckData(int index)
    {
        var deck = GetDeck(index);
        return deck != null && deck.cardNumbers != null && deck.cardNumbers.Count > 0;
    }

    // -----------------------------
    // デバッグ用削除
    // -----------------------------
    public void ClearDeck(int index)
    {
        if (index < 0 || index >= saveFile.decks.Count) return;

        saveFile.decks[index] = new DeckData { cardNumbers = new List<string>() };
        Save();

        Debug.Log($"Deck {index + 1} cleared");
    }

    public void ClearAllDecks()
    {
        for (int i = 0; i < saveFile.decks.Count; i++)
            saveFile.decks[i] = new DeckData { cardNumbers = new List<string>() };

        Save();
        Debug.Log("All decks cleared");
    }
}
