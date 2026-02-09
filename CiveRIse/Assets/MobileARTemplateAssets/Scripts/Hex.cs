using UnityEngine;
using System.Collections.Generic;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

// КЛАСС ДЛЯ КАЖДОГО ГЕКСА (внутренний класс)
public class HexInfo : MonoBehaviour
{
    public int q;
    public int r;
    public HexGridAR grid;

    public void SetMaterial(Material mat)
    {
        Renderer rend = GetComponent<Renderer>();
        if (rend != null && mat != null)
        {
            rend.material = mat;
        }
    }

    // Для теста в редакторе
    void OnMouseDown()
    {
        if (grid != null)
        {
            grid.OnHexSelected(this);
        }
    }
}

// ОСНОВНОЙ КЛАСС СЕТКИ
public class HexGridAR : MonoBehaviour
{
    [Header("Основные настройки")]
    public GameObject hexPrefab; // Префаб шестиугольника
    public int gridRadius = 4; // Размер сетки
    public float hexSize = 0.5f; // Размер гекса

    [Header("AR настройки")]
    public ARRaycastManager raycastManager;
    private bool gridPlaced = false;
    private GameObject currentGrid;

    [Header("Материалы")]
    public Material defaultMat;
    public Material highlightedMat;
    public Material moveMat;

    // Словарь для хранения гексов
    private Dictionary<string, GameObject> hexMap = new Dictionary<string, GameObject>();
    private GameObject selectedUnit;

    void Start()
    {
        // Авто-размещение для демо
        Invoke("AutoPlaceGrid", 2f);
    }

    void Update()
    {
        // Размещение сетки по тапу
        if (!gridPlaced && Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
            {
                PlaceGrid(touch.position);
            }
        }

        // Выбор гекса/юнита
        if (gridPlaced && Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
            {
                SelectHexOrUnit(touch.position);
            }
        }
    }

    void AutoPlaceGrid()
    {
        if (!gridPlaced)
        {
            // Размещаем по центру экрана
            PlaceGrid(new Vector2(Screen.width / 2, Screen.height / 2));
        }
    }

    void PlaceGrid(Vector2 screenPos)
    {
        if (raycastManager == null)
        {
            Debug.LogError("Нет ARRaycastManager!");
            return;
        }

        List<ARRaycastHit> hits = new List<ARRaycastHit>();

        if (raycastManager.Raycast(screenPos, hits, TrackableType.PlaneWithinPolygon))
        {
            Pose hitPose = hits[0].pose;

            // Создаем контейнер для сетки
            currentGrid = new GameObject("HexGridContainer");
            currentGrid.transform.position = hitPose.position;

            // Генерируем сетку
            GenerateHexGrid();

            // Создаем игрока
            CreatePlayerUnit();

            gridPlaced = true;
            Debug.Log("✅ Сетка размещена! Тапай на юнита и гексы.");

            // Вибрация
            if (SystemInfo.supportsVibration)
                Handheld.Vibrate();
        }
    }

    void GenerateHexGrid()
    {
        hexMap.Clear();

        for (int q = -gridRadius; q <= gridRadius; q++)
        {
            for (int r = -gridRadius; r <= gridRadius; r++)
            {
                int s = -q - r;

                // Проверяем, находится ли гекс в пределах
                if (Mathf.Abs(q) + Mathf.Abs(r) + Mathf.Abs(s) <= gridRadius * 2)
                {
                    // Вычисляем позицию
                    Vector3 hexPosition = HexToWorld(q, r);

                    // Создаем гекс
                    GameObject hex = Instantiate(hexPrefab, currentGrid.transform);
                    hex.transform.localPosition = hexPosition;
                    hex.name = $"Hex_{q}_{r}";

                    // Добавляем компонент HexInfo
                    HexInfo hexInfo = hex.GetComponent<HexInfo>();
                    if (hexInfo == null)
                        hexInfo = hex.AddComponent<HexInfo>();

                    hexInfo.q = q;
                    hexInfo.r = r;
                    hexInfo.grid = this;

                    // Сохраняем
                    string key = $"{q},{r}";
                    hexMap[key] = hex;

                    // Назначаем материал
                    if (defaultMat != null)
                        hexInfo.SetMaterial(defaultMat);
                }
            }
        }
    }

    Vector3 HexToWorld(int q, int r)
    {
        float x = hexSize * (Mathf.Sqrt(3f) * q + Mathf.Sqrt(3f) / 2f * r);
        float z = hexSize * (3f / 2f * r);
        return new Vector3(x, 0, z);
    }

    void SelectHexOrUnit(Vector2 screenPos)
    {
        Ray ray = Camera.main.ScreenPointToRay(screenPos);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            GameObject hitObj = hit.collider.gameObject;

            // Проверяем, гекс ли это
            HexInfo hexInfo = hitObj.GetComponent<HexInfo>();
            if (hexInfo != null)
            {
                OnHexSelected(hexInfo);
                return;
            }

            // Проверяем, юнит ли это
            if (hitObj.CompareTag("Player"))
            {
                OnUnitSelected(hitObj);
            }
        }
    }

    // Вызывается при клике на гекс
    public void OnHexSelected(HexInfo hexInfo)
    {
        if (selectedUnit != null)
        {
            // Перемещаем юнита
            MoveUnitToHex(selectedUnit, hexInfo);
            ClearHighlights();
            selectedUnit = null;
        }
        else
        {
            // Просто подсвечиваем гекс
            ClearHighlights();
            hexInfo.SetMaterial(highlightedMat);
            Debug.Log($"Гекс ({hexInfo.q}, {hexInfo.r}) выбран");
        }
    }

    // Вызывается при клике на юнита
    public void OnUnitSelected(GameObject unit)
    {
        selectedUnit = unit;
        Debug.Log("🎯 Юнит выбран");
        ShowMovementRange(unit);
    }

    void ShowMovementRange(GameObject unit)
    {
        ClearHighlights();

        // Находим гекс под юнитом
        HexInfo unitHex = GetHexAtPosition(unit.transform.position);
        if (unitHex == null) return;

        int moveRange = 3; // Дистанция движения

        // Подсвечиваем доступные гексы
        foreach (var kvp in hexMap)
        {
            HexInfo hex = kvp.Value.GetComponent<HexInfo>();
            if (hex == null) continue;

            int distance = HexDistance(unitHex.q, unitHex.r, hex.q, hex.r);

            if (distance <= moveRange && distance > 0)
            {
                hex.SetMaterial(moveMat);
            }
        }

        // Подсвечиваем текущий гекс
        unitHex.SetMaterial(highlightedMat);
    }

    void MoveUnitToHex(GameObject unit, HexInfo targetHex)
    {
        Vector3 targetPos = targetHex.transform.position + Vector3.up * 0.2f;
        unit.transform.position = targetPos;
        Debug.Log($"🚀 Юнит перемещен на ({targetHex.q}, {targetHex.r})");
    }

    int HexDistance(int q1, int r1, int q2, int r2)
    {
        int s1 = -q1 - r1;
        int s2 = -q2 - r2;

        return (Mathf.Abs(q1 - q2) + Mathf.Abs(r1 - r2) + Mathf.Abs(s1 - s2)) / 2;
    }

    HexInfo GetHexAtPosition(Vector3 position)
    {
        foreach (var kvp in hexMap)
        {
            if (Vector3.Distance(kvp.Value.transform.position, position) < hexSize)
            {
                return kvp.Value.GetComponent<HexInfo>();
            }
        }
        return null;
    }

    void ClearHighlights()
    {
        foreach (var kvp in hexMap)
        {
            HexInfo hex = kvp.Value.GetComponent<HexInfo>();
            if (hex != null && defaultMat != null)
            {
                hex.SetMaterial(defaultMat);
            }
        }
    }

    void CreatePlayerUnit()
    {
        // Создаем простой куб как юнита
        GameObject player = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        player.transform.localScale = new Vector3(0.2f, 0.1f, 0.2f);
        player.name = "Player";
        player.tag = "Player";

        // Красим
        Renderer rend = player.GetComponent<Renderer>();
        rend.material.color = Color.blue;

        // Ставим на центральный гекс
        string centerKey = "0,0";
        if (hexMap.ContainsKey(centerKey))
        {
            GameObject centerHex = hexMap[centerKey];
            player.transform.position = centerHex.transform.position + Vector3.up * 0.3f;
        }
        else
        {
            player.transform.position = currentGrid.transform.position + Vector3.up * 0.3f;
        }

        // Добавляем физику для кликов
        if (player.GetComponent<Collider>() == null)
            player.AddComponent<BoxCollider>();
    }
}