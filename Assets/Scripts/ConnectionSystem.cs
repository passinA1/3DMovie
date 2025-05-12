using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using System.Collections;

public class ConnectionSystem : MonoBehaviour
{   
    [Header("筛选组件")]
    public TMPro.TMP_Dropdown yearDropdown;
    public TMPro.TMP_Dropdown countryDropdown;
    public TMPro.TMP_Dropdown genreDropdown;

    [Header("性能优化")]
    [SerializeField] private int initialPoolSize = 500;
    [SerializeField] private int maxLinesPerFrame = 1000;

    // 筛选状态
    private HashSet<int> selectedYears = new HashSet<int>();
    private HashSet<string> selectedCountries = new HashSet<string>();
    private HashSet<string> selectedGenres = new HashSet<string>();

    // 对象池
    private Queue<LineRenderer> linePool = new Queue<LineRenderer>();
    private List<LineRenderer> activeLines = new List<LineRenderer>();

    [Header("连线参数")]
    public Gradient yearColorGradient;
    public float lineHeight = 0f;
    public Material lineMaterial;

    [Header("节点层级")]
    public Transform mapRoot; // 对应Canvas/Map
    public Transform planeRoot; // 对应Canvas/Plane

    [Header("层级绑定")]
    public Transform connectionParent;

    void Start()
    {
        InitializeDropdowns();
        InitializeObjectPool();
        StartCoroutine(DelayedInitialization());
    }

    IEnumerator DelayedInitialization()
    {
        yield return new WaitUntil(() => CSVReader.allMovies != null);
        PopulateDropdownOptions();
        UpdateAllLines();
    }

    void InitializeDropdowns()
    {
        yearDropdown.onValueChanged.AddListener(OnFilterChanged);
        countryDropdown.onValueChanged.AddListener(OnFilterChanged);
        genreDropdown.onValueChanged.AddListener(OnFilterChanged);
    }
    void InitializeObjectPool()
    {
        for (int i = 0; i < initialPoolSize; i++)
        {
            LineRenderer lr = CreateNewLine();
            lr.gameObject.SetActive(false);
            linePool.Enqueue(lr);
        }
    }

    void PopulateDropdownOptions()
    {
        // 动态填充年份下拉菜单
        var years = CSVReader.yearIndex.Keys
            .OrderBy(y => y)
            .Select(y => y.ToString())
            .ToList();
        yearDropdown.AddOptions(new List<string> { "All" }.Union(years).ToList());

        // 动态填充国家下拉菜单（匹配Map节点）
        var countries = CSVReader.countryIndex.Keys
            .OrderBy(c => c)
            .ToList();
        countryDropdown.AddOptions(new List<string> { "All" }.Union(countries).ToList());

        // 动态填充类型下拉菜单（匹配Plane/genres节点）
        var genres = CSVReader.genreIndex.Keys
            .OrderBy(g => g)
            .ToList();
        genreDropdown.AddOptions(new List<string> { "All" }.Union(genres).ToList());
    }

    void OnFilterChanged(int _)
    {
        UpdateSelections();
        UpdateAllLines();
    }

    void UpdateSelections()
    {
        /*// 年份单独处理为int类型
        selectedYears = GetSelectedYears(yearDropdown);

        // 国家和类型保持string类型
        selectedCountries = GetSelectedStrings(countryDropdown);
        selectedGenres = GetSelectedStrings(genreDropdown);*/

        // 获取选中的索引（单选）
        int yearIndex = yearDropdown.value;
        int countryIndex = countryDropdown.value;
        int genreIndex = genreDropdown.value;

        // 解析实际值（All选项对应索引0）
        selectedYears = yearIndex == 0 ? new HashSet<int>() :
            new HashSet<int> { int.Parse(yearDropdown.options[yearIndex].text) };

        selectedCountries = countryIndex == 0 ? new HashSet<string>() :
            new HashSet<string> { countryDropdown.options[countryIndex].text };

        selectedGenres = genreIndex == 0 ? new HashSet<string>() :
            new HashSet<string> { genreDropdown.options[genreIndex].text };
    }

    // 处理年份下拉菜单（返回int集合）
    HashSet<int> GetSelectedYears(TMP_Dropdown dropdown)
    {
        return new HashSet<int>(dropdown.options
            .Where((_, i) => dropdown.value == i) // 单选组件
            .Select(opt => int.Parse(opt.text))
            .ToList());
    }

    // 处理其他字符串下拉菜单
    HashSet<string> GetSelectedStrings(TMP_Dropdown dropdown)
    {
        return new HashSet<string>(dropdown.options
            .Where((_, i) => dropdown.value == i)
            .Select(opt => opt.text)
            .ToList());
    }

    void UpdateAllLines()
    {
        // 回收所有连线
        foreach (var line in activeLines)
        {
            line.gameObject.SetActive(false);
            linePool.Enqueue(line);
        }
        activeLines.Clear();

        // 获取筛选后的电影数据
        var filteredMovies = CSVReader.allMovies
            .Where(m => IsMovieValid(m))
            .ToList();

        // 分批生成连线
        StartCoroutine(GenerateLinesGradually(filteredMovies));
    }

    bool IsMovieValid(MovieData movie)
    {
        // 年份筛选
        if (selectedYears.Count > 0 && !selectedYears.Contains(movie.releaseYear))
            return false;

        // 国家筛选
        if (selectedCountries.Count > 0 && 
            !movie.productionCountries.Any(c => selectedCountries.Contains(c)))
            return false;

        // 类型筛选
        if (selectedGenres.Count > 0 && 
            !movie.genres.Any(g => selectedGenres.Contains(g)))
            return false;

        return true;
    }

    IEnumerator GenerateLinesGradually(List<MovieData> movies)
    {
        int count = 0;
        foreach (var movie in movies)
        {
            foreach (var country in movie.productionCountries)
            {
                Transform countryNode = FindNodeRecursive(mapRoot, country);
                if (!countryNode) continue;

                foreach (var genre in movie.genres)
                {
                    Transform genreNode = FindGenreNode(movie.releaseYear, genre);
                    if (!genreNode) continue;

                    if (count++ % maxLinesPerFrame == 0)
                        yield return null;

                    LineRenderer lr = GetLineFromPool();
                    SetupLine(lr, countryNode.position, genreNode.position, movie);
                    activeLines.Add(lr);
                }
            }
        }
    }

    LineRenderer GetLineFromPool()
    {
        if (linePool.Count > 0)
        {
            LineRenderer lr = linePool.Dequeue();
            lr.gameObject.SetActive(true);
            return lr;
        }
        return CreateNewLine();
    }

    LineRenderer CreateNewLine()
    {
        GameObject lineObj = new GameObject("ConnectionLine");

        lineObj.transform.SetParent(connectionParent);
        lineObj.layer = LayerMask.NameToLayer("3DModel");
        lineObj.transform.localPosition = Vector3.zero;

        LineRenderer lr = lineObj.AddComponent<LineRenderer>();
        lr.material = new Material(Shader.Find("Standard"));
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.positionCount = 2;
        return lr;
    }

    void SetupLine(LineRenderer lr, Vector3 start, Vector3 end, MovieData movie)
    {
        // 坐标转换（适配父级坐标系）
        Vector3 localStart = connectionParent.InverseTransformPoint(start);
        Vector3 localEnd = connectionParent.InverseTransformPoint(end);

        lr.useWorldSpace = false;
        lr.SetPosition(0, localStart);
        lr.SetPosition(1, localEnd);

        // 动态样式设置
        lr.startWidth = movie.normalizedRevenue * 3f;
        lr.endWidth = lr.startWidth * 3f;
        lr.material = lineMaterial;
        lr.material.color = movie.genreColor;
    }

    void GenerateAllLines()
    {
        if (CSVReader.allMovies == null || CSVReader.allMovies.Count == 0)
        {
            Debug.LogError("电影数据未加载");
            return;
        }

        foreach (var movie in CSVReader.allMovies)
        {
            CreateCountryToGenreLinks(movie);
        }
    }

    void CreateCountryToGenreLinks(MovieData movie)
    {   
        foreach (var country in movie.productionCountries)
        {
            string standardizedCountry = country switch
            {
                "United States of America" => "United States of America", // 匹配图片中的特殊名称
                "China" => "China",
                "Hong Kong" => "China",
                "TaiWan" => "China",
                "Macao" => "China",
                "Japan" => "Japan",

                "United Kingdom" => "United Kingdom",
                "France" => "France",
                "Italy" => "Italy",
                "Germany" => "Germany",
                "Spain" => "Spain",
                "Switzerland" => "Switzerland",
                "Belgium" => "Belgium",
                "Netherlands" => "Netherlands",


                "Australia" => "Australia",
                "New Zealand" => "New Zealand",





                _ => country.Replace(" ", "") // 处理其他国家名称的潜在空格问题
            };


            Transform countryNode = FindNodeRecursive(mapRoot, country);
            if (!countryNode)
            {
                Debug.LogWarning($"国家节点未找到 | 输入名: {country} | 转换后: {standardizedCountry}");
                continue;
            }

            foreach (var genre in movie.genres)
            {
                Transform genreNode = FindGenreNode(movie.releaseYear, genre);

                if (!genreNode)
                {
                    Debug.LogWarning($"类型节点未找到 | 年份: {movie.releaseYear} | 类型: {genre}");
                    continue;
                }

                if (genreNode)
                {
                    CreateLineRenderer(
                        start: countryNode.position ,
                        end: genreNode.position ,
                        width: movie.normalizedRevenue,
                        color: movie.genreColor
                    );
                }
            }
        }
    }

    Transform FindGenreNode(int year, string genre)
    {
        Transform yearParent = FindNodeRecursive(planeRoot, year.ToString());
        return yearParent?.Find("Cube/genres/" + genre);
    }

    Transform FindNodeRecursive(Transform parent, string targetName)
    {
        if (parent.name.Equals(targetName, System.StringComparison.OrdinalIgnoreCase))
            return parent;

        foreach (Transform child in parent)
        {
            Transform result = FindNodeRecursive(child, targetName);
            if (result != null) return result;
        }
        return null;
    }

    void CreateLineRenderer(Vector3 start, Vector3 end, float width, Color color)
    {
        GameObject lineObj = new GameObject("ConnectionLine");
        int modelLayer = LayerMask.NameToLayer("3DModel");

        if (modelLayer != -1)
        {
            lineObj.layer = modelLayer; // 设置父对象层级

            lineObj.transform.SetParent(connectionParent);
            lineObj.transform.localPosition = Vector3.zero;

            // 转换坐标到父空间
            Vector3 localStart = connectionParent.InverseTransformPoint(start);
            Vector3 localEnd = connectionParent.InverseTransformPoint(end);

            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            lr.useWorldSpace = false; // 禁用世界坐标系
            lr.positionCount = 2;
            lr.SetPosition(0, localStart);
            lr.SetPosition(1, localEnd);


            lr.material = new Material(Shader.Find("Standard"));
            lr.material.color = color;
            lr.material.SetInt("_Zwrite", 0); //禁用深度写入


            lr.startWidth = width*2f;
            lr.endWidth = width*2f;

            // 优化性能设置
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;

            foreach (Transform child in lineObj.transform)
            {
                child.gameObject.layer = modelLayer;
            }

            
        }     
    }
}