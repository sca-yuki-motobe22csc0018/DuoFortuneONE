using UnityEngine;

public enum UIClipId
{
    ClickOK,
    ClickCancel,
    ClickGeneric,
    HoverHandCard,
}

public enum SfxClipId
{
    CardMove,   // ドロー/捨て/奪う/回収（共通）
    LifeBreak,
    CardUse,
    TurnStart,
    TurnEnd,
    Victory,
    Defeat,

    // ★追加：マナ系（別音）
    ManaMaxUp,
    ManaMaxDown,
    ManaRecover,

    // ★追加：効果発動音（別音）
    AttackEffect,
    BlockEffect,
}


[CreateAssetMenu(menuName = "Audio/Audio Library", fileName = "AudioLibrary")]
public class AudioLibrary : ScriptableObject
{
    // AudioLibrary.cs の中（UI Clips / SFX Clips の下あたり）に追加
    [Header("BGM Clips")]
    public AudioClip bgmMainGame;
    public AudioClip bgmResult;


    [Header("UI Clips")]
    public AudioClip uiClickOK;
    public AudioClip uiClickCancel;
    public AudioClip uiClickGeneric;
    public AudioClip uiHoverHandCard;

    [Header("SFX Clips")]
    public AudioClip sfxCardMove;
    public AudioClip sfxLifeBreak;
    public AudioClip sfxCardUse;
    public AudioClip sfxTurnStart;
    public AudioClip sfxTurnEnd;
    public AudioClip sfxVictory;
    public AudioClip sfxDefeat;

    public AudioClip sfxManaMaxUp;
    public AudioClip sfxManaMaxDown;
    public AudioClip sfxManaRecover;
    public AudioClip sfxAttackEffect;
    public AudioClip sfxBlockEffect;

    public AudioClip GetUI(UIClipId id)
    {
        switch (id)
        {
            case UIClipId.ClickOK: return uiClickOK;
            case UIClipId.ClickCancel: return uiClickCancel;
            case UIClipId.ClickGeneric: return uiClickGeneric;
            case UIClipId.HoverHandCard: return uiHoverHandCard;
            default: return null;
        }
    }

    public AudioClip GetSfx(SfxClipId id)
    {
        switch (id)
        {
            case SfxClipId.CardMove: return sfxCardMove;
            case SfxClipId.LifeBreak: return sfxLifeBreak;
            case SfxClipId.CardUse: return sfxCardUse;
            case SfxClipId.TurnStart: return sfxTurnStart;
            case SfxClipId.TurnEnd: return sfxTurnEnd;
            case SfxClipId.Victory: return sfxVictory;
            case SfxClipId.Defeat: return sfxDefeat;

            // ★追加
            case SfxClipId.ManaMaxUp: return sfxManaMaxUp;
            case SfxClipId.ManaMaxDown: return sfxManaMaxDown;
            case SfxClipId.ManaRecover: return sfxManaRecover;
            case SfxClipId.AttackEffect: return sfxAttackEffect;
            case SfxClipId.BlockEffect: return sfxBlockEffect;

            default: return null;
        }
    }

}
