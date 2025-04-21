using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ModelController : MonoBehaviour, IDragHandler, IScrollHandler
{
    [Header("RotateSpeed")]
    public float rotateSpeed = 0.5f;

    [Header("ZoomScale")]
    public float minZoom = 1f;
    public float maxZoom = 10f;
    public float zoomSpeed = 1f;

    private Vector3 lastMousePos;
    public Transform modelContainer;

    [Header("ResetButton")]
    public Button resetButton; // 绑定按钮

    // 初始模型位置、旋转、缩放
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private Vector3 initialScale;

    void Start()
    {
        // 记录初始状态
        initialPosition = modelContainer.position;
        initialRotation = modelContainer.rotation;
        initialScale = modelContainer.localScale;

        // 绑定按钮点击事件
        if (resetButton != null)
            resetButton.onClick.AddListener(ResetModel);
    }

    // 重置模型
    public void ResetModel()
    {
        modelContainer.position = initialPosition;
        modelContainer.rotation = initialRotation;
        modelContainer.localScale = initialScale;
    }

    public void OnDrag(PointerEventData eventData)
    {
        //Debug.Log($"Dragging: {eventData.position}");

        // 根据拖拽距离旋转模型
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            Vector2 delta = eventData.delta;
            modelContainer.Rotate(Vector3.up, -delta.x * rotateSpeed, Space.World);
            modelContainer.Rotate(Vector3.right, delta.y * rotateSpeed, Space.World);
        }
    }
    void Update()
    {
        
        
    }

    // 滚轮缩放
    public void OnScroll(PointerEventData eventData)
    {
        if (IsMouseOver3DView())
        {
            float zoomDelta = -eventData.scrollDelta.y * zoomSpeed;
            Vector3 newPos = modelContainer.position + modelContainer.forward * zoomDelta;
            newPos.z = Mathf.Clamp(newPos.z, minZoom, maxZoom);
            modelContainer.position = newPos;
        }
    }

    // 判断鼠标是否在3D视图区域
    private bool IsMouseOver3DView()
    {
        return RectTransformUtility.RectangleContainsScreenPoint(
            GetComponent<RectTransform>(),
            Input.mousePosition,
            Camera.main
        );
    }
}
