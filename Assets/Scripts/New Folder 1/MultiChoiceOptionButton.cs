using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 候補ボタンPrefab側に付けるコンポーネント
/// ・文章が書かれたボタン
/// ・その中にランプ(黒/緑)を並べる
/// </summary>
public class MultiChoiceOptionButton : MonoBehaviour
{
    [Header("UI")]
    public Button button;
    public TMP_Text labelText;
    public Transform lampParent;

    private MultiChoiceWindow window;
    private int index;

    private List<Image> lamps = new List<Image>();
    private Sprite offSprite;
    private Sprite onSprite;

    private int _pickedCount = 0;
    private bool _canClick = true;

    public void Setup(
        MultiChoiceWindow owner,
        int optionIndex,
        string text,
        int maxLampCount,
        GameObject lampPrefab,
        Sprite lampOff,
        Sprite lampOn
    )
    {
        window = owner;
        index = optionIndex;
        offSprite = lampOff;
        onSprite = lampOn;

        if (labelText != null)
            labelText.text = text ?? "";

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClicked);
        }

        BuildLamps(maxLampCount, lampPrefab);

        // 初期状態
        Refresh(0, true);
    }

    private void BuildLamps(int maxLampCount, GameObject lampPrefab)
    {
        // 既存削除
        for (int i = 0; i < lamps.Count; i++)
        {
            if (lamps[i] != null)
                Destroy(lamps[i].gameObject);
        }
        lamps.Clear();

        if (lampParent == null) return;
        if (lampPrefab == null) return;

        int count = Mathf.Clamp(maxLampCount, 1, 4);

        for (int i = 0; i < count; i++)
        {
            var obj = Instantiate(lampPrefab, lampParent);
            var img = obj.GetComponent<Image>();
            if (img != null)
            {
                lamps.Add(img);
            }
        }
    }

    /// <summary>
    /// ボタンの見た目更新（ランプ＋クリック可否）
    /// </summary>
    public void Refresh(int pickedCount, bool canClick)
    {
        _pickedCount = pickedCount;
        _canClick = canClick;
        ApplyState();
    }

    /// <summary>
    /// MultiChoiceWindow側が「選択数だけ」更新したい場合用（互換）
    /// </summary>
    public void SetSelectedCount(int pickedCount)
    {
        _pickedCount = pickedCount;
        ApplyState();
    }

    /// <summary>
    /// MultiChoiceWindow側が「クリック可否だけ」更新したい場合用（互換）
    /// </summary>
    public void SetInteractable(bool interactable)
    {
        _canClick = interactable;
        ApplyState();
    }

    private void ApplyState()
    {
        // ボタンのクリック可否（上限なら無反応）
        if (button != null)
            button.interactable = _canClick;

        // ランプ更新：緑が _pickedCount 個、残り黒
        for (int i = 0; i < lamps.Count; i++)
        {
            if (lamps[i] == null) continue;

            bool on = (i < _pickedCount);
            lamps[i].sprite = on ? onSprite : offSprite;
        }
    }

    private void OnClicked()
    {
        if (window == null) return;
        window.OnOptionClicked(index);
    }
}
