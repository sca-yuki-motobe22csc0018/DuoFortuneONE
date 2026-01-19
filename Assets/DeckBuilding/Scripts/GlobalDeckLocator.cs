using System.Collections.Generic;
using UnityEngine;

public class GlobalDeckLocator : MonoBehaviour
{
    public static GlobalDeckLocator Instance;

    public List<string> selectedDeck = new();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void SetDeck(List<string> cardNumbers)
    {
        selectedDeck = new List<string>(cardNumbers);
    }
}
