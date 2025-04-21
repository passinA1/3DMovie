using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using CsvHelper;
using CsvHelper.Configuration;
using Newtonsoft.Json;
using System.Linq;
using System;

[System.Serializable]
public class Movie
{
    public string Title { get; set; }
    public int Id { get; set; }
    public float Popularity { get; set; }
    public List<Country> ProductionCountries { get; set; }
    public DateTime ReleaseDate { get; set; }
    public decimal Revenue { get; set; }
    public List<Genre> Genres { get; set; }

    public bool IsValid() => !string.IsNullOrEmpty(Title) && ReleaseDate.Year > 1900;
}

[System.Serializable]
public class Genre
{
    public int Id { get; set; }
    public string Name { get; set; }
}

[System.Serializable]
public class Country
{
    public string Iso3166_1 { get; set; }
    public string Name { get; set; }
}

public class CSVReader : MonoBehaviour
{
    public string filePath = "Assets/Resources/movies1.csv";

    // 核心数据存储
    public static List<Movie> allMovies = new List<Movie>();

    // 索引系统
    public static SortedDictionary<int, HashSet<string>> yearCategories = new SortedDictionary<int, HashSet<string>>();
    public static Dictionary<int, List<Movie>> yearIndex = new Dictionary<int, List<Movie>>();
    public static Dictionary<string, List<Movie>> countryIndex = new Dictionary<string, List<Movie>>();
    public static Dictionary<string, List<Movie>> genreIndex = new Dictionary<string, List<Movie>>();

    void Start()
    {
        ReadCSV(filePath);
        BuildIndices();
        ProcessYearlyCategories();
    }

    void BuildIndices()
    {
        yearIndex.Clear();
        countryIndex.Clear();
        genreIndex.Clear();

        foreach (var movie in allMovies.Where(m => m.IsValid()))
        {
            // 年份索引
            var year = movie.ReleaseDate.Year;
            if (!yearIndex.ContainsKey(year)) yearIndex[year] = new List<Movie>();
            yearIndex[year].Add(movie);

            // 国家索引
            foreach (var country in movie.ProductionCountries ?? new List<Country>())
            {
                var code = country.Iso3166_1;
                if (string.IsNullOrEmpty(code)) continue;

                if (!countryIndex.ContainsKey(code)) countryIndex[code] = new List<Movie>();
                countryIndex[code].Add(movie);
            }

            // 类型索引
            foreach (var genre in movie.Genres ?? new List<Genre>())
            {
                var name = genre.Name;
                if (string.IsNullOrEmpty(name)) continue;

                if (!genreIndex.ContainsKey(name)) genreIndex[name] = new List<Movie>();
                genreIndex[name].Add(movie);
            }
        }
    }

    void ProcessYearlyCategories()
    {
        yearCategories.Clear();
        foreach (var movie in allMovies.Where(m => m.IsValid()))
        {
            var year = movie.ReleaseDate.Year;
            if (!yearCategories.ContainsKey(year)) yearCategories[year] = new HashSet<string>();

            foreach (var genre in movie.Genres)
                yearCategories[year].Add(genre.Name);
        }
    }

    void ReadCSV(string path)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = ",",
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null,
            HeaderValidated = null
        };

        try
        {
            using (var reader = new StreamReader(path))
            using (var csv = new CsvReader(reader, config))
            {
                csv.Context.RegisterClassMap<MovieMap>();
                allMovies = csv.GetRecords<Movie>().Where(m => m.IsValid()).ToList();
                Debug.Log($"成功加载 {allMovies.Count} 条有效数据");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"CSV读取失败: {e.Message}");
        }
    }
}

public sealed class MovieMap : ClassMap<Movie>
{
    public MovieMap()
    {
        Map(m => m.Genres).Convert(args => SafeConvertGenres(args.Row));
        Map(m => m.Id).Convert(args => SafeConvertInt(args.Row.GetField("id")));
        Map(m => m.Popularity).Convert(args => SafeConvertFloat(args.Row.GetField("popularity")));
        Map(m => m.ReleaseDate).Convert(args => SafeConvertDate(args.Row.GetField("release_date")));
        Map(m => m.Revenue).Convert(args => SafeConvertDecimal(args.Row.GetField("revenue")));
        Map(m => m.Title).Name("title");
    }

    private List<Genre> SafeConvertGenres(IReaderRow row)
    {
        try
        {
            var json = row.GetField("genres")?
                .Replace("'", "\"")
                .Replace("id", "Id")
                .Replace("name", "Name");

            return JsonConvert.DeserializeObject<List<Genre>>(json, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Ignore
            }) ?? new List<Genre>();
        }
        catch
        {
            return new List<Genre>();
        }
    }

    private int SafeConvertInt(string value) =>
        int.TryParse(value, out int result) ? result : 0;

    private float SafeConvertFloat(string value) =>
        float.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out float result) ? result : 0f;

    private DateTime SafeConvertDate(string value) =>
        DateTime.TryParseExact(value, "yyyy/M/d", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime result) ? result : default;

    private decimal SafeConvertDecimal(string value) =>
        decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal result) ? result : 0m;
}

// 筛选系统
[System.Serializable]
public class MovieFilter
{
    public int? minYear;
    public int? maxYear;
    public List<string> countries = new List<string>();
    public List<string> genres = new List<string>();

    public IEnumerable<Movie> Apply()
    {
        IEnumerable<Movie> result = CSVReader.allMovies;

        // 时间筛选
        if (minYear.HasValue || maxYear.HasValue)
        {
            var min = minYear ?? CSVReader.yearIndex.Keys.Min();
            var max = maxYear ?? CSVReader.yearIndex.Keys.Max();
            result = result.Where(m => m.ReleaseDate.Year >= min && m.ReleaseDate.Year <= max);
        }

        // 国家筛选
        if (countries.Count > 0)
        {
            result = result.Where(m => m.ProductionCountries?
                .Any(c => countries.Contains(c.Iso3166_1)) ?? false);
        }

        // 类型筛选
        if (genres.Count > 0)
        {
            result = result.Where(m => m.Genres?
                .Any(g => genres.Contains(g.Name)) ?? false);
        }

        return result.Distinct();
    }
}
