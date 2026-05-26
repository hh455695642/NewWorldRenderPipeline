using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public enum ProgressMode
{
    LineRenderer,
    SelfMaterial
}

public class TaskPointParabolaGenerator : MonoBehaviour
{
    [Header("进度模式")]
    public ProgressMode mode = ProgressMode.LineRenderer;
    
    [Header("任务点列表")]
    public Transform[] taskPoint;
    
    [Header("LineRenderer预设")]
    public GameObject linePrefab;

    [Header("抛物线设置")]
    public float apexHeight = 25f;
    public float pointDensity = 0.5f;
    public float yOffset = 11f;
    
    [Header("贴图tilling控制")]
    public float repetitionDensity = 5f;  // 当长度=10时，Tiling.x=3
    
    [Header("整条路径进度")]
    [Range(0, 1)]
    public float progress;

    private List<LineRenderer> lines = new List<LineRenderer>();
    private List<float> lineLengths = new List<float>();

    private float totalPathLength = 0f;

    private MaterialPropertyBlock mpb;

    // ----- 性能优化：缓存组件与ID -----
    private Renderer selfRenderer;
    private UnityEngine.UI.Graphic selfGraphic;
    
    private static readonly int ProgressID = Shader.PropertyToID("_Progress");
    private static readonly int TilingOffsetID = Shader.PropertyToID("_Tiling_Offset");

    void Awake()
    {
        mpb = new MaterialPropertyBlock();
        selfRenderer = GetComponent<Renderer>();
        if (selfRenderer == null)
        {
            selfGraphic = GetComponent<UnityEngine.UI.Graphic>();
        }
    }

    void Start()
    {
        if (mode == ProgressMode.LineRenderer)
        {
            GenerateParabolas();
        }
        SetProgress(progress);
    }

    void Update()
    {
        SetProgress(progress);
    }

    public void GenerateParabolas()
    {
        if (taskPoint == null || taskPoint.Length < 2 || linePrefab == null) return;

        var sortedtaskPoint = taskPoint.OrderBy(t => t.name).ToArray();

        foreach (var line in lines)
        {
            if (line != null) Destroy(line.gameObject);
        }

        lines.Clear();
        lineLengths.Clear();
        totalPathLength = 0f;

        for (int i = 0; i < sortedtaskPoint.Length - 1; i++)
        {
            Transform startTower = sortedtaskPoint[i];
            Transform endTower = sortedtaskPoint[i + 1];

            GameObject lineObj = Instantiate(linePrefab, Vector3.zero, Quaternion.identity, this.transform);

            /*
             * Legacy project layer override disabled. Keep cloned prefab on its default layer.

            lineObj.layer = LayerMask.NameToLayer("MainMap"); //为大地图RT相机添加

             */
            LineRenderer line = lineObj.GetComponent<LineRenderer>();
            line.useWorldSpace = true;

            // line.gameObject.SetActive(false);
            lines.Add(line);

            DrawParabola(line, startTower.position, endTower.position);

            // 计算曲线长度
            float totalLength = 0f;
            Vector3 previousPosition = line.GetPosition(0);
            for (int j = 1; j < line.positionCount; j++)
            {
                Vector3 currentPosition = line.GetPosition(j);
                totalLength += Vector3.Distance(previousPosition, currentPosition);
                previousPosition = currentPosition;
            }
            
            // 根据长度调整Tiling
            float tilingX = (totalLength / repetitionDensity);

            Material mat = line.sharedMaterial; // 避免产生新的材质实例
            if (mat != null)
            {
                line.GetPropertyBlock(mpb);
                Vector4 tilingOffset = mat.GetVector(TilingOffsetID);
                tilingOffset.x = tilingX;
                mpb.SetVector(TilingOffsetID, tilingOffset);
                line.SetPropertyBlock(mpb);
            }

            lineLengths.Add(totalLength);
            totalPathLength += totalLength;
        }
    }

    void DrawParabola(LineRenderer line, Vector3 startPos, Vector3 endPos)
    {
        startPos.y += yOffset;
        endPos.y += yOffset;

        Vector3 apex = (startPos + endPos) / 2f;
        apex.y += apexHeight;

        // 避免不必要的乘法器
        float distance = Vector3.Distance(startPos, endPos);
        int pointCount = Mathf.Max(2, Mathf.CeilToInt(distance * pointDensity));

        line.positionCount = pointCount;

        for (int i = 0; i < pointCount; i++)
        {
            float t = i / (float)(pointCount - 1);
            float oneMinusT = 1f - t;

            // 优化数学计算
            Vector3 point = (oneMinusT * oneMinusT) * startPos
                          + (2f * oneMinusT * t) * apex
                          + (t * t) * endPos;

            line.SetPosition(i, point);
        }
    }

    // =========================
    // 整条路径进度控制
    // =========================

    public void SetProgress(float p)
    {
        progress = p;
        if (mode == ProgressMode.LineRenderer)
        {
            if (lines.Count == 0 || totalPathLength <= 0f) return;

            float currentLength = p * totalPathLength;
            float accumulated = 0f;
            
            for (int i = 0; i < lines.Count; i++)
            {
                float lineLen = lineLengths[i];
                float localProgress = 0f;

                if (currentLength >= accumulated + lineLen)
                {
                    localProgress = 1f;
                }
                else if (currentLength > accumulated)
                {
                    localProgress = (currentLength - accumulated) / lineLen;
                }

                lines[i].GetPropertyBlock(mpb);
                mpb.SetFloat(ProgressID, localProgress);
                lines[i].SetPropertyBlock(mpb);

                accumulated += lineLen;
            }
        }
        else if (mode == ProgressMode.SelfMaterial)
        {
            if (selfRenderer != null)
            {
                selfRenderer.GetPropertyBlock(mpb);
                mpb.SetFloat(ProgressID, p);
                selfRenderer.SetPropertyBlock(mpb);
            }
            else if (selfGraphic != null && selfGraphic.material != null)
            {
                // UI材质如果要生效可能需要修改material（默认不使用PropertyBlock）
                selfGraphic.material.SetFloat(ProgressID, p);
            }
        }
    }

    public void SetLineShow(int id)
    {
        if (mode == ProgressMode.LineRenderer)
        {
            lines[id].gameObject.SetActive(true);
        }

    }
}
