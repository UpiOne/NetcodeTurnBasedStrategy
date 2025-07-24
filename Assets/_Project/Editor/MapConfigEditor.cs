using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(MapConfig))]
public class MapConfigEditor : Editor
{
    private MapConfig _mapConfig;
    private EditMode _currentMode = EditMode.None;
    private Vector2Int _dragStartPos;
    private Vector2 _scrollPosition;
    private bool _showGrid = true;
    private bool _showCoordinates = false;
    private float _mapScale = 1f;
    
    private readonly Color OBSTACLE_COLOR = new Color(1f, 0.6f, 0f, 0.7f);
    private readonly Color PLAYER1_COLOR = new Color(0.2f, 0.8f, 0.2f, 0.7f);
    private readonly Color PLAYER2_COLOR = new Color(0.8f, 0.2f, 0.2f, 0.7f);
    private readonly Color GRID_COLOR = new Color(0.5f, 0.5f, 0.5f, 0.3f);
    private readonly Color BACKGROUND_COLOR = new Color(0.15f, 0.15f, 0.15f, 1f);
    private readonly Color HOVER_COLOR = new Color(1f, 1f, 1f, 0.3f);

    private enum EditMode 
    { 
        None, 
        Obstacles, 
        Player1Spawn, 
        Player2Spawn,
        Erase
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        _mapConfig = (MapConfig)target;
        
        ValidateMapConfig();
        
        DrawBasicSettings();
        
        DrawStartingArmies();
        
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Interactive Map Editor", EditorStyles.boldLabel);
        
        DrawToolbar();
        
        DrawInteractiveMap();
        
        DrawInfoPanel();

        serializedObject.ApplyModifiedProperties();
    }

    private void ValidateMapConfig()
    {
        if (_mapConfig.gridWidth <= 0) _mapConfig.gridWidth = 10;
        if (_mapConfig.gridHeight <= 0) _mapConfig.gridHeight = 10;
        
        if (_mapConfig.playerSpawns == null || _mapConfig.playerSpawns.Count < 2)
        {
            _mapConfig.playerSpawns = new List<PlayerSpawnConfig>
            {
                new PlayerSpawnConfig { playerId = 1, spawnZone = new RectInt(0, 0, 3, 3) },
                new PlayerSpawnConfig { playerId = 2, spawnZone = new RectInt(_mapConfig.gridWidth - 3, _mapConfig.gridHeight - 3, 3, 3) }
            };
        }
    }

    private void DrawBasicSettings()
    {
        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.LabelField("Grid Settings", EditorStyles.boldLabel);
        
        EditorGUI.BeginChangeCheck();
        int newWidth = EditorGUILayout.IntSlider("Grid Width", _mapConfig.gridWidth, 5, 50);
        int newHeight = EditorGUILayout.IntSlider("Grid Height", _mapConfig.gridHeight, 5, 50);
        
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(_mapConfig, "Change Grid Size");
            _mapConfig.gridWidth = newWidth;
            _mapConfig.gridHeight = newHeight;
            EditorUtility.SetDirty(_mapConfig);
        }
        
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.Space(5);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("obstacleTypes"));
    }

    private void DrawStartingArmies()
    {
        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.LabelField("Starting Armies", EditorStyles.boldLabel);
        var playerSpawnsProp = serializedObject.FindProperty("playerSpawns");
        for (int i = 0; i < playerSpawnsProp.arraySize; i++)
        {
            var spawnProp = playerSpawnsProp.GetArrayElementAtIndex(i);
            var playerIdProp = spawnProp.FindPropertyRelative("playerId");
            var startingArmyProp = spawnProp.FindPropertyRelative("startingArmy");
            EditorGUILayout.LabelField($"Player {playerIdProp.intValue}", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(startingArmyProp, new GUIContent("Starting Army"), true);
        }
        EditorGUILayout.EndVertical();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginVertical(GUI.skin.box);
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Tools", EditorStyles.boldLabel, GUILayout.Width(60));
        
        if (DrawCompactModeButton("", EditMode.Obstacles, OBSTACLE_COLOR))
            _currentMode = EditMode.Obstacles;
            
        if (DrawCompactModeButton("", EditMode.Player1Spawn, PLAYER1_COLOR))
            _currentMode = EditMode.Player1Spawn;
            
        if (DrawCompactModeButton("", EditMode.Player2Spawn, PLAYER2_COLOR))
            _currentMode = EditMode.Player2Spawn;
            
        if (DrawCompactModeButton("", EditMode.Erase, Color.white))
            _currentMode = EditMode.Erase;
        
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        
        if (_currentMode != EditMode.None)
        {
            string modeText = GetShortModeDescription(_currentMode);
            EditorGUILayout.HelpBox(modeText, MessageType.None);
        }
        
        EditorGUILayout.EndVertical();
    }

    private bool DrawCompactModeButton(string icon, EditMode mode, Color color)
    {
        Color originalColor = GUI.backgroundColor;
        if (_currentMode == mode)
        {
            GUI.backgroundColor = color;
        }
        
        bool clicked = GUILayout.Button(icon, GUILayout.Width(30), GUILayout.Height(25));
        GUI.backgroundColor = originalColor;
        
        return clicked;
    }

    private string GetShortModeDescription(EditMode mode)
    {
        switch (mode)
        {
            case EditMode.Obstacles: return "Draw obstacle zones (orange)";
            case EditMode.Player1Spawn: return "Draw Player 1 spawn zone";
            case EditMode.Player2Spawn: return "Draw Player 2 spawn zone";
            case EditMode.Erase: return " Erase zones";
            default: return "";
        }
    }
    

    private void DrawInteractiveMap()
    {
        EditorGUILayout.BeginVertical(GUI.skin.box);
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Map", EditorStyles.boldLabel, GUILayout.Width(60));
        GUILayout.FlexibleSpace();
        _showGrid = GUILayout.Toggle(_showGrid, "Grid", "Button", GUILayout.Width(40));
        _mapScale = GUILayout.HorizontalSlider(_mapScale, 0.3f, 1.2f, GUILayout.Width(60));
        EditorGUILayout.EndHorizontal();
        
        float inspectorWidth = EditorGUIUtility.currentViewWidth - 40;
        float maxMapSize = Mathf.Min(inspectorWidth * 0.9f, 280f);
        float mapSize = maxMapSize * _mapScale;
        
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        
        Rect mapRect = GUILayoutUtility.GetRect(mapSize, mapSize);
        float cellSize = mapSize / Mathf.Max(_mapConfig.gridWidth, _mapConfig.gridHeight);
        
        DrawMapBackground(mapRect);
        
        if (_showGrid && cellSize > 3f)
            DrawGridLines(mapRect, cellSize);
            
        DrawZones(mapRect, cellSize);
        
        if (_showCoordinates && cellSize > 12f)
            DrawCoordinates(mapRect, cellSize);
            
        HandleMouseInput(mapRect, cellSize);
        
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.EndVertical();
    }

    private void DrawMapBackground(Rect mapRect)
    {
        EditorGUI.DrawRect(mapRect, BACKGROUND_COLOR);
        
        Rect border = new Rect(mapRect.x - 1, mapRect.y - 1, mapRect.width + 2, mapRect.height + 2);
        EditorGUI.DrawRect(border, Color.white);
    }

    private void DrawGridLines(Rect mapRect, float cellSize)
    {
        for (int x = 0; x <= _mapConfig.gridWidth; x++)
        {
            Rect line = new Rect(mapRect.x + x * cellSize, mapRect.y, 1, mapRect.height);
            EditorGUI.DrawRect(line, GRID_COLOR);
        }
        
        for (int y = 0; y <= _mapConfig.gridHeight; y++)
        {
            Rect line = new Rect(mapRect.x, mapRect.y + y * cellSize, mapRect.width, 1);
            EditorGUI.DrawRect(line, GRID_COLOR);
        }
    }

    private void DrawZones(Rect mapRect, float cellSize)
    {
        DrawZoneOnMap(mapRect, _mapConfig.obstacleZone, OBSTACLE_COLOR, cellSize);
        
        if (_mapConfig.playerSpawns.Count > 0)
            DrawZoneOnMap(mapRect, _mapConfig.playerSpawns[0].spawnZone, PLAYER1_COLOR, cellSize);
        if (_mapConfig.playerSpawns.Count > 1)
            DrawZoneOnMap(mapRect, _mapConfig.playerSpawns[1].spawnZone, PLAYER2_COLOR, cellSize);
    }

    private void DrawZoneOnMap(Rect mapRect, RectInt zone, Color color, float cellSize)
    {
        if (zone.width <= 0 || zone.height <= 0) return;
        
        Rect displayRect = new Rect(
            mapRect.x + zone.x * cellSize,
            mapRect.y + (_mapConfig.gridHeight - zone.yMax) * cellSize,
            zone.width * cellSize,
            zone.height * cellSize
        );
        
        EditorGUI.DrawRect(displayRect, color);
        
        Color borderColor = color;
        borderColor.a = 1f;
        DrawRectBorder(displayRect, borderColor, 2f);
    }

    private void DrawRectBorder(Rect rect, Color color, float thickness)
    {
        // Верх
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
        // Низ
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
        // Лево
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
        // Право
        EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
    }

    private void DrawCoordinates(Rect mapRect, float cellSize)
    {
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = Mathf.Max(8, (int)(cellSize * 0.3f));
        labelStyle.alignment = TextAnchor.MiddleCenter;
        labelStyle.normal.textColor = Color.white;

        for (int x = 0; x < _mapConfig.gridWidth; x++)
        {
            for (int y = 0; y < _mapConfig.gridHeight; y++)
            {
                Rect cellRect = new Rect(
                    mapRect.x + x * cellSize,
                    mapRect.y + (_mapConfig.gridHeight - 1 - y) * cellSize,
                    cellSize,
                    cellSize
                );
                
                GUI.Label(cellRect, $"{x},{y}", labelStyle);
            }
        }
    }

    private void HandleMouseInput(Rect mapRect, float cellSize)
    {
        if (_currentMode == EditMode.None) return;

        Event currentEvent = Event.current;
        Vector2 mousePos = currentEvent.mousePosition;

        if (!mapRect.Contains(mousePos)) return;
        
        int x = Mathf.FloorToInt((mousePos.x - mapRect.x) / cellSize);
        int y = _mapConfig.gridHeight - 1 - Mathf.FloorToInt((mousePos.y - mapRect.y) / cellSize);
        
        if (x < 0 || x >= _mapConfig.gridWidth || y < 0 || y >= _mapConfig.gridHeight) return;
        
        Vector2Int gridPos = new Vector2Int(x, y);
        
        if (currentEvent.type == EventType.MouseMove)
        {
            Rect hoverRect = new Rect(
                mapRect.x + x * cellSize + 1,
                mapRect.y + (_mapConfig.gridHeight - 1 - y) * cellSize + 1,
                cellSize - 2,
                cellSize - 2
            );
            EditorGUI.DrawRect(hoverRect, HOVER_COLOR);
            Repaint();
        }
        
        if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0)
        {
            _dragStartPos = gridPos;
            currentEvent.Use();
        }
        else if (currentEvent.type == EventType.MouseDrag && currentEvent.button == 0)
        {
            UpdateZone(gridPos);
            currentEvent.Use();
        }
        else if (currentEvent.type == EventType.MouseUp && currentEvent.button == 0)
        {
            UpdateZone(gridPos);
            _currentMode = EditMode.None;
            currentEvent.Use();
        }
        
        Repaint();
    }

    private void UpdateZone(Vector2Int dragEndPos)
    {
        Undo.RecordObject(_mapConfig, "Edit Map Zone");
        
        int xMin = Mathf.Min(_dragStartPos.x, dragEndPos.x);
        int yMin = Mathf.Min(_dragStartPos.y, dragEndPos.y);
        int xMax = Mathf.Max(_dragStartPos.x, dragEndPos.x);
        int yMax = Mathf.Max(_dragStartPos.y, dragEndPos.y);
        
        RectInt newZone = new RectInt(xMin, yMin, xMax - xMin + 1, yMax - yMin + 1);
        
        switch (_currentMode)
        {
            case EditMode.Obstacles:
                _mapConfig.obstacleZone = newZone;
                break;
            case EditMode.Player1Spawn:
                _mapConfig.playerSpawns[0].spawnZone = newZone;
                break;
            case EditMode.Player2Spawn:
                _mapConfig.playerSpawns[1].spawnZone = newZone;
                break;
            case EditMode.Erase:
                EraseZonesInArea(newZone);
                break;
        }
        
        EditorUtility.SetDirty(_mapConfig);
    }

    private void EraseZonesInArea(RectInt eraseArea)
    {
        if (_mapConfig.obstacleZone.Overlaps(eraseArea))
        {
            _mapConfig.obstacleZone = SubtractRects(_mapConfig.obstacleZone, eraseArea);
        }
        
        for (int i = 0; i < _mapConfig.playerSpawns.Count; i++)
        {
            if (_mapConfig.playerSpawns[i].spawnZone.Overlaps(eraseArea))
            {
                _mapConfig.playerSpawns[i].spawnZone = SubtractRects(_mapConfig.playerSpawns[i].spawnZone, eraseArea);
            }
        }
    }

    private RectInt SubtractRects(RectInt original, RectInt subtract)
    {
        if (original.Overlaps(subtract))
        {
            return new RectInt(0, 0, 0, 0);
        }
        return original;
    }

    private void DrawInfoPanel()
    {
        EditorGUILayout.BeginVertical(GUI.skin.box);
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Zones:", EditorStyles.boldLabel, GUILayout.Width(60));
        EditorGUILayout.LabelField($"{GetZoneSize(_mapConfig.obstacleZone)}", GUILayout.Width(50));
        if (_mapConfig.playerSpawns.Count > 0)
            EditorGUILayout.LabelField($"{GetZoneSize(_mapConfig.playerSpawns[0].spawnZone)}", GUILayout.Width(50));
        if (_mapConfig.playerSpawns.Count > 1)
            EditorGUILayout.LabelField($"{GetZoneSize(_mapConfig.playerSpawns[1].spawnZone)}", GUILayout.Width(50));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Clear", GUILayout.Height(20)))
        {
            if (EditorUtility.DisplayDialog("Clear All", "Clear all zones?", "Yes", "No"))
                ClearAllZones();
        }
        if (GUILayout.Button("Random", GUILayout.Height(20)))
        {
            GenerateRandomZones();
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.EndVertical();
    }

    private string GetZoneSize(RectInt zone)
    {
        if (zone.width <= 0 || zone.height <= 0) return "0";
        return (zone.width * zone.height).ToString();
    }

    private string FormatZoneInfo(RectInt zone)
    {
        if (zone.width <= 0 || zone.height <= 0)
            return "Not set";
        return $"({zone.x}, {zone.y}) - {zone.width}x{zone.height} ({zone.width * zone.height} cells)";
    }

    private void ClearAllZones()
    {
        Undo.RecordObject(_mapConfig, "Clear All Zones");
        
        _mapConfig.obstacleZone = new RectInt(0, 0, 0, 0);
        for (int i = 0; i < _mapConfig.playerSpawns.Count; i++)
        {
            _mapConfig.playerSpawns[i].spawnZone = new RectInt(0, 0, 0, 0);
        }
        
        EditorUtility.SetDirty(_mapConfig);
    }

    private void GenerateRandomZones()
    {
        Undo.RecordObject(_mapConfig, "Generate Random Zones");
        
        int w = _mapConfig.gridWidth;
        int h = _mapConfig.gridHeight;
        
        int obsW = w / 3;
        int obsH = h / 3;
        _mapConfig.obstacleZone = new RectInt(w/2 - obsW/2, h/2 - obsH/2, obsW, obsH);
        
        int spawnSize = Mathf.Max(3, Mathf.Min(w, h) / 5);
        _mapConfig.playerSpawns[0].spawnZone = new RectInt(1, 1, spawnSize, spawnSize);
        _mapConfig.playerSpawns[1].spawnZone = new RectInt(w - spawnSize - 1, h - spawnSize - 1, spawnSize, spawnSize);
        
        EditorUtility.SetDirty(_mapConfig);
    }
}