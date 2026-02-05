using UnityEngine;
using UnityEngine.UI;

public enum UIButtonSfxType
{
    OK,
    Cancel,
    Generic,
    Custom,
}

[RequireComponent(typeof(Button))]
public class UIButtonSfx : MonoBehaviour
{
    public UIButtonSfxType type = UIButtonSfxType.Generic;

    [Header("Custom (type=Custom)")]
    public AudioClip customClip;

    [Range(0f, 2f)]
    public float volumeScale = 1f;

    Button _btn;

    void Awake()
    {
        _btn = GetComponent<Button>();
    }

    void OnEnable()
    {
        if (_btn != null) _btn.onClick.AddListener(OnClicked);
    }

    void OnDisable()
    {
        if (_btn != null) _btn.onClick.RemoveListener(OnClicked);
    }

    void OnClicked()
    {
        var am = AudioManager.Instance;
        if (am == null) return;

        switch (type)
        {
            case UIButtonSfxType.OK:
                am.PlayUI(UIClipId.ClickOK, volumeScale);
                break;

            case UIButtonSfxType.Cancel:
                am.PlayUI(UIClipId.ClickCancel, volumeScale);
                break;

            case UIButtonSfxType.Generic:
                am.PlayUI(UIClipId.ClickGeneric, volumeScale);
                break;

            case UIButtonSfxType.Custom:
                am.PlayUI(customClip, volumeScale);
                break;
        }
    }
}
