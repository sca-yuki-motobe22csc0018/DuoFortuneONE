using System;
using UnityEngine;

/// <summary>
/// CSV の A〜H 列に対応するカードデータクラス
/// A: Name (id), B: ruby, C: Type, D: Rarity, E: Cost, F: Text, G: Image, H: effectType1
/// </summary>
[System.Serializable]
public class CardInfo
{
    public string number;     // A Number
    public string name;       // B Name
    public string ruby;       // C Ruby
    public string type;       // D Type
    public string rarity;     // E Rarity
    public int cost;          // F Cost
    [TextArea(3, 10)]
    public string text;       // G Text
    public string imageName;  // H Image（番号）
    [NonSerialized]
    public Sprite sprite;
}
