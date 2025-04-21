using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(LineRenderer))]
public class ModelAxisSystem : MonoBehaviour
{
    // �������������
    [Header("���������")]
    public Camera modelCamera;
    public Camera uiCamera;

    [Header("�㼶����")]
    [Tooltip("��Ⱦ�㼶����")]
    public string renderLayer = "3DModel";
    private int modelLayer;

    // ��������
    [Header("Base Settings")]
    public MeshFilter targetModel;
    [Tooltip("��������")] public float axisWidth = 0.02f;
    [Tooltip("��ǩƫ�ƾ���")] public float labelDistance = 0.3f;

    // ʱ��������
    [Header("ʱ��������")]
    public int startYear = 1990;
    public int endYear = 2020;
    [Range(0f, 1f)] public float zAxisOffset = 0f;

    // ���ӻ�Ԫ��
    [Header("Visual Elements")]
    public Material axisMaterial;
    public Material gridMaterial;
    public TMP_FontAsset labelFont;
    public Color zAxisColor = Color.blue;
    public float labelFontSize = 800f;

    // �̶�����
    [Header("�̶�����")]
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

    [Header("��������")]
    [Tooltip("�����߿��")]
    public float gridLineWidth = 0.01f;  // �����߿�ȱ���

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

    #region ��ʼ��ϵͳ
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
            Debug.LogError("Ŀ��ģ��δ����!");
            return false;
        }
        if (axisMaterial == null || gridMaterial == null)
        {
            Debug.LogError("����δ����!");
            return false;
        }
        return true;
    }
    #endregion

    #region �������ᴴ��
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
        // ת������������ϵ
        Vector3 worldMinZ = targetModel.transform.TransformPoint(localMinZ);
        Vector3 worldMaxZ = targetModel.transform.TransformPoint(localMaxZ);
        Vector3 worldMinX = targetModel.transform.TransformPoint(localMinX);
        Vector3 worldMaxX = targetModel.transform.TransformPoint(localMaxX);

        // ����ʵ�ʷ�������
        trueZDirection = (worldMaxZ - worldMinZ).normalized;
        trueXDirection = (worldMaxX - worldMinX).normalized;

        // ȷ��������
        trueXDirection = Vector3.Cross(trueZDirection, Vector3.up).normalized;
    }

    Vector3 GetLocalEdgeStartPoint()
    {
        // ʹ��ģ������ϵ�������
        return new Vector3(
            modelBounds.min.x,
            0,  // ����ԭʼY����
            modelBounds.min.z + modelBounds.size.z * zAxisOffset
        );
    }


    void CreateMainAxes()
    {
        // ����Z��
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

    #region ʱ������������
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

        // ������ֱ�̶�
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

        // ��ȡ��׼���ģ�ͱ�������
        Vector3 localBase = GetLocalEdgeStartPoint();

        // ��ģ�ͱ���Z��ƫ��
        Vector3 localLineStart = localBase + new Vector3(0, 0, zPosition/130);
        
       
        // ת��Ϊ��������
        Vector3 worldLineStart = targetModel.transform.TransformPoint(localLineStart);

        // ����ʵ��X�᳤�ȣ��������ź���ת��
        float scaledXLength = Vector3.Scale(modelBounds.size, targetModel.transform.lossyScale).x;

        // ���������߶���
        GameObject gridObj = new GameObject($"GridLine_{zPosition}");
        gridObj.transform.SetParent(targetModel.transform); // ����Ϊģ���Ӷ���
        gridObj.layer = modelLayer;  // ���������߲㼶

        LineRenderer lr = gridObj.AddComponent<LineRenderer>();
        ConfigureGridRenderer(lr);
        lr.useWorldSpace = false;

        // ���������߶˵㣨������ʵ����
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

    #region ͨ�����÷���
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

    #region ��ǩϵͳ
    void CreateAxisLabel(Vector3 position, string text, Color color)
    {
        GameObject label = new GameObject($"Label_{text}");
        label.transform.SetParent(targetModel.transform); // ����Ϊģ���Ӷ���
        label.layer = modelLayer;  // ���ñ�ǩ�㼶
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
        label.transform.SetParent(targetModel.transform); // ����Ϊģ���Ӷ���
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

    #region ����ع���
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

    #region ��̬����ϵͳ
    void UpdateAllAxes()
    {
        // ÿ�θ���ʱ���¼��㷽��
        CalculateTrueDirections();

        totalZLength = GetZAxisLength();
        // ����Z��
        Vector3 localZStart = GetLocalEdgeStartPoint();
        Vector3 newZStart = targetModel.transform.TransformPoint(localZStart);
        Vector3 newZEnd = newZStart + trueZDirection * totalZLength;
        zAxis.SetPositions(new Vector3[] { newZStart, newZEnd });

        // ��������������
        foreach (LineRenderer lr in gridLines)
        {
            float zPos = ParseZPositionFromName(lr.gameObject.name);
            // ���������
            Vector3 newLocalStart = localZStart + new Vector3(0, 0, zPos);
            Vector3 newWorldStart = targetModel.transform.TransformPoint(newLocalStart);

            // ���¼������ź��X����
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

    #region ������
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

    #region ����ϵͳ
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