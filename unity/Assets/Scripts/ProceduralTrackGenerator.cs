using System.Collections.Generic;
using UnityEngine;

public class ProceduralTrackGenerator : MonoBehaviour
{
    [Header("City Street Shape")]
    public int numberOfSegments = 50;
    public float straightLength = 20f;
    public float roadWidth = 10f;       
    public float sidewalkWidth = 4f;    
    public float curbHeight = 0.02f;    
    
    [Header("Curve Settings")]
    public float curveRadius = 15f;
    public int curveResolution = 10;
    public float sharpTurnAngle = 90f;
    public float slightTurnAngle = 45f;

    [Header("Wall Settings")]
    public float wallHeight = 3f;

    [Header("Tags for ML-Agents")]
    public string roadTag = "Untagged";
    public string sidewalkTag = "Sidewalk"; 
    public string wallTag = "Wall";         
    public string checkpointTag = "Checkpoint";
    public string finishTag = "FinishLine";

    [Header("Visuals & Textures")]
    public Material asphaltMaterial;
    public Material sidewalkMaterial;
    public Material wallMaterial;
    public float textureTiling = 5f;

    public int checkpointSpacing = 5;

    [HideInInspector] public Vector3 startPosition;
    [HideInInspector] public Quaternion startRotation;

    void Start()
    {
        RebuildTrack();
    }

    public void RebuildTrack()
    {
        foreach (Transform child in transform) { Destroy(child.gameObject); }
        
        // Pure mathematical path generation (No straight-line overrides)
        List<Vector3> path = GenerateSmartPath();
        
        Vector3 startDirection = (path[1] - path[0]).normalized;
        Vector3 localStartPos = path[0] + (startDirection * 8f) + (Vector3.up * 0.1f); 
        
        startPosition = this.transform.TransformPoint(localStartPos); 
        startRotation = this.transform.rotation * Quaternion.LookRotation(startDirection);

        BuildRealisticMeshes(path);
        SpawnStartWall(path); 
        SpawnCheckpoints(path);
    }

    List<Vector3> GenerateSmartPath()
    {
        List<Vector3> points = new List<Vector3>();
        Vector3 currentPos = Vector3.zero;
        Vector3 currentDir = Vector3.forward;
        points.Add(currentPos);

        for (int i = 0; i < numberOfSegments; i++)
        {
            float dice = Random.value;
            float turnAngle = 0f;
            bool isCurve = true;

            if (dice < 0.60f)      { isCurve = false; } 
            else if (dice < 0.70f) { turnAngle = sharpTurnAngle; }
            else if (dice < 0.80f) { turnAngle = -sharpTurnAngle; }
            else if (dice < 0.90f) { turnAngle = slightTurnAngle; }
            else                   { turnAngle = -slightTurnAngle; }

            if (!isCurve)
            {
                Vector3 nextPos = currentPos + (currentDir * straightLength);
                if (!IsOverlapping(nextPos, points))
                {
                    currentPos = nextPos;
                    points.Add(currentPos);
                }
            }
            else
            {
                float anglePerStep = turnAngle / curveResolution;
                float arcLength = 2f * Mathf.PI * curveRadius * (Mathf.Abs(turnAngle) / 360f);
                float stepLength = arcLength / curveResolution;

                bool curveFailed = false;
                List<Vector3> tempCurvePoints = new List<Vector3>();
                Vector3 tempPos = currentPos;
                Vector3 tempDir = currentDir;

                for (int c = 0; c < curveResolution; c++)
                {
                    tempDir = Quaternion.Euler(0, anglePerStep, 0) * tempDir;
                    tempPos += tempDir * stepLength;
                    
                    if (IsOverlapping(tempPos, points)) { curveFailed = true; break; }
                    tempCurvePoints.Add(tempPos);
                }

                if (!curveFailed)
                {
                    points.AddRange(tempCurvePoints);
                    currentPos = tempPos;
                    currentDir = tempDir;
                }
            }
        }
        return points;
    }

    bool IsOverlapping(Vector3 testPos, List<Vector3> history)
    {
        int safeBuffer = 15; 
        float totalWidth = roadWidth + (sidewalkWidth * 2); 
        for (int i = 0; i < history.Count - safeBuffer; i++)
        {
            if (Vector3.Distance(testPos, history[i]) < totalWidth * 1.1f) return true;
        }
        return false;
    }

    void BuildRealisticMeshes(List<Vector3> points)
    {
        int pCount = points.Count;
        if (pCount < 2) return;

        Vector3[] rVerts = new Vector3[pCount * 2];
        Vector2[] rUVs = new Vector2[pCount * 2];
        int[] rTris = new int[(pCount - 1) * 6];

        Vector3[] sVerts = new Vector3[pCount * 8];
        Vector2[] sUVs = new Vector2[pCount * 8];
        int[] sTris = new int[(pCount - 1) * 24];

        Vector3[] wVerts = new Vector3[pCount * 4];
        Vector2[] wUVs = new Vector2[pCount * 4];
        int[] wTris = new int[(pCount - 1) * 12];

        float currentDist = 0f;

        for (int i = 0; i < pCount; i++)
        {
            Vector3 forward = (i < pCount - 1) ? (points[i + 1] - points[i]).normalized : (points[i] - points[i - 1]).normalized;
            Vector3 right = new Vector3(forward.z, 0, -forward.x);

            if (i > 0) currentDist += Vector3.Distance(points[i], points[i - 1]);
            float v = currentDist / textureTiling;

            Vector3 roadL = points[i] - right * (roadWidth / 2f);
            Vector3 roadR = points[i] + right * (roadWidth / 2f);
            
            Vector3 curbTopL = roadL + Vector3.up * curbHeight;
            Vector3 curbTopR = roadR + Vector3.up * curbHeight;
            
            Vector3 walkL = curbTopL - right * sidewalkWidth;
            Vector3 walkR = curbTopR + right * sidewalkWidth;

            Vector3 wallTopL = walkL + Vector3.up * wallHeight;
            Vector3 wallTopR = walkR + Vector3.up * wallHeight;

            rVerts[i * 2] = roadL; rVerts[i * 2 + 1] = roadR;
            rUVs[i * 2] = new Vector2(0, v); rUVs[i * 2 + 1] = new Vector2(1, v);

            sVerts[i*8] = roadL; sVerts[i*8+1] = curbTopL;     
            sVerts[i*8+2] = curbTopL; sVerts[i*8+3] = walkL;   
            sVerts[i*8+4] = roadR; sVerts[i*8+5] = curbTopR;   
            sVerts[i*8+6] = curbTopR; sVerts[i*8+7] = walkR;   
            
            sUVs[i*8] = new Vector2(0,v); sUVs[i*8+1] = new Vector2(0.2f,v); 
            sUVs[i*8+2] = new Vector2(0.2f,v); sUVs[i*8+3] = new Vector2(1,v);
            sUVs[i*8+4] = new Vector2(0,v); sUVs[i*8+5] = new Vector2(0.2f,v);
            sUVs[i*8+6] = new Vector2(0.2f,v); sUVs[i*8+7] = new Vector2(1,v);

            wVerts[i*4] = wallTopL; wVerts[i*4+1] = walkL;
            wVerts[i*4+2] = walkR; wVerts[i*4+3] = wallTopR;

            wUVs[i*4] = new Vector2(0,v); wUVs[i*4+1] = new Vector2(1,v);
            wUVs[i*4+2] = new Vector2(0,v); wUVs[i*4+3] = new Vector2(1,v);
        }

        for (int i = 0; i < pCount - 1; i++)
        {
            int r = i * 6; int rc = i * 2; int rn = (i + 1) * 2;
            rTris[r] = rc; rTris[r+1] = rn; rTris[r+2] = rn+1;
            rTris[r+3] = rc; rTris[r+4] = rn+1; rTris[r+5] = rc+1;

            int s = i * 24; int sc = i * 8; int sn = (i + 1) * 8;
            
            sTris[s] = sc; sTris[s+1] = sc+1; sTris[s+2] = sn+1;
            sTris[s+3] = sc; sTris[s+4] = sn+1; sTris[s+5] = sn;
            
            sTris[s+6] = sc+2; sTris[s+7] = sc+3; sTris[s+8] = sn+3;
            sTris[s+9] = sc+2; sTris[s+10] = sn+3; sTris[s+11] = sn+2;
            
            sTris[s+12] = sc+4; sTris[s+13] = sn+4; sTris[s+14] = sn+5;
            sTris[s+15] = sc+4; sTris[s+16] = sn+5; sTris[s+17] = sc+5;
            
            sTris[s+18] = sc+6; sTris[s+19] = sn+6; sTris[s+20] = sn+7;
            sTris[s+21] = sc+6; sTris[s+22] = sn+7; sTris[s+23] = sc+7;

            int w = i * 12; int wc = i * 4; int wn = (i + 1) * 4;
            wTris[w] = wc; wTris[w+1] = wn; wTris[w+2] = wn+1;
            wTris[w+3] = wc; wTris[w+4] = wn+1; wTris[w+5] = wc+1;
            
            wTris[w+6] = wc+3; wTris[w+7] = wc+2; wTris[w+8] = wn+2;
            wTris[w+9] = wc+3; wTris[w+10] = wn+2; wTris[w+11] = wn+3;
        }

        CreateMeshObject("1_Road", rVerts, rTris, rUVs, roadTag, asphaltMaterial);
        CreateMeshObject("2_Sidewalk", sVerts, sTris, sUVs, sidewalkTag, sidewalkMaterial);
        CreateMeshObject("3_BuildingWalls", wVerts, wTris, wUVs, wallTag, wallMaterial);
    }

    void CreateMeshObject(string name, Vector3[] verts, int[] tris, Vector2[] uvs, string tag, Material mat)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(this.transform);
        
        // --- THE MISSING FIX: Actually assign the tag! ---
        if (!string.IsNullOrEmpty(tag) && tag != "Untagged") 
        {
            obj.tag = tag;
        }

        // Prefab Local Spacing
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localRotation = Quaternion.identity;

        Mesh mesh = new Mesh();
        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.uv = uvs;
        mesh.RecalculateNormals(); 

        MeshFilter filter = obj.AddComponent<MeshFilter>();
        filter.mesh = mesh;

        MeshRenderer renderer = obj.AddComponent<MeshRenderer>();
        if (mat != null) renderer.material = mat;
        else renderer.material = new Material(Shader.Find("Standard")); 

        MeshCollider collider = obj.AddComponent<MeshCollider>();
        collider.sharedMesh = mesh;
    }

    void SpawnStartWall(List<Vector3> points)
    {
        if (points.Count < 2) return;

        float totalWidth = roadWidth + (sidewalkWidth * 2);
        Vector3 forward = (points[1] - points[0]).normalized;

        GameObject backWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        backWall.name = "Back_Wall";
        backWall.tag = wallTag; 
        
        backWall.transform.SetParent(this.transform);

        // --- PREFAB FIX: Use localPosition and localRotation ---
        backWall.transform.localPosition = points[0] + (Vector3.up * (wallHeight / 2f));
        backWall.transform.localRotation = Quaternion.LookRotation(forward);
        backWall.transform.localScale = new Vector3(totalWidth, wallHeight, 1f);
        
        if (wallMaterial != null) backWall.GetComponent<MeshRenderer>().material = wallMaterial;
    }

    void SpawnCheckpoints(List<Vector3> points)
    {
        GameObject cpRoot = new GameObject("Checkpoints");
        cpRoot.transform.SetParent(this.transform);
        cpRoot.transform.localPosition = Vector3.zero;
        cpRoot.transform.localRotation = Quaternion.identity;

        int cpIndex = 0;
        float totalWidth = roadWidth + (sidewalkWidth * 2);

        for (int i = 0; i < points.Count - 1; i += checkpointSpacing)
        {
            Vector3 pos = points[i];
            Vector3 forward = (points[i + 1] - points[i]).normalized;
            
            GameObject cp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cp.name = "Checkpoint_" + cpIndex;
            cp.tag = checkpointTag;
            cp.layer = LayerMask.NameToLayer("Ignore Raycast"); 
            
            cp.transform.SetParent(cpRoot.transform);

            // --- PREFAB FIX: Use localPosition and localRotation ---
            cp.transform.localPosition = pos + Vector3.up * 2f;
            cp.transform.localRotation = Quaternion.LookRotation(forward);
            cp.transform.localScale = new Vector3(totalWidth, 4f, 0.5f);
            
            cp.GetComponent<MeshRenderer>().enabled = false;
            cp.GetComponent<Collider>().isTrigger = true;
            cpIndex++;
        }

        if (cpRoot.transform.childCount > 0)
        {
            Transform lastCp = cpRoot.transform.GetChild(cpRoot.transform.childCount - 1);
            lastCp.name = "FINISH_LINE";
            lastCp.tag = finishTag;
            lastCp.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast"); 
        }
    }
}