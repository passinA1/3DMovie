using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class AdvancedModelController : MonoBehaviour, IDragHandler, IBeginDragHandler
{
    [Header("Model Reference")]
    public Transform modelContainer;

    [Header("Rotation Settings")]
    [Range(0.1f, 2f)]
    public float rotateSpeed = 0.5f;

    [Header("Zoom Settings")]
    public Slider zoomSlider;
    [Min(0.1f)] public float minZoom = 0.5f;
    [Min(1f)] public float maxZoom = 3f;

    [Header("Pan Settings")]
    [Range(0.1f, 10f)]
    public float panSensitivity = 5f;
    public float maxPanDistance = 5f;
    public float panActivationZoom = 3f;


    [Header("Reset Control")]
    public Button resetButton;

    private Vector3 _initPosition;
    private Quaternion _initRotation;
    private Vector3 _initScale;
    private Vector3 _dragStartPosition;
    private Vector3 _modelStartPosition;

    void Start()
    {
        InitializeModel();
        SetupControls();
    }

    void InitializeModel()
    {
        _initPosition = modelContainer.position;
        _initRotation = modelContainer.rotation;
        _initScale = modelContainer.localScale;
    }

    void SetupControls()
    {
        // Slider��ʼ��
        zoomSlider.minValue = minZoom;
        zoomSlider.maxValue = maxZoom;
        zoomSlider.value = modelContainer.localScale.x;
        zoomSlider.onValueChanged.AddListener(OnZoomChanged);

        // ���ð�ť
        resetButton.onClick.AddListener(() =>
        {
            modelContainer.SetPositionAndRotation(_initPosition, _initRotation);
            modelContainer.localScale = _initScale;
            zoomSlider.value = _initScale.x;
        });
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        _dragStartPosition = Input.mousePosition;
        _modelStartPosition = modelContainer.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (Input.GetKey(KeyCode.LeftAlt) || IsZoomedIn())
        {
            HandlePan(eventData);
        }
        else
        {
            HandleRotation(eventData);
        }
    }

    void HandleRotation(PointerEventData eventData)
    {
        Vector2 delta = eventData.delta;
        modelContainer.Rotate(Vector3.up, -delta.x * rotateSpeed, Space.World);
        modelContainer.Rotate(Camera.main.transform.right, delta.y * rotateSpeed, Space.World);
    }

    void HandlePan(PointerEventData eventData)
    {
        // ������Ļ�����������ȼ���
        float zoomFactor = Mathf.Clamp(zoomSlider.value / panActivationZoom, 0.5f, 2f);
        float adaptiveSensitivity = panSensitivity * zoomFactor;

        // ��ȡ��Ļ����ϵ���ƶ���
        Vector3 screenDelta = (Input.mousePosition - _dragStartPosition) * adaptiveSensitivity * 0.1f;

        // ת��Ϊ����ռ���ƶ������������������
        Vector3 movement = new Vector3(
            screenDelta.x * Camera.main.transform.right.x +
            screenDelta.y * Camera.main.transform.up.x,

            screenDelta.x * Camera.main.transform.right.y +
            screenDelta.y * Camera.main.transform.up.y,

            screenDelta.x * Camera.main.transform.right.z +
            screenDelta.y * Camera.main.transform.up.z
        );

        // Ӧ�ô����Ի������ƶ�
        Vector3 targetPosition = _modelStartPosition + movement;
        modelContainer.position = Vector3.Lerp(
            modelContainer.position,
            targetPosition,
            Time.deltaTime * 10f // ���ƻ����ٶ�
        );

        // ��̬�߽����ƣ����ݵ�ǰ���ż��������
        float maxOffset = maxPanDistance * (100f - zoomSlider.normalizedValue);
        modelContainer.position = new Vector3(
            Mathf.Clamp(modelContainer.position.x, _initPosition.x - maxOffset, _initPosition.x + maxOffset),
            Mathf.Clamp(modelContainer.position.y, _initPosition.y - maxOffset, _initPosition.y + maxOffset),
            Mathf.Clamp(modelContainer.position.z, _initPosition.z - maxOffset, _initPosition.z + maxOffset)
        );
    }

    void OnZoomChanged(float value)
    {
        modelContainer.localScale = Vector3.one * Mathf.Clamp(value, minZoom, maxZoom);

        // �Զ�����ƽ��ģʽʱ����λ��
        if (IsZoomedIn())
        {
            modelContainer.position = _initPosition;
        }
    }

    bool IsZoomedIn() => zoomSlider.value > 1.2f;

    void OnDrawGizmosSelected()
    {
        if (modelContainer != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(modelContainer.position, 0.1f);
            Gizmos.DrawWireCube(_initPosition, Vector3.one * 0.2f);
        }
    }
}