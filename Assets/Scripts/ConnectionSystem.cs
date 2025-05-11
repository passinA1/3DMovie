// ConnectionSystem.cs (������Canvas����)
using UnityEngine;

public class ConnectionSystem : MonoBehaviour
{
    [Header("���߲���")]
    public Gradient yearColorGradient;
    public float lineHeight = 100f;
    public Material lineMaterial;

    [Header("�ڵ�㼶")]
    public Transform mapRoot; // ��ӦCanvas/Map
    public Transform planeRoot; // ��ӦCanvas/Plane

    void Start()
    {
        GenerateAllLines();
    }

    void GenerateAllLines()
    {
        foreach (var movie in CSVReader.allMovies)
        {
            CreateCountryToGenreLinks(movie);
        }
    }

    void CreateCountryToGenreLinks(MovieData movie)
    {
        foreach (var country in movie.productionCountries)
        {
            Transform countryNode = FindNodeRecursive(mapRoot, country);
            if (!countryNode) continue;

            foreach (var genre in movie.genres)
            {
                Transform genreNode = FindGenreNode(movie.releaseYear, genre);
                if (genreNode)
                {
                    CreateLineRenderer(
                        start: countryNode.position + Vector3.up * lineHeight,
                        end: genreNode.position + Vector3.up * lineHeight,
                        year: movie.releaseYear
                    );
                }
            }
        }
    }

    Transform FindGenreNode(int year, string genre)
    {
        Transform yearParent = FindNodeRecursive(planeRoot, year.ToString());
        return yearParent?.Find($"genres/{genre}");
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

    void CreateLineRenderer(Vector3 start, Vector3 end, int year)
    {
        GameObject lineObj = new GameObject("ConnectionLine");
        LineRenderer lr = lineObj.AddComponent<LineRenderer>();

        lr.positionCount = 2;
        lr.SetPositions(new Vector3[] { start, end });
        lr.material = lineMaterial;
        lr.startWidth = lr.endWidth = 0.05f;

        float colorPos = Mathf.InverseLerp(2015, 2020, year);
        lr.startColor = lr.endColor = yearColorGradient.Evaluate(colorPos);
    }
}