using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class GridTerrainGenerator : MonoBehaviour
{
    #region Grid
    [Header("Grid")]
    public int rows = 9;
    public int cols = 11;
    [Tooltip("路径平面高度（例如 10）")]
    public float baseY = 10f;
    public bool IsGridReady => hasGrid;


    [Header("Random Seed")]
    public int Seed = 0;

    public void RegenerateFromSeed()
    {
        Random.InitState(Seed);
        ResetAllToOrigin();
        SignalGenerateAndRaise();
    }

    [Tooltip("如果父物体子节点数量不等于 rows*cols，且勾选本项，将使用 cubePrefab 自动生成网格")]
    public bool instantiateIfMissing = false;
    public GameObject cubePrefab;
    [Tooltip("仅用于自动生成时的间距")]
    public float cellSize = 1f;
    [Tooltip("原始高度（Reset 时回到该高度）")]
    public float originY = 0f;

    // Path length switch
    public void SetPathLengthByMode(bool isEndless, int normalLen = 20, int endlessLen = 35)
    {
        int target = isEndless ? endlessLen : normalLen;
        int maxPossible = rows * cols;
        minPathLength = Mathf.Clamp(target, 2, maxPossible);
        Debug.Log($"[GridTerrainGenerator] minPathLength set to {minPathLength} (endless={isEndless})");
    }

    // 内部数据
    private GameObject[,] cells;   // [r,c]
    private bool[,] isPath;
    private bool[,] isAdjBoost;
    private float[,] targetHeights;
    private List<Vector2Int> pathList;
    private Vector2Int start, goal;

    private struct RenderSlot
    {
        public Renderer renderer;
        public Material matInstance;
        public Color originalColor;
    }
    private RenderSlot[,] renderSlots;
    #endregion

    #region Perlin
    [Header("Perlin Noise")]
    public float noiseFrequency = 0.15f; // 频率
    public float heightAmplitude = 4f;   // 抬升幅度（±Amplitude）
    public Vector2 noiseOffset;          // 噪声偏移（可随 seed 改动）
    #endregion

    #region Path Constraints
    [Header("Path Constraints")]
    public int minPathLength = 15;       // 至少 15 格
    public int maxGenerateTries = 500;   // 生成失败重试次数上限
    #endregion

    #region Markers (optional)
    [Header("Markers (optional)")]
    public GameObject startMarkerPrefab;
    public GameObject endMarkerPrefab;
    public float markerYOffset = 2f;

    [Header("Marker Sizing")]
    public Vector3 markerWorldScale = Vector3.one; // 期望的世界尺寸
    public bool compensateParentScale = true;      // 是否对父缩放做补偿

    private GameObject startMarker;
    private GameObject endMarker;
    #endregion

    #region Highlight & Colors
    public enum HighlightMode { Constant, Pulse }

    [Header("Color Ease (Common)")]
    public float colorFadeInDuration = 0.25f;
    public Ease colorFadeEase = Ease.OutSine;

    [Header("Path Highlight")]
    public bool highlightPath = true;
    public HighlightMode pathHighlightMode = HighlightMode.Pulse;
    public Color pathColor = Color.white;
    public float pathPulseDuration = 1.2f;
    public Ease pathPulseEase = Ease.InOutSine;

    [Header("Adjacent Boost Color")]
    public bool colorAdjBoost = true;
    public HighlightMode adjColorMode = HighlightMode.Constant;
    public Color adjColor = new Color(1f, 0.8f, 0.2f);
    public float adjPulseDuration = 1.2f;
    public Ease adjPulseEase = Ease.InOutSine;
    #endregion

    #region Adjacent Boost
    [Header("Adjacent Boost")]
    [Tooltip("路径相邻方块总数量至少为 ceil(pathLen*adjacentCountScale)。")]
    public float adjacentCountScale = 1.5f;
    [Tooltip("相邻方块目标高度至少为 baseY * adjacentRaiseMultiplier。")]
    public float adjacentRaiseMultiplier = 1.25f;
    #endregion

    #region Raise Orchestration
    [Header("Raise Orchestration")]
    public bool autoSignalOnStart = false;
    public float delayAfterSignal = 1.5f;
    public bool smoothRaise = true;
    public float raiseDuration = 1.2f;
    public Ease raiseEase = Ease.InOutSine;
    public bool stagedRaise = false;
    public float stageInterval = 0.6f;
    public float resetDuration = 0.6f;
    public Ease resetEase = Ease.InOutSine;
    #endregion

    #region State
    private bool hasGrid = false;
    private bool generated = false;
    private bool raising = false;
    #endregion

    void Start()
    {
        BuildOrLoadGrid();
        CaptureRenderers();

        if (autoSignalOnStart)
        {
            SignalGenerateAndRaise();
        }
    }

    #region Build / Load
    void BuildOrLoadGrid()
    {
        int childCount = transform.childCount;
        int need = rows * cols;

        if (childCount != need)
        {
            if (!instantiateIfMissing || cubePrefab == null)
            {
                Debug.LogError($"子物体数量({childCount}) != rows*cols({need})，且未开启自动生成/缺少cubePrefab。");
                return;
            }

            // 清空现有子物体
            var toDelete = new List<Transform>();
            foreach (Transform t in transform) toDelete.Add(t);
            foreach (var t in toDelete) DestroyImmediate(t.gameObject);

            // 以父物体为原点，生成规则网格
            Vector3 origin = transform.position;
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    Vector3 pos = origin + new Vector3(c * cellSize, originY, r * cellSize);
                    var go = Instantiate(cubePrefab, pos, Quaternion.identity, transform);
                    go.name = $"Cell_{r}_{c}";
                }
        }

        // 把子物体按 Z(行) 再按 X(列) 排序，映射进数组
        var list = new List<Transform>();
        foreach (Transform t in transform) list.Add(t);

        list.Sort((a, b) =>
        {
            int zCmp = a.position.z.CompareTo(b.position.z);
            if (zCmp != 0) return zCmp;
            return a.position.x.CompareTo(b.position.x);
        });

        cells = new GameObject[rows, cols];
        for (int i = 0; i < list.Count; i++)
        {
            int r = i / cols;
            int c = i % cols;
            cells[r, c] = list[i].gameObject;

            // 确保初始在 originY
            var p = cells[r, c].transform.position;
            p.y = originY;
            cells[r, c].transform.position = p;

            // 确保挂载 ClickableCell 并写入索引
            var cc = cells[r, c].GetComponent<ClickableCell>();
            if (cc == null) cc = cells[r, c].AddComponent<ClickableCell>();
            cc.row = r;
            cc.col = c;
            cc.hasTower = false;
        }

        // 预分配数组
        isPath = new bool[rows, cols];
        isAdjBoost = new bool[rows, cols];
        targetHeights = new float[rows, cols];

        hasGrid = true;
    }

    void CaptureRenderers()
    {
        renderSlots = new RenderSlot[rows, cols];
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                var rend = cells[r, c].GetComponentInChildren<Renderer>();
                if (rend != null)
                {
                    var inst = rend.material; // 实例化材质
                    renderSlots[r, c] = new RenderSlot
                    {
                        renderer = rend,
                        matInstance = inst,
                        originalColor = inst.HasProperty("_Color") ? inst.color : Color.white
                    };
                }
            }
    }
    #endregion

    #region External Signal API
    public void SignalGenerateAndRaise()
    {
        if (!hasGrid) { Debug.LogError("网格未就绪"); return; }
        if (raising) { Debug.LogWarning("正在抬升中，忽略新的信号"); return; }

        StopAllCoroutines();
        StartCoroutine(Co_SignalGenerateAndRaise());
    }
    #endregion

    #region Generation Orchestration
    System.Collections.IEnumerator Co_SignalGenerateAndRaise()
    {
        raising = true;

        if (delayAfterSignal > 0f)
            yield return new WaitForSeconds(delayAfterSignal);

        // 生成（唯一路径 + 目标高度）
        GenerateValidTerrain();

        // 同步二次抬升可交互标记
        SyncClickableCellFlags();

        // 放置标记
        PlaceMarkers();

        // 高亮
        ApplyHighlightEffects();

        // 抬升
        if (stagedRaise)
        {
            RaiseGroup((r, c) => !isPath[r, c] && !isAdjBoost[r, c]);
            if (stageInterval > 0f) yield return new WaitForSeconds(stageInterval);

            RaiseGroup((r, c) => isPath[r, c]);
            if (stageInterval > 0f) yield return new WaitForSeconds(stageInterval);

            RaiseGroup((r, c) => isAdjBoost[r, c]);
        }
        else
        {
            RaiseGroup((r, c) => true);
        }

        raising = false;
    }
    #endregion

    #region Generation (Path + Targets)
    void GenerateValidTerrain()
    {
        if (Seed == 0)
            Seed = UnityEngine.Random.Range(1, int.MaxValue);
        Random.InitState(Seed);

        generated = false;

        for (int attempt = 0; attempt < maxGenerateTries; attempt++)
        {
            System.Array.Clear(isPath, 0, isPath.Length);
            System.Array.Clear(isAdjBoost, 0, isAdjBoost.Length);

            if (!TryBuildSinglePath(out pathList, out start, out goal)) continue;

            ComputeBaseTargets();
            SelectAndBoostAdjacent();

            if (ValidateAll(pathList, start, goal))
            {
                generated = true;
                Debug.Log($"地形生成成功，路径长度：{pathList.Count}（尝试次数 {attempt + 1}）");
                return;
            }
        }

        Debug.LogError("在最大尝试次数内未能生成满足所有条件的地形，请调整参数或增大 maxGenerateTries。");
    }

    bool TryBuildSinglePath(out List<Vector2Int> path, out Vector2Int s, out Vector2Int g)
    {
        path = null; s = g = default;

        var visited = new HashSet<Vector2Int>();
        var result = new List<Vector2Int>();

        Vector2Int cur = new Vector2Int(Random.Range(0, rows), Random.Range(0, cols));
        result.Add(cur);
        visited.Add(cur);
        isPath[cur.x, cur.y] = true;

        int guard = rows * cols * 10;
        while (guard-- > 0 && result.Count < Mathf.Max(minPathLength, 4))
        {
            var candidates = GetShuffledNeighbors(cur);
            bool advanced = false;
            foreach (var nb in candidates)
            {
                if (!InBounds(nb)) continue;
                if (visited.Contains(nb)) continue;
                if (WouldCreate2x2(nb)) continue;
                if (WouldBranch(nb)) continue;

                result.Add(nb);
                visited.Add(nb);
                isPath[nb.x, nb.y] = true;
                cur = nb;
                advanced = true;
                break;
            }

            if (!advanced) break;
        }

        if (result.Count < minPathLength) return false;

        path = result;
        s = result[0];
        g = result[result.Count - 1];
        return true;
    }

    List<Vector2Int> GetShuffledNeighbors(Vector2Int p)
    {
        var v = new List<Vector2Int>
        {
            new Vector2Int(p.x - 1, p.y),
            new Vector2Int(p.x + 1, p.y),
            new Vector2Int(p.x, p.y - 1),
            new Vector2Int(p.x, p.y + 1),
        };
        for (int i = v.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (v[i], v[j]) = (v[j], v[i]);
        }
        return v;
    }

    bool InBounds(Vector2Int p) => p.x >= 0 && p.x < rows && p.y >= 0 && p.y < cols;

    bool WouldCreate2x2(Vector2Int next)
    {
        int x = next.x, y = next.y;

        bool IsP(int r, int c)
        {
            if (r < 0 || c < 0 || r >= rows || c >= cols) return false;
            return isPath[r, c] || (r == x && c == y);
        }

        if (IsP(x, y) && IsP(x - 1, y) && IsP(x, y - 1) && IsP(x - 1, y - 1)) return true;
        if (IsP(x, y) && IsP(x - 1, y) && IsP(x, y + 1) && IsP(x - 1, y + 1)) return true;
        if (IsP(x, y) && IsP(x + 1, y) && IsP(x, y - 1) && IsP(x + 1, y - 1)) return true;
        if (IsP(x, y) && IsP(x + 1, y) && IsP(x, y + 1) && IsP(x + 1, y + 1)) return true;

        return false;
    }

    bool WouldBranch(Vector2Int next)
    {
        int adjacent = 0;
        var dirs = new[] { new Vector2Int(-1, 0), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(0, 1) };
        foreach (var d in dirs)
        {
            var q = next + d;
            if (InBounds(q) && isPath[q.x, q.y]) adjacent++;
            if (adjacent > 1) return true;
        }
        return false;
    }

    void ComputeBaseTargets()
    {
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                if (isPath[r, c])
                {
                    targetHeights[r, c] = baseY;
                }
                else
                {
                    float nx = noiseOffset.x + c * noiseFrequency;
                    float ny = noiseOffset.y + r * noiseFrequency;
                    float n = Mathf.PerlinNoise(nx, ny); // [0,1]
                    float offset = (n - 0.5f) * 2f * heightAmplitude;
                    targetHeights[r, c] = baseY + offset;
                }
            }
    }

    void SelectAndBoostAdjacent()
    {
        var adjSet = new HashSet<Vector2Int>();
        var dirs = new[] { new Vector2Int(-1, 0), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(0, 1) };

        foreach (var p in pathList)
        {
            foreach (var d in dirs)
            {
                var q = p + d;
                if (!InBounds(q)) continue;
                if (isPath[q.x, q.y]) continue;
                adjSet.Add(q);
            }
        }

        int need = Mathf.CeilToInt(pathList.Count * adjacentCountScale);
        List<Vector2Int> adjList = new List<Vector2Int>(adjSet);

        if (adjList.Count < need)
        {
            Debug.LogWarning($"相邻可用方块不足：需要 {need}，仅有 {adjList.Count}。将使用全部可用相邻方块。");
            need = adjList.Count;
        }

        Shuffle(adjList);
        for (int i = 0; i < need; i++)
        {
            var q = adjList[i];
            isAdjBoost[q.x, q.y] = true;

            float minHeight = baseY * adjacentRaiseMultiplier;
            if (targetHeights[q.x, q.y] < minHeight)
                targetHeights[q.x, q.y] = minHeight;
        }
    }

    void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
    #endregion

    #region Validation (using isPath + targetHeights)
    bool ValidateAll(List<Vector2Int> path, Vector2Int s, Vector2Int g)
    {
        return ValidateUniquePath(path, s, g)
               && ValidateAxisAdjacency(path)
               && ValidateNo2x2()
               && ValidatePathHeight()
               && path.Count >= minPathLength;
    }

    bool ValidateUniquePath(List<Vector2Int> path, Vector2Int s, Vector2Int g)
    {
        int CountAdj(Vector2Int p)
        {
            int cnt = 0;
            var dirs = new[] { new Vector2Int(-1, 0), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(0, 1) };
            foreach (var d in dirs)
            {
                var q = p + d;
                if (InBounds(q) && isPath[q.x, q.y]) cnt++;
            }
            return cnt;
        }

        for (int i = 0; i < path.Count; i++)
        {
            int deg = CountAdj(path[i]);
            if (i == 0 || i == path.Count - 1)
            {
                if (deg != 1) { Debug.LogWarning("端点度数不是1"); return false; }
            }
            else
            {
                if (deg != 2) { Debug.LogWarning("中间点度数不是2"); return false; }
            }
        }

        var qd = new Queue<Vector2Int>();
        var seen = new HashSet<Vector2Int>();
        qd.Enqueue(s);
        seen.Add(s);
        while (qd.Count > 0)
        {
            var p = qd.Dequeue();
            var dirs = new[] { new Vector2Int(-1, 0), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };
            foreach (var d in dirs)
            {
                var nb = p + d;
                if (!InBounds(nb) || !isPath[nb.x, nb.y] || seen.Contains(nb)) continue;
                seen.Add(nb);
                qd.Enqueue(nb);
            }
        }
        if (!seen.Contains(g)) { Debug.LogWarning("起终点不连通"); return false; }
        if (seen.Count != path.Count) { Debug.LogWarning("路径集合不是一条链（存在分支或孤立）"); return false; }

        return true;
    }

    bool ValidateAxisAdjacency(List<Vector2Int> path)
    {
        for (int i = 1; i < path.Count; i++)
        {
            var a = path[i - 1];
            var b = path[i];
            int dr = Mathf.Abs(a.x - b.x);
            int dc = Mathf.Abs(a.y - b.y);
            if (!((dr == 1 && dc == 0) || (dr == 0 && dc == 1)))
            {
                Debug.LogWarning("路径中存在对角或不相邻跳跃");
                return false;
            }
        }
        return true;
    }

    bool ValidateNo2x2()
    {
        for (int r = 0; r < rows - 1; r++)
            for (int c = 0; c < cols - 1; c++)
            {
                int cnt = 0;
                cnt += isPath[r, c] ? 1 : 0;
                cnt += isPath[r + 1, c] ? 1 : 0;
                cnt += isPath[r, c + 1] ? 1 : 0;
                cnt += isPath[r + 1, c + 1] ? 1 : 0;
                if (cnt == 4)
                {
                    Debug.LogWarning("检测到 2x2 路径块");
                    return false;
                }
            }
        return true;
    }

    bool ValidatePathHeight()
    {
        foreach (var p in pathList)
        {
            float y = targetHeights[p.x, p.y];
            if (Mathf.Abs(y - baseY) > 0.001f)
            {
                Debug.LogWarning("路径方块的目标高度不是 baseY");
                return false;
            }
        }
        return true;
    }
    #endregion

    #region Markers
    void AttachMarkerToCell(GameObject marker, Transform cell, float yOffset)
    {
        if (!compensateParentScale)
        {
            marker.transform.position = cell.position + Vector3.up * yOffset;
            marker.transform.rotation = Quaternion.identity;
            marker.transform.localScale = markerWorldScale;
            marker.transform.SetParent(cell, worldPositionStays: true);
            return;
        }

        Vector3 parentLossy = cell.lossyScale;
        float sx = Mathf.Approximately(parentLossy.x, 0f) ? 1f : parentLossy.x;
        float sy = Mathf.Approximately(parentLossy.y, 0f) ? 1f : parentLossy.y;
        float sz = Mathf.Approximately(parentLossy.z, 0f) ? 1f : parentLossy.z;

        marker.transform.SetParent(cell, worldPositionStays: false);
        marker.transform.localRotation = Quaternion.identity;

        marker.transform.localScale = new Vector3(
            markerWorldScale.x / sx,
            markerWorldScale.y / sy,
            markerWorldScale.z / sz
        );

        marker.transform.localPosition = new Vector3(0f, yOffset / sy, 0f);
    }

    void PlaceMarkers()
    {
        if (startMarker != null) Destroy(startMarker);
        if (endMarker != null) Destroy(endMarker);

        if (startMarkerPrefab != null && cells != null)
        {
            var cell = cells[start.x, start.y].transform;
            startMarker = Instantiate(startMarkerPrefab);
            startMarker.name = "StartMarker";
            AttachMarkerToCell(startMarker, cell, markerYOffset);
        }

        if (endMarkerPrefab != null && cells != null)
        {
            var cell = cells[goal.x, goal.y].transform;
            endMarker = Instantiate(endMarkerPrefab);
            endMarker.name = "EndMarker";
            AttachMarkerToCell(endMarker, cell, markerYOffset);
        }
    }
    #endregion

    #region Highlight & Colors
    void ApplyHighlightEffects()
    {
        DOTween.Kill(this, complete: false);
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                var slot = renderSlots[r, c];
                if (slot.matInstance == null) continue;
                DOTween.Kill(slot.matInstance);
                slot.matInstance.color = slot.originalColor;
            }

        if (highlightPath && pathList != null)
        {
            foreach (var p in pathList)
            {
                var slot = renderSlots[p.x, p.y];
                if (slot.matInstance == null) continue;

                DOTween.Kill(slot.matInstance);

                if (pathHighlightMode == HighlightMode.Constant)
                {
                    var fromColor = slot.matInstance.HasProperty("_Color") ? slot.matInstance.color : slot.originalColor;
                    slot.matInstance.color = fromColor;
                    slot.matInstance
                        .DOColor(pathColor, Mathf.Max(0.01f, colorFadeInDuration))
                        .SetEase(colorFadeEase)
                        .SetTarget(slot.matInstance);
                }
                else
                {
                    var seq = DOTween.Sequence().SetTarget(slot.matInstance);
                    var fromColor = slot.matInstance.HasProperty("_Color") ? slot.matInstance.color : slot.originalColor;
                    seq.Append(slot.matInstance
                        .DOColor(pathColor, Mathf.Max(0.01f, colorFadeInDuration))
                        .SetEase(colorFadeEase));
                    seq.Append(slot.matInstance
                        .DOColor(slot.originalColor, Mathf.Max(0.01f, pathPulseDuration * 0.5f))
                        .SetEase(pathPulseEase)
                        .SetLoops(-1, LoopType.Yoyo));
                }
            }
        }

        if (colorAdjBoost)
        {
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    if (!isAdjBoost[r, c]) continue;
                    var slot = renderSlots[r, c];
                    if (slot.matInstance == null) continue;

                    if (adjColorMode == HighlightMode.Constant)
                    {
                        slot.matInstance.color = adjColor;
                    }
                    else
                    {
                        slot.matInstance.DOColor(adjColor, adjPulseDuration * 0.5f)
                            .SetEase(adjPulseEase)
                            .SetLoops(-1, LoopType.Yoyo)
                            .SetTarget(slot.matInstance);
                    }
                }
        }
    }
    #endregion

    #region Raise
    delegate bool CellPredicate(int r, int c);

    void RaiseGroup(CellPredicate predicate)
    {
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                if (!predicate(r, c)) continue;

                var t = cells[r, c].transform;
                float targetY = targetHeights[r, c];

                if (smoothRaise)
                {
                    t.DOMoveY(targetY, raiseDuration).SetEase(raiseEase);
                }
                else
                {
                    var p = t.position;
                    p.y = targetY;
                    t.position = p;
                }
            }
    }
    #endregion

    #region Public Reset
    public void ResetAllToOrigin()
    {
        StopAllCoroutines();
        raising = false;

        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                var slot = renderSlots[r, c];
                if (slot.matInstance != null)
                {
                    DOTween.Kill(slot.matInstance);
                    slot.matInstance.DOColor(slot.originalColor, 0.15f).SetEase(Ease.Linear);
                }
            }

        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                var t = cells[r, c].transform;
                if (smoothRaise)
                {
                    t.DOMoveY(originY, resetDuration).SetEase(resetEase);
                }
                else
                {
                    var p = t.position;
                    p.y = originY;
                    t.position = p;
                }
            }

        generated = false;
        System.Array.Clear(isPath, 0, isPath.Length);
        System.Array.Clear(isAdjBoost, 0, isAdjBoost.Length);
    }
    #endregion

    #region Export / Helpers (供 BattleManager 调用)
    // 与 BattleManager 期望的命名保持一致
    public Vector3 GetStartWorldPosition()
    {
        if (cells == null) return transform.position;
        var p = (pathList != null && pathList.Count > 0) ? pathList[0] : new Vector2Int(0, 0);
        return cells[p.x, p.y].transform.position + Vector3.up * 1f;
    }

    public List<Vector3> GetCurrentPathWorldPositions()
    {
        return GetCurrentPathPositions();
    }

    public List<Vector3> GetCurrentPathPositions()
    {
        List<Vector3> pathPositions = new List<Vector3>();
        if (pathList == null || pathList.Count == 0 || cells == null)
        {
            Debug.LogWarning("Path not generated yet!");
            return pathPositions;
        }

        for (int i = 0; i < pathList.Count; i++)
        {
            var cell = cells[pathList[i].x, pathList[i].y];
            if (cell != null)
            {
                var rend = cell.GetComponentInChildren<Renderer>();
                if (rend != null)
                {
                    var topY = rend.bounds.max.y;
                    pathPositions.Add(new Vector3(cell.transform.position.x, topY + 0.5f, cell.transform.position.z));
                }
                else
                {
                    pathPositions.Add(cell.transform.position + Vector3.up * 1f);
                }
            }
        }
        return pathPositions;
    }

    public ClickableCell GetCell(int r, int c)
    {
        if (cells == null) return null;
        if (r < 0 || c < 0 || r >= rows || c >= cols) return null;
        return cells[r, c].GetComponent<ClickableCell>();
    }

    void SyncClickableCellFlags()
    {
        if (cells == null) return;
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                var cc = cells[r, c].GetComponent<ClickableCell>();
                if (cc) cc.isAdjBoostCell = isAdjBoost[r, c];
            }
    }
    #endregion

    public Vector3 GetCellTopWorldPosition(int row, int col, float offset = 0.5f)
    {
        if (cells == null) return Vector3.zero;
        var cell = cells[row, col];
        if (!cell) return Vector3.zero;
        var pos = cell.transform.position;
        var bounds = cell.GetComponentInChildren<Renderer>()?.bounds;
        float topY = bounds.HasValue ? bounds.Value.max.y : pos.y;
        return new Vector3(pos.x, topY + offset, pos.z);
    }

}
