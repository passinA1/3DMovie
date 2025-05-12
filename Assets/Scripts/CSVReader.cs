using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using CsvHelper;
using CsvHelper.Configuration;
using System.Linq;


public class CSVReader : MonoBehaviour
{
    public TextAsset csvFile;
    public static List<MovieData> allMovies = new List<MovieData>();

    // 索引系统
    public static Dictionary<int, List<MovieData>> yearIndex = new Dictionary<int, List<MovieData>>();
    public static Dictionary<string, List<MovieData>> genreIndex = new Dictionary<string, List<MovieData>>();
    public static Dictionary<string, List<MovieData>> countryIndex = new Dictionary<string, List<MovieData>>();

    private static decimal _maxRevenue;  // 全局最大收入值

    void Start()
    {
        LoadCSVData();
        BuildIndices();
        Debug.Log($"成功加载 {allMovies.Count} 部有效电影");
    }

    void LoadCSVData()
    {
        Debug.Log($"开始加载CSV文件，文件长度：{csvFile.text.Length}字节");

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = ",",
            BadDataFound = args => Debug.LogWarning($"异常数据 @行{args.Context.Parser.Row}: {args.RawRecord}"), // 添加错误数据日志
            MissingFieldFound = null,
            HeaderValidated = null,
            PrepareHeaderForMatch = args => {
                Debug.Log($"原始列名：{args.Header} → 转换后：{args.Header.ToLower()}");
                return args.Header.ToLower();
            }
        };

        using (var reader = new StringReader(csvFile.text))
        using (var csv = new CsvReader(reader, config))
        {
            csv.Context.RegisterClassMap<MovieDataMap>();
            Debug.Log("已注册类映射关系");

            try
            {
                var records = csv.GetRecords<MovieData>();
                Debug.Log("开始解析数据记录...");

                int totalCount = 0, validCount = 0;

                //设置最大revenue
                decimal globalMaxRevenue = 0;

                foreach (var record in records)
                {

                    if (record.revenue > globalMaxRevenue)
                        globalMaxRevenue = record.revenue;


                    totalCount++;
                    //Debug.Log($"正在处理第{totalCount}条记录 - 原始数据：{string.Join("|", csv.Parser.RawRecord)}");

                    if (record.IsValid)
                    {
                        var cleaned = CleanData(record);
                        //Debug.Log($"有效记录：{cleaned.title.PadRight(20)} | 年份：{cleaned.releaseYear} | 类型：{string.Join(",", cleaned.genres)} |国家：{string.Join(",",cleaned.productionCountries)}");
                        allMovies.Add(cleaned);
                        validCount++;
                    }
                    else
                    {
                        Debug.LogWarning($"无效记录：标题[{record.title}] 年份[{record.releaseYear}] 类型数[{record.genres?.Count}]");
                    }
                }
                _maxRevenue = globalMaxRevenue;
                Debug.Log($"解析完成，共处理{totalCount}条记录，有效记录{validCount}条");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"解析失败：{ex.Message}\n{ex.StackTrace}");
            }
        }
    }

    MovieData CleanData(MovieData raw)
    {
        // 清理截图中的特殊数据格式
        raw.genres = CleanListField(raw.genres);
        raw.productionCountries = CleanListField(raw.productionCountries);
        raw.regionTag = CleanSingleField(raw.regionTag);
        raw.originalLanguage = CleanLanguageField(raw.originalLanguage);
        return raw;
    }

    void BuildIndices()
    {
        foreach (var movie in allMovies)
        {

            movie.normalizedRevenue = Mathf.Clamp((float)(movie.revenue / _maxRevenue) * 5f, 0f, 5f);
            movie.genreColor = GetGenreColor(movie.genres.FirstOrDefault());

            // 年份索引
            if (!yearIndex.ContainsKey(movie.releaseYear))
                yearIndex[movie.releaseYear] = new List<MovieData>();
            yearIndex[movie.releaseYear].Add(movie);

            // 类型索引
            foreach (var genre in movie.genres)
            {
                var cleanGenre = genre.Trim();
                if (!genreIndex.ContainsKey(cleanGenre))
                    genreIndex[cleanGenre] = new List<MovieData>();
                genreIndex[cleanGenre].Add(movie);
            }

            // 国家索引
            foreach (var country in movie.productionCountries)
            {
                var code =country.Trim();
                if (!countryIndex.ContainsKey(code))
                    countryIndex[code] = new List<MovieData>();
                countryIndex[code].Add(movie);
            }
        }
    }

    // 类型颜色映射
    private static readonly Dictionary<string, Color> _genreColors = new Dictionary<string, Color>
    {
        // 基础三原色
    {"Animation",    new Color(1.0f, 0.2f, 0.2f, 0.3f)},   // 鲜艳红（卡通风格）
    {"Comedy",       new Color(0.3f, 0.8f, 0.2f, 0.3f)},   // 苹果绿（轻松明快）
    {"Drama",        new Color(0.2f, 0.4f, 1.0f, 0.3f)},   // 深蓝（情感深邃）

    // 悬疑/惊悚类
    {"Thriller",     new Color(0.5f, 0.5f, 0.5f, 0.3f)},   // 中灰色（紧张氛围）
    {"Mystery",      new Color(0.4f, 0.1f, 0.6f, 0.3f)},   // 深紫色（神秘感）

    // 动作/冒险类
    {"Action",       new Color(1.0f, 0.6f, 0.0f, 0.3f)},   // 橙红（激烈动作）
    {"Adventure",    new Color(1.0f, 0.8f, 0.0f, 0.3f)},   // 金色（探险精神）
    {"War",          new Color(0.6f, 0.3f, 0.2f, 0.3f)},   // 赭石色（战场泥土）

    // 爱情/家庭类  
    {"Romance",      new Color(1.0f, 0.4f, 0.7f, 0.3f)},   // 粉红（浪漫）
    {"Family",       new Color(0.9f, 0.9f, 0.2f, 0.3f)},   // 明黄色（温馨）

    // 科幻/奇幻类
    {"Science Fiction", new Color(0.0f, 0.8f, 1.0f, 0.3f)},// 荧光蓝（科技感）
    {"Fantasy",       new Color(0.6f, 0.2f, 1.0f, 0.3f)},  // 紫罗兰（魔幻）

    // 犯罪/黑色类
    {"Crime",        new Color(0.3f, 0.3f, 0.3f, 0.3f)},   // 炭灰（阴暗）
    {"Horror",       new Color(0.3f, 0.0f, 0.0f, 0.3f)},   // 暗红（血腥）

    // 纪实/历史类
    {"Documentary",  new Color(0.6f, 0.6f, 0.6f, 0.3f)},   // 银灰（纪实中性）
    {"History",      new Color(0.5f, 0.4f, 0.2f, 0.3f)},   // 棕褐（复古）

    // 特殊类别
    {"Foreign",      new Color(0.8f, 0.6f, 1.0f, 0.3f)},   // 薰衣草紫（异域）
    {"Music",        new Color(0.9f, 0.2f, 0.8f, 0.3f)},   // 品红（动感）
    {"TV Movie",     new Color(0.7f, 0.7f, 0.9f, 0.3f)},   // 淡蓝灰（电视荧幕）
    {"Western",      new Color(0.8f, 0.5f, 0.3f, 0.3f)},   // 陶土红（西部荒漠）
    };

    private Color GetGenreColor(string genre)
    {
        return _genreColors.TryGetValue(genre, out var color) ? color : new Color(1, 1, 1, 0.3f); // 默认白色
    }

    #region 数据清理方法
    List<string> CleanListField(List<string> input)
    {
        return input
            .ConvertAll(s => s.Trim('\'', '[', ']', ' ', '"'))
            .FindAll(s => !string.IsNullOrEmpty(s));
    }

    string CleanSingleField(string input) =>
        input?.Trim('\'', '[', ']', ' ', '"') ?? string.Empty;

    string CleanLanguageField(string input)
    {
        var clean = input.Split(' ')[0].Trim();
        return clean.Length == 2 ? clean : "en";
    }

    string GetCountryCode(string countryName) => countryName switch
    {
        "United States of America" => "US",
        "China" => "CN",
        _ => countryName.Length >= 2 ? countryName[..2].ToUpper() : "XX"
    };
    #endregion
}

public sealed class MovieDataMap : ClassMap<MovieData>
{
    public MovieDataMap()
    {
        Map(m => m.title).Name("title");
        Map(m => m.releaseYear).Name("release_year");
        Map(m => m.genres).Convert(args => ParseList(args.Row.GetField("genres")));
        Map(m => m.productionCountries).Convert(args => ParseList(args.Row.GetField("production_countries")));
        Map(m => m.regionTag).Convert(args => ParseSingle(args.Row.GetField("region_tags")));
        Map(m => m.budget).Convert(args => ParseDecimal(args.Row.GetField("budget")));
        Map(m => m.revenue).Convert(args => ParseDecimal(args.Row.GetField("revenue")));
        Map(m => m.voteAverage).Convert(args => ParseFloat(args.Row.GetField("vote_average")));
        Map(m => m.voteCount).Convert(args => ParseInt(args.Row.GetField("vote_count")));
        Map(m => m.originalLanguage).Convert(args => args.Row.GetField("original_language"));
    }

    #region 针对截图数据格式的转换器
    List<string> ParseList(string input)
    {
        if (string.IsNullOrEmpty(input)) return new List<string>();

        return input.Split(',')
            .Select(s => s.Trim('\'', '[', ']', ' ', '"'))
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();
    }

    string ParseSingle(string input) =>
        ParseList(input).FirstOrDefault() ?? string.Empty;

    decimal ParseDecimal(string input)
    {
        var clean = input?
            .Replace(",", "")
            .Replace("$", "")
            .Replace("杂", "")  // 处理截图中的异常字符
            .Trim();

        return decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out var result)
            ? result : 0m;
    }

    float ParseFloat(string input) =>
        float.TryParse(input?.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var result)
            ? result : 0f;

    int ParseInt(string input) =>
        int.TryParse(input?.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var result)
            ? result : 0;
    #endregion
}
// 根据截图数据结构优化的数据类
[System.Serializable]
public class MovieData
{
    public string title;
    public int releaseYear;
    public List<string> genres;
    public List<string> productionCountries;
    public string regionTag;
    public decimal budget;
    public decimal revenue;
    public float voteAverage;
    public int voteCount;
    public string originalLanguage;

    [System.NonSerialized]
    public float normalizedRevenue; // 标准化后的收入值

    [System.NonSerialized]
    public Color genreColor;        // 类型对应的颜色

    public bool IsValid =>
        !string.IsNullOrEmpty(title) &&
        releaseYear > 1900 &&
        genres.Count > 0 &&
        productionCountries.Count > 0;
}