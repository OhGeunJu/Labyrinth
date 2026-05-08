using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 이벤트 발생 시 화면 상단에 팝업으로 알림 표시.
// 인스펙터에서 panel, iconImage, titleText, descText 연결.
public class EventNotificationUI : MonoBehaviour
{
    [SerializeField] GameObject panel;
    [SerializeField] Image iconImage;
    [SerializeField] TextMeshProUGUI titleText;
    [SerializeField] TextMeshProUGUI descText;
    [Tooltip("자동으로 닫히기까지 대기 시간(초)")]
    [SerializeField] float displayDuration = 5f;

    Coroutine _closeRoutine;

    void Awake()
    {
        if (panel != null) panel.SetActive(false);
        EventManager.OnEventTriggered += Show;
    }

    void OnDestroy() => EventManager.OnEventTriggered -= Show;

    void Show(GameEventData data)
    {
        if (panel == null) return;

        if (iconImage  != null) { iconImage.sprite  = data.Icon; iconImage.enabled = data.Icon != null; }
        if (titleText  != null) titleText.text = data.EventName;
        if (descText   != null) descText.text  = data.Description;

        panel.SetActive(true);

        if (_closeRoutine != null) StopCoroutine(_closeRoutine);
        _closeRoutine = StartCoroutine(AutoClose());
    }

    IEnumerator AutoClose()
    {
        yield return new WaitForSeconds(displayDuration);
        if (panel != null) panel.SetActive(false);
    }

    public void Close()
    {
        if (_closeRoutine != null) StopCoroutine(_closeRoutine);
        if (panel != null) panel.SetActive(false);
    }
}
