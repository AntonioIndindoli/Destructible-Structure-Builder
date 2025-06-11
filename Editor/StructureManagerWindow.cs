using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace Mayuns.DSB.Editor
{
    public class ManagerWindow : EditorWindow
    {
        [MenuItem("Tools/Structure Manager")]
        public static void ShowWindow()
        {
            GetWindow<ManagerWindow>("Structure Manager");
        }

        public static ManagerWindow instance;
        private StructureBuildSettings buildSettings;
        private Vector2 scrollPosition;
        private bool wallSelected => StructureBuildTool.SelectedWall != null;
        private StructureBuildTool.WallEditSubMode selectedWallEditSubMode => StructureBuildTool.currentWallEditSubMode;
        private string wallDesignName = "";
        const string kPrefsKey = "SB_WALL_DESIGN_FOLDER_GUID";
        private string _lastDesignPath;

        private void OnEnable()
        {
            instance = this;
            // First try to reuse the last asset the user picked this session
            if (buildSettings == null && SessionState.GetInt("SBS_GUID_SET", 0) == 1)
            {
                string guid = SessionState.GetString("SBS_GUID", string.Empty);
                if (!string.IsNullOrEmpty(guid))
                    buildSettings = AssetDatabase.LoadAssetAtPath<StructureBuildSettings>(
                        AssetDatabase.GUIDToAssetPath(guid));
            }

            // If still null, search the project
            if (buildSettings == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:StructureBuildSettings", null);
                if (guids.Length > 0)
                    buildSettings = AssetDatabase.LoadAssetAtPath<StructureBuildSettings>(
                        AssetDatabase.GUIDToAssetPath(guids[0]));
            }

            // If none found, offer to create one at a user‑chosen location
            if (buildSettings == null)
            {
                if (EditorUtility.DisplayDialog("Create Build Settings",
                    "No StructureBuildSettings asset found. Create one now?", "Create", "Later"))
                {
                    string path = EditorUtility.SaveFilePanelInProject(
                        "Save Build Settings", "StructureBuildSettings", "asset",
                        "Choose location for the new StructureBuildSettings asset");
                    if (!string.IsNullOrEmpty(path))
                    {
                        buildSettings = ScriptableObject.CreateInstance<StructureBuildSettings>();
                        AssetDatabase.CreateAsset(buildSettings, path);
                        AssetDatabase.SaveAssets();
                    }
                }
            }

            // Cache the result for the session so re‑opening the window is instant
            if (buildSettings != null)
            {
                SessionState.SetInt("SBS_GUID_SET", 1);
                SessionState.SetString("SBS_GUID", AssetDatabase.AssetPathToGUID(
                    AssetDatabase.GetAssetPath(buildSettings)));
            }

            _ = GetDesignFolderPathCached(); // Validate and auto-clean invalid folder key
        }

        private void OnDisable()
        {
            instance = null;
        }

        string GetDesignFolderPathCached()
        {
            string guid = EditorPrefs.GetString(kPrefsKey, string.Empty);
            if (string.IsNullOrEmpty(guid))
                return null;

            string path = AssetDatabase.GUIDToAssetPath(guid);

            if (!AssetDatabase.IsValidFolder(path))
            {
                Debug.LogWarning($"Design folder at '{path}' is missing. Clearing cached folder reference.");
                EditorPrefs.DeleteKey(kPrefsKey);
                return null;
            }

            return path;
        }

        void PromptForDesignFolder()
        {
            string initial = GetDesignFolderPathCached() ?? Application.dataPath;
            string abs = EditorUtility.OpenFolderPanel("Choose wall‑design folder", initial, "");
            if (string.IsNullOrEmpty(abs)) return; // user cancelled

            if (!abs.StartsWith(Application.dataPath))
            {
                EditorUtility.DisplayDialog("Invalid Location",
                    "Folder must be inside this project’s Assets folder.", "OK");
                return;
            }

            string rel = "Assets" + abs.Substring(Application.dataPath.Length);
            rel = rel.Replace("\\", "/"); // Normalize Windows slashes

            if (!AssetDatabase.IsValidFolder(rel))
            {
                // Ensure parent exists first
                string parent = System.IO.Path.GetDirectoryName(rel);
                string newFolder = System.IO.Path.GetFileName(rel);

                if (!AssetDatabase.IsValidFolder(parent))
                {
                    EditorUtility.DisplayDialog("Missing Parent Folder",
                        $"The parent folder '{parent}' does not exist. Create it first.", "OK");
                    return;
                }

                AssetDatabase.CreateFolder(parent, newFolder);
                AssetDatabase.Refresh();
            }

            string guid = AssetDatabase.AssetPathToGUID(rel);
            if (string.IsNullOrEmpty(guid))
            {
                Debug.LogError($"Failed to get GUID for path: {rel}");
                return;
            }

            EditorPrefs.SetString(kPrefsKey, guid);
            Debug.Log($"Wall design folder set to: {rel}");
        }

        public static void RepaintIfOpen()
        {
            if (instance != null)
                instance.Repaint();
        }

        private void OnGUI()
        {
            DrawModeSelection();

            EditorGUILayout.Space(10);

            DrawBuildSettings();
        }

        private void DrawModeSelection()
        {
            GUILayout.Label("Build Mode Selection", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal("box");
            var newBuildMode = (StructureBuildTool.BuildMode)EditorGUILayout.EnumPopup("Select Build Mode:", StructureBuildTool.currentMode);

            // Add some spacing or alignment if needed here

            if (newBuildMode != StructureBuildTool.currentMode)
            {
                StructureBuildTool.currentMode = newBuildMode;
                StructureBuildTool.SetBuildMode(StructureBuildTool.currentMode);
            }

            // Apply fixed width to the button using GUILayout.Width
            if (GUILayout.Button("Disable Mode", GUILayout.Width(100)))
            {
                StructureBuildTool.currentMode = StructureBuildTool.BuildMode.None;
                StructureBuildTool.SetBuildMode(StructureBuildTool.currentMode);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginVertical("box");
            // Show additional UI
            if (StructureBuildTool.currentMode == StructureBuildTool.BuildMode.StructuralMemberBuild)
            {
                DrawStructuralMemberSettings();
            }
            else if (StructureBuildTool.currentMode == StructureBuildTool.BuildMode.CreateStructure)
            {
                DrawStructureSettings();
            }
            else if (StructureBuildTool.currentMode == StructureBuildTool.BuildMode.WallBuild)
            {
                DrawWallBuildSettings();
            }
            else if (StructureBuildTool.currentMode == StructureBuildTool.BuildMode.WallEdit)
            {
                DrawWallEditMode();
            }
            else if (StructureBuildTool.currentMode == StructureBuildTool.BuildMode.ApplyDesign)
            {
                EditorGUILayout.Space(10);
                GUILayout.Label("Apply Design Mode", EditorStyles.boldLabel);

                StructureBuildTool.selectedDesignToApply = (WallDesign)EditorGUILayout.ObjectField(
                    "Design to Apply", StructureBuildTool.selectedDesignToApply, typeof(WallDesign), false);

                if (StructureBuildTool.selectedDesignToApply == null)
                {
                    EditorGUILayout.HelpBox("Please select a WallDesign asset to apply.", MessageType.Info);
                }
            }
            else if (StructureBuildTool.currentMode == StructureBuildTool.BuildMode.ApplyMaterial)
            {
                EditorGUILayout.Space(10);
                GUILayout.Label("Apply Material Mode", EditorStyles.boldLabel);

                StructureBuildTool.selectedMaterialToApply = (Material)EditorGUILayout.ObjectField(
                    "Material to Apply", StructureBuildTool.selectedMaterialToApply, typeof(Material), false);

                buildSettings.memberTextureScaleX = EditorGUILayout.FloatField("Member Texture Scale X", buildSettings.memberTextureScaleX);
                buildSettings.memberTextureScaleY = EditorGUILayout.FloatField("Member Texture Scale Y", buildSettings.memberTextureScaleY);
                buildSettings.wallTextureScaleX = EditorGUILayout.FloatField("Wall Texture Scale X", buildSettings.wallTextureScaleX);
                buildSettings.wallTextureScaleY = EditorGUILayout.FloatField("Wall Texture Scale Y", buildSettings.wallTextureScaleY);

                if (StructureBuildTool.selectedMaterialToApply == null)
                {
                    EditorGUILayout.HelpBox("Please select a Material to apply.", MessageType.Info);
                }
            }
            else if (StructureBuildTool.currentMode == StructureBuildTool.BuildMode.Delete)
            {
                EditorGUILayout.HelpBox("Click on a wall or structural member in the scene to delete it.", MessageType.Warning);
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawWallBuildSettings()
        {
            GUILayout.Label("Wall Build Settings", EditorStyles.boldLabel);

            DrawWallSettings();

            EditorGUILayout.Space(6);

            // Default design for new walls
            buildSettings.defaultWallDesign = (WallDesign)EditorGUILayout.ObjectField(
                new GUIContent("Default Wall Design", "Design used when spawning new walls"),
                buildSettings.defaultWallDesign,
                typeof(WallDesign),
                false
            );

            if (buildSettings.defaultWallDesign == null)
            {
                EditorGUILayout.HelpBox("No default wall design selected.", MessageType.Info);
            }
        }

        private void DrawWallEditMode()
        {
            EditorGUILayout.Space(10);
            EditorGUI.BeginChangeCheck();

            if (!wallSelected)
            {
                GUILayout.Label("Wall Edit Mode - Wall Select", EditorStyles.boldLabel);

                EditorGUILayout.HelpBox("Select a wall to begin editing.", MessageType.Info);
            }
            else
            {
                GUILayout.Label("Wall Edit Mode - Editting Selected wall", EditorStyles.boldLabel);

                EditorGUILayout.BeginHorizontal();

                // Dropdown for wall edit modes
                var newWallMode = (StructureBuildTool.WallEditSubMode)EditorGUILayout.EnumPopup("Select Wall Edit Mode:", StructureBuildTool.currentWallEditSubMode);

                // Apply new mode if changed
                if (newWallMode != StructureBuildTool.currentWallEditSubMode)
                {
                    StructureBuildTool.currentWallEditSubMode = newWallMode;
                    StructureBuildTool.SetWallEditSubMode(newWallMode);
                }

                if (GUILayout.Button("Deselect Wall", GUILayout.Width(100)))
                {
                    StructureBuildTool.currentWallEditSubMode = StructureBuildTool.WallEditSubMode.None;
                    StructureBuildTool.SetWallEditSubMode(StructureBuildTool.WallEditSubMode.None);
                }

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(10);

                // ------------- SIZE -----------------
                int newCols = EditorGUILayout.IntField("Columns", StructureBuildTool.SelectedWall.numColumns);
                int newRows = EditorGUILayout.IntField("Rows", StructureBuildTool.SelectedWall.numRows);
                bool sizeChanged = newCols != StructureBuildTool.SelectedWall.numColumns
                                || newRows != StructureBuildTool.SelectedWall.numRows;

                // ------------- MATERIALS ------------
                var oldWallMat = buildSettings.wallMaterial;
                var oldGlassMat = buildSettings.glassMaterial;
                float oldScaleX = buildSettings.wallTextureScaleX;
                float oldScaleY = buildSettings.wallTextureScaleY;

                buildSettings.wallMaterial = (Material)EditorGUILayout.ObjectField("Wall Material", buildSettings.wallMaterial, typeof(Material), false);
                buildSettings.glassMaterial = (Material)EditorGUILayout.ObjectField("Wall Glass Material", buildSettings.glassMaterial, typeof(Material), false);
                buildSettings.wallTextureScaleX = EditorGUILayout.FloatField("Wall Texture Scale X", buildSettings.wallTextureScaleX);
                buildSettings.wallTextureScaleY = EditorGUILayout.FloatField("Wall Texture Scale Y", buildSettings.wallTextureScaleY);

                bool materialChanged = oldWallMat != buildSettings.wallMaterial || oldGlassMat != buildSettings.glassMaterial;
                bool scaleChanged = !Mathf.Approximately(oldScaleX, buildSettings.wallTextureScaleX) || !Mathf.Approximately(oldScaleY, buildSettings.wallTextureScaleY);

                // ------------------------------------
                // queue the heavy rebuild (outside GUI pass)
                if (sizeChanged || materialChanged || scaleChanged)
                {
                    // keep globals in sync
                    StructureBuildTool.buildSettings.wallMaterial = buildSettings.wallMaterial;
                    StructureBuildTool.buildSettings.glassMaterial = buildSettings.glassMaterial;
                    StructureBuildTool.buildSettings.wallTextureScaleX = buildSettings.wallTextureScaleX;
                    StructureBuildTool.buildSettings.wallTextureScaleY = buildSettings.wallTextureScaleY;

                    StructureBuildTool.RequestWallRefresh(
                        sizeChanged ? newCols : (int?)null,
                        sizeChanged ? newRows : (int?)null);
                }


                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Save Wall Design", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("Current Folder");
                string folderPath = GetDesignFolderPathCached();
                EditorGUILayout.LabelField(string.IsNullOrEmpty(folderPath) ? "(none set)" : folderPath, GUILayout.MaxHeight(EditorGUIUtility.singleLineHeight));
                if (GUILayout.Button("Select...", GUILayout.Width(100)))
                {
                    PromptForDesignFolder();  // safe to call on MouseUp event
                }


                if (GUILayout.Button("Reset Folder", GUILayout.Width(100)))
                {
                    EditorPrefs.DeleteKey(kPrefsKey);
                }
                EditorGUILayout.EndHorizontal();

                wallDesignName = EditorGUILayout.TextField("Design Name", wallDesignName);

                string validationMessage = GetDesignNameValidationMessage(wallDesignName);
                if (!string.IsNullOrEmpty(validationMessage))
                {
                    EditorGUILayout.HelpBox(validationMessage, MessageType.Warning);
                }

                using (new EditorGUI.DisabledScope(!CanSaveCurrentDesign()))
                {
                    if (GUILayout.Button("Save Design"))
                    {
                        SaveCurrentWallDesign(wallDesignName.Trim());
                        wallDesignName = "";
                    }
                }


            }
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(buildSettings, "Modify Build Settings");
                EditorUtility.SetDirty(buildSettings);

                if (StructureBuildTool.SelectedWall != null)
                    Undo.RecordObject(StructureBuildTool.SelectedWall, "Resize Wall");
            }
        }

        private string GetDesignNameValidationMessage(string designName)
        {
            designName = designName.Trim();
            if (string.IsNullOrEmpty(designName))
                return "Design name cannot be empty.";

            string folder = GetDesignFolderPathCached();
            if (string.IsNullOrEmpty(folder)) return "Wall design folder must be present!";

            string path = $"{folder}/{designName}.asset";
            if (System.IO.File.Exists(path))
                return $"A design named \"{designName}\" already exists.";

            return null;
        }

        private bool CanSaveCurrentDesign()
        {
            if (!wallSelected) return false;

            string folder = GetDesignFolderPathCached();
            if (string.IsNullOrEmpty(folder)) return false;

            string designName = wallDesignName.Trim();
            if (string.IsNullOrEmpty(designName)) return false;

            string path = $"{folder}/{designName}.asset";
            return !System.IO.File.Exists(path);
        }

        private void SaveCurrentWallDesign(string designName)
        {
            string folder = GetDesignFolderPathCached();
            if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder))
            {
                Debug.LogWarning("Wall design folder is invalid or missing. Prompting user to choose a new folder.");
                PromptForDesignFolder();  // re-prompt
                folder = GetDesignFolderPathCached();

                if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder))
                {
                    EditorUtility.DisplayDialog("Missing Folder",
                        "Wall design folder is not set or could not be restored. Aborting save.",
                        "OK");
                    return;
                }
            }

            WallManager wall = StructureBuildTool.SelectedWall;
            if (wall == null)
            {
                Debug.LogError("No wall selected – aborting save.");
                return;
            }

            WallDesign design = ScriptableObject.CreateInstance<WallDesign>();
            string path = $"{folder}/{designName}.asset";
            _lastDesignPath = path;
            AssetDatabase.CreateAsset(design, path);
            Undo.RegisterCreatedObjectUndo(design, "Create Wall Design");

            design.material = wall.wallMaterial;
            design.glassMaterial = wall.glassMaterial;
            design.columns = wall.numColumns;
            design.rows = wall.numRows;
            design.cells = new List<WallDesign.Cell>(design.columns * design.rows);

            for (int c = 0; c < wall.numColumns; ++c)
            {
                for (int r = 0; r < wall.numRows; ++r)
                {
                    int idx = c + r * wall.numColumns;
                    WallPiece piece = wall.wallGrid[idx];
                    var cell = new WallDesign.Cell();

                    if (piece == null)
                        cell.type = WallDesign.CellType.Empty;
                    else if (piece.isWindow)
                        cell.type = WallDesign.CellType.Window;
                    else if (piece.cornerDesignation != WallPiece.TriangularCornerDesignation.None)
                    {
                        switch (piece.cornerDesignation)
                        {
                            case WallPiece.TriangularCornerDesignation.BottomLeft: cell.type = WallDesign.CellType.TriangleBL; break;
                            case WallPiece.TriangularCornerDesignation.TopLeft: cell.type = WallDesign.CellType.TriangleTL; break;
                            case WallPiece.TriangularCornerDesignation.BottomRight: cell.type = WallDesign.CellType.TriangleBR; break;
                            case WallPiece.TriangularCornerDesignation.TopRight: cell.type = WallDesign.CellType.TriangleTR; break;
                        }
                    }
                    else
                        cell.type = WallDesign.CellType.Cube;

                    design.cells.Add(cell);
                }
            }

            EditorUtility.SetDirty(design);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            /* hook undo once */
            Undo.undoRedoPerformed -= OnUndoRedo;   // avoid duplicates
            Undo.undoRedoPerformed += OnUndoRedo;

            EditorUtility.DisplayDialog("Wall Design Saved",
                $"Saved '{designName}' to:\n{path}", "OK");
        }

        private void OnUndoRedo()
        {
            if (!string.IsNullOrEmpty(_lastDesignPath) &&
                !AssetDatabase.LoadAssetAtPath<WallDesign>(_lastDesignPath))
            {
                // Object gone from DB ⇒ user just undid the creation
                if (System.IO.File.Exists(_lastDesignPath))
                    AssetDatabase.DeleteAsset(_lastDesignPath);
            }
        }

        private void DrawBuildSettings()
        {
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            GUILayout.Label("Build Settings", EditorStyles.boldLabel);

            buildSettings = (StructureBuildSettings)EditorGUILayout.ObjectField(
                new GUIContent("Build Settings Asset", "Assign your StructureBuildSettings ScriptableObject asset."),
                buildSettings, typeof(StructureBuildSettings), false);

            if (buildSettings == null)
            {
                if (GUILayout.Button("Create BuildSettings Asset"))
                    CreateBuildSettingsAsset();

                EditorGUILayout.HelpBox("Please assign or create a StructureBuildSettings asset.", MessageType.Warning);
                return;
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.Space(15);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            DrawMeshCacheSettings();

            GUILayout.Label("Runtime Debris Settings", EditorStyles.boldLabel);
            DrawGibManagerSettings();

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(buildSettings, "Modify Build Settings");
                EditorUtility.SetDirty(buildSettings);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawGibManagerSettings()
        {
            GibManager gibManager = FindFirstObjectByType<GibManager>();

            if (gibManager == null)
            {
                EditorGUILayout.HelpBox("No GibManager found in the scene.", MessageType.Warning);

                if (GUILayout.Button("Create GibManager in Scene"))
                {
                    GameObject newGibManagerGO = new GameObject("GibManager");
                    gibManager = newGibManagerGO.AddComponent<GibManager>();
                    Undo.RegisterCreatedObjectUndo(newGibManagerGO, "Create GibManager");
                    Selection.activeObject = newGibManagerGO;
                }
                return;
            }
            float originalLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 200f;

            SerializedObject serializedGibManager = new SerializedObject(gibManager);
            serializedGibManager.Update();

            void DrawProperty(string propName, string label)
            {
                SerializedProperty prop = serializedGibManager.FindProperty(propName);
                if (prop != null)
                {
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(prop, new GUIContent(label));
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(gibManager, $"Modify {label}");
                        serializedGibManager.ApplyModifiedProperties();
                        EditorUtility.SetDirty(gibManager);
                    }
                }
            }

            DrawProperty("maxActiveGibs", "Max Active Gibs");
            DrawProperty("maxPoolSize", "Max Pool Size");

            EditorGUILayout.Space();
            DrawProperty("smallGibLifetime", "Small Gib Lifetime");
            DrawProperty("mediumGibLifetime", "Medium Gib Lifetime");
            DrawProperty("largeGibLifetime", "Large Gib Lifetime");

            EditorGUILayout.Space();
            DrawProperty("maxUncombinesPerWindow", "Max Uncombines Per Window");
            DrawProperty("uncombineWindowSeconds", "Uncombine Window Seconds");

            EditorGUILayout.Space();
            DrawProperty("hidePooledGibsInHierarchy", "Hide Pooled Gibs In Hierarchy");

            EditorGUIUtility.labelWidth = originalLabelWidth;
        }

        private void DrawStructuralMemberSettings()
        {
            GUILayout.Label("Structural Member Settings", EditorStyles.boldLabel);
            buildSettings.memberLength = EditorGUILayout.FloatField("Member Length", buildSettings.memberLength);
            buildSettings.memberThickness = EditorGUILayout.FloatField("Member Thickness", buildSettings.memberThickness);
            buildSettings.memberTextureScaleX = EditorGUILayout.FloatField("Member Texture Scale X", buildSettings.memberTextureScaleX);
            buildSettings.memberTextureScaleY = EditorGUILayout.FloatField("Member Texture Scale Y", buildSettings.memberTextureScaleY);
            buildSettings.memberSupportCapacity = EditorGUILayout.FloatField("Member Support Capacity", buildSettings.memberSupportCapacity);
            buildSettings.memberMaterial = (Material)EditorGUILayout.ObjectField("Member Material", buildSettings.memberMaterial, typeof(Material), false);
            buildSettings.disableDirection = (DisableDirection)EditorGUILayout.EnumPopup("Disable Direction", buildSettings.disableDirection);
            EditorGUILayout.Space(10);
            GUILayout.Label("Connection Node Settings", EditorStyles.boldLabel);
            buildSettings.connectionSize = EditorGUILayout.FloatField("Connection Size", buildSettings.connectionSize);
            buildSettings.connectionMaterial = (Material)EditorGUILayout.ObjectField("Connection Material", buildSettings.connectionMaterial, typeof(Material), false);
        }

        private void DrawWallSettings()
        {
            float originalLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 200f;

            buildSettings.wallThickness = EditorGUILayout.FloatField("Wall Thickness", buildSettings.wallThickness);
            buildSettings.wallHeight = EditorGUILayout.FloatField("Wall Height", buildSettings.wallHeight);
            buildSettings.wallWidth = EditorGUILayout.FloatField("Wall Width", buildSettings.wallWidth);
            buildSettings.isWallCentered = EditorGUILayout.Toggle("Is Centered", buildSettings.isWallCentered);
            buildSettings.matchMemberLength = EditorGUILayout.Toggle("Width Match Member Length", buildSettings.matchMemberLength);
            buildSettings.allowWallOverlap = EditorGUILayout.Toggle("Allow Overlap", buildSettings.allowWallOverlap);
            buildSettings.wallColumnCellCount = EditorGUILayout.IntField("Wall Column Cells", buildSettings.wallColumnCellCount);
            buildSettings.wallRowCellCount = EditorGUILayout.IntField("Wall Row Cells", buildSettings.wallRowCellCount);
            EditorGUILayout.Space(10);
            GUILayout.Label("Materials", EditorStyles.boldLabel);
            buildSettings.wallMaterial = (Material)EditorGUILayout.ObjectField("Wall Material", buildSettings.wallMaterial, typeof(Material), false);
            buildSettings.glassMaterial = (Material)EditorGUILayout.ObjectField("Wall Glass Material", buildSettings.glassMaterial, typeof(Material), false);
            buildSettings.wallTextureScaleX = EditorGUILayout.FloatField("Wall Texture Scale X", buildSettings.wallTextureScaleX);
            buildSettings.wallTextureScaleY = EditorGUILayout.FloatField("Wall Texture Scale Y", buildSettings.wallTextureScaleY);

            EditorGUIUtility.labelWidth = originalLabelWidth;
        }

        private void DrawStructureSettings()
        {
            float originalLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 200f;

            GUILayout.Label("Initial Connection Node Material", EditorStyles.boldLabel);
            buildSettings.connectionMaterial = (Material)EditorGUILayout.ObjectField("Connection Material", buildSettings.connectionMaterial, typeof(Material), false);
            EditorGUILayout.Space(10);
            GUILayout.Label("Structural Strength", EditorStyles.boldLabel);
            buildSettings.strengthModifier = EditorGUILayout.IntField("Member Support Capacity Modifier", buildSettings.strengthModifier);
            EditorGUILayout.Space(10);
            GUILayout.Label("Initial Piece Settings", EditorStyles.boldLabel);
            buildSettings.memberMass = EditorGUILayout.FloatField("Member Piece Mass", buildSettings.memberMass);
            buildSettings.memberPieceHealth = EditorGUILayout.FloatField("Member Piece Health", buildSettings.memberPieceHealth);
            buildSettings.wallPieceMass = EditorGUILayout.FloatField("Wall Piece Mass", buildSettings.wallPieceMass);
            buildSettings.wallPieceHealth = EditorGUILayout.FloatField("Wall Piece Health", buildSettings.wallPieceHealth);
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Stress Damage Propagation Timing", EditorStyles.boldLabel);

            // Display editable float fields for precise input
            EditorGUILayout.BeginHorizontal();
            buildSettings.minPropagationTime = EditorGUILayout.FloatField("Min", buildSettings.minPropagationTime);
            buildSettings.maxPropagationTime = EditorGUILayout.FloatField("Max", buildSettings.maxPropagationTime);
            EditorGUILayout.EndHorizontal();

            // Clamp to valid positive range
            buildSettings.minPropagationTime = Mathf.Max(0f, buildSettings.minPropagationTime);
            buildSettings.maxPropagationTime = Mathf.Max(buildSettings.minPropagationTime, buildSettings.maxPropagationTime); // ensure min <= max

            EditorGUILayout.MinMaxSlider(new GUIContent("Propagation Time Range"),
                ref buildSettings.minPropagationTime,
                ref buildSettings.maxPropagationTime,
                0f, 100f);
            EditorGUILayout.HelpBox($"Propagation will randomly occur between {buildSettings.minPropagationTime:0.00}s and {buildSettings.maxPropagationTime:0.00}s.", MessageType.Info);
            EditorGUIUtility.labelWidth = originalLabelWidth;
        }

        private void CreateBuildSettingsAsset()
        {
            // 1. Let the user pick a location *before* we allocate anything
            string path = EditorUtility.SaveFilePanelInProject(
                "Save Build Settings",
                "StructureBuildSettings",
                "asset",
                "Choose where to save the new StructureBuildSettings asset");

            if (string.IsNullOrEmpty(path)) return;          // User cancelled

            // 2. Create the asset only once, after we know it will be saved
            var settings = ScriptableObject.CreateInstance<StructureBuildSettings>();
            AssetDatabase.CreateAsset(settings, path);
            AssetDatabase.SaveAssets();

            // 3. Keep a live reference for the window
            buildSettings = settings;
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        // ──────────────────────────────────────────────────────────────────────────
        //  Mesh‑cache UI (toggle + folder picker)
        // ──────────────────────────────────────────────────────────────────────────
        void DrawMeshCacheSettings()
        {
            GUILayout.Label("Mesh Cache", EditorStyles.boldLabel);

            // Enable / disable caching
            bool enabled = MeshCacheUtility.Enabled;
            bool newEnabled = EditorGUILayout.Toggle("Enable Mesh Persistence", enabled);
            if (newEnabled != enabled)
                MeshCacheUtility.SetEnabled(newEnabled);

            // Folder controls (disabled when cache off)
            using (new EditorGUI.DisabledScope(!newEnabled))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("Cache Folder");

                string folder = MeshCacheUtility.CachePath;
                EditorGUILayout.LabelField(string.IsNullOrEmpty(folder) ? "(default)" : folder,
                                           GUILayout.MaxHeight(EditorGUIUtility.singleLineHeight));

                if (GUILayout.Button("Change...", GUILayout.Width(80)))
                    MeshCacheUtility.PickFolder();

            
                if (GUILayout.Button("Clean Unused Cached Meshes"))
                    MeshCacheUtility.CleanUnusedCache();

                   EditorGUILayout.EndHorizontal(); 
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        }


    }
}