using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(LineRenderer))]
public class ModelAxisSystem : MonoBehaviour
{
    // 新增摄像机引用
    [Header("摄像机设置")]
    public Camera modelCamera;
    public Camera uiCamera;

    [Header("层级设置")]
    [Tooltip("渲染层级名称")]
    public string renderLayer = "3DModel";
    private int modelLayer;

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
    public Material gridMaterial;
    public TMP_FontAsset labelFont;
    public Color zAxisColor = Color.blue;
    public float labelFontSize = 800f;

    // 刻度配置
    [Header("刻度设置")]
    public float tickSize = 0.1f;

    private LineRenderer zAxis;
    private readonly List<LineRenderer> gridLines = new List<LineRenderer>();
    private List<GameObject> ticksPool = new List<GameObject>();
    private Bounds modelBounds;
    private Vector3 lastModelScale;
    private Camera mainCamera;
    private float totalZLength;
    private Vector3 trueZDirection;
    private Vector3 trueXDirection;

    [Header("网格设置")]
    [Tooltip("网格线宽度")]
    public float gridLineWidth = 0.01f;  // 网格线宽度变量

    void Awake()
    {
        mainCamera = modelCamera;
        InitializeAxisSystem();
        lastModelScale = targetModel.transform.lossyScale;
        modelLayer = LayerMask.NameToLayer(renderLayer);
    }

    void Update()
    {
        if (targetModel.transform.lossyScale != lastModelScale)
        {
            UpdateAllAxes();
            lastModelScale = targetModel.transform.lossyScale;
        }
    }

    #region 初始化系统
    void InitializeAxisSystem()
    {
        if (!ValidateComponents()) return;

        modelBounds = targetModel.sharedMesh.bounds;
        CalculateTrueDirections();
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
        if (axisMaterial == null || gridMaterial == null)
        {
            Debug.LogError("材质未分配!");
            return false;
        }
        return true;
    }
    #endregion

    #region 主坐标轴创建
    void CalculateTrueDirections()
    {
        Mesh mesh = targetModel.sharedMesh;
        Vector3[] vertices = mesh.vertices;

        Vector3 localMinZ = new Vector3(0, 0, float.MaxValue);
        Vector3 localMaxZ = new Vector3(0, 0, float.MinValue);
        Vector3 localMinX = new Vector3(float.MaxValue, 0, 0);
        Vector3 localMaxX = new Vector3(float.MinValue, 0, 0);

        foreach (Vector3 v in vertices)
        {
            if (v.z < localMinZ.z) localMinZ = v;
            if (v.z > localMaxZ.z) localMaxZ = v;
            if (v.x < localMinX.x) localMinX = v;
            if (v.x > localMaxX.x) localMaxX = v;
        }
        // 转换到世界坐标系
        Vector3 worldMinZ = targetModel.transform.TransformPoint(localMinZ);
        Vector3 worldMaxZ = targetModel.transform.TransformPoint(localMaxZ);
        Vector3 worldMinX = targetModel.transform.TransformPoint(localMinX);
        Vector3 worldMaxX = targetModel.transform.TransformPoint(localMaxX);

        // 计算实际方向向量
        trueZDirection = (worldMaxZ - worldMinZ).normalized;
        trueXDirection = (worldMaxX - worldMinX).normalized;

        // 确保正交性
        trueXDirection = Vector3.Cross(trueZDirection, Vector3.up).normalized;
    }

    Vector3 GetLocalEdgeStartPoint()
    {
        // 使用模型坐标系计算起点
        return new Vector3(
            modelBounds.min.x,
            0,  // 保持原始Y坐标
            modelBounds.min.z + modelBounds.size.z * zAxisOffset
        );
    }


    void CreateMainAxes()
    {
        // 创建Z轴
        GameObject zAxisObj = new GameObject("Z_Axis");
        zAxisObj.layer = modelLayer;
        zAxisObj.transform.SetParent(targetModel.transform);
        zAxisObj.transform.localPosition = Vector3.zero;
        zAxis = zAxisObj.AddComponent<LineRenderer>();
        ConfigureAxisRenderer(zAxis, zAxisColor);

        Vector3 zStart = targetModel.transform.TransformPoint(GetLocalEdgeStartPoint());
        totalZLength = GetZAxisLength();
        Vector3 zEnd = zStart + targetModel.transform.forward * totalZLength;

        zAxis.useWorldSpace = true;
        zAxis.SetPositions(new Vector3[] { zStart, zEnd });
        CreateAxisLabel(zEnd + Vector3.forward * labelDistance, "Time", zAxisColor);
    }

    Vector3 GetEdgeStartPoint()
    {
        Matrix4x4 modelMatrix = targetModel.transform.localToWorldMatrix;
        Vector3 localFrontEdge = new Vector3(
            modelBounds.min.x,
            0,
            modelBounds.min.z + modelBounds.size.z * zAxisOffset
        );
        return modelMatrix.MultiplyPoint3x4(localFrontEdge);
    }

    float GetZAxisLength()
    {
        Vector3 scaledSize = Vector3.Scale(modelBounds.size, targetModel.transform.lossyScale);
        return scaledSize.z * (1 - zAxisOffset * 2);
    }
    #endregion

    #region 时间轴网格生成
    IEnumerator GenerateTimeAxisAsync()
    {
        int yearCount = endYear - startYear + 1;
        float yearStep = totalZLength / (yearCount - 1);
        for (int i = 0; i < yearCount; i++)
        {
            
            CreateYearTick(i * yearStep, startYear + i);
            CreateGridLine(i * yearStep);

            if (i % 5 == 0) yield return null;
        }
    }

    void CreateYearTick(float position, int year)
    {
        GameObject tick = GetPooledTick();
        tick.SetActive(true);
        tick.transform.SetParent(transform);

        LineRenderer lr = tick.GetComponent<LineRenderer>();
        Vector3 basePos = GetEdgeStartPoint() + trueZDirection * position;

        // 创建垂直刻度
        Vector3 tickDir = Vector3.Cross(trueZDirection, trueXDirection).normalized;
        lr.SetPositions(new Vector3[] {
            basePos - tickDir * tickSize,
            basePos + tickDir * tickSize
        });

        CreateTickLabel(basePos + tickDir * labelDistance, year.ToString());
    }

    void CreateGridLine(float zPosition)
    {
        //Debug.Log("zPosition: "+zPosition);

        // 获取基准点的模型本地坐标
        Vector3 localBase = GetLocalEdgeStartPoint();

        // 沿模型本地Z轴偏移
        Vector3 localLineStart = localBase + new Vector3(0, 0, zPosition/130);
        
       
        // 转换为世界坐标
        Vector3 worldLineStart = targetModel.transform.TransformPoint(localLineStart);

        // 计算实际X轴长度（考虑缩放和旋转）
        float scaledXLength = Vector3.Scale(modelBounds.size, targetModel.transform.lossyScale).x;

        // 创建网格线对象
        GameObject gridObj = new GameObject($"GridLine_{zPosition}");
        gridObj.transform.SetParent(targetModel.transform); // 设置为模型子对象
        gridObj.layer = modelLayer;  // 设置网格线层级

        LineRenderer lr = gridObj.AddComponent<LineRenderer>();
        ConfigureGridRenderer(lr);
        lr.useWorldSpace = false;

        // 设置网格线端点（基于真实方向）
        lr.SetPositions(new Vector3[] {
        worldLineStart,
        worldLineStart - trueXDirection * scaledXLength
    });

        gridLines.Add(lr);
    }

    void ConfigureGridRenderer(LineRenderer lr)
    {
        lr.material = gridMaterial;
        lr.startColor = Color.gray;
        lr.endColor = Color.gray;
        lr.startWidth = axisWidth * 0.5f;
        lr.endWidth = axisWidth * 0.5f;
        lr.useWorldSpace = false;
    }
    #endregion

    #region 通用配置方法
    void ConfigureAxisRenderer(LineRenderer lr, Color color)
    {
        lr.material = axisMaterial;
        lr.startColor = color;
        lr.endColor = color;
        lr.startWidth = gridLineWidth;
        lr.endWidth = gridLineWidth;
        lr.useWorldSpace = true;
    }
    #endregion

    #region 标签系统
    void CreateAxisLabel(Vector3 position, string text, Color color)
    {
        GameObject label = new GameObject($"Label_{text}");
        label.transform.SetParent(targetModel.transform); // 设置为模型子对象
        label.layer = modelLayer;  // 设置标签层级
        label.transform.position = position;

        TextMeshPro tmp = label.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = labelFontSize;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.font = labelFont;

        label.AddComponent<Billboard>().targetCamera = mainCamera;
    }

    void CreateTickLabel(Vector3 position, string text)
    {
        GameObject label = new GameObject($"TickLabel_{text}");
        label.transform.SetParent(targetModel.transform); // 设置为模型子对象
        label.layer = modelLayer;
        label.transform.position = position;

        TextMeshPro tmp = label.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = labelFontSize * 0.5f;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Bottom;
        tmp.font = labelFont;

        //label.AddComponent<Billboard>().targetCamera = mainCamera;
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
        newTick.layer = modelLayer;
        LineRenderer lr = newTick.AddComponent<LineRenderer>();
        lr.material = gridMaterial;
        lr.startWidth = axisWidth * 0.3f;
        lr.endWidth = axisWidth * 0.3f;
        ticksPool.Add(newTick);
        return newTick;
    }
    #endregion

    #region 动态更新系统
    void UpdateAllAxes()
    {
        // 每次更新时重新计算方向
        CalculateTrueDirections();

        totalZLength = GetZAxisLength();
        // 更新Z轴
        Vector3 localZStart = GetLocalEdgeStartPoint();
        Vector3 newZStart = targetModel.transform.TransformPoint(localZStart);
        Vector3 newZEnd = newZStart + trueZDirection * totalZLength;
        zAxis.SetPositions(new Vector3[] { newZStart, newZEnd });

        // 更新所有网格线
        foreach (LineRenderer lr in gridLines)
        {
            float zPos = ParseZPositionFromName(lr.gameObject.name);
            // 计算新起点
            Vector3 newLocalStart = localZStart + new Vector3(0, 0, zPos);
            Vector3 newWorldStart = targetModel.transform.TransformPoint(newLocalStart);

            // 重新计算缩放后的X长度
            float scaledXLength = Vector3.Scale(modelBounds.size, targetModel.transform.lossyScale).x;
            

            lr.SetPositions(new Vector3[] { newWorldStart, trueXDirection*scaledXLength });
        }
    }

    float ParseZPositionFromName(string axisName)
    {
        string[] parts = axisName.Split('_');
        if (parts.Length >= 3 && float.TryParse(parts[2], out float zPos))
        {
            return zPos;
        }
        return 0f;
    }
    #endregion

    #region 辅助类
    public class Billboard : MonoBehaviour
    {
        public Camera targetCamera;

        void LateUpdate()
        {
            if (targetCamera == null) return;
            transform.rotation = targetCamera.transform.rotation;
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

        gridLines.Clear();
    }
    #endregion
}