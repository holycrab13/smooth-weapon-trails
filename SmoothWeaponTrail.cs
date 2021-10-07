using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Smooth Weapon Trails by Jan Forberg
/// </summary>
public class SmoothWeaponTrail : MonoBehaviour
{
    /*
     * |  head                           tail  |
     * 
     * |    1    |    2    |    3    |    4    |  <--- segments
     *  
     * x -- o -- x -- o -- x -- o -- x -- o -- x
     * |    |    |    |    |    |    |    |    |
     * |    |    |    |    |    |    |    |    |
     * x -- o -- x -- o -- x -- o -- x -- o -- x
     * |    |    |    |    |    |    |    |    |
     * |    |    |    |    |    |    |    |    |
     * x -- o -- x -- o -- x -- o -- x -- o -- x   <--- line
     * 
     * |-------------lineVertexCount-----------|
     * 
     * Example: 
     *  nodeCount = 3
     *  segments = 4
     *  subdivisions = 1
     *  lineVertexCount = 9
     *  segmentVertexCount = 6
     *  vertexCount = 27
     *  
     *  Approach:
     *  
     *  - Generate a tringale mesh
     *  - Always move the head vertices with the transforms specified in "nodes"
     *  - Move all segment vertices (x's) to its headward neighbor along each line n times per second
     *  - Recalculate the position of all interpolation vertices (o's) based on hermite interpolation of the neighboring
     *    segment vertices (x's)
     *  - Determine the world space length of each line (keep all segments lengths in a helper array to avoid 
     *    costly distance calculations - only the segment lengths of the head segment changes)
     *  - Calculate the UV coordinate for each vertex (x's AND o's) based on the line length and its position on 
     *    the line from tail to head
    */

    /// <summary>
    /// The number of segments of the trail mesh
    /// </summary>
    [SerializeField]
    private int segments = 10;

    /// <summary>
    /// The number of vertical 
    /// </summary>
    [SerializeField]
    private int subdivisions = 1;

    [SerializeField]
    private float subdivisionHermiteTension = 0;

    [SerializeField]
    private float subdivisionHermiteBias = 0;

    [SerializeField]
    private float updatesPerSecond = 60;

    [SerializeField]
    private Transform[] nodes;

    [SerializeField]
    private Material material;

    [SerializeField]
    private bool disabledByDefault = true;

    private GameObject trailObject;

    private MeshRenderer meshRenderer;

    private Vector3[] positions;

    private Vector3[] uvs;

    private float[,] segmentLengths;

    private Mesh mesh;

    private float updateTimer;

    private int lineVertexCount;

    private int segmentVertexCount;

    private int vertexCount;

    public void SetActive(bool value)
    {
        enabled = value;
    }

    private void OnDestroy()
    {
        Destroy(trailObject);
    }

    private void OnEnable()
    {
        for (int i = lineVertexCount - 1; i >= 0; i--)
        {
            for (int j = 0; j < nodes.Length; j++)
            {
                positions[i * nodes.Length + j] = nodes[j].position;
            }
        }

        mesh.vertices = positions;
        mesh.SetUVs(0, uvs);
        mesh.RecalculateBounds();
        trailObject.SetActive(true);
    }

    private void OnDisable()
    {
        trailObject.SetActive(false);
    }

    /// <summary>
    /// Mesh creation
    /// </summary>
    private void Awake()
    {
        lineVertexCount = segments * (subdivisions + 1) + 1;
        segmentVertexCount = (subdivisions + 1) * nodes.Length;
        vertexCount = nodes.Length * lineVertexCount;

        positions = new Vector3[vertexCount];
        uvs = new Vector3[vertexCount];
        segmentLengths = new float[segments, nodes.Length];
        List<int> indices = new List<int>();

        int k = 0;

        for (int j = 0; j < lineVertexCount; j++)
        {
            for (int i = 0; i < nodes.Length; i++)
            {
                positions[k] = new Vector3(i, 0, j);
                uvs[k] = new Vector2(0, 0);

                if (i < nodes.Length - 1 && j < lineVertexCount - 1)
                {
                    indices.Add(k);
                    indices.Add(k + nodes.Length);
                    indices.Add(k + nodes.Length + 1);

                    indices.Add(k);
                    indices.Add(k + nodes.Length + 1);
                    indices.Add(k + 1);
                }

                k++;
            }
        }

        // Create trail mesh
        mesh = new Mesh();
        mesh.vertices = positions;
        mesh.SetUVs(0, uvs);
        mesh.SetIndices(indices.ToArray(), MeshTopology.Triangles, 0);
        mesh.RecalculateBounds();

        // Create a trail object in the scene to render the mesh
        trailObject = new GameObject("Trail");
        meshRenderer = trailObject.AddComponent<MeshRenderer>();
        meshRenderer.material = material;
        meshRenderer.receiveShadows = true;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        MeshFilter filter = trailObject.AddComponent<MeshFilter>();
        filter.mesh = mesh;

        if (disabledByDefault)
        {
            enabled = false;
        }
    }

    private void Update()
    {
        // Update the length array at the first column.
        // Distances are the distance of the tracers to the vertices right behind the trail head
        for (int j = 0; j < nodes.Length; j++)
        {
            segmentLengths[0, j] = Vector3.Distance(nodes[j].position, positions[(subdivisions + 1) * nodes.Length + j]);
        }

        // Only do this x times per second...
        if (updateTimer < 0)
        {
            // Reset the timer
            updateTimer = 1.0f / updatesPerSecond;

            // Update positions of all segment columns (leave out subdivisions)
            for (int i = segments; i >= 1; i--)
            {
                for (int j = 0; j < nodes.Length; j++)
                {
                    if (i < segments)
                    {
                        segmentLengths[i, j] = segmentLengths[i - 1, j];
                    }

                    positions[i * segmentVertexCount + j] = positions[(i - 1) * segmentVertexCount + j];
                }
            }

            // Update the position of subdivision vertices
            for (int i = segments - 1; i >= 0; i--)
            {
                // For each segment along each line, find 4 subsequent segment vertices along a line
                // Perform hermite interpolation for all the interpolation vertices
                for (int j = 0; j < nodes.Length; j++)
                {
                    // Start and endpoint for hermite interpolation
                    Vector3 end = positions[(i + 1) * segmentVertexCount + j];
                    Vector3 start = positions[i * segmentVertexCount + j];

                    // Additional previous and next point for hermite interpolation
                    Vector3 prev;

                    if(i > 0)
                    {
                        prev = positions[(i - 1) * segmentVertexCount + j];
                    }
                    else
                    {
                        // Create an artificial point if we are at the end of the line
                        prev = 2 * start - end;
                    }

                    Vector3 next;

                    if(i < segments - 1)
                    {
                        next = positions[(i + 2) * segmentVertexCount + j];
                    }
                    else
                    {
                        // Create an artificial point if we are at the end of the line
                        next = 2 * end - start;
                    }

                    // Perform interpolation for all subdivision vertices
                    for (int s = 1; s <= subdivisions; s++)
                    {
                        float subdivisionLerp = (float)s / (subdivisions + 1);
                        int subdivisionColumnIndex = i * segmentVertexCount + nodes.Length * s;

                        positions[subdivisionColumnIndex + j] = 
                            HermiteInterpolate(prev, start, end, next, subdivisionLerp, subdivisionHermiteTension, subdivisionHermiteBias);
                    }
                }
            }
        }

        // Update UVs for all vertices
        for (int j = 0; j < nodes.Length; j++)
        {
            float lineLength = 0;
            float u = j * (1.0f / (nodes.Length - 1));

            for (int i = 0; i < segments; i++)
            {
                lineLength += segmentLengths[i, j];
            }

            float progressAlongLine = 0.0f;

            // Set the uvs of the tail of the line
            uvs[segments * segmentVertexCount + j] = new Vector2(u, 0);

            //// Set the uvs of the head of the line
            //uvs[j] = new Vector2(u, 1);

            if (lineLength == 0.0f)
            {
                // If the entire line has length 0, distribute the v coordinate evenly
                for (int i = lineVertexCount - 1; i >= 0; i--)
                {
                    uvs[i * nodes.Length + j] = new Vector2(u, 1.0f / lineVertexCount);
                }
            }
            else
            {
                // Track the progress along each line
                float previousV = 0;

                for (int i = segments - 1; i >= 0; i--)
                {
                    // Update the progress along the line
                    progressAlongLine += segmentLengths[i, j];
                    float segmentV = progressAlongLine / lineLength;

                    for (int s = 0; s <= subdivisions; s++)
                    {
                        float subdivisionLerp = (float)s / (subdivisions + 1);
                        int subdivisionColumnIndex = i * segmentVertexCount + nodes.Length * s;

                        float v = Mathf.Lerp(segmentV, previousV, subdivisionLerp);
                        uvs[subdivisionColumnIndex + j] = new Vector2(u, v);
                    }

                    previousV = segmentV;
                }
            }
        }

        // Update the head vertices
        for (int j = 0; j < nodes.Length; j++)
        {
            positions[j] = nodes[j].position;

            Vector3 end = positions[segmentVertexCount + j];
            Vector3 start = positions[j];

            for (int s = 1; s <= subdivisions; s++)
            {
                float subdivisionLerp = (float)s / (subdivisions + 1);
                int subdivisionColumnIndex = nodes.Length * s;
                positions[subdivisionColumnIndex + j] = Vector3.Lerp(start, end, subdivisionLerp);


            }
        }

        updateTimer -= Time.deltaTime;
        mesh.vertices = positions;
        mesh.SetUVs(0, uvs);
        mesh.RecalculateBounds();
    }

    /// <summary>
    /// Hermite interpolation taken and adjusted from http://paulbourke.net/miscellaneous/interpolation/ 
    /// </summary>
    /// <param name="p0"></param>
    /// <param name="p1"></param>
    /// <param name="p2"></param>
    /// <param name="p3"></param>
    /// <param name="mu"></param>
    /// <param name="tension"></param>
    /// <param name="bias"></param>
    /// <returns></returns>
    private Vector3 HermiteInterpolate(
       Vector3 p0, Vector3 p1,
       Vector3 p2, Vector3 p3,
       float mu,
       float tension,
       float bias)
    {
        Vector3 m0, m1;
        float mu2, mu3;
        float a0, a1, a2, a3;

        mu2 = mu * mu;
        mu3 = mu2 * mu;
        m0 = (p1 - p0) * (1 + bias) * (1 - tension) / 2;
        m0 += (p2 - p1) * (1 - bias) * (1 - tension) / 2;
        m1 = (p2 - p1) * (1 + bias) * (1 - tension) / 2;
        m1 += (p3 - p2) * (1 - bias) * (1 - tension) / 2;
        a0 = 2 * mu3 - 3 * mu2 + 1;
        a1 = mu3 - 2 * mu2 + mu;
        a2 = mu3 - mu2;
        a3 = -2 * mu3 + 3 * mu2;

        return a0 * p1 + a1 * m0 + a2 * m1 + a3 * p2;
    }
}
