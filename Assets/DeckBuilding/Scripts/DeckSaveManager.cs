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
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        Load();
    }

    string FilePath => Path.Combine(Application.persistentDataPath, fileName);

    public void Save()
    {
        string json = JsonUtility.ToJson(saveFile, true);
        File.WriteAllText(FilePath, json);
    }

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

    public DeckData GetDeck(int index)
    {
        if (index < 0 || index >= saveFile.decks.Count) return null;
        return saveFile.decks[index];
    }

    public void SetDeck(int index, DeckData data)
    {
        if (index < 0 || index >= saveFile.decks.Count) return;
        saveFile.decks[index] = data;
        Save();
    }

    //--------------------------------------------------
    // ★使用するデッキを指定
    //--------------------------------------------------
    public void SetSelectedDeck(int index)
    {
        PlayerPrefs.SetInt(SELECTED_DECK_KEY, index);
        PlayerPrefs.Save();

        Debug.Log($"Selected Deck = {index + 1}");
    }

    //--------------------------------------------------
    // ★使用するデッキ番号を取得
    //--------------------------------------------------
    public int GetSelectedDeckIndex()
    {
        return PlayerPrefs.GetInt(SELECTED_DECK_KEY, 0);
    }

    // -----------------------------
    // デバッグ用削除
    // -----------------------------
    public void ClearDeck(int index)
    {
        if (index < 0 || index >= saveFile.decks.Count) return;
        saveFile.decks[index] = new DeckData { cardNumbers = new List<string>() };
        Save();
        Debug.Log($"Deck {index} cleared");
    }

    public void ClearAllDecks()
    {
        for (int i = 0; i < saveFile.decks.Count; i++)
            saveFile.decks[i] = new DeckData { cardNumbers = new List<string>() };

        Save();
        Debug.Log("All decks cleared");
    }
}
