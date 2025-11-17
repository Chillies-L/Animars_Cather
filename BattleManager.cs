using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class BattleManager : MonoBehaviour
{
    [Header("Scene References")]
    public GridTerrainGenerator terrain;           // 地形生成器引用
    public List<Tower> towerPrefabs;               // 可用炮塔类型列表
    [Tooltip("与 towerPrefabs 顺序一一对应")]
    public List<int> towerPrices;                  // 炮塔价格（与上面顺序对应）
    public Transform enemyPrefab;                  // 敌人Prefab（必须带 Enemy 组件）

    [Header("UI Elements")]
    public Text hpText;
    public Text goldText;
    public Text waveText;
    public Text saveInfoText;

    [Header("Gameplay Settings")]
    public int baseHP = 100;
    public int gold = 200;
    public int wave = 1;
    public bool endlessMode = false;
    public int enemiesPerWave = 10;
    public float spawnInterval = 2f;               // 敌人逐个生成的间隔，同时也用于两波之间的等待

    private int enemiesAlive = 0;
    private float spawnTimer = 0f;

    [Header("存档设置")]
    [Tooltip("自动保存的存档槽 (0-2)")]
    public int autoSaveSlot = 0;
    [Tooltip("是否每波结束自动保存")]
    public bool autoSaveEnabled = true;

    public static BattleManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private IEnumerator Start()
    {
        Time.timeScale = 1f;

        // 等待 GridTerrainGenerator 初始化
        yield return new WaitUntil(() => terrain != null && terrain.IsGridReady);

        LoadGame(autoSaveSlot);
        ApplyPathLengthByMode();
        StartNextWave();
        UpdateUI();
    }


    private void Update()
    {
        // 没有存活敌人时，计时后开下一波
        if (enemiesAlive <= 0)
        {
            spawnTimer += Time.deltaTime;
            if (spawnTimer >= spawnInterval)
            {
                spawnTimer = 0f;
                wave++;
                AutoSaveIfNeeded();
                StartNextWave();
            }
        }
    }

    #region === 存档结构 ===
    [Serializable]
    public class SaveData
    {
        public int version = 2;                    // ✅ 存档版本号
        public string savedAt;                     // ✅ 存档时间戳
        public bool endlessMode;
        public int baseHP;
        public int gold;
        public int wave;
        public int terrainSeed;                    // ✅ 随机种子
        public int rows, cols, minPathLength;      // ✅ 地形参数
        public List<TowerSave> towers = new();     // ✅ 炮塔按格子索引保存
    }

    [Serializable]
    public class TowerSave
    {
        public int id;
        public int level;
        public int row;
        public int col;
    }
    #endregion

    #region === 存档路径 ===
    private string SavePath(int slot = 0)
    {
        return Path.Combine(Application.persistentDataPath, $"savegame_{slot}.json");
    }
    #endregion

    #region === 存档操作 ===
    public void SaveGame(int slot = 0)
    {
        if (terrain == null) return;

        SaveData data = new()
        {
            version = 2,
            savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            endlessMode = endlessMode,
            baseHP = baseHP,
            gold = gold,
            wave = wave,
            terrainSeed = terrain.Seed,
            rows = terrain.rows,
            cols = terrain.cols,
            minPathLength = terrain.minPathLength
        };

        // 保存炮塔（按格子索引）
        foreach (var tower in FindObjectsOfType<Tower>())
        {
            var cell = tower.GetComponentInParent<ClickableCell>(); // 注意：没有 TryGetComponentInParent
            if (cell != null)
            {
                data.towers.Add(new TowerSave
                {
                    id = tower.towerID,
                    level = tower.level,
                    row = cell.row,
                    col = cell.col
                });
            }
        }

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(SavePath(slot), json);

        Debug.Log($"💾 存档成功 Slot {slot}: {SavePath(slot)}");
        if (saveInfoText)
            saveInfoText.text = $"已保存 (Slot {slot}) {data.savedAt}";
    }

    public void LoadGame(int slot = 0)
    {
        string path = SavePath(slot);
        if (!File.Exists(path))
        {
            Debug.LogWarning($"⚠️ 未找到存档：{path}");
            // 没有存档则直接生成一套地形
            if (terrain) terrain.SignalGenerateAndRaise();
            return;
        }

        string json = File.ReadAllText(path);
        SaveData data = JsonUtility.FromJson<SaveData>(json);

        // 旧版本升级
        if (data.version < 2)
        {
            Debug.Log("⚙️ 升级旧存档格式 -> v2");
            data.version = 2;
        }

        endlessMode = data.endlessMode;
        baseHP = data.baseHP;
        gold = data.gold;
        wave = data.wave;

        // 恢复地形参数 + 按种子重生成
        if (terrain)
        {
            terrain.rows = data.rows;
            terrain.cols = data.cols;
            terrain.minPathLength = data.minPathLength;
            terrain.Seed = data.terrainSeed;
            terrain.RegenerateFromSeed();
        }

        // 清理旧塔
        foreach (var oldTower in FindObjectsOfType<Tower>())
            Destroy(oldTower.gameObject);

        // 放回炮塔（按格子索引）
        foreach (var ts in data.towers)
        {
            var cell = terrain.GetCell(ts.row, ts.col);
            if (cell)
            {
                int idx = Mathf.Clamp(ts.id, 0, towerPrefabs.Count - 1);
                var prefab = towerPrefabs[idx];
                var tower = Instantiate(prefab, cell.transform.position + Vector3.up * 2f, Quaternion.identity, cell.transform);
                tower.level = ts.level;
                tower.towerID = idx;
                cell.SetTower(tower);
            }
        }

        if (saveInfoText)
            saveInfoText.text = $"已加载存档 (Slot {slot}) {data.savedAt}";
        UpdateUI();
    }

    public void ClearSave(int slot = 0)
    {
        string path = SavePath(slot);
        if (File.Exists(path))
        {
            File.Delete(path);
            Debug.Log($"🗑️ 已清空存档 Slot {slot}");
            if (saveInfoText)
                saveInfoText.text = $"已清空存档 Slot {slot}";
        }
    }

    public void AutoSaveIfNeeded()
    {
        if (!autoSaveEnabled) return;
        if (enemiesAlive <= 0)
            SaveGame(autoSaveSlot);
    }
    #endregion

    #region === 波次与地形控制 ===
    private void StartNextWave()
    {
        StartCoroutine(SpawnWaveCoroutine());
    }

    private System.Collections.IEnumerator SpawnWaveCoroutine()
    {
        enemiesAlive = enemiesPerWave;
        for (int i = 0; i < enemiesPerWave; i++)
        {
            SpawnEnemy();
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    private void SpawnEnemy()
    {
        if (terrain == null) return;

        // 获取起点格子的行列
        var startPath = terrain.GetCurrentPathPositions();
        if (startPath == null || startPath.Count == 0) return;

        Vector3 startPos = startPath[0] + Vector3.up * 0.5f; // 稍微抬高

        var enemy = Instantiate(enemyPrefab, startPos, Quaternion.identity);
        var e = enemy.GetComponent<Enemy>();

        e.InitPath(terrain.GetCurrentPathPositions());
        e.onDeath += OnEnemyDeath;
        e.onReachGoal += OnEnemyReachGoal;
    }


    private void OnEnemyDeath(Enemy e)
    {
        gold += e.goldReward;
        enemiesAlive--;
        UpdateUI();
    }

    private void OnEnemyReachGoal(Enemy e)
    {
        baseHP -= e.damageToBase;
        enemiesAlive--;
        UpdateUI();

        if (baseHP <= 0)
        {
            Debug.Log("❌ 游戏结束");
            Time.timeScale = 0f;
        }
    }

    private void ApplyPathLengthByMode()
    {
        if (terrain)
        {
            terrain.SetPathLengthByMode(endlessMode, 20, 35);
        }
    }
    #endregion

    #region === 购塔接口（TowerMenuCanvas 调用） ===
    public bool TryPurchaseTower(ClickableCell cell, int towerIndex)
    {
        if (cell == null)
        {
            Debug.LogWarning("TryPurchaseTower: cell 为空。");
            return false;
        }
        if (cell.hasTower)
        {
            Debug.LogWarning("该格子已有炮塔！");
            return false;
        }
        if (towerIndex < 0 || towerIndex >= towerPrefabs.Count)
        {
            Debug.LogWarning("无效的炮塔索引！");
            return false;
        }
        if (towerPrices == null || towerIndex >= towerPrices.Count)
        {
            Debug.LogWarning("未配置炮塔价格列表或索引越界！");
            return false;
        }

        int price = towerPrices[towerIndex];
        if (gold < price)
        {
            Debug.Log("金币不足！");
            return false;
        }

        gold -= price;

        Tower tower = Instantiate(
            towerPrefabs[towerIndex],
            cell.transform.position + Vector3.up * 2f,
            Quaternion.identity,
            cell.transform
        );
        tower.towerID = towerIndex;
        cell.SetTower(tower);

        UpdateUI();
        return true;
    }
    #endregion

    #region === UI ===
    public void UpdateUI()
    {
        if (hpText) hpText.text = $"HP: {baseHP}";
        if (goldText) goldText.text = $"金币: {gold}";
        if (waveText) waveText.text = $"波次: {wave}";
    }
    #endregion
}
