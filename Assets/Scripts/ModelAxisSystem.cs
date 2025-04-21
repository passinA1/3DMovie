/*using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(LineRenderer))]
public class ModelAxisSystem : MonoBehaviour
{
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
    public Material tickMaterial;
    public TMP_FontAsset labelFont;
    public Color xAxisColor = Color.red;
    public Color zAxisColor = Color.blue;
    public float labelFontSize = 800f;

    // �̶�����
    [Header("�̶�����")]
    public float tickSize = 0.1f;

    // ???��ʱ����
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

    #region ��ʼ��ϵͳ
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
            Debug.LogError("Ŀ��ģ��δ����!");
            return false;
        }
        if (axisMaterial == null || tickMaterial == null)
        {
            Debug.LogError("����δ����!");
            return false;
        }
        return true;
    }
    #endregion

    #region �������ᴴ��
    void CreateMainAxes()
    {
        // ����Z��
        GameObject zAxisObj = new GameObject("Z_Axis");
        zAxisObj.transform.SetParent(transform);

        zAxis = zAxisObj.AddComponent<LineRenderer>();
        ConfigureAxisRenderer(zAxis, zAxisColor);

        // ��ȡ��ʵZ�᷽��
        Vector3 trueZDirection = GetTrueZDirection();

        // ����Z�����
        Vector3 zStart = GetEdgeStartPoint();
        Vector3 zEnd = zStart + trueZDirection * GetZAxisLength();

        zAxis.SetPositions(new Vector3[] { zStart, zEnd });
        CreateAxisLabel(zEnd + targetModel.transform.forward * labelDistance, "Z", zAxisColor);
        Debug.DrawRay(zStart, trueZDirection * 5f, Color.green, 10f);
        
    }
    Vector3 GetTrueZDirection()
    {
        // ��ȡģ����������
        Mesh mesh = targetModel.sharedMesh;
        Vector3[] vertices = mesh.vertices;

        // Ѱ��Z�᷽����������˶���
        Vector3 minZVert = vertices[0];
        Vector3 maxZVert = vertices[0];
        foreach (Vector3 v in vertices)
        {
            if (v.z < minZVert.z) minZVert = v;
            if (v.z > maxZVert.z) maxZVert = v;
        }

        // ת��������ռ�
        Vector3 worldMin = targetModel.transform.TransformPoint(minZVert);
        Vector3 worldMax = targetModel.transform.TransformPoint(maxZVert);

        return (worldMax - worldMin).normalized;
    }

    Vector3 GetEdgeStartPoint()
    {
        // ��ȡģ�ͱ任����
        Matrix4x4 modelMatrix = targetModel.transform.localToWorldMatrix;

        // ����ʵ��ǰԵ�㣨����Y�ᣩ
        Vector3 localFrontEdge = new Vector3(
            modelBounds.min.x,
            0, // Y���õ�
            modelBounds.min.z + modelBounds.size.z * zAxisOffset
        );

        // Ӧ��ģ�ͱ任
        return modelMatrix.MultiplyPoint3x4(localFrontEdge);
    }

    float GetZAxisLength()
    {
        Vector3 scaledSize = Vector3.Scale(modelBounds.size, targetModel.transform.lossyScale);
        return scaledSize.z * (1 - zAxisOffset * 2);
    }
    #endregion

    #region ʱ����ϵͳ
    IEnumerator GenerateTimeAxisAsync()
    {
        int yearCount = endYear - startYear + 1;
        float yearStep = totalZLength / (yearCount - 1);

        // ����ʱ��̶Ⱥ�X��
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

        // ����˫��̶�
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

        // ������ʼ�㣨��Ӵ�ֱƫ�ƣ�
        Vector3 startPoint = GetEdgeStartPoint() + zDir * zPosition;
        startPoint += Vector3.up * 0.2f; // ��ֹ��ƽ���ص�

        // ����ʵ��X�᳤��
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
        // ����???�����ݵ���ʵ�Ϸ������
        Mesh mesh = targetModel.sharedMesh;
        Vector3[] vertices = mesh.vertices;

        // �ҵ�Y���򼫶˵�
        Vector3 minY = vertices[0];
        Vector3 maxY = vertices[0];
        foreach (Vector3 v in vertices)
        {
            if (v.y < minY.y) minY = v;
            if (v.y > maxY.y) maxY = v;
        }

        // ת��Ϊ��������
        Vector3 worldMin = targetModel.transform.TransformPoint(minY);
        Vector3 worldMax = targetModel.transform.TransformPoint(maxY);

        return (worldMax - worldMin).normalized;
    }

    float CalculateDynamicXLength()
    {
        // ���㿼��ģ�����ź��X�᳤��
        Vector3 scaledSize = Vector3.Scale(
            modelBounds.size,
            targetModel.transform.lossyScale
        );

        // Ӧ�ð�ȫϵ����ֹ���
        float safetyMargin = 0.02f;
        return Mathf.Clamp(
            scaledSize.x * 0.85f,
            modelBounds.size.x * 0.1f,
            scaledSize.x * 2f
        ) - safetyMargin;
    }
    float ParseZPositionFromName(string axisName)
    {
        // ʹ��������ʽ����������ʽ"X_Axis_{zPosition}"
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

        // �ݴ���ƣ�ͨ���ռ��ϵ���¼���
        Debug.LogWarning($"�޷�����������λ��: {axisName}");
        LineRenderer lr = GameObject.Find(axisName).GetComponent<LineRenderer>();
        return Vector3.Dot(
            lr.GetPosition(0) - GetEdgeStartPoint(),
            GetTrueZDirection()
        );
    }
    
    #endregion

    #region ͨ�����÷���
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

    #region ��̬����ϵͳ
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
        // ����Z��
        Vector3 newZStart = GetEdgeStartPoint();
        Vector3 newZEnd = newZStart + GetTrueZDirection() * GetZAxisLength();
        zAxis.SetPositions(new Vector3[] { newZStart, newZEnd });

        // ��������X��
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

    #region ��ǩϵͳ
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
        // ��ȡ��ʵ����ϵ����
        Vector3 zDir = GetTrueZDirection();
        Vector3 xDir = GetTrueXDirection();
        Vector3 upDir = GetTrueUpDirection();

        // ��̬����ƫ����
        float dynamicYOffset = modelBounds.size.y * 0.1f;
        float dynamicXOffset = modelBounds.size.x * 0.05f;

        // ��ά��������
        Vector3 labelPos = basePos +
                          zDir * (labelDistance * 0.3f) +
                          upDir * dynamicYOffset +
                          xDir * dynamicXOffset;

        // ������ǩ����
        GameObject label = new GameObject($"TickLabel_{text}");
        label.transform.position = labelPos;

        // ����Զ���ת���
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
                        // ��̬����λ��
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
        // ����Z�᷽��
        Gizmos.color = Color.cyan;
        Vector3 zStart = GetEdgeStartPoint();
        Gizmos.DrawSphere(zStart, 0.1f);
        Gizmos.DrawLine(zStart, zStart + GetTrueZDirection() * 2f);

        // ����X�᷽��
        Gizmos.color = Color.red;
        Vector3 xStart = zStart;
        Gizmos.DrawLine(xStart, xStart + GetTrueXDirection() * 2f);
    }

    void LogAxisParameters()
    {
        Debug.Log($"Z����ʼ��: {GetEdgeStartPoint()}");
        Debug.Log($"Z�᷽��: {GetTrueZDirection()}");
        Debug.Log($"X�᳤��: {Vector3.Scale(modelBounds.size, targetModel.transform.lossyScale).x}");
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
        LineRenderer lr = newTick.AddComponent<LineRenderer>();
        lr.material = tickMaterial;
        lr.startWidth = axisWidth * 0.5f;
        lr.endWidth = axisWidth * 0.5f;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ticksPool.Add(newTick);
        return newTick;
    }
    #endregion

    #region ����ϵͳ
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
        // �����λ��ת��Ϊ�ֲ�����
        Vector3 localPos = targetModel.transform.InverseTransformPoint(worldPos);

        // �����Ӧ���
        float zOffset = Vector3.Dot(localPos, Vector3.forward);
        float normalizedYear = Mathf.InverseLerp(0, modelBounds.size.z, zOffset);
        int selectedYear = Mathf.RoundToInt(Mathf.Lerp(startYear, endYear, normalizedYear));

        Debug.Log($"ѡ�����: {selectedYear}");
    }
    #endregion

    #region �����Ż�
    [Header("�����Ż�")]
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

        // ���ƿ̶���ʾ
        foreach (var tick in ticksPool)
        {
            tick.SetActive(highDetail);
        }

        // ����X���ܶ�
        for (int i = 0; i < dynamicXAxes.Count; i++)
        {
            bool show = highDetail || (i % lodThreshold == 0);
            dynamicXAxes[i].gameObject.SetActive(show);
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

        dynamicXAxes.Clear();
    }
    #endregion

    #region ������
    public class Billboard : MonoBehaviour
    {
        [HideInInspector] public Camera targetCamera;
        [Tooltip("��תƫ�ƽǶ�")] public Vector3 rotationOffset = Vector3.zero;
        [Tooltip("�Ƿ���")] public bool flipDirection = true;


        void LateUpdate()
        {
            if (targetCamera == null) return;

            Vector3 dirToCamera = transform.position - targetCamera.transform.position;
            // ��ӷ������
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