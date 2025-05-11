using UnityEngine;
using TMPro;
using System.Collections.Generic;

[RequireComponent(typeof(ModelAxisSystem))]
public class GenreAxisManager : MonoBehaviour
{
    [Header("Genre Settings")]
    public List<string> genres = new List<string> {
        "Drama", "Comedy", "Thriller", "Action", "Romance", "Documentary",
        "Horror", "Crime", "Adventure", "Science Fiction", "Family", "Mystery",
        "Fantasy", "Animation", "Foreign", "Music", "History", "Movie", "War", "Western"
    };

    [Header("Label Settings")]
    public float labelOffset = 0.3f;
    public Color labelColor = Color.white;
    public float labelFontSize = 600f;
    public TMP_FontAsset labelFont;
    public float labelSpacingFactor = 1.1f;

    private ModelAxisSystem axisSystem;
    private List<GameObject> genreLabels = new List<GameObject>();

    void Awake()
    {
        axisSystem = GetComponent<ModelAxisSystem>();
        InitializeGenreAxis();
    }

    void InitializeGenreAxis()
    {
        if (axisSystem == null || genres.Count == 0) return;

        // ��ȡģ�ͱ߽���Ϣ
        Bounds modelBounds = axisSystem.targetModel.sharedMesh.bounds;
        Vector3 modelScale = axisSystem.targetModel.transform.lossyScale;

        // ����X��ʵ�ʳ���
        float xAxisLength = modelBounds.size.x * modelScale.x;
        Vector3 axisStartPoint = GetXAxisStartPoint();

        // �����ǩ���
        float spacing = xAxisLength / (genres.Count - 1) * labelSpacingFactor;

        for (int i = 0; i < genres.Count; i++)
        {
            // �����ǩλ��
            Vector3 labelPosition = axisStartPoint +
                axisSystem.trueXDirection * (spacing * i) +
                axisSystem.trueZDirection * axisSystem.zAxisOffset;

            CreateGenreLabel(labelPosition, genres[i], i);
        }
    }

    Vector3 GetXAxisStartPoint()
    {
        // ��ȡģ�ͱ����������
        Vector3 localStart = new Vector3(
            axisSystem.modelBounds.min.x,
            0,
            axisSystem.modelBounds.min.z + axisSystem.modelBounds.size.z * axisSystem.zAxisOffset
        );

        // ת��Ϊ��������
        return axisSystem.targetModel.transform.TransformPoint(localStart);
    }

    void CreateGenreLabel(Vector3 position, string genre, int index)
    {
        GameObject label = new GameObject($"GenreLabel_{genre}");
        label.transform.SetParent(axisSystem.targetModel.transform);
        label.layer = LayerMask.NameToLayer(axisSystem.renderLayer);

        // ����������
        TextMeshPro tmp = label.AddComponent<TextMeshPro>();
        tmp.text = genre;
        tmp.fontSize = labelFontSize;
        tmp.color = labelColor;
        tmp.alignment = TextAlignmentOptions.Midline;
        tmp.font = labelFont;

        // ����λ�úͷ���
        label.transform.position = position +
            Vector3.Cross(axisSystem.trueXDirection, Vector3.up).normalized * labelOffset;
        label.transform.rotation = Quaternion.LookRotation(
            axisSystem.modelCamera.transform.forward,
            axisSystem.modelCamera.transform.up
        );

        // ����Զ��������
        label.AddComponent<TextAutoScaler>().Initialize(axisSystem.modelCamera);
        genreLabels.Add(label);
    }

    void OnDestroy()
    {
        foreach (var label in genreLabels)
        {
            if (label != null) Destroy(label.gameObject);
        }
    }
}

// �Զ��������ִ�С�ĸ������
[RequireComponent(typeof(TextMeshPro))]
public class TextAutoScaler : MonoBehaviour
{
    private Camera targetCamera;
    private TextMeshPro tmp;
    private float baseFontSize;
    private float baseDistance;

    public void Initialize(Camera cam)
    {
        targetCamera = cam;
        tmp = GetComponent<TextMeshPro>();
        baseFontSize = tmp.fontSize;
        baseDistance = Vector3.Distance(transform.position, targetCamera.transform.position);
    }

    void Update()
    {
        if (targetCamera == null) return;

        float currentDistance = Vector3.Distance(transform.position, targetCamera.transform.position);
        tmp.fontSize = baseFontSize * (currentDistance / baseDistance);
    }
}