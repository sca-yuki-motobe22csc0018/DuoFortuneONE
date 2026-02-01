using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MultiChoiceWindow : MonoBehaviour
{
    // ★追加：inactiveでも参照できるように singleton + Get()
    public static MultiChoiceWindow Instance { get; private set; }

    public static MultiChoiceWindow Get()
    {
        if (Instance != null) return Instance;

        // inactive含めて探す（シーン上にあるものだけ拾う）
        var all = Resources.FindObjectsOfTypeAll<MultiChoiceWindow>();
        for (int i = 0; i < all.Length; i++)
        {
            var w = all[i];
            if (w == null) continue;
            if (!w.gameObject.scene.IsValid()) continue;
            if (!w.gameObject.scene.isLoaded) continue;

            Instance = w;
            return Instance;
        }
        return null;
    }

    [Header("Root")]
    [SerializeField] private GameObject panelRoot;

    [Header("Texts")]
    [SerializeField] private TMP_Text fullEffectText;
    [SerializeField] private TMP_Text pickedNowText;   // 例: 2
    [SerializeField] private TMP_Text pickedMaxText;   // 例: 3  （/ は別UI想定）

    [Header("Buttons")]
    [SerializeField] private Button okButton;
    [SerializeField] private Button resetButton;

    [Header("Options")]
    [SerializeField] private Transform optionParent;
    [SerializeField] private MultiChoiceOptionButton optionButtonPrefab;

    [Header("Lamp (OptionButtonへ渡す)")]
    [SerializeField] private GameObject lampPrefab;
    [SerializeField] private Sprite lampOffSprite;
    [SerializeField] private Sprite lampOnSprite;

    private readonly List<MultiChoiceOptionButton> _spawnedButtons = new List<MultiChoiceOptionButton>();

    private int[] _pickedPerOption;
    private int _pickMax;
    private int _sameMax;

    private Action<int[]> _onConfirm;

    private void Awake()
    {
        // ★追加：Instance設定（非アクティブでもAwakeは呼ばれる想定）
        if (Instance != null && Instance != this)
        {
            // 同じシーンに複数置いた時は最初のを優先（必要ならここは調整）
            return;
        }
        Instance = this;

        if (okButton != null) okButton.onClick.AddListener(OnOkClicked);
        if (resetButton != null) resetButton.onClick.AddListener(OnResetClicked);

        Hide();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Open(string csvFullText, string[] optionTexts, int pickMax, int sameMax, Action<int[]> onConfirm)
    {
        if (panelRoot == null) panelRoot = gameObject;

        _onConfirm = onConfirm;

        _pickMax = Mathf.Clamp(pickMax, 1, 4);
        _sameMax = Mathf.Clamp(sameMax, 1, 4);

        int optionCount = (optionTexts != null) ? Mathf.Clamp(optionTexts.Length, 0, 4) : 0;

        _pickedPerOption = new int[optionCount];

        if (fullEffectText != null) fullEffectText.text = csvFullText ?? "";

        EnsureOptionButtons(optionTexts, optionCount);

        RefreshAll();

        panelRoot.SetActive(true);
    }

    private void EnsureOptionButtons(string[] optionTexts, int optionCount)
    {
        if (optionButtonPrefab == null || optionParent == null) return;

        // 必要数まで生成
        while (_spawnedButtons.Count < optionCount)
        {
            var b = Instantiate(optionButtonPrefab, optionParent);
            _spawnedButtons.Add(b);
        }

        // 表示/非表示 + セットアップ
        for (int i = 0; i < _spawnedButtons.Count; i++)
        {
            bool active = i < optionCount;
            _spawnedButtons[i].gameObject.SetActive(active);

            if (active)
            {
                string txt = optionTexts[i] ?? "";

                _spawnedButtons[i].Setup(
                    this,
                    i,
                    txt,
                    _sameMax,
                    lampPrefab,
                    lampOffSprite,
                    lampOnSprite
                );

                _spawnedButtons[i].Refresh(0, true);
            }
        }
    }

    // ★OptionButtonから呼ばれるので public
    public void OnOptionClicked(int index)
    {
        if (_pickedPerOption == null) return;
        if (index < 0 || index >= _pickedPerOption.Length) return;

        int total = GetTotalPicked();
        if (total >= _pickMax) return; // 全体上限
        if (_pickedPerOption[index] >= _sameMax) return; // 同一文章上限

        _pickedPerOption[index]++;
        RefreshAll();
    }

    private void OnResetClicked()
    {
        if (_pickedPerOption == null) return;

        for (int i = 0; i < _pickedPerOption.Length; i++)
            _pickedPerOption[i] = 0;

        RefreshAll();
    }

    private void OnOkClicked()
    {
        if (_pickedPerOption == null) return;

        int total = GetTotalPicked();
        if (total != _pickMax) return; // ぴったりのみ確定

        int[] result = new int[_pickedPerOption.Length];
        Array.Copy(_pickedPerOption, result, _pickedPerOption.Length);

        Hide();

        _onConfirm?.Invoke(result);
    }

    private void RefreshAll()
    {
        int total = GetTotalPicked();

        if (pickedNowText != null) pickedNowText.text = total.ToString();
        if (pickedMaxText != null) pickedMaxText.text = _pickMax.ToString();

        if (okButton != null) okButton.interactable = (total == _pickMax);

        for (int i = 0; i < _spawnedButtons.Count; i++)
        {
            if (!_spawnedButtons[i].gameObject.activeSelf) continue;

            int picked = (i < _pickedPerOption.Length) ? _pickedPerOption[i] : 0;
            bool canClick = (total < _pickMax) && (picked < _sameMax);

            _spawnedButtons[i].Refresh(picked, canClick);
        }
    }

    private int GetTotalPicked()
    {
        if (_pickedPerOption == null) return 0;

        int sum = 0;
        for (int i = 0; i < _pickedPerOption.Length; i++)
            sum += _pickedPerOption[i];

        return sum;
    }

    public void Hide()
    {
        if (panelRoot == null) panelRoot = gameObject;
        panelRoot.SetActive(false);
    }
}
