using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.SceneManagement;

namespace Mayuns.DSB.Editor
{
    [InitializeOnLoad]
    public static class StructureBuildTool
    {
        public static StructureBuildSettings buildSettings;
        const string kBuildSettingsGuidKey = "SB_BUILD_SETTINGS_GUID";

        public enum BuildMode
        {
            None,
            CreateStructure,
            StructuralMemberBuild,
            GroundedMode,
            WallBuild,
            WallEdit,
            ApplyDesign,
            ApplyMaterial,
            Delete
        }
        public enum WallEditSubMode
        {
            None,
            DeletePiece,
            AddPiece,
            AddWindow,
            AddTriangle
        }

        public static BuildMode currentMode = BuildMode.None;
        static float raycastDistance = 100f;
        static StructuralConnection lastHoveredConnection = null;
        static StructuralMember lastHoveredMember = null;
        static Vector3 lastWallHoverPoint;
        static Vector3 lastWallHoverNormal;
        public static WallManager selectedWall = null;
        public static WallEditSubMode currentWallEditSubMode = WallEditSubMode.None;
        public static WallManager SelectedWall => selectedWall;
        public static WallDesign selectedDesignToApply;
        public static Material selectedMaterialToApply;
        static WallManager _pendingWall;
        static int _pendingCols;
        static int _pendingRows;
        static readonly RaycastHit[] raycastHits = new RaycastHit[16];
        static readonly Dictionary<BuildMode, string> modeDescriptions = new()
    {
        { BuildMode.CreateStructure, "Click to place a new structure at the cursor." },
        { BuildMode.StructuralMemberBuild, "Click a connection to build members from it." },
        { BuildMode.WallBuild, "Click a member to attach a wall to it." },
        { BuildMode.WallEdit, "Click wall cells to add/remove elements." },
        { BuildMode.ApplyDesign, "Click a wall to apply the selected design." },
        { BuildMode.ApplyMaterial, "Click a member or wall to apply the selected material." },
        { BuildMode.Delete, "Click elements to delete them from the scene." },
        { BuildMode.GroundedMode, "Click members to toggle their grounded state." },
    };

        static StructureBuildTool()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            buildSettings = ResolvebuildSettings();
        }

        static StructureBuildSettings ResolvebuildSettings()
        {
            string guid = EditorPrefs.GetString(kBuildSettingsGuidKey, "");
            if (!string.IsNullOrEmpty(guid))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<StructureBuildSettings>(path);
                if (asset) return asset;
            }

            // Project‑wide search (runs once)
            var guids = AssetDatabase.FindAssets("t:StructureBuildSettings");
            if (guids.Length > 0)
                return AssetDatabase.LoadAssetAtPath<StructureBuildSettings>(
                    AssetDatabase.GUIDToAssetPath(guids[0]));

            return null; // safe fallback
        }

        public static void SetBuildMode(BuildMode newMode)
        {
            currentMode = newMode;
            Debug.Log($"Build Mode: {newMode}");

            // Reset any state when switching off modes.
            if (newMode == BuildMode.None)
            {
                lastHoveredConnection = null;
                lastHoveredMember = null;
                selectedWall = null;
                currentWallEditSubMode = WallEditSubMode.None;
            }
            else if (newMode == BuildMode.WallEdit)
            {
                currentWallEditSubMode = WallEditSubMode.DeletePiece;
            }
            ManagerWindow.RepaintIfOpen();
            SceneView.RepaintAll();
        }

        public static void SetWallEditSubMode(WallEditSubMode subMode)
        {
            currentWallEditSubMode = subMode;
            Debug.Log($"Wall Edit SubMode: {subMode}");
            if (subMode == WallEditSubMode.None)
            {
                selectedWall = null;
            }
            ManagerWindow.RepaintIfOpen();
            SceneView.RepaintAll();
        }

        static Vector3[] GetLocalBuildDirections(StructuralConnection connection)
        {
            // Grab local axes
            Vector3 nodeUp = connection.transform.up;
            Vector3 nodeDown = -connection.transform.up;
            Vector3 nodeRight = connection.transform.right;
            Vector3 nodeLeft = -connection.transform.right;
            Vector3 nodeFwd = connection.transform.forward;
            Vector3 nodeBack = -connection.transform.forward;

            List<Vector3> dirs = new List<Vector3>()
        {
        nodeUp, nodeDown,
        nodeRight, nodeLeft,
        nodeFwd, nodeBack
        };
            dirs.Add((nodeUp + nodeRight).normalized);
            dirs.Add((nodeUp + nodeLeft).normalized);
            dirs.Add((nodeDown + nodeRight).normalized);
            dirs.Add((nodeDown + nodeLeft).normalized);
            dirs.Add((nodeFwd + nodeRight).normalized);
            dirs.Add((nodeFwd + nodeLeft).normalized);
            dirs.Add((nodeBack + nodeRight).normalized);
            dirs.Add((nodeBack + nodeLeft).normalized);
            dirs.Add((nodeUp + nodeFwd).normalized);
            dirs.Add((nodeUp + nodeBack).normalized);
            dirs.Add((nodeDown + nodeFwd).normalized);
            dirs.Add((nodeDown + nodeBack).normalized);
            return dirs.ToArray();
        }

        static void OnSceneGUI(SceneView sceneView)
        {
            if (currentMode == BuildMode.None || buildSettings == null) return;

            Event e = Event.current;

            // Top left info box in scene view
            Handles.BeginGUI();

            GUILayout.BeginArea(new Rect(10, 10, 300, 70), EditorStyles.helpBox);

            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                normal = { textColor = Color.cyan }
            };

            GUILayout.Label($"MODE: {currentMode}", titleStyle);

            if (modeDescriptions.TryGetValue(currentMode, out string description))
            {
                GUILayout.Label(description, EditorStyles.wordWrappedLabel);
            }

            GUILayout.EndArea();

            Handles.EndGUI();


            // Handle early-return modes
            switch (currentMode)
            {
                case BuildMode.GroundedMode:
                    DrawGroundedGizmos();
                    break;

                case BuildMode.CreateStructure:
                    HandleCreateNewStructureMode(e, sceneView);
                    break;

                case BuildMode.WallEdit:
                    if (selectedWall == null)
                        DrawWallGizmos(true);
                    else
                        DrawWallCells(selectedWall);
                    break;

                case BuildMode.ApplyDesign:
                    DrawWallGizmos(false);
                    break;

                case BuildMode.ApplyMaterial:
                    HandleApplyMaterialMode(e);
                    break;

                case BuildMode.Delete:
                    HandleDeleteMode(e);
                    break;
            }

            if (currentMode is BuildMode.StructuralMemberBuild or BuildMode.WallBuild)
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
                HandleRaycast(currentMode, ray);
            }

            switch (currentMode)
            {
                case BuildMode.StructuralMemberBuild:
                    if (lastHoveredConnection != null)
                        DrawGhostMembersForConnection(lastHoveredConnection);
                    break;

                case BuildMode.WallBuild:
                    if (lastHoveredMember != null)
                        DrawGhostWallForMember(lastHoveredMember, lastWallHoverPoint, lastWallHoverNormal);
                    break;
            }

            // Prevent scene selection
            if (e.type == EventType.Layout)
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        }

        static void HandleRaycast(BuildMode mode, Ray ray)
        {
            int hitCount = Physics.RaycastNonAlloc(ray, raycastHits, raycastDistance);
            for (int i = 0; i < hitCount; i++)
            {
                var hit = raycastHits[i];
                var collider = hit.collider;

                switch (mode)
                {
                    case BuildMode.StructuralMemberBuild:
                        {
                            var connection = collider.GetComponent<StructuralConnection>();
                            if (connection != null)
                            {
                                lastHoveredConnection = connection;
                                HighlightHitPoint(hit.point);
                                return; // stop after first match
                            }
                            break;
                        }

                    case BuildMode.WallBuild:
                        {
                            var member = collider.GetComponentInParent<StructuralMember>();
                            if (member != null)
                            {
                                lastHoveredMember = member;
                                lastWallHoverPoint = hit.point;
                                lastWallHoverNormal = hit.normal;
                                return; // stop after first match
                            }
                            break;
                        }

                        // Add other modes here as needed (CreateStructure, Delete, etc.)
                }
            }
        }

        private static void HandleCreateNewStructureMode(Event e, SceneView sceneView)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            Vector3 spawnPosition = Vector3.zero;

            if (Physics.Raycast(ray, out RaycastHit hit, raycastDistance))
            {
                spawnPosition = hit.point;
            }
            else if (e.type == EventType.MouseDown && e.button == 0)
            {
                Debug.Log("No collision detected. Creating structure at Origin.");
            }

            Handles.color = Color.cyan;
            Handles.DrawWireCube(spawnPosition, Vector3.one);

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                GameObject newStructure = new GameObject("NewStructure");
                Undo.RegisterCreatedObjectUndo(newStructure, "Create New Structure");
                newStructure.transform.position = spawnPosition;
                StructuralGroupManager structuralGroup = newStructure.AddComponent<StructuralGroupManager>();
                Rigidbody rb = newStructure.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                GameObject connectionGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
                connectionGO.name = "StructuralConnection";

                connectionGO.transform.position = spawnPosition;
                connectionGO.transform.localScale = Vector3.one * buildSettings.connectionSize;
                connectionGO.transform.SetParent(newStructure.transform, true);

                StructuralConnection connectionReference = connectionGO.AddComponent<StructuralConnection>();

                if (buildSettings.connectionMaterial != null)
                    connectionGO.GetComponent<MeshRenderer>().sharedMaterial = buildSettings.connectionMaterial;

                structuralGroup.memberConnections.Add(connectionReference);
                structuralGroup.strengthModifier = buildSettings.strengthModifier;
                structuralGroup.minPropagationTime = buildSettings.minPropagationTime;
                structuralGroup.maxPropagationTime = buildSettings.maxPropagationTime;
                structuralGroup.buildSettings = buildSettings;
                connectionGO.AddComponent<BoxCollider>();

                Debug.Log($"New structure created at {spawnPosition}");
                EditorUtility.SetDirty(newStructure);

                SetBuildMode(BuildMode.StructuralMemberBuild);
            }

            if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag)
            {
                SceneView.RepaintAll();
            }

        }

        static void HandleDeleteMode(Event e)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, raycastDistance)) return;

            // --- Try to delete a WallManager ---
            WallManager wall = hit.collider.GetComponentInParent<WallManager>();
            if (wall != null)
            {
                DrawDeleteHandle(
                    wall.transform.position,
                    wall.transform.rotation,
                    wall.transform.lossyScale,
                    () =>
                    {
                        Debug.Log($"Deleted Wall: {wall.name}");
                        Undo.DestroyObjectImmediate(wall.transform.parent.gameObject);

                    },
                    "Delete Wall"
                );
                return;
            }

            // --- Try to delete a StructuralMember ---
            StructuralMember member = hit.collider.GetComponentInParent<StructuralMember>();
            if (member != null)
            {
                Vector3 pos = member.transform.position;
                Quaternion rot = member.transform.rotation;
                Vector3 scale = Vector3.one;

                MeshFilter mesh = member.GetComponent<MeshFilter>();
                if (mesh != null && mesh.sharedMesh != null)
                {
                    Bounds bounds = mesh.sharedMesh.bounds;
                    pos = member.transform.TransformPoint(bounds.center);
                    scale = Vector3.Scale(member.transform.lossyScale, bounds.size);
                }

                DrawDeleteHandle(
                    pos,
                    rot,
                    scale,
                    () =>
                    {
                        Debug.Log($"Deleted StructuralMember: {member.name}");
                        Undo.DestroyObjectImmediate(member.gameObject);

                    },
                    "Delete Member"
                );
                return;
            }

            // --- Try to delete a StructuralConnection ---
            StructuralConnection connection = hit.collider.GetComponentInParent<StructuralConnection>();
            if (connection != null)
            {
                Vector3 pos = connection.transform.position;
                Quaternion rot = connection.transform.rotation;
                Vector3 scale = Vector3.one;

                MeshFilter mesh = connection.GetComponent<MeshFilter>();
                if (mesh != null && mesh.sharedMesh != null)
                {
                    Bounds bounds = mesh.sharedMesh.bounds;
                    pos = connection.transform.TransformPoint(bounds.center);
                    scale = Vector3.Scale(connection.transform.lossyScale, bounds.size);
                }

                DrawDeleteHandle(
                    pos,
                    rot,
                    scale,
                    () =>
                    {
                        List<StructuralMember> connectedMembers = connection.GetMembers();

                        // Filter out self and destroyed/null
                        connectedMembers.RemoveAll(s => s == null);

                        // Remove adjacency info from connections
                        foreach (var member in connectedMembers)
                        {
                            Undo.RecordObject(member, "Remove Adjacency Information");
                            member.cachedAdjacentMembers.RemoveAll(adjacent => connectedMembers.Contains(adjacent));
                        }
                        Debug.Log($"Deleted StructuralConnection: {connection.name}");
                        Undo.DestroyObjectImmediate(connection.gameObject);

                    },
                    "Delete Connection"
                );
            }
        }

        static void DrawDeleteHandle(Vector3 position, Quaternion rotation, Vector3 scale, System.Action onClick, string label = null)
        {
            Matrix4x4 oldMatrix = Handles.matrix;
            Handles.matrix = Matrix4x4.TRS(position, rotation, scale);

            Handles.color = new Color(1f, 0f, 0f, 0.6f); // red
            if (Handles.Button(Vector3.zero, Quaternion.identity, 1f, 1f, Handles.CubeHandleCap))
            {
                onClick?.Invoke();
            }

            if (!string.IsNullOrEmpty(label))
            {
                Handles.Label(Vector3.up * 1.1f, label, EditorStyles.boldLabel);
            }

            Handles.matrix = oldMatrix;
        }

        static void DrawApplyMaterialHandle(Vector3 position, Quaternion rotation, Vector3 scale, System.Action onClick, string label = null)
        {
            Matrix4x4 oldMatrix = Handles.matrix;
            Handles.matrix = Matrix4x4.TRS(position, rotation, scale);

            Handles.color = new Color(0f, 1f, 1f, 0.6f); // cyan
            if (Handles.Button(Vector3.zero, Quaternion.identity, 1f, 1f, Handles.CubeHandleCap))
            {
                onClick?.Invoke();
            }

            if (!string.IsNullOrEmpty(label))
            {
                Handles.Label(Vector3.up * 1.1f, label, EditorStyles.boldLabel);
            }

            Handles.matrix = oldMatrix;
        }

        static void HandleApplyMaterialMode(Event e)
        {
            if (selectedMaterialToApply == null)
                return;

            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, raycastDistance)) return;

            WallManager wall = hit.collider.GetComponentInParent<WallManager>();
            if (wall != null)
            {
                DrawApplyMaterialHandle(
                    wall.transform.position,
                    wall.transform.rotation,
                    wall.transform.lossyScale,
                    () => ApplyMaterialToWall(wall, selectedMaterialToApply),
                    "Apply Material"
                );
                return;
            }

            StructuralMember member = hit.collider.GetComponentInParent<StructuralMember>();
            if (member != null)
            {
                Vector3 pos = member.transform.position;
                Quaternion rot = member.transform.rotation;
                Vector3 scale = Vector3.one;

                MeshFilter mesh = member.GetComponent<MeshFilter>();
                if (mesh != null && mesh.sharedMesh != null)
                {
                    Bounds bounds = mesh.sharedMesh.bounds;
                    pos = member.transform.TransformPoint(bounds.center);
                    scale = Vector3.Scale(member.transform.lossyScale, bounds.size);
                }

                DrawApplyMaterialHandle(
                    pos,
                    rot,
                    scale,
                    () => ApplyMaterialToMember(member, selectedMaterialToApply),
                    "Apply Material"
                );
                return;
            }

            StructuralConnection connection = hit.collider.GetComponentInParent<StructuralConnection>();
            if (connection != null)
            {
                Vector3 pos = connection.transform.position;
                Quaternion rot = connection.transform.rotation;
                Vector3 scale = connection.transform.lossyScale;

                MeshFilter mesh = connection.GetComponent<MeshFilter>();
                if (mesh != null && mesh.sharedMesh != null)
                {
                    Bounds bounds = mesh.sharedMesh.bounds;
                    pos = connection.transform.TransformPoint(bounds.center);
                    scale = Vector3.Scale(connection.transform.lossyScale, bounds.size);
                }

                DrawApplyMaterialHandle(
                    pos,
                    rot,
                    scale,
                    () => ApplyMaterialToConnection(connection, selectedMaterialToApply),
                    "Apply Material"
                );
            }
        }

        static void ApplyMaterialToConnection(StructuralConnection conn, Material mat)
        {
            if (conn == null) return;
            var rend = conn.GetComponent<Renderer>();
            if (rend != null)
            {
                Undo.RecordObject(rend, "Apply Material");
                rend.sharedMaterial = mat;
            }
        }

        static void ApplyMaterialToMember(StructuralMember member, Material mat)
        {
            if (member == null) return;

            var rend = member.GetComponent<Renderer>();
            if (rend != null)
            {
                Undo.RecordObject(rend, "Apply Material");
                rend.sharedMaterial = mat;
            }

            member.UncombineMember();
            member.BuildMember();

            ApplyMaterialToConnection(member.startConnection, mat);
            ApplyMaterialToConnection(member.endConnection, mat);
        }

        static void ApplyMaterialToWall(WallManager wall, Material mat)
        {
            if (wall == null || buildSettings == null) return;

            Undo.RecordObject(wall, "Apply Wall Material");
            wall.wallMaterial = mat;
            wall.InstantUncombine();
            wall.RelinkWallGridReferences();
            wall.BuildWall(wall.wallGrid, true, buildSettings);
            EditorUtility.SetDirty(wall);
        }

        static void DrawWallCells(WallManager wall)
        {
            float wallLocalWidth = 1f;
            float wallLocalHeight = 1f;
            float wallDepth = 1f;
            int columns = wall.numColumns;
            int rows = wall.numRows;

            // Ensure wallGrid is initialized
            if (wall.wallGrid == null || wall.wallGrid.Count != columns * rows)
            {
                wall.wallGrid = new List<WallPiece>(new WallPiece[columns * rows]);
            }

            // Calculate cell dimensions
            float cellWidth = wallLocalWidth / columns;
            float cellHeight = wallLocalHeight / rows;
            Vector3 bottomLeft = new Vector3(-wallLocalWidth * 0.5f, -wallLocalHeight * 0.5f, -wallDepth * 0.5f);

            Matrix4x4 oldMatrix = Handles.matrix;
            Handles.matrix = Matrix4x4.TRS(wall.transform.position, wall.transform.rotation, wall.transform.lossyScale);
            float cellScaleFactor = 0.99f;

            for (int col = 0; col < columns; col++)
            {
                for (int row = 0; row < rows; row++)
                {
                    Vector2Int cellCoord = new Vector2Int(col, row);
                    float centerX = bottomLeft.x + (col + 0.5f) * cellWidth;
                    float centerY = bottomLeft.y + (row + 0.5f) * cellHeight;
                    Vector3 cellCenter = new Vector3(centerX, centerY, 0f);
                    Vector3 cellScale = new Vector3(cellWidth * cellScaleFactor, cellHeight * cellScaleFactor, wallDepth * cellScaleFactor);

                    Matrix4x4 cellMatrix = Matrix4x4.TRS(cellCenter, Quaternion.identity, cellScale);
                    Matrix4x4 combinedMatrix = Handles.matrix * cellMatrix;
                    Matrix4x4 preCellMatrix = Handles.matrix;
                    Handles.matrix = combinedMatrix;

                    int idx = col + row * columns;
                    WallPiece wallPiece = (idx >= 0 && idx < wall.wallGrid.Count) ? wall.wallGrid[idx] : null;
                    bool cellEmpty = (wallPiece == null);

                    if (currentWallEditSubMode == WallEditSubMode.AddTriangle)
                    {
                        Vector2[] offsets = new Vector2[]
                        {
                    new Vector2(-0.25f,  0.25f),
                    new Vector2( 0.25f,  0.25f),
                    new Vector2(-0.25f, -0.25f),
                    new Vector2( 0.25f, -0.25f)
                        };

                        WallPiece.TriangularCornerDesignation[] designations = new WallPiece.TriangularCornerDesignation[]
                        {
                    WallPiece.TriangularCornerDesignation.BottomLeft,
                    WallPiece.TriangularCornerDesignation.TopLeft,
                    WallPiece.TriangularCornerDesignation.BottomRight,
                    WallPiece.TriangularCornerDesignation.TopRight
                        };

                        float subButtonSize = 0.5f;

                        for (int i = 0; i < 4; i++)
                        {
                            Vector2 offset = offsets[i];
                            Vector3 subCenter = new Vector3(offset.x, offset.y, 0f);
                            Handles.color = new Color(0f, 1f, 1f, 0.5f);
                            if (Handles.Button(subCenter, Quaternion.identity, subButtonSize, subButtonSize, Handles.CubeHandleCap))
                            {
                                AddWallTriangle(wall, cellCoord, designations[i]);
                            }
                        }
                    }
                    else
                    {
                        Color cellColor = cellEmpty ? new Color(1f, 0f, 0f, 0.5f) : new Color(0f, 1f, 0f, 0.5f);
                        Handles.color = cellColor;
                        if (Handles.Button(Vector3.zero, Quaternion.identity, 1f, 1f, Handles.CubeHandleCap))
                        {
                            switch (currentWallEditSubMode)
                            {
                                case WallEditSubMode.DeletePiece:
                                    DeleteWallSection(wall, cellCoord);
                                    break;
                                case WallEditSubMode.AddPiece:
                                    AddWallPiece(wall, cellCoord);
                                    break;
                                case WallEditSubMode.AddWindow:
                                    AddWindowToWall(wall, cellCoord);
                                    break;
                                default:
                                    break;
                            }
                        }
                    }
                    Handles.matrix = preCellMatrix;
                }
            }
            Handles.matrix = oldMatrix;
        }

        private static void AddWallTriangle(WallManager wall, Vector2Int cell, WallPiece.TriangularCornerDesignation triangularCornerDesignation)
        {
            if (cell.x < 0 || cell.x >= wall.numColumns || cell.y < 0 || cell.y >= wall.numRows)
                return;

            Undo.RecordObject(wall, "Add Wall Triangle");
            wall.InstantUncombine();
            wall.RelinkWallGridReferences();

            int idx = cell.x + cell.y * wall.numColumns;
            WallPiece oldPiece = wall.wallGrid[idx];
            if (oldPiece != null)
            {
                Undo.DestroyObjectImmediate(oldPiece.gameObject);
                //wall.wallGrid[idx] = null;
            }

            GameObject newPiece = new GameObject("WallPiece");
            Undo.RegisterCreatedObjectUndo(newPiece, "Create WallPiece");
            newPiece.transform.SetParent(wall.transform, false);

            WallPiece newPieceComponent = Undo.AddComponent<WallPiece>(newPiece);
            newPieceComponent.cornerDesignation = triangularCornerDesignation;
            wall.wallGrid[idx] = newPieceComponent;

            wall.glassMaterial = buildSettings.glassMaterial;
            wall.wallMaterial = buildSettings.wallMaterial;

            wall.BuildWall(wall.wallGrid, true, buildSettings);
            EditorUtility.SetDirty(wall);
        }

        private static void AddWindowToWall(WallManager wall, Vector2Int cell)
        {
            if (cell.x < 0 || cell.x >= wall.numColumns || cell.y < 0 || cell.y >= wall.numRows)
                return;

            Undo.RecordObject(wall, "Add Window To Wall");
            wall.InstantUncombine();
            wall.RelinkWallGridReferences();

            int idx = cell.x + cell.y * wall.numColumns;
            WallPiece oldPiece = wall.wallGrid[idx];
            if (oldPiece != null)
            {
                Undo.DestroyObjectImmediate(oldPiece.gameObject);
                //wall.wallGrid[idx] = null;
            }

            GameObject newPiece = new GameObject("WallPiece");
            Undo.RegisterCreatedObjectUndo(newPiece, "Create Window");

            newPiece.transform.SetParent(wall.transform, false);

            WallPiece newPieceComponent = Undo.AddComponent<WallPiece>(newPiece);
            Undo.RecordObject(newPieceComponent, "Set WallPiece Properties");
            newPieceComponent.isWindow = true;
            wall.wallGrid[idx] = newPieceComponent;

            wall.glassMaterial = buildSettings.glassMaterial;
            wall.wallMaterial = buildSettings.wallMaterial;

            wall.BuildWall(wall.wallGrid, true, buildSettings);
            EditorUtility.SetDirty(wall);
        }

        private static void AddWallPiece(WallManager wall, Vector2Int cell)
        {
            if (cell.x < 0 || cell.x >= wall.numColumns || cell.y < 0 || cell.y >= wall.numRows)
                return;


            Undo.RecordObject(wall, "Add Piece To Wall");
            wall.InstantUncombine();
            wall.RelinkWallGridReferences();

            int idx = cell.x + cell.y * wall.numColumns;
            WallPiece oldPiece = wall.wallGrid[idx];
            if (oldPiece != null)
            {
                Undo.DestroyObjectImmediate(oldPiece.gameObject);
                //wall.wallGrid[idx] = null;
            }

            GameObject newPiece = new GameObject("WallPiece");
            newPiece.transform.SetParent(wall.transform, false);
            WallPiece newPieceComponent = newPiece.AddComponent<WallPiece>();
            wall.wallGrid[idx] = newPieceComponent;

            wall.glassMaterial = buildSettings.glassMaterial;
            wall.wallMaterial = buildSettings.wallMaterial;

            wall.BuildWall(wall.wallGrid, true, buildSettings);
            EditorUtility.SetDirty(wall);
        }

        private static void ApplyDesign(WallManager wall, WallDesign design)
        {
            if (wall == null || design == null || buildSettings == null)
            {
                Debug.LogError("Missing reference in ApplyDesign");
                return;
            }
            Undo.RecordObject(wall, "Delete Wall Piece Wall");
            wall.InstantUncombine();
            wall.RelinkWallGridReferences();
            if (wall.wallGrid != null)
            {
                foreach (var piece in wall.wallGrid)
                {
                    if (piece != null)
                    {
                        Undo.DestroyObjectImmediate(piece.gameObject);
                    }
                }
            }
            wall.wallGrid?.Clear();
            wall.numColumns = design.columns;
            wall.numRows = design.rows;
            wall.wallGrid = new List<WallPiece>(new WallPiece[wall.numColumns * wall.numRows]);
            wall.WallPieceMass = buildSettings.wallPieceMass;
            wall.wallPieceHealth = buildSettings.wallPieceHealth;
            wall.wallPieceWindowHealth = buildSettings.wallPieceWindowHealth;
            wall.textureScaleX = buildSettings.wallTextureScaleX;
            wall.textureScaleY = buildSettings.wallTextureScaleY;

            for (int row = 0; row < wall.numRows; row++)
            {
                for (int col = 0; col < wall.numColumns; col++)
                {
                    int cellIdx = col * wall.numRows + row;
                    WallDesign.Cell cell = design.cells[cellIdx];
                    WallPiece newPiece = null;

                    switch (cell.type)
                    {
                        case WallDesign.CellType.Cube:
                            newPiece = CreateWallPieceUndoable(wall.transform, false, false, null);
                            break;
                        case WallDesign.CellType.Window:
                            newPiece = CreateWallPieceUndoable(wall.transform, true, false, null);
                            break;
                        case WallDesign.CellType.TriangleBL:
                        case WallDesign.CellType.TriangleTL:
                        case WallDesign.CellType.TriangleBR:
                        case WallDesign.CellType.TriangleTR:
                            newPiece = CreateWallPieceUndoable(wall.transform, false, true, cell.type);
                            break;
                    }

                    int idx = col + row * wall.numColumns;
                    wall.wallGrid[idx] = newPiece;

                    if (newPiece != null)
                        newPiece.gridPosition = new Vector2Int(col, row);
                }
            }

            wall.glassMaterial = buildSettings.glassMaterial;
            wall.wallMaterial = buildSettings.wallMaterial;

            wall.BuildWall(wall.wallGrid, true, buildSettings);

            EditorUtility.SetDirty(wall);
        }

        private static WallPiece CreateWallPieceUndoable(Transform parent, bool isWindow, bool isTriangle, object triangleType = null)
        {
            GameObject go = new GameObject("WallPiece");
            go.transform.SetParent(parent, false);

            Undo.RegisterCreatedObjectUndo(go, "Create WallPiece");

            WallPiece piece = go.AddComponent<WallPiece>();
            piece.isWindow = isWindow;

            if (isTriangle && triangleType is WallDesign.CellType type)
            {
                piece.cornerDesignation = type switch
                {
                    WallDesign.CellType.TriangleBL => WallPiece.TriangularCornerDesignation.BottomLeft,
                    WallDesign.CellType.TriangleTL => WallPiece.TriangularCornerDesignation.TopLeft,
                    WallDesign.CellType.TriangleBR => WallPiece.TriangularCornerDesignation.BottomRight,
                    WallDesign.CellType.TriangleTR => WallPiece.TriangularCornerDesignation.TopRight,
                    _ => WallPiece.TriangularCornerDesignation.BottomLeft
                };
            }
            return piece;
        }

        static void DeleteWallSection(WallManager wall, Vector2Int cell)
        {
            if (cell.x < 0 || cell.x >= wall.numColumns || cell.y < 0 || cell.y >= wall.numRows)
                return;

            Undo.RecordObject(wall, "Delete Wall Piece Wall");
            wall.InstantUncombine();
            wall.RelinkWallGridReferences();

            int idx = cell.x + cell.y * wall.numColumns;
            WallPiece oldPiece = wall.wallGrid[idx];
            if (oldPiece != null)
            {
                Undo.DestroyObjectImmediate(oldPiece.gameObject);
                //wall.wallGrid[idx] = null;
            }
            wall.wallGrid[idx] = null;

            wall.glassMaterial = buildSettings.glassMaterial;
            wall.wallMaterial = buildSettings.wallMaterial;

            wall.BuildWall(wall.wallGrid, true, buildSettings);
            EditorUtility.SetDirty(wall);
        }

        static void DrawWallGizmos(bool selectForEdit)
        {
            WallManager[] walls = Object.FindObjectsByType<WallManager>(FindObjectsSortMode.None);
            Event e = Event.current;

            WallManager hoveredWall = null;

            // Perform raycast to find the hovered member
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, raycastDistance))
            {
                hoveredWall = hit.collider.GetComponentInParent<WallManager>();
            }

            // Iterate over all structural walls
            foreach (var wall in walls)
            {
                Vector3 gizmoPosition = wall.transform.position;
                Vector3 gizmoScale = wall.transform.lossyScale;
                Quaternion gizmoRotation = wall.transform.rotation;

                // Set the transformation matrix for the button
                Matrix4x4 oldMatrix = Handles.matrix;
                Handles.matrix = Matrix4x4.TRS(gizmoPosition, gizmoRotation, gizmoScale);

                if (wall == hoveredWall)
                {
                    Handles.color = Color.green;
                    if (selectForEdit)
                    {
                        if (Handles.Button(Vector3.zero, Quaternion.identity, 1f, 1f, Handles.CubeHandleCap))
                        {
                            selectedWall = wall;
                            Debug.Log($"Editing {wall.name}");
                            EditorUtility.SetDirty(wall);
                            ManagerWindow.RepaintIfOpen();
                        }
                    }
                    else
                    {
                        if (Handles.Button(Vector3.zero, Quaternion.identity, 1f, 1f, Handles.CubeHandleCap))
                        {
                            if (selectedDesignToApply != null)
                            {
                                ApplyDesign(wall, selectedDesignToApply);
                                Debug.Log($"Applied design to wall: {wall.name}");
                            }
                            else
                            {
                                Debug.LogWarning("No wall design selected to apply.");
                            }

                            EditorUtility.SetDirty(wall);
                            ManagerWindow.RepaintIfOpen();
                        }

                    }

                }

                Handles.matrix = oldMatrix;
            }
        }

        public static void RequestWallRefresh(int? cols = null, int? rows = null)
        {
            // queue once per GUI event
            _pendingWall = selectedWall;
            _pendingCols = cols ?? selectedWall?.numColumns ?? 1;
            _pendingRows = rows ?? selectedWall?.numRows ?? 1;

            EditorApplication.delayCall -= DoPendingRefresh; // ensure single call
            EditorApplication.delayCall += DoPendingRefresh;
        }

        static void DoPendingRefresh()
        {
            EditorApplication.delayCall -= DoPendingRefresh;

            var wall = _pendingWall;
            if (wall == null || buildSettings == null) return;

            Undo.RecordObject(wall, "Rebuild Wall");

            // --- wipe current grid ---
            wall.InstantUncombine();
            wall.RelinkWallGridReferences();
            if (wall.wallGrid != null)
                foreach (var piece in wall.wallGrid.Where(p => p != null))
                    Undo.DestroyObjectImmediate(piece.gameObject);

            wall.numColumns = Mathf.Max(1, _pendingCols);
            wall.numRows = Mathf.Max(1, _pendingRows);

            // --- build fresh grid (all cubes) ---
            int total = wall.numColumns * wall.numRows;
            wall.wallGrid = new List<WallPiece>(new WallPiece[total]);

            for (int i = 0; i < total; i++)
                wall.wallGrid[i] = CreateWallPieceUndoable(wall.transform, false, false, null);

            // --- apply materials & rebuild mesh ---
            wall.wallMaterial = buildSettings.wallMaterial;
            wall.glassMaterial = buildSettings.glassMaterial;
            wall.BuildWall(wall.wallGrid, true, buildSettings);

            EditorUtility.SetDirty(wall);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(wall.gameObject.scene);
        }

        static void DrawGroundedGizmos()
        {
            StructuralMember[] members = Object.FindObjectsByType<StructuralMember>(FindObjectsSortMode.None);
            Event e = Event.current;

            StructuralMember hoveredMember = null;

            // Perform raycast to find the hovered member
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, raycastDistance))
            {
                hoveredMember = hit.collider.GetComponentInParent<StructuralMember>();
            }

            // Iterate over all structural members
            foreach (var member in members)
            {
                MeshFilter meshFilter = member.GetComponent<MeshFilter>();
                Vector3 gizmoPosition = member.transform.position;
                Vector3 gizmoScale = Vector3.one;
                Quaternion gizmoRotation = member.transform.rotation;

                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    // Get the mesh bounds (which are in the local space of the mesh)
                    Bounds meshBounds = meshFilter.sharedMesh.bounds;
                    // Transform the mesh bounds center into world space
                    gizmoPosition = member.transform.TransformPoint(meshBounds.center);
                    // Scale the mesh bounds size by the object's lossyScale to get the world size
                    gizmoScale = Vector3.Scale(member.transform.lossyScale, meshBounds.size);
                }

                // Set the transformation matrix for the button
                Matrix4x4 oldMatrix = Handles.matrix;
                Handles.matrix = Matrix4x4.TRS(gizmoPosition, gizmoRotation, gizmoScale);

                // Always draw green buttons for grounded members
                if (member.isGrounded)
                {
                    Handles.color = Color.green;
                    if (Handles.Button(Vector3.zero, Quaternion.identity, 1f, 1f, Handles.CubeHandleCap))
                    {
                        Undo.RecordObject(member, "Toggle Grounded State");
                        member.isGrounded = false;
                        Debug.Log($"Toggled grounded state of {member.name} to {member.isGrounded}");
                        EditorUtility.SetDirty(member);
                    }
                }

                // Draw yellow buttons for non-grounded hovered members
                if (member == hoveredMember && !member.isGrounded)
                {
                    Handles.color = Color.yellow;
                    if (Handles.Button(Vector3.zero, Quaternion.identity, 1f, 1f, Handles.CubeHandleCap))
                    {
                        Undo.RecordObject(member, "Toggle Grounded State");
                        member.isGrounded = true;
                        Debug.Log($"Toggled grounded state of {member.name} to {member.isGrounded}");
                        EditorUtility.SetDirty(member);
                    }
                }
                Handles.matrix = oldMatrix;
            }
        }

        static void DrawGhostMembersForConnection(StructuralConnection connection)
        {
            if (buildSettings == null) return; // Ensure settings are loaded

            float baseLength = buildSettings.memberLength;
            float thickness = buildSettings.memberThickness;
            float halfNodeSize = buildSettings.memberThickness;

            // Get build directions in local space and transform to world space
            Vector3[] localDirections = GetLocalBuildDirections(connection);

            foreach (var localDir in localDirections)
            {
                // Transform local direction to world space
                //Vector3 localDir = connection.transform.TransformDirection(localDir).normalized;

                // Check if there's already a member in this direction
                StructuralMember existingMember = DirectionToMember(connection, localDir);
                if (existingMember != null) continue;

                string slot = connection.DirectionToSlot(localDir);
                bool isDiagonal = slot.Contains("-");

                float finalLength;

                switch (buildSettings.disableDirection)
                {
                    case DisableDirection.Diagonal when isDiagonal:
                    case DisableDirection.Orthogonal when !isDiagonal:
                        continue;
                }

                if (isDiagonal)
                {
                    finalLength = Mathf.Sqrt((baseLength * baseLength) + (baseLength * baseLength));
                }
                else
                {
                    finalLength = baseLength;
                }



                // Compute end position
                Vector3 startPos = connection.transform.position;

                // Compute the midpoint for ghost placement
                Vector3 ghostPos = startPos + localDir * (finalLength / 2);

                // Determine ghost rotation
                Quaternion ghostRot = connection.transform.rotation * connection.SlotRotation(connection.DirectionToSlot(localDir));


                // Define ghost scale (thickness in X/Y, length in Z)
                Vector3 ghostScale = new Vector3(thickness, thickness, finalLength);

                // Draw the ghost
                Matrix4x4 oldMatrix = Handles.matrix;
                Handles.matrix = Matrix4x4.TRS(ghostPos, ghostRot, Vector3.one);

                Handles.color = new Color(0f, 1f, 1f, 0.8f);
                Handles.DrawWireCube(Vector3.zero, ghostScale);

                // Add clickable handle for the ghost
                Handles.matrix *= Matrix4x4.Scale(ghostScale);
                float handleHalfSize = 1f;
                bool clicked = Handles.Button(
                    Vector3.zero,
                    Quaternion.identity,
                    handleHalfSize,
                    handleHalfSize,
                    Handles.CubeHandleCap
                );

                if (clicked)
                {
                    Undo.RecordObject(connection, "Spawn StructuralMember");
                    SpawnMemberInDirection(
                        connection,
                        localDir,
                        buildSettings.memberMaterial,
                        buildSettings.connectionMaterial,
                        buildSettings.memberLength,
                        buildSettings.memberThickness,
                        buildSettings.memberMass,
                        buildSettings.memberPieceHealth,
                        buildSettings.memberSupportCapacity,
                        buildSettings.connectionSize
                        );
                    EditorUtility.SetDirty(connection);
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(connection.gameObject.scene);
                }

                Handles.matrix = oldMatrix; // Reset the matrix
            }
        }

        static void SpawnMemberInDirection(
                    StructuralConnection connection,
                    Vector3 directionWorldSpace,
                    Material selectedMemberMat,
                    Material selectedConnectionMat,
                    float baseLength,
                    float thickness,
                    float memberMass,
                    float memberPieceHealth,
                    float memberSupportCapacity,
                    float connectionThickness)
        {
            //──────────────────────────────────────────────────────────────────────────
            // 0) start an Undo group so the whole operation rolls back in ONE step
            //──────────────────────────────────────────────────────────────────────────
            Undo.IncrementCurrentGroup();                       // begin a new group (returns void here)
            Undo.SetCurrentGroupName("Add Structural Member / Connection");
            int undoGroup = Undo.GetCurrentGroup();             // now fetch the ID

            //──────────────────────────────────────────────────────────────────────────
            // 1) PREP – cache data we’ll need
            //──────────────────────────────────────────────────────────────────────────
            string slotStr = connection.DirectionToSlot(directionWorldSpace);
            Slot slot = SlotLookup.FromString[slotStr];
            bool isDiag = slotStr.Contains("-");
            if (isDiag)
                baseLength = Mathf.Sqrt(baseLength * baseLength * 2f);

            Vector3 dirN = directionWorldSpace.normalized;
            Vector3 nodeCenter = connection.transform.position;
            Vector3 memberPos = nodeCenter + dirN * (baseLength * .5f);
            Vector3 endConnPos = nodeCenter + dirN * baseLength;

            // ────────────────────────────────────────────────────────────────────────
            // 2) Make SURE we can undo every change to existing objects
            //──────────────────────────────────────────────────────────────────────────
            Undo.RecordObject(connection, "Modify Connection");
            Undo.RecordObject(connection.transform, "Modify Connection Transform");

            // StructuralGroup must exist
            connection.structuralGroup = connection.GetComponentInParent<StructuralGroupManager>();
            if (!connection.structuralGroup)
            {
                Debug.LogError("StructuralGroupManager missing in parents");
                return;
            }
            Undo.RecordObject(connection.structuralGroup, "Modify StructuralGroup");

            //──────────────────────────────────────────────────────────────────────────
            // 3) Spawn the MEMBER
            //──────────────────────────────────────────────────────────────────────────
            GameObject memberGO = CreateDefaultCube("StructuralMember", undoGroup);

            // register transform changes **after** the object exists
            Undo.RecordObject(memberGO.transform, "Position Structural Member");
            memberGO.transform.SetPositionAndRotation(
                memberPos,
                connection.transform.rotation * SlotLookup.Rotation[slot]);

            memberGO.transform.localScale = isDiag
                ? new Vector3(thickness, thickness, baseLength)
                : new Vector3(thickness, thickness, baseLength);

            if (selectedMemberMat)
                memberGO.GetComponent<Renderer>().sharedMaterial = selectedMemberMat;

            // add & configure StructuralMember component through Undo
            StructuralMember newMem = Undo.AddComponent<StructuralMember>(memberGO);
            newMem.thickness = thickness;
            newMem.length = baseLength;
            newMem.mass = memberMass;
            newMem.memberPieceHealth = memberPieceHealth;
            newMem.textureScaleX = buildSettings.memberTextureScaleX;
            newMem.textureScaleY = buildSettings.memberTextureScaleY;
            newMem.supportCapacity = memberSupportCapacity;
            newMem.BuildMember();

            // parenting & bookkeeping
            Undo.SetTransformParent(memberGO.transform,
                                    connection.structuralGroup.transform,
                                    "Parent Structural Member");
            connection.structuralGroup.structuralMembers.Add(newMem);

            //──────────────────────────────────────────────────────────────────────────
            // 4) Spawn or fetch the END CONNECTION
            //──────────────────────────────────────────────────────────────────────────
            StructuralConnection endConn = connection.structuralGroup.memberConnections
                .FirstOrDefault(c => c && Vector3.Distance(c.transform.position, endConnPos) < 0.1f);

            if (!endConn)   // need to create one
            {
                GameObject connGO = CreateDefaultCube("StructuralConnection", undoGroup);

                Undo.RecordObject(connGO.transform, "Position Structural Connection");
                connGO.transform.SetPositionAndRotation(endConnPos, connection.transform.rotation);
                connGO.transform.localScale = Vector3.one * thickness * 1.01f; // Same as member thickness, but scaled up by 1% to prevent Z fighting

                if (selectedConnectionMat)
                    connGO.GetComponent<Renderer>().sharedMaterial = selectedConnectionMat;

                Undo.AddComponent<BoxCollider>(connGO);
                endConn = Undo.AddComponent<StructuralConnection>(connGO);
                endConn.structuralGroup = connection.structuralGroup;

                Undo.SetTransformParent(connGO.transform,
                                        connection.structuralGroup.transform,
                                        "Parent Structural Connection");
                connection.structuralGroup.memberConnections.Add(endConn);
            }

            //──────────────────────────────────────────────────────────────────────────
            // 5) Wire‑up references + adjacency
            //──────────────────────────────────────────────────────────────────────────
            connection.AssignMemberRef(slot, newMem, endConn);

            newMem.startConnection = connection;
            newMem.endConnection = endConn;
            newMem.cachedAdjacentMembers.Clear();

            List<StructuralMember> potential = new();
            connection.AddMembersFromConnection(connection, potential);
            connection.AddMembersFromConnection(endConn, potential);
            potential.RemoveAll(s => s == null || s == newMem || s.isDestroyed);

            foreach (var adj in potential)
            {
                if (!newMem.cachedAdjacentMembers.Contains(adj))
                    newMem.cachedAdjacentMembers.Add(adj);

                if (!adj.cachedAdjacentMembers.Contains(newMem))
                    adj.cachedAdjacentMembers.Add(newMem);
            }

            //──────────────────────────────────────────────────────────────────────────
            // 6) Dirty objects / scenes so Unity serialises them
            //──────────────────────────────────────────────────────────────────────────
            EditorUtility.SetDirty(connection.structuralGroup);
            EditorUtility.SetDirty(connection);
            PrefabUtility.RecordPrefabInstancePropertyModifications(connection.structuralGroup);

            if (!Application.isPlaying)
                EditorSceneManager.MarkSceneDirty(connection.gameObject.scene);

            //──────────────────────────────────────────────────────────────────────────
            // 7) Collapse the group so “Ctrl‑Z” rolls back everything in one shot
            //──────────────────────────────────────────────────────────────────────────
            Undo.CollapseUndoOperations(undoGroup);
        }

        //──────────────────────────────────────────────────────────────────────────────
        // Helper — any new primitive MUST be registered with Undo on creation
        //──────────────────────────────────────────────────────────────────────────────
        static GameObject CreateDefaultCube(string name, int undoGroup)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;

            // register creation BEFORE making further edits
            Undo.RegisterCreatedObjectUndo(cube, "Create " + name);

            // remove collider safely
            Collider col = cube.GetComponent<Collider>();
            if (col) Undo.DestroyObjectImmediate(col);

            return cube;
        }

        static void DrawGhostWallForMember(StructuralMember Member, Vector3 hoverPoint, Vector3 hoverNormal)
        {
            float wallHeight = buildSettings.wallHeight;
            float wallWidth = buildSettings.wallWidth;
            float wallThickness = buildSettings.wallThickness;
            bool isWallCentered = buildSettings.isWallCentered;
            bool allowOverlap = buildSettings.allowWallOverlap;
            float thickness = Member.thickness;
            float memberLength = Member.length;

            // Convert the hoverPoint to the member's local space.
            Vector3 localHitPoint = Member.transform.InverseTransformPoint(hoverPoint);

            float halfExtent = thickness / 2f - wallThickness / 2f;
            // Convert the world-space hover normal into the member's local space.
            Vector3 localNormal = Member.transform.InverseTransformDirection(hoverNormal);

            // Determine which axis (x or z) has the dominant contribution.
            Vector3 edgeOffset = Vector3.zero;
            if (Mathf.Abs(localNormal.x) >= Mathf.Abs(localNormal.y) && !isWallCentered)
            {
                edgeOffset = (localHitPoint.y > 0) ? Member.transform.up * halfExtent
                                                 : -Member.transform.up * halfExtent;
            }
            else if (!isWallCentered)
            {
                edgeOffset = (localHitPoint.x >= 0) ? Member.transform.right * halfExtent
                                                                 : -Member.transform.right * halfExtent;
            }

            // Compute the wall position by offsetting from the member's center along the edge direction,
            // then adjust vertically using the hoverNormal.
            Vector3 wallPosition = Member.transform.position + edgeOffset + (hoverNormal * wallHeight / 2f);

            if (Member.structuralGroup != null && !allowOverlap)
            {
                Member.structuralGroup.walls.RemoveAll(walls => walls == null);
                WallManager overlappingWall = Member.structuralGroup.walls.FirstOrDefault(
                c => c && Vector3.Distance(c.transform.position, wallPosition) < 0.1f);
                if (overlappingWall != null)
                {
                    return;
                }
            }

            // Set the wall rotation so its forward is along the member and it aligns with the hover normal.
            Quaternion wallRotation = Quaternion.LookRotation(Member.transform.forward, hoverNormal);

            Vector3 wallScale = new Vector3(wallThickness, wallHeight - thickness, wallWidth - thickness);

            if (buildSettings.matchMemberLength == true)
            {
                wallScale = new Vector3(wallThickness, wallHeight - thickness, memberLength - thickness);
            }

            // Save the current matrix.
            Matrix4x4 oldMatrix = Handles.matrix;
            Handles.matrix = Matrix4x4.TRS(wallPosition, wallRotation, Vector3.one);

            // Draw the ghost wall as a wireframe cube.
            Handles.color = new Color(1, 0, 1, 0.5f);
            Handles.DrawWireCube(Vector3.zero, wallScale);

            // Draw a button to allow spawning the wall.
            Handles.matrix *= Matrix4x4.Scale(wallScale);
            float handleHalfSize = 1f;
            bool clicked = Handles.Button(Vector3.zero, Quaternion.identity, handleHalfSize, handleHalfSize, Handles.CubeHandleCap);

            if (clicked)
            {
                Transform structuralMemberParent = Member.GetComponentInParent<StructuralGroupManager>().transform;

                if (structuralMemberParent != null)
                {
                    Undo.RecordObject(structuralMemberParent, "Spawn Wall");
                    SpawnWall(wallPosition, wallRotation, wallScale, structuralMemberParent, Member);
                    EditorUtility.SetDirty(structuralMemberParent);
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(structuralMemberParent.gameObject.scene);
                }
            }

            Handles.matrix = oldMatrix;
        }

        static void SpawnWall(Vector3 position, Quaternion rotation, Vector3 scale, Transform parent, StructuralMember Member)
        {
            if (buildSettings == null) return;

            int columns = buildSettings.wallColumnCellCount;
            int rows = buildSettings.wallRowCellCount;

            // Create a neutral parent to reset transformations
            GameObject WallParent = new GameObject("WallParent");
            Undo.RegisterCreatedObjectUndo(WallParent, "Create Wall Parent");
            WallParent.transform.position = position;
            WallParent.transform.rotation = rotation;
            WallParent.transform.localScale = Vector3.one;

            // Create the wall as a child of the neutral parent
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(wall, "Create Wall");
            wall.name = "Wall";

            if (buildSettings.wallMaterial != null)
            {
                wall.GetComponent<Renderer>().sharedMaterial = buildSettings.wallMaterial;
            }

            // Set clean local scale and positioning relative to the parent
            wall.transform.SetParent(WallParent.transform, false);
            wall.transform.localScale = new Vector3(
                scale.y,
                scale.z,
                scale.x
            );

            // Reparent the neutral parent to the StructuralMember for organizational hierarchy
            Undo.SetTransformParent(WallParent.transform, parent, "Parent Wall to Structure");

            wall.transform.localRotation = Quaternion.Euler(0, 90, 90);
            BoxCollider boxCollider = wall.GetComponent<BoxCollider>();
            if (boxCollider != null)
            {
                Undo.DestroyObjectImmediate(boxCollider);
            }

            // Add the WallManager script to the wall for functionality
            WallManager spawnedWall = Undo.AddComponent<WallManager>(wall);

            spawnedWall.WallPieceMass = buildSettings.wallPieceMass;
            spawnedWall.wallPieceHealth = buildSettings.wallPieceHealth;
            spawnedWall.wallPieceWindowHealth = buildSettings.wallPieceWindowHealth;
            spawnedWall.textureScaleX = buildSettings.wallTextureScaleX;
            spawnedWall.textureScaleY = buildSettings.wallTextureScaleY;

            StructuralGroupManager structuralGroup = parent.GetComponent<StructuralGroupManager>();
            if (structuralGroup)
                spawnedWall.structuralGroup = structuralGroup;

            spawnedWall.numColumns = columns;
            spawnedWall.numRows = rows;

            if (structuralGroup)
            {
                Undo.RecordObject(structuralGroup, "Add Wall to Structural Group");
                structuralGroup.walls.Add(spawnedWall);
                structuralGroup.walls.RemoveAll(walls => walls == null);
            }

            // --- FLAT LIST WALL GRID ---
            spawnedWall.wallGrid = new List<WallPiece>(new WallPiece[columns * rows]);

            if (buildSettings.defaultWallDesign != null)
            {
                ApplyDesign(spawnedWall, buildSettings.defaultWallDesign);
            }
            else
            {
                spawnedWall.glassMaterial = buildSettings.glassMaterial;
                spawnedWall.wallMaterial = buildSettings.wallMaterial;
                spawnedWall.BuildWall(spawnedWall.wallGrid, false, buildSettings);
            }
        }

        static void HighlightHitPoint(Vector3 point)
        {
            Handles.color = Color.yellow;
            Handles.DrawWireCube(point, Vector3.one * 0.5f);
        }

        static StructuralMember DirectionToMember(StructuralConnection connection, Vector3 direction)
        {
            float bestDot = -Mathf.Infinity;
            string bestSlot = "top";

            // Check cardinal directions
            CheckDir("top", connection.transform.up, direction, ref bestDot, ref bestSlot);
            CheckDir("bottom", -connection.transform.up, direction, ref bestDot, ref bestSlot);
            CheckDir("left", -connection.transform.right, direction, ref bestDot, ref bestSlot);
            CheckDir("right", connection.transform.right, direction, ref bestDot, ref bestSlot);
            CheckDir("front", connection.transform.forward, direction, ref bestDot, ref bestSlot);
            CheckDir("back", -connection.transform.forward, direction, ref bestDot, ref bestSlot);

            // Check diagonal directions
            CheckDir("top-right", (connection.transform.up + connection.transform.right).normalized, direction, ref bestDot, ref bestSlot);
            CheckDir("top-left", (connection.transform.up - connection.transform.right).normalized, direction, ref bestDot, ref bestSlot);
            CheckDir("bottom-right", (-connection.transform.up + connection.transform.right).normalized, direction, ref bestDot, ref bestSlot);
            CheckDir("bottom-left", (-connection.transform.up - connection.transform.right).normalized, direction, ref bestDot, ref bestSlot);
            CheckDir("front-right", (connection.transform.forward + connection.transform.right).normalized, direction, ref bestDot, ref bestSlot);
            CheckDir("front-left", (connection.transform.forward - connection.transform.right).normalized, direction, ref bestDot, ref bestSlot);
            CheckDir("back-right", (-connection.transform.forward + connection.transform.right).normalized, direction, ref bestDot, ref bestSlot);
            CheckDir("back-left", (-connection.transform.forward - connection.transform.right).normalized, direction, ref bestDot, ref bestSlot);
            CheckDir("top-front", (connection.transform.up + connection.transform.forward).normalized, direction, ref bestDot, ref bestSlot);
            CheckDir("top-back", (connection.transform.up - connection.transform.forward).normalized, direction, ref bestDot, ref bestSlot);
            CheckDir("bottom-front", (-connection.transform.up + connection.transform.forward).normalized, direction, ref bestDot, ref bestSlot);
            CheckDir("bottom-back", (-connection.transform.up - connection.transform.forward).normalized, direction, ref bestDot, ref bestSlot);

            return SlotToMember(connection, bestSlot);
        }

        static void CheckDir(string slot, Vector3 dir, Vector3 direction, ref float bestDot, ref string bestSlot)
        {
            float d = Vector3.Dot(direction.normalized, dir.normalized);
            if (d > bestDot)
            {
                bestDot = d;
                bestSlot = slot;
            }
        }
        static StructuralMember SlotToMember(StructuralConnection c, string slot)
        {
            return SlotLookup.FromString.TryGetValue(slot, out var s) ? c.Get(s) : null;
        }

    }
}