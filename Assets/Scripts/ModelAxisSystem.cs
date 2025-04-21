/*using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(LineRenderer))]
public class ModelAxisSystem : MonoBehaviour
{
    // 基础配置
    [Header("Base Settings")]
    public MeshFilter targetModel;
    [Tooltip("坐标轴宽度")] public float axisWidth = 0.02f;
    [Tooltip("标签偏移距离")] public float labelDistance = 0.3f;

    // 时间轴配置
    [Header("时间轴设置")]
    public int startYear = 1990;
    public int endYear = 2020;
    [Range(0f, 1f)] public float zAxisOffset = 0f;

    // 可视化元素
    [Header("Visual Elements")]
    public Material axisMaterial;
    public Material tickMaterial;
    public TMP_FontAsset labelFont;
    public Color xAxisColor = Color.red;
    public Color zAxisColor = Color.blue;
    public float labelFontSize = 800f;

    // 刻度配置
    [Header("刻度设置")]
    public float tickSize = 0.1f;

    // ???行时数据
    private LineRenderer zAxis;
    private readonly List<LineRenderer> dynamicXAxes = new List<LineRenderer>();
    private List<GameObject> ticksPool = new List<GameObject>();
    private Bounds modelBounds;
    private Vector3 lastModelScale;
    private Camera mainCamera;
    private float totalZLength;

    void Awake()
    {
        mainCamera = Camera.main;
        InitializeAxisSystem();
        lastModelScale = targetModel.transform.lossyScale;
    }

    void Update()
    {
        HandleDynamicUpdate();
        HandleInteraction();
        UpdateLOD();
        LogAxisParameters();
        //OnDrawGizmosSelected();
        if (targetModel.transform.hasChanged)
        {
            UpdateAllAxes();
            targetModel.transform.hasChanged = false;
        }
    }

    #region 初始化系统
    void InitializeAxisSystem()
    {
        if (!ValidateComponents()) return;

        modelBounds = targetModel.sharedMesh.bounds;
        CreateMainAxes();
        StartCoroutine(GenerateTimeAxisAsync());
    }

    bool ValidateComponents()
    {
        if (targetModel == null)
        {
            Debug.LogError("目标模型未分配!");
            return false;
        }
        if (axisMaterial == null || tickMaterial == null)
        {
            Debug.LogError("材质未分配!");
            return false;
        }
        return true;
    }
    #endregion

    #region 主坐标轴创建
    void CreateMainAxes()
    {
        // 创建Z轴
        GameObject zAxisObj = new GameObject("Z_Axis");
        zAxisObj.transform.SetParent(transform);

        zAxis = zAxisObj.AddComponent<LineRenderer>();
        ConfigureAxisRenderer(zAxis, zAxisColor);

        // 获取真实Z轴方向
        Vector3 trueZDirection = GetTrueZDirection();

        // 计算Z轴参数
        Vector3 zStart = GetEdgeStartPoint();
        Vector3 zEnd = zStart + trueZDirection * GetZAxisLength();

        zAxis.SetPositions(new Vector3[] { zStart, zEnd });
        CreateAxisLabel(zEnd + targetModel.transform.forward * labelDistance, "Z", zAxisColor);
        Debug.DrawRay(zStart, trueZDirection * 5f, Color.green, 10f);
        
    }
    Vector3 GetTrueZDirection()
    {
        // 获取模型网格数据
        Mesh mesh = targetModel.sharedMesh;
        Vector3[] vertices = mesh.vertices;

        // 寻找Z轴方向的两个极端顶点
        Vector3 minZVert = vertices[0];
        Vector3 maxZVert = vertices[0];
        foreach (Vector3 v in vertices)
        {
            if (v.z < minZVert.z) minZVert = v;
            if (v.z > maxZVert.z) maxZVert = v;
        }

        // 转换到世界空间
        Vector3 worldMin = targetModel.transform.TransformPoint(minZVert);
        Vector3 worldMax = targetModel.transform.TransformPoint(maxZVert);

        return (worldMax - worldMin).normalized;
    }

    Vector3 GetEdgeStartPoint()
    {
        // 获取模型变换矩阵
        Matrix4x4 modelMatrix = targetModel.transform.localToWorldMatrix;

        // 计算实际前缘点（忽略Y轴）
        Vector3 localFrontEdge = new Vector3(
            modelBounds.min.x,
            0, // Y轴置底
            modelBounds.min.z + modelBounds.size.z * zAxisOffset
        );

        // 应用模型变换
        return modelMatrix.MultiplyPoint3x4(localFrontEdge);
    }

    float GetZAxisLength()
    {
        Vector3 scaledSize = Vector3.Scale(modelBounds.size, targetModel.transform.lossyScale);
        return scaledSize.z * (1 - zAxisOffset * 2);
    }
    #endregion

    #region 时间轴系统
    IEnumerator GenerateTimeAxisAsync()
    {
        int yearCount = endYear - startYear + 1;
        float yearStep = totalZLength / (yearCount - 1);

        // 生成时间刻度和X轴
        for (int i = 0; i < yearCount; i++)
        {
            CreateYearTick(i * yearStep, startYear + i);
            CreateDynamicXAxis(i * yearStep);

            if (i % 5 == 0) yield return null;
        }
    }

    void CreateYearTick(float position, int year)
    {
        GameObject tick = GetPooledTick();
        tick.SetActive(true);
        tick.transform.SetParent(transform);

        LineRenderer lr = tick.GetComponent<LineRenderer>();
        Vector3 basePos = GetEdgeStartPoint() + targetModel.transform.forward * position;

        // 创建双向刻度
        Vector3 tickDir = (targetModel.transform.up + targetModel.transform.right).normalized;
        lr.SetPositions(new Vector3[] {
            basePos - tickDir * tickSize,
            basePos + tickDir * tickSize
        });

        CreateTickLabel(basePos + targetModel.transform.forward * 0.1f, year.ToString());
    }

    void CreateDynamicXAxis(float zPosition)
    {
        Vector3 zDir = GetTrueZDirection();
        Vector3 xDir = GetTrueXDirection();

        // 计算起始点（添加垂直偏移）
        Vector3 startPoint = GetEdgeStartPoint() + zDir * zPosition;
        startPoint += Vector3.up * 0.2f; // 防止与平面重叠

        // 计算实际X轴长度
        float scaledXLength = Vector3.Scale(modelBounds.size, targetModel.transform.lossyScale).x * 0.85f;

        LineRenderer lr = new GameObject($"X_Axis_{zPosition}").AddComponent<LineRenderer>();
        ConfigureAxisRenderer(lr, xAxisColor);
        lr.SetPositions(new Vector3[] {
        startPoint,
        startPoint + xDir * scaledXLength
    });
    }

    Vector3 GetTrueXDirection()
    {
        Mesh mesh = targetModel.sharedMesh;
        Vector3[] vertices = mesh.vertices;

        Vector3 minX = vertices[0];
        Vector3 maxX = vertices[0];
        foreach (Vector3 v in vertices)
        {
            if (v.x < minX.x) minX = v;
            if (v.x > maxX.x) maxX = v;
        }

        Vector3 worldMin = targetModel.transform.TransformPoint(minX);
        Vector3 worldMax = targetModel.transform.TransformPoint(maxX);

        return (worldMax - worldMin).normalized;
    }
    Vector3 GetTrueUpDirection()
    {
        // 基于???点数据的真实上方向计算
        Mesh mesh = targetModel.sharedMesh;
        Vector3[] vertices = mesh.vertices;

        // 找到Y方向极端点
        Vector3 minY = vertices[0];
        Vector3 maxY = vertices[0];
        foreach (Vector3 v in vertices)
        {
            if (v.y < minY.y) minY = v;
            if (v.y > maxY.y) maxY = v;
        }

        // 转换为世界坐标
        Vector3 worldMin = targetModel.transform.TransformPoint(minY);
        Vector3 worldMax = targetModel.transform.TransformPoint(maxY);

        return (worldMax - worldMin).normalized;
    }

    float CalculateDynamicXLength()
    {
        // 计算考虑模型缩放后的X轴长度
        Vector3 scaledSize = Vector3.Scale(
            modelBounds.size,
            targetModel.transform.lossyScale
        );

        // 应用安全系数防止溢出
        float safetyMargin = 0.02f;
        return Mathf.Clamp(
            scaledSize.x * 0.85f,
            modelBounds.size.x * 0.1f,
            scaledSize.x * 2f
        ) - safetyMargin;
    }
    float ParseZPositionFromName(string axisName)
    {
        // 使用正则表达式解析命名格式"X_Axis_{zPosition}"
        System.Text.RegularExpressions.Match match =
            System.Text.RegularExpressions.Regex.Match(
                axisName,
                @"X_Axis_([-+]?\d*\.?\d+)"
            );

        if (match.Success &&
            float.TryParse(match.Groups[1].Value, out float zPos))
        {
            return zPos;
        }

        // 容错机制：通过空间关系重新计算
        Debug.LogWarning($"无法解析坐标轴位置: {axisName}");
        LineRenderer lr = GameObject.Find(axisName).GetComponent<LineRenderer>();
        return Vector3.Dot(
            lr.GetPosition(0) - GetEdgeStartPoint(),
            GetTrueZDirection()
        );
    }
    
    #endregion

    #region 通用配置方法
    void ConfigureAxisRenderer(LineRenderer lr, Color color)
    {
        lr.material = axisMaterial;
        lr.startColor = color;
        lr.endColor = color;
        lr.startWidth = axisWidth;
        lr.endWidth = axisWidth;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.useWorldSpace = true;
    }
    #endregion

    #region 动态更新系统
    void HandleDynamicUpdate()
    {
        if (targetModel.transform.lossyScale != lastModelScale)
        {
            UpdateAllAxes();
            lastModelScale = targetModel.transform.lossyScale;
        }
    }

    void UpdateAllAxes()
    {
        // 更新Z轴
        Vector3 newZStart = GetEdgeStartPoint();
        Vector3 newZEnd = newZStart + GetTrueZDirection() * GetZAxisLength();
        zAxis.SetPositions(new Vector3[] { newZStart, newZEnd });

        // 更新所有X轴
        for (int i = 0; i < dynamicXAxes.Count; i++)
        {
            LineRenderer lr = dynamicXAxes[i];
            float zPos = ParseZPositionFromName(lr.gameObject.name);

            Vector3 newStart = newZStart + GetTrueZDirection() * zPos;
            Vector3 newEnd = newStart + GetTrueXDirection() * CalculateDynamicXLength();

            lr.SetPositions(new Vector3[] { newStart, newEnd });
        }
    }
    #endregion

    #region 标签系统
    void CreateAxisLabel(Vector3 position, string text, Color color)
    {
        GameObject label = new GameObject($"Label_{text}");
        label.transform.SetParent(transform);
        label.transform.position = position;

        TextMeshPro tmp = label.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = labelFontSize;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.font = labelFont;

        label.AddComponent<Billboard>().targetCamera = mainCamera;
    }

    void CreateTickLabel(Vector3 basePos, string text)
    {
        // 获取真实坐标系方向
        Vector3 zDir = GetTrueZDirection();
        Vector3 xDir = GetTrueXDirection();
        Vector3 upDir = GetTrueUpDirection();

        // 动态计算偏移量
        float dynamicYOffset = modelBounds.size.y * 0.1f;
        float dynamicXOffset = modelBounds.size.x * 0.05f;

        // 三维螺旋布局
        Vector3 labelPos = basePos +
                          zDir * (labelDistance * 0.3f) +
                          upDir * dynamicYOffset +
                          xDir * dynamicXOffset;

        // 创建标签对象
        GameObject label = new GameObject($"TickLabel_{text}");
        label.transform.position = labelPos;

        // 添加自动旋转组件
        SmartBillboard sb = label.AddComponent<SmartBillboard>();
        sb.Initialize(mainCamera, basePos);

        TextMeshPro tmp = label.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = labelFontSize;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Bottom;
        tmp.font = labelFont;
        tmp.color = Color.black;

    }
    class SmartBillboard : MonoBehaviour
    {
        private Camera targetCam;
        private Vector3 anchorPoint;
        private float checkRadius = 0.5f;

        public void Initialize(Camera cam, Vector3 anchor)
        {
            targetCam = cam;
            anchorPoint = anchor;
            StartCoroutine(AntiOverlapCheck());
        }

        IEnumerator AntiOverlapCheck()
        {
            while (true)
            {
                Collider[] colliders = Physics.OverlapSphere(transform.position, checkRadius);
                foreach (var col in colliders)
                {
                    if (col.gameObject != gameObject && col.CompareTag("AxisLabel"))
                    {
                        // 动态调整位置
                        Vector3 escapeDir = (transform.position - anchorPoint).normalized;
                        transform.position += escapeDir * checkRadius * 0.5f;
                    }
                }
                yield return new WaitForSeconds(0.2f);
            }
        }
    }


    void OnDrawGizmosSelected()
    {
        // 绘制Z轴方向
        Gizmos.color = Color.cyan;
        Vector3 zStart = GetEdgeStartPoint();
        Gizmos.DrawSphere(zStart, 0.1f);
        Gizmos.DrawLine(zStart, zStart + GetTrueZDirection() * 2f);

        // 绘制X轴方向
        Gizmos.color = Color.red;
        Vector3 xStart = zStart;
        Gizmos.DrawLine(xStart, xStart + GetTrueXDirection() * 2f);
    }

    void LogAxisParameters()
    {
        Debug.Log($"Z轴起始点: {GetEdgeStartPoint()}");
        Debug.Log($"Z轴方向: {GetTrueZDirection()}");
        Debug.Log($"X轴长度: {Vector3.Scale(modelBounds.size, targetModel.transform.lossyScale).x}");
    }

    #endregion

    #region 对象池管理
    GameObject GetPooledTick()
    {
        foreach (var t in ticksPool)
        {
            if (!t.activeSelf) return t;
        }

        GameObject newTick = new GameObject("Tick");
        LineRenderer lr = newTick.AddComponent<LineRenderer>();
        lr.material = tickMaterial;
        lr.startWidth = axisWidth * 0.5f;
        lr.endWidth = axisWidth * 0.5f;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ticksPool.Add(newTick);
        return newTick;
    }
    #endregion

    #region 交互系统
    void HandleInteraction()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100))
            {
                if (hit.collider.transform.parent == transform)
                {
                    HandleAxisClick(hit.point);
                }
            }
        }
    }

    void HandleAxisClick(Vector3 worldPos)
    {
        // 将点击位置转换为局部坐标
        Vector3 localPos = targetModel.transform.InverseTransformPoint(worldPos);

        // 计算对应年份
        float zOffset = Vector3.Dot(localPos, Vector3.forward);
        float normalizedYear = Mathf.InverseLerp(0, modelBounds.size.z, zOffset);
        int selectedYear = Mathf.RoundToInt(Mathf.Lerp(startYear, endYear, normalizedYear));

        Debug.Log($"选中年份: {selectedYear}");
    }
    #endregion

    #region 性能优化
    [Header("性能优化")]
    public bool enableLOD = true;
    [Range(1, 50)] public int lodThreshold = 15;
    [Range(10, 100)] public float lodDistance = 30f;

    void UpdateLOD()
    {
        if (!enableLOD) return;

        float distance = Vector3.Distance(
            mainCamera.transform.position,
            transform.position
        );

        bool highDetail = distance < lodDistance;

        // 控制刻度显示
        foreach (var tick in ticksPool)
        {
            tick.SetActive(highDetail);
        }

        // 控制X轴密度
        for (int i = 0; i < dynamicXAxes.Count; i++)
        {
            bool show = highDetail || (i % lodThreshold == 0);
            dynamicXAxes[i].gameObject.SetActive(show);
        }
    }
    #endregion

    #region 清理系统
    void OnDestroy()
    {
        foreach (var tick in ticksPool)
        {
            if (tick != null) Destroy(tick);
        }

        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }

        dynamicXAxes.Clear();
    }
    #endregion

    #region 辅助类
    public class Billboard : MonoBehaviour
    {
        [HideInInspector] public Camera targetCamera;
        [Tooltip("旋转偏移角度")] public Vector3 rotationOffset = Vector3.zero;
        [Tooltip("是否反向")] public bool flipDirection = true;


        void LateUpdate()
        {
            if (targetCamera == null) return;

            Vector3 dirToCamera = transform.position - targetCamera.transform.position;
            // 添加反向控制
            if (flipDirection)
            {
                dirToCamera = -dirToCamera;
            }
            Quaternion lookRot = Quaternion.LookRotation(dirToCamera);


            transform.rotation = lookRot * Quaternion.Euler(rotationOffset);
        }
    }
    #endregion
}
*/