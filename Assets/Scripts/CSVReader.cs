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

    // ����ϵͳ
    public static Dictionary<int, List<MovieData>> yearIndex = new Dictionary<int, List<MovieData>>();
    public static Dictionary<string, List<MovieData>> genreIndex = new Dictionary<string, List<MovieData>>();
    public static Dictionary<string, List<MovieData>> countryIndex = new Dictionary<string, List<MovieData>>();

    private static decimal _maxRevenue;  // ȫ���������ֵ

    void Start()
    {
        LoadCSVData();
        BuildIndices();
        Debug.Log($"�ɹ����� {allMovies.Count} ����Ч��Ӱ");
    }

    void LoadCSVData()
    {
        Debug.Log($"��ʼ����CSV�ļ����ļ����ȣ�{csvFile.text.Length}�ֽ�");

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = ",",
            BadDataFound = args => Debug.LogWarning($"�쳣���� @��{args.Context.Parser.Row}: {args.RawRecord}"), // ��Ӵ���������־
            MissingFieldFound = null,
            HeaderValidated = null,
            PrepareHeaderForMatch = args => {
                Debug.Log($"ԭʼ������{args.Header} �� ת����{args.Header.ToLower()}");
                return args.Header.ToLower();
            }
        };

        using (var reader = new StringReader(csvFile.text))
        using (var csv = new CsvReader(reader, config))
        {
            csv.Context.RegisterClassMap<MovieDataMap>();
            Debug.Log("��ע����ӳ���ϵ");

            try
            {
                var records = csv.GetRecords<MovieData>();
                Debug.Log("��ʼ�������ݼ�¼...");

                int totalCount = 0, validCount = 0;

                //�������revenue
                decimal globalMaxRevenue = 0;

                foreach (var record in records)
                {

                    if (record.revenue > globalMaxRevenue)
                        globalMaxRevenue = record.revenue;


                    totalCount++;
                    //Debug.Log($"���ڴ����{totalCount}����¼ - ԭʼ���ݣ�{string.Join("|", csv.Parser.RawRecord)}");

                    if (record.IsValid)
                    {
                        var cleaned = CleanData(record);
                        //Debug.Log($"��Ч��¼��{cleaned.title.PadRight(20)} | ��ݣ�{cleaned.releaseYear} | ���ͣ�{string.Join(",", cleaned.genres)} |���ң�{string.Join(",",cleaned.productionCountries)}");
                        allMovies.Add(cleaned);
                        validCount++;
                    }
                    else
                    {
                        Debug.LogWarning($"��Ч��¼������[{record.title}] ���[{record.releaseYear}] ������[{record.genres?.Count}]");
                    }
                }
                _maxRevenue = globalMaxRevenue;
                Debug.Log($"������ɣ�������{totalCount}����¼����Ч��¼{validCount}��");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"����ʧ�ܣ�{ex.Message}\n{ex.StackTrace}");
            }
        }
    }

    MovieData CleanData(MovieData raw)
    {
        // �����ͼ�е��������ݸ�ʽ
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

            // �������
            if (!yearIndex.ContainsKey(movie.releaseYear))
                yearIndex[movie.releaseYear] = new List<MovieData>();
            yearIndex[movie.releaseYear].Add(movie);

            // ��������
            foreach (var genre in movie.genres)
            {
                var cleanGenre = genre.Trim();
                if (!genreIndex.ContainsKey(cleanGenre))
                    genreIndex[cleanGenre] = new List<MovieData>();
                genreIndex[cleanGenre].Add(movie);
            }

            // ��������
            foreach (var country in movie.productionCountries)
            {
                var code =country.Trim();
                if (!countryIndex.ContainsKey(code))
                    countryIndex[code] = new List<MovieData>();
                countryIndex[code].Add(movie);
            }
        }
    }

    // ������ɫӳ��
    private static readonly Dictionary<string, Color> _genreColors = new Dictionary<string, Color>
    {
        // ������ԭɫ
    {"Animation",    new Color(1.0f, 0.2f, 0.2f, 0.3f)},   // ���޺죨��ͨ���
    {"Comedy",       new Color(0.3f, 0.8f, 0.2f, 0.3f)},   // ƻ���̣��������죩
    {"Drama",        new Color(0.2f, 0.4f, 1.0f, 0.3f)},   // ������������䣩

    // ����/�����
    {"Thriller",     new Color(0.5f, 0.5f, 0.5f, 0.3f)},   // �л�ɫ�����ŷ�Χ��
    {"Mystery",      new Color(0.4f, 0.1f, 0.6f, 0.3f)},   // ����ɫ�����ظУ�

    // ����/ð����
    {"Action",       new Color(1.0f, 0.6f, 0.0f, 0.3f)},   // �Ⱥ죨���Ҷ�����
    {"Adventure",    new Color(1.0f, 0.8f, 0.0f, 0.3f)},   // ��ɫ��̽�վ���
    {"War",          new Color(0.6f, 0.3f, 0.2f, 0.3f)},   // ��ʯɫ��ս��������

    // ����/��ͥ��  
    {"Romance",      new Color(1.0f, 0.4f, 0.7f, 0.3f)},   // �ۺ죨������
    {"Family",       new Color(0.9f, 0.9f, 0.2f, 0.3f)},   // ����ɫ����ܰ��

    // �ƻ�/�����
    {"Science Fiction", new Color(0.0f, 0.8f, 1.0f, 0.3f)},// ӫ�������Ƽ��У�
    {"Fantasy",       new Color(0.6f, 0.2f, 1.0f, 0.3f)},  // ��������ħ�ã�

    // ����/��ɫ��
    {"Crime",        new Color(0.3f, 0.3f, 0.3f, 0.3f)},   // ̿�ң�������
    {"Horror",       new Color(0.3f, 0.0f, 0.0f, 0.3f)},   // ���죨Ѫ�ȣ�

    // ��ʵ/��ʷ��
    {"Documentary",  new Color(0.6f, 0.6f, 0.6f, 0.3f)},   // ���ң���ʵ���ԣ�
    {"History",      new Color(0.5f, 0.4f, 0.2f, 0.3f)},   // �غ֣����ţ�

    // �������
    {"Foreign",      new Color(0.8f, 0.6f, 1.0f, 0.3f)},   // ޹�²��ϣ�����
    {"Music",        new Color(0.9f, 0.2f, 0.8f, 0.3f)},   // Ʒ�죨���У�
    {"TV Movie",     new Color(0.7f, 0.7f, 0.9f, 0.3f)},   // �����ң�����ӫĻ��
    {"Western",      new Color(0.8f, 0.5f, 0.3f, 0.3f)},   // �����죨������Į��
    };

    private Color GetGenreColor(string genre)
    {
        return _genreColors.TryGetValue(genre, out var color) ? color : new Color(1, 1, 1, 0.3f); // Ĭ�ϰ�ɫ
    }

    #region ����������
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

    #region ��Խ�ͼ���ݸ�ʽ��ת����
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
            .Replace("��", "")  // �����ͼ�е��쳣�ַ�
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
// ���ݽ�ͼ���ݽṹ�Ż���������
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
    public float normalizedRevenue; // ��׼���������ֵ

    [System.NonSerialized]
    public Color genreColor;        // ���Ͷ�Ӧ����ɫ

    public bool IsValid =>
        !string.IsNullOrEmpty(title) &&
        releaseYear > 1900 &&
        genres.Count > 0 &&
        productionCountries.Count > 0;
}