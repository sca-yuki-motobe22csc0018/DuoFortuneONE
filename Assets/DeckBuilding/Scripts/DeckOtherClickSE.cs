using UnityEngine;
using UnityEngine.EventSystems;

public class DeckOtherClickSE : MonoBehaviour, IPointerClickHandler
{
    SoundManager sound;

    void Start()
    {
        sound = FindObjectOfType<SoundManager>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        sound.PlaySE3();
    }
}
