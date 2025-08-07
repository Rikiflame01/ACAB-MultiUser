using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public class HierarchyScriptAnalyzer : EditorWindow
{
    private Vector2 scrollPosition;
    private bool includeInactiveObjects = true;
    private bool sortByObjectName = false;
    private bool showObjectPath = true;
    private bool showScriptCount = true;
    private string searchFilter = "";
    private List<ObjectScriptInfo> cachedResults;
    
    [System.Serializable]
    public class ObjectScriptInfo
    {
        public GameObject gameObject;
        public string objectName;
        public string objectPath;
        public List<string> scriptNames;
        public int scriptCount;
        public bool isActive;
    }

    [MenuItem("Tools/Hierarchy Script Analyzer")]
    public static void ShowWindow()
    {
        GetWindow<HierarchyScriptAnalyzer>("Hierarchy Script Analyzer");
    }

    void OnGUI()
    {
        EditorGUILayout.BeginVertical("box");
        
        // Header
        EditorGUILayout.LabelField("Hierarchy Script Analyzer", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        
        // Options
        EditorGUILayout.BeginHorizontal();
        includeInactiveObjects = EditorGUILayout.Toggle("Include Inactive Objects", includeInactiveObjects);
        sortByObjectName = EditorGUILayout.Toggle("Sort by Object Name", sortByObjectName);
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        showObjectPath = EditorGUILayout.Toggle("Show Object Path", showObjectPath);
        showScriptCount = EditorGUILayout.Toggle("Show Script Count", showScriptCount);
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space();
        
        // Search filter
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Search Filter:", GUILayout.Width(80));
        searchFilter = EditorGUILayout.TextField(searchFilter);
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space();
        
        // Buttons
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Analyze Current Scene", GUILayout.Height(30)))
        {
            AnalyzeCurrentScene();
        }
        
        if (GUILayout.Button("Export to Console", GUILayout.Height(30)))
        {
            ExportToConsole();
        }
        
        if (GUILayout.Button("Copy to Clipboard", GUILayout.Height(30)))
        {
            CopyToClipboard();
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.EndVertical();
        
        // Results
        if (cachedResults != null && cachedResults.Count > 0)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Found {cachedResults.Count} objects with scripts", EditorStyles.boldLabel);
            
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            var filteredResults = cachedResults;
            if (!string.IsNullOrEmpty(searchFilter))
            {
                filteredResults = cachedResults.Where(obj => 
                    obj.objectName.ToLower().Contains(searchFilter.ToLower()) ||
                    obj.scriptNames.Any(script => script.ToLower().Contains(searchFilter.ToLower()))
                ).ToList();
            }
            
            foreach (var objInfo in filteredResults)
            {
                DrawObjectInfo(objInfo);
            }
            
            EditorGUILayout.EndScrollView();
        }
    }
    
    void DrawObjectInfo(ObjectScriptInfo objInfo)
    {
        EditorGUILayout.BeginVertical("box");
        
        // Object header
        EditorGUILayout.BeginHorizontal();
        
        // Object selection button
        if (GUILayout.Button("→", GUILayout.Width(25)))
        {
            Selection.activeGameObject = objInfo.gameObject;
            EditorGUIUtility.PingObject(objInfo.gameObject);
        }
        
        // Object name and info
        var objectLabel = objInfo.objectName;
        if (showScriptCount)
            objectLabel += $" ({objInfo.scriptCount} script{(objInfo.scriptCount != 1 ? "s" : "")})";
        
        if (!objInfo.isActive)
            objectLabel += " [INACTIVE]";
            
        EditorGUILayout.LabelField(objectLabel, EditorStyles.boldLabel);
        EditorGUILayout.EndHorizontal();
        
        // Object path
        if (showObjectPath && !string.IsNullOrEmpty(objInfo.objectPath))
        {
            EditorGUILayout.LabelField($"Path: {objInfo.objectPath}", EditorStyles.miniLabel);
        }
        
        // Scripts
        EditorGUILayout.BeginVertical();
        foreach (var scriptName in objInfo.scriptNames)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("  • " + scriptName, EditorStyles.label);
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();
    }
    
    void AnalyzeCurrentScene()
    {
        cachedResults = new List<ObjectScriptInfo>();
        
        // Get all GameObjects in the scene
        GameObject[] allObjects = includeInactiveObjects ? 
            FindObjectsOfType<GameObject>(true) : 
            FindObjectsOfType<GameObject>();
        
        foreach (GameObject obj in allObjects)
        {
            // Get all MonoBehaviour components
            MonoBehaviour[] scripts = obj.GetComponents<MonoBehaviour>();
            
            if (scripts.Length > 0)
            {
                var objInfo = new ObjectScriptInfo
                {
                    gameObject = obj,
                    objectName = obj.name,
                    objectPath = GetGameObjectPath(obj),
                    scriptNames = new List<string>(),
                    isActive = obj.activeInHierarchy
                };
                
                foreach (MonoBehaviour script in scripts)
                {
                    if (script != null)
                    {
                        objInfo.scriptNames.Add(script.GetType().Name);
                    }
                    else
                    {
                        objInfo.scriptNames.Add("[Missing Script]");
                    }
                }
                
                objInfo.scriptCount = objInfo.scriptNames.Count;
                cachedResults.Add(objInfo);
            }
        }
        
        // Sort results
        if (sortByObjectName)
        {
            cachedResults = cachedResults.OrderBy(obj => obj.objectName).ToList();
        }
        else
        {
            cachedResults = cachedResults.OrderBy(obj => obj.objectPath).ToList();
        }
        
        Debug.Log($"Analysis complete! Found {cachedResults.Count} objects with scripts in the current scene.");
    }
    
    string GetGameObjectPath(GameObject obj)
    {
        string path = obj.name;
        Transform current = obj.transform.parent;
        
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }
        
        return path;
    }
    
    void ExportToConsole()
    {
        if (cachedResults == null || cachedResults.Count == 0)
        {
            Debug.LogWarning("No analysis results to export. Please run 'Analyze Current Scene' first.");
            return;
        }
        
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== HIERARCHY SCRIPT ANALYSIS ===");
        sb.AppendLine($"Scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
        sb.AppendLine($"Total objects with scripts: {cachedResults.Count}");
        sb.AppendLine($"Analysis date: {System.DateTime.Now}");
        sb.AppendLine();
        
        foreach (var objInfo in cachedResults)
        {
            sb.AppendLine($"GameObject: {objInfo.objectName}");
            if (showObjectPath)
                sb.AppendLine($"  Path: {objInfo.objectPath}");
            sb.AppendLine($"  Active: {objInfo.isActive}");
            sb.AppendLine($"  Scripts ({objInfo.scriptCount}):");
            
            foreach (var scriptName in objInfo.scriptNames)
            {
                sb.AppendLine($"    • {scriptName}");
            }
            sb.AppendLine();
        }
        
        Debug.Log(sb.ToString());
    }
    
    void CopyToClipboard()
    {
        if (cachedResults == null || cachedResults.Count == 0)
        {
            Debug.LogWarning("No analysis results to copy. Please run 'Analyze Current Scene' first.");
            return;
        }
        
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("HIERARCHY SCRIPT ANALYSIS");
        sb.AppendLine($"Scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
        sb.AppendLine($"Total objects: {cachedResults.Count}");
        sb.AppendLine();
        
        foreach (var objInfo in cachedResults)
        {
            sb.AppendLine($"{objInfo.objectName} ({objInfo.objectPath}):");
            foreach (var scriptName in objInfo.scriptNames)
            {
                sb.AppendLine($"  • {scriptName}");
            }
            sb.AppendLine();
        }
        
        EditorGUIUtility.systemCopyBuffer = sb.ToString();
        Debug.Log("Analysis results copied to clipboard!");
    }
    
    void OnInspectorUpdate()
    {
        // Refresh the window periodically
        Repaint();
    }
}
