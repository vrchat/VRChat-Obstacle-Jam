using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System;
using System.Collections.Generic;
using Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.Udon;
using Button = UnityEngine.UIElements.Button;
using Image = UnityEngine.UIElements.Image;

///
/// Hi there! Momo the Monster here. You're welcome to look through this code and modify it for your purposes
/// It uses UIElements to do window layout instead of IMGUI
/// There's lots of experimental stuff in here - automatically finding and Injecting Udon Variables, Prefab placement, etc
/// If you make a cool change or improvement, submit a Pull Request to the GitHub repo so we can all check it out!
/// 

namespace VRC.Examples.Obstacle
{
    public class ObstacleCourseEditorWindow : EditorWindow
    {
        private const string class_h1 = "h1";
        private const string class_h2 = "h2";
        private const string class_flexRow = "flex-row";
        private const string class_sceneCheckpointContainer = "class-scene-checkpoint-container";
        private const string class_scenePowerUpContainer = "class-scene-powerUp-container";

        private const string variable_checkPointsArray = "checkPoints";
        private const string variable_powerUps = "powerUps";
        private const string object_recordPath = "RecordCamPath";
        
        private static ObstacleCourseEditorWindow _window;

        private ObstacleCourseAsset _courseAsset;

        private GameObject _courseGameObject;
        private GameObject _prefabToCreate;
        
        private PrefabTypes _prefabTypeToCreate;
        
        private VisualElement _rootView;
        private ScrollView _scrollView;

        private SerializedObject _serializedObject;
        
        private UdonBehaviour courseBehaviour;
        private UdonBehaviour playerDataManager;
        private UdonBehaviour playerModsManager;
        private UdonBehaviour scoreManager;

        private Texture2D _sceneViewTooltipBg;
        private GUIStyle _sceneViewTooltipStyle;

        private bool _showAdvanced = false;
        
        // Helps to open Starter Scene if we're in a scene without a Course Asset
        private readonly string _starterScenePath = "Assets/_WorldJam2/Scenes/StarterScene.unity";

        public static class Strings
        {
            public const string MenuHeader = "▶ Obstacle Course Toolkit";
        }

        private void SetupTooltipStyle() {
            // this variable is stored in the class
            // 1 pixel image, only 1 color to set
            _sceneViewTooltipBg = new Texture2D(1, 1, TextureFormat.RGBAFloat, false); 
            _sceneViewTooltipBg.SetPixel(0, 0, new Color(0, 0, 0, 0.75f));
            _sceneViewTooltipBg.Apply(); // not sure if this is necessary

            // basically just create a copy of the "none style"
            // and then change the properties as desired
            _sceneViewTooltipStyle = new GUIStyle(GUIStyle.none); 
            _sceneViewTooltipStyle.fontSize = 12;
            _sceneViewTooltipStyle.normal.textColor = new Color(255, 243, 0);
            _sceneViewTooltipStyle.normal.background = _sceneViewTooltipBg;
            _sceneViewTooltipStyle.contentOffset = new Vector2(10, 5);
            _sceneViewTooltipStyle.overflow = new RectOffset(0, 0, 0,10);
        }
        private void OnEnable()
        {
            SetupTooltipStyle();
            RefreshCourseAsset(); // load asset before building view
            InitializeRootView();
            SceneView.duringSceneGui += OnSceneGUI;
            EditorSceneManager.sceneOpened += OnSceneLoad;
            Refresh();
        }

        /// <summary>
        ///     When we close, unsubscribe from the SceneView
        /// </summary>
        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            EditorSceneManager.sceneOpened -= OnSceneLoad;
        }

        private void OnSceneLoad(Scene scene, OpenSceneMode mode)
        {
            Refresh();
        }

        [MenuItem(Strings.MenuHeader + "/Open Window")]
        private static void ShowWindow()
        {
            _window = GetWindow<ObstacleCourseEditorWindow>(Strings.MenuHeader, true);
            _window.titleContent = new GUIContent(Strings.MenuHeader);
        }

        private void InitializeRootView()
        {
            _rootView = rootVisualElement;
            _rootView.styleSheets.Add((StyleSheet) Resources.Load("ObstacleCourseWindow"));

            _rootView.Add(new Image
            {
                image = (Texture2D) Resources.Load("ObstacleEditorWindowHeader"),
                scaleMode = ScaleMode.ScaleToFit,
                name = "header-image",
            });

            _scrollView = MakeScrollView();

            _rootView.Add(_scrollView);

            Button button = new Button(Refresh) {text = "Refresh"};
            _rootView.Add(button);
        }

        private void Refresh()
        {
            _scrollView.Clear();
            
            RefreshCourseAsset();
            
            // Course Asset Data
            if (_courseAsset == null)
            {
                Debug.LogWarning($"This scene does not have a CourseAsset, not loading Utility Window");
                _scrollView.Add(new Label("This scene does not have a CourseAsset, add one before you can use this window."));
                _scrollView.Add(new Button(OpenStarterScene){ text = "Open Starter Scene"});
                return;
            }
            InjectVariableReferences();

            _showAdvanced = _courseAsset.showAdvanced;

            // Create serialized objects to bind other things to
            _serializedObject = new SerializedObject(_courseAsset);

            // Cache references to required UdonBehaviours
            playerModsManager = GetBehaviourFromProgram(_courseAsset.playerModsManagerProgram);
            playerDataManager = GetBehaviourFromProgram(_courseAsset.playerDataManagerProgram);
            scoreManager = GetBehaviourFromProgram(_courseAsset.scoreManagerProgram);
            courseBehaviour = GetBehaviourFromProgram(_courseAsset.courseProgram);

            AddCourseDataSection(_scrollView);
            AddCheckpointsSection(_scrollView);
            AddPlayerManagerSection(_scrollView);
            AddScoreManagerSection(_scrollView);
            AddPowerUpsSection(_scrollView);
            AddRecorderSection(_scrollView);

            _scrollView.Bind(_serializedObject);
        }
        
        private void OpenStarterScene()
        {
            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            EditorSceneManager.OpenScene(_starterScenePath);
        }

        private void InjectVariableReferences()
        {
            // Find GameObjects from names
            Dictionary<string, GameObject> resolvedSceneObjects = new Dictionary<string, GameObject>();
            foreach (string sceneObjectName in _courseAsset.variableToSceneObjectLookup.Values)
            {
                GameObject target = null;
                if (resolvedSceneObjects.ContainsKey(sceneObjectName))
                {
                    target = resolvedSceneObjects[sceneObjectName];
                }
                else
                {
                    target = GameObject.Find(sceneObjectName);
                }
                
                // Add to cache, even if it's null
                if (!resolvedSceneObjects.ContainsKey(sceneObjectName))
                {
                    resolvedSceneObjects.Add(sceneObjectName, target);
                }

                // Move to next object if we couldn't find the GameObject
                if (target == null)
                {
                    Debug.LogWarning($"Could not find Scene Object called {sceneObjectName}");
                }
            }
            
            // Use populated Dictionary to set all references
            var root = SceneManager.GetActiveScene().GetRootGameObjects();
            if (root.Length == 0)
            {
                Debug.LogWarning($"Could not resolve references without a scene open");
                return;
            }

            // Find every UdonBehaviour, and try to set each variable in our lookup to the resolved Scene Object
            foreach (GameObject gameObject in root)
            {
                foreach (UdonBehaviour ub in gameObject.GetComponentsInChildren<UdonBehaviour>(true))
                {
                    foreach (KeyValuePair<string,string> variableToName in _courseAsset.variableToSceneObjectLookup)
                    {
                        string variableName = variableToName.Key;
                        string sceneObjectName = variableToName.Value;
                        if (resolvedSceneObjects.TryGetValue(sceneObjectName, out GameObject targetGameObject))
                        {
                            // if(doLog) Debug.Log($"{ub.name}: Found {targetGameObject.name} for {sceneObjectName}");
                            if (targetGameObject != null)
                            {
                                if (ub.publicVariables.TryGetVariableType(variableName, out Type theType))
                                {
                                    if (theType == typeof(UdonBehaviour))
                                    {
                                        UdonBehaviour targetUB = targetGameObject.GetComponent<UdonBehaviour>();
                                        if (targetUB != null)
                                        {
                                            ub.publicVariables.TrySetVariableValue(variableName, targetUB);
                                        }
                                        else
                                        {
                                            Debug.LogWarning($"Trying to add UB for {variableName} but no UB found on {gameObject.name}");
                                        }
                                    }
                                    else if (theType == typeof(GameObject))
                                    {
                                        ub.publicVariables.TrySetVariableValue(variableName, targetGameObject);
                                    }
                                    else if (theType == typeof(CinemachineVirtualCamera))
                                    {
                                        CinemachineVirtualCamera targetCam = targetGameObject.GetComponent<CinemachineVirtualCamera>();
                                        if (targetCam != null)
                                        {
                                            ub.publicVariables.TrySetVariableValue(variableName, targetCam);
                                        }
                                    }
                                    else if (theType == typeof(Text))
                                    {
                                        Text targetText = targetGameObject.GetComponent<Text>();
                                        if (targetText != null)
                                        {
                                            ub.publicVariables.TrySetVariableValue(variableName, targetText);
                                        }
                                    }
                                    else
                                    {
                                        Debug.LogWarning($"Cannot resolve type {theType} yet. Will not set {variableName} on {targetGameObject.name}");
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private void ShowPrefabOverlay()
        {
            if (_prefabToCreate == null) return;

            Handles.BeginGUI();
            int width = 170;
            int height = 60;
            var mousePosition = Event.current.mousePosition;
            mousePosition.x -= (width / 2);
            mousePosition.y += (height / 2);
            GUILayout.BeginArea(new Rect(mousePosition, new Vector2(width, height)));
            GUILayout.Box(new GUIContent($"Space ▶️ Create {_prefabToCreate.name}\nEsc ▶️ Exit Creation Mode"), _sceneViewTooltipStyle);
            GUILayout.EndArea();
            Handles.EndGUI();
        }

        private UdonBehaviour GetBehaviourFromProgram(AbstractUdonProgramSource asset, GameObject[] roots = null)
        {
            if (roots == null)
            {
                roots = SceneManager.GetActiveScene().GetRootGameObjects();
            }

            foreach (GameObject gameObject in roots)
            {
                if (gameObject == null) continue;
                foreach (UdonBehaviour udonBehaviour in gameObject.GetComponentsInChildren<UdonBehaviour>()) 
                    if (udonBehaviour.programSource == asset)
                        {
                            return udonBehaviour;
                        }
            }

            return null;
        }

        private VisualElement MakeSection()
        {
            VisualElement element = new VisualElement();
            element.AddToClassList(class_flexRow);
            return element;
        }

        private static void PersistFoldoutValue(Foldout foldout)
        {
            foldout.value = SessionState.GetBool(foldout.text, false);
            foldout.RegisterValueChangedCallback(evt =>
            {
                SessionState.SetBool(foldout.text, evt.newValue);
            });
        }

        private ScrollView MakeScrollView()
        {
            ScrollView scrollView = new ScrollView();
            scrollView.viewDataKey = "main-scroll-view";
            scrollView.name = "main-scroll-view";
            //scrollView.stretchContentWidth = true;
            scrollView.verticalScroller.slider.pageSize = 100;
            scrollView.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.target.GetType().DeclaringType == typeof(ObjectField))
                {
                    VisualElement t = evt.target as VisualElement;
                    ObjectField oField = t.parent.parent as ObjectField;
                    if (oField != null && oField.value as GameObject != null)
                    {
                        _prefabToCreate = oField.value as GameObject;
                        if (_prefabToCreate != null && _prefabToCreate.scene.isLoaded)
                        {
                            // scene object, not a prefab to create
                            _prefabToCreate = null;
                        }
                        else
                        {
                            // get type from bindingPath - a little obtuse, but a solid route while using propertyFields
                            var dotIndex = oField.bindingPath.IndexOf(".");
                            var id = dotIndex > 0 ? oField.bindingPath.Substring(0, dotIndex) : oField.bindingPath;
                            if (!Enum.TryParse(id, true, out _prefabTypeToCreate))
                                // don't create non-handled prefabs, like PlayerObject
                                _prefabToCreate = null;
                        }
                    }
                }
                else
                {
                    _prefabToCreate = null;
                    SceneView.RepaintAll();
                }
            }, TrickleDown.TrickleDown);
            return scrollView;
        }

        #region Section Creation

        private void AddCourseDataSection(VisualElement parent)
        {
            VisualElement courseSection = MakeSection();
            courseSection.name = "course-section";
            parent.Add(courseSection);

            // Add header label
            var courseName =
                _courseAsset != null ? $"{_courseAsset.name}" : "Could not find Course Asset";
            Label courseHeader = new Label(courseName);
            courseHeader.AddToClassList(class_h1);
            courseSection.Add(courseHeader);

            // Add course data selector
            ObjectField courseField = new ObjectField
            {
                objectType = typeof(ObstacleCourseAsset),
                value = _courseAsset
            };
            courseField.RegisterValueChangedCallback(evt =>
            {
                ObstacleCourseAsset a = (ObstacleCourseAsset) evt.newValue;
                _courseAsset = a;
                Refresh();
            });
            courseSection.Add(courseField);
            if (_showAdvanced)
            {
                courseSection.Add(new PropertyField(_serializedObject.FindProperty(nameof(ObstacleCourseAsset.creationMask))));
            }
        }

        private GameObject[] GetSceneCheckpoints()
        {
            if (courseBehaviour.publicVariables.TryGetVariableValue(variable_checkPointsArray,
                out GameObject[] sceneCheckpoints))
            {
                return sceneCheckpoints;
            }
            else return null;
        }
        
        private void AddCheckpointsSection(VisualElement parent)
        {
            VisualElement checkpointsSection = MakeFoldoutHeader("Checkpoints", class_h1);

            // Program Picker
            if (_showAdvanced)
            {
                checkpointsSection.Add(new PropertyField(_serializedObject.FindProperty(nameof(ObstacleCourseAsset.courseProgram)))); 
            }

            // Prefabs Picker
            checkpointsSection.Add(
                new PropertyField(_serializedObject.FindProperty(nameof(ObstacleCourseAsset.checkpointPrefabs))));

            // In-Scene Checkpoints
            Foldout sceneCheckpointsContainer = new Foldout
            {
                text = "Checkpoints In Scene",
                pickingMode = PickingMode.Position
            };
            PersistFoldoutValue(sceneCheckpointsContainer);
            checkpointsSection.Add(sceneCheckpointsContainer);

            // Focus on In-Scene Checkpoints when selected
            if (courseBehaviour != null)
            {
                var sceneCheckpoints = GetSceneCheckpoints();
                if (sceneCheckpoints != null)
                {
                    {
                        for (int i = 0; i < sceneCheckpoints.Length; i++)
                        {
                            GameObject checkpoint = sceneCheckpoints[i];

                            // remove destroyed checkpoints
                            if (checkpoint == null)
                            {
                                var list = new List<GameObject>(sceneCheckpoints);
                                list.RemoveAt(i);
                                courseBehaviour.publicVariables.TrySetVariableValue(variable_checkPointsArray,
                                    list.ToArray());
                                Refresh();
                                return;
                            }

                            // Rename Checkpoint
                            if (i == 0)
                            {
                                checkpoint.name = "Start Gate";
                            }
                            else if (i == sceneCheckpoints.Length - 1)
                            {
                                checkpoint.name = "Finish Gate";
                            }
                            else
                            {
                                checkpoint.name = $"Checkpoint {i}";
                            }

                            var behaviour = GetBehaviourFromProgram(_courseAsset.checkpointProgram, new[] {checkpoint});
                            if (behaviour != null)
                            {
                                behaviour.publicVariables.TrySetVariableValue("index", i);
                            }

                            var container = new VisualElement();
                            container.AddToClassList(class_sceneCheckpointContainer);
                            ObjectField oField = new ObjectField
                            {
                                allowSceneObjects = true,
                                objectType = typeof(SceneAsset),
                                value = checkpoint,
                                userData = i,
                            };
                            oField.RegisterCallback<MouseDownEvent>(evt =>
                            {
                                GameObject g = oField.value as GameObject;
                                UnityEditor.Tools.current = Tool.Move;
                                Selection.activeTransform = g.transform;
                                SceneView.lastActiveSceneView.Frame(
                                    new Bounds(g.transform.position, g.transform.lossyScale * 7), false);
                            }, TrickleDown.TrickleDown);

                            container.Add(oField);
                            container.Add(new Button(() =>
                            {
                                int index = (int) oField.userData;
                                // don't move top one
                                if (index > 0)
                                {
                                    // swap places with index before this one
                                    sceneCheckpoints[index] = sceneCheckpoints[index - 1];
                                    sceneCheckpoints[index - 1] = checkpoint;
                                    courseBehaviour.publicVariables.TrySetVariableValue(variable_checkPointsArray,
                                        sceneCheckpoints);
                                    Refresh();
                                }
                            }) {text = "▲"});
                            container.Add(new Button(() =>
                            {
                                int index = (int) oField.userData;
                                // don't move last one
                                if (index < sceneCheckpoints.Length - 1)
                                {
                                    // swap places with index after this one
                                    sceneCheckpoints[index] = sceneCheckpoints[index + 1];
                                    sceneCheckpoints[index + 1] = checkpoint;
                                    courseBehaviour.publicVariables.TrySetVariableValue(variable_checkPointsArray,
                                        sceneCheckpoints);
                                    Refresh();
                                }
                            }) {text = "▼"});
                            container.Add(new Button(() =>
                            {
                                int index = (int) oField.userData;
                                // don't move top one
                                if (index > 0)
                                {
                                    // remove from scene, then from array, update array variable on Course
                                    DestroyImmediate(sceneCheckpoints[index]);
                                    var list = new List<GameObject>(sceneCheckpoints);
                                    list.RemoveAt(index);
                                    courseBehaviour.publicVariables.TrySetVariableValue(variable_checkPointsArray,
                                        list.ToArray());
                                    Refresh();
                                }
                            }) {text = "X"});
                            sceneCheckpointsContainer.Add(container);
                        }
                    }
                }
            }
            
            checkpointsSection.Add(new PropertyField(_serializedObject.FindProperty(nameof(ObstacleCourseAsset.showCheckpointLabels))));
            checkpointsSection.Add(new PropertyField(_serializedObject.FindProperty(nameof(ObstacleCourseAsset.checkpointLabelOffset))));
                
            parent.Add(checkpointsSection);
        }

        private void AddPlayerManagerSection(VisualElement parent)
        {
            // Gathering requirements
            if (playerDataManager == null)
            {
                Debug.LogError("You need a PlayerDataManager in your project, should show instructions here");
                return;
            }

            VRCObjectPool pool = playerDataManager.GetComponent<VRCObjectPool>();
            if (pool == null)
            {
                Debug.LogError("You need an Object Pool on your PlayerDataManager, should show instructions here");
                return;
            }

            // Start of Visual Setup
            VisualElement section = MakeFoldoutHeader("Player Manager", class_h1);

            // Program Picker
            if (_showAdvanced)
            {
                section.Add(new PropertyField(_serializedObject.FindProperty(nameof(ObstacleCourseAsset.playerDataManagerProgram))));
            }

            // Prefabs Picker
            section.Add(new PropertyField(_serializedObject.FindProperty(nameof(ObstacleCourseAsset.playerObjectPrefab))));

            // Should only show playercount section if we have a prefab above
            VisualElement playerCountSection = MakeSection();

            playerCountSection.Add(new Label("Number of Players:"));
            // NOTE: was getting the error `Trying to read value of type Float while reading a value of type Enum`
            // when using an IntegerField. Not sure why, but this fixes it for now.
            var playerCountField = new LongField(2)
             {
                 value = _courseAsset.playerCount,
                 isDelayed = true,
             };
             playerCountField.RegisterValueChangedCallback(evt =>
             {
                 // Limit values between 1 and 40
                 int value = (int)Mathf.Clamp(evt.newValue, 1, 40);
                 if (evt.newValue < 1 || evt.newValue > 40)
                 {
                     playerCountField.value = value;
                     return;
                 }
            
                 // Deselect field
                 playerCountField.Blur();
            
                 _courseAsset.playerCount = value;
            
                 // Clear out Children of PlayerDataManager GameObject
                 for (var i = playerDataManager.transform.childCount - 1; i >= 0; i--)
                     DestroyImmediate(playerDataManager.transform.GetChild(i).gameObject);
            
                 // make list for new objects, to use for pool later
                 var newObjects = new List<GameObject>();
                 
                 // add new children to manager
                 for (var i = 0; i < value; i++)
                 {
                     GameObject newPlayer = Instantiate(_courseAsset.playerObjectPrefab, Vector3.zero,
                         Quaternion.identity, playerDataManager.transform);
                     newPlayer.name = $"PlayerData{i}";
                     UdonBehaviour player = newPlayer.GetComponent<UdonBehaviour>();
                     if (player == null)
                     {
                         Debug.LogError("Your Player Object needs an UdonBehaviour!");
                         return;
                     }
                     newObjects.Add(newPlayer);
                 }
            
                 // add new children to pool
                 pool.Pool = newObjects.ToArray();
                 Refresh();
             });

            // add to hierarchy
            playerCountSection.Add(playerCountField);
            section.Add(playerCountSection);
            parent.Add(section);
        }

        private void AddScoreManagerSection(VisualElement parent)
        {
            VisualElement header = MakeFoldoutHeader("Score Manager", class_h1);

            // Program Picker
            if (_showAdvanced)
            {
                header.Add(new PropertyField(_serializedObject.FindProperty(nameof(ObstacleCourseAsset.scoreManagerProgram))));
            }

            // Prefabs Picker
            header.Add(new PropertyField(_serializedObject.FindProperty(nameof(ObstacleCourseAsset.scoreObjectPrefab))));

            VisualElement section = MakeSection();
            header.Add(section);

            section.Add(new Label("Number of Scores to Show:"));
            // NOTE: was getting the error `Trying to read value of type Float while reading a value of type Enum`
            // when using an IntegerField. Not sure why, but this fixes it for now.
            var scoreCountField = new LongField()
            {
                value = _courseAsset.scoreFieldsCount,
                isDelayed = true,
            };
            
            // Add handler for when the value changes
            scoreCountField.RegisterValueChangedCallback(evt =>
            {
                // Limit values between 1 and 99
                int value = (int)Mathf.Clamp(evt.newValue, 1, 99);
                if (evt.newValue < 1 || evt.newValue > 99)
                {
                    scoreCountField.value = value;
                    return;
                }
            
                // Get container from ScoreManager
                Transform container = null;
                if (scoreManager.publicVariables.TryGetVariableValue("fieldsContainer", out var containerValue))
                    container = (Transform) containerValue;
            
                if (container == null)
                {
                    Debug.LogError(
                        "Could not create Score Fields without a valid 'fieldsContainer' on the ScoreManager");
                    return;
                }
            
                // Clear out Children of ScoreManager GameObject
                for (var i = container.transform.childCount - 1; i >= 0; i--)
                    DestroyImmediate(container.transform.GetChild(i).gameObject);
            
                // add new children to manager
                var newObjects = new List<Transform>();
                for (var i = 0; i < value; i++)
                {
                    GameObject newField = Instantiate(_courseAsset.scoreObjectPrefab, container, false);
                    newField.name = $"ScoreField{i}";
                    newObjects.Add(newField.transform);
                }
            
                scoreManager.publicVariables.TrySetVariableValue("fields", newObjects.ToArray());
            
                // Deselect field
                scoreCountField.Blur();
                _courseAsset.scoreFieldsCount = value;
            });
            section.Add(scoreCountField);

            parent.Add(header);
        }

        private void AddPowerUpsSection(VisualElement parent)
        {
            VisualElement section = MakeFoldoutHeader("Power Ups", class_h1);

            // Program Picker
            if (_showAdvanced)
            {
                section.Add(new PropertyField(_serializedObject.FindProperty(nameof(ObstacleCourseAsset.playerModsManagerProgram))));
            }

            // Prefabs Picker
            section.Add(
                new PropertyField(_serializedObject.FindProperty(nameof(ObstacleCourseAsset.powerUpPrefabs))));

            // In-Scene PowerUps
            Foldout scenePowerUpsFoldout = new Foldout
            {
                text = "PowerUps In Scene",
                pickingMode = PickingMode.Position,
                value = false
            };
            PersistFoldoutValue(scenePowerUpsFoldout);

            // Focus on In-Scene PowerUps when selected
            if (playerModsManager != null)
                if (playerModsManager.publicVariables.TryGetVariableValue(variable_powerUps, out GameObject[] scenePowerUps))
                    for (int i = 0; i < scenePowerUps.Length; i++)
                    {
                        GameObject powerUp = scenePowerUps[i];
                        
                        // remove destroyed powerUps
                        if (powerUp == null)
                        {
                            var list = new List<GameObject>(scenePowerUps);
                            list.RemoveAt(i);
                            playerModsManager.publicVariables.TrySetVariableValue(variable_powerUps, list.ToArray());
                            Refresh();
                            return;
                        }
                        
                        var container = new VisualElement();
                        container.AddToClassList(class_scenePowerUpContainer);
                        ObjectField oField = new ObjectField
                        {
                            allowSceneObjects = true,
                            objectType = typeof(SceneAsset),
                            value = powerUp,
                            userData = i,
                        };

                        GameObject targetObject = oField.value as GameObject;
                        oField.RegisterCallback<MouseDownEvent>(evt =>
                        {
                            UnityEditor.Tools.current = Tool.Move;
                            Selection.activeTransform = targetObject.transform;
                            SceneView.lastActiveSceneView.Frame(new Bounds(targetObject.transform.position, targetObject.transform.lossyScale * 5), false);
                        }, TrickleDown.TrickleDown);
                        container.Add(oField);

                        var behaviour = GetBehaviourFromProgram(_courseAsset.powerUpProgram, new []{targetObject});
                        if (behaviour != null)
                        {
                            container.Add(MakePowerUpField(behaviour, "speedChange", "speed"));
                            container.Add(MakePowerUpField(behaviour, "jumpChange", "jump"));
                            container.Add(MakePowerUpField(behaviour, "effectDuration", "duration"));
                        }
                        scenePowerUpsFoldout.Add(container);
                    }

            section.Add(scenePowerUpsFoldout);

            section.Add(MakeHeader("Defaults", class_h2));

            // Make moveSpeed field
            PropertyField moveSpeed =
                new PropertyField(_serializedObject.FindProperty(nameof(ObstacleCourseAsset.moveSpeed)));
            moveSpeed.RegisterCallback<ChangeEvent<float>>(evt =>
            {
                playerModsManager.publicVariables.TrySetVariableValue("runSpeed", evt.newValue);
            });
            section.Add(moveSpeed);
            
            // Make jumpImpulse field
            PropertyField jumpImpulse =
                new PropertyField(_serializedObject.FindProperty(nameof(ObstacleCourseAsset.jumpImpulse)));
            jumpImpulse.RegisterCallback<ChangeEvent<float>>(evt =>
            {
                playerModsManager.publicVariables.TrySetVariableValue("jumpImpulse", evt.newValue);
            });
            
            section.Add(jumpImpulse);

            parent.Add(section);
        }

        private VisualElement MakePowerUpField(UdonBehaviour behaviour, string variableName, string label)
        {
            if (behaviour.publicVariables.TryGetVariableValue(variableName, out float v))
            {
                var field = new FloatField(label)
                {
                    value = v
                };
                field.RegisterValueChangedCallback(evt =>
                {
                    behaviour.publicVariables.TrySetVariableValue(variableName, evt.newValue);
                });
                return field;
            }

            return null;
        }
        
        private CinemachineSmoothPath _recordingPath;
        private void AddRecorderSection(VisualElement parent)
        {
            VisualElement section = MakeFoldoutHeader("Recording", class_h1);

            // Make yOffset field
            PropertyField yOffset = new PropertyField(_serializedObject.FindProperty(nameof(ObstacleCourseAsset.recordPathYOffset)));
            yOffset.RegisterCallback<ChangeEvent<float>>(evt =>
            {
                if (_courseAsset.autoUpdateCheckpoints)
                {
                    UpdateRecorderPath();
                }
            });
            
            var updatePathButton = new Button(UpdateRecorderPath)
            {
                text = "Update Path from Checkpoints"
            };
            
            // Make auto-update fields
            PropertyField autoUpdateCheckpoints =
                new PropertyField(_serializedObject.FindProperty(nameof(ObstacleCourseAsset.autoUpdateCheckpoints)));
            autoUpdateCheckpoints.RegisterCallback<ChangeEvent<bool>>(evt =>
            {
                updatePathButton.visible = !evt.newValue;
                evt.StopPropagation();
            });
            
            section.Add(yOffset);
            section.Add(updatePathButton);
            section.Add(autoUpdateCheckpoints);

            parent.Add(section);
        }

        private void UpdateRecorderPath()
        {
            var recordingObject = GameObject.Find(object_recordPath);
            if (recordingObject == null) return;

            _recordingPath = recordingObject.GetComponent<CinemachineSmoothPath>();
            if (_recordingPath == null) return;
            
            // We now have a good ref to _recordingPath
            // update its checkpoints
            var checkpoints = GetSceneCheckpoints();
            if (checkpoints != null)
            {
                var waypoints = new List<CinemachineSmoothPath.Waypoint>();
                var positionOffset = new Vector3(0, _courseAsset.recordPathYOffset, 0);
                foreach (var checkpoint in checkpoints)
                {
                    if (checkpoint == null) continue;
                    
                    var waypoint = new CinemachineSmoothPath.Waypoint()
                    {
                        position = checkpoint.transform.position + positionOffset,
                        roll = checkpoint.transform.rotation.eulerAngles.z,
                    };
                    waypoints.Add(waypoint);
                }

                // set path waypoints from list
                _recordingPath.m_Waypoints = waypoints.ToArray();
                _recordingPath.InvalidateDistanceCache();
            }
        }

        #endregion

        private VisualElement MakeFoldoutHeader(string text, string className)
        {
            Foldout headerLabel = new Foldout
            {
                text = text
            };
            headerLabel.AddToClassList(className);
            PersistFoldoutValue(headerLabel);
            return headerLabel;
        }

        private VisualElement MakeHeader(string text, string className)
        {
            Label headerLabel = new Label(text);
            headerLabel.AddToClassList(className);
            return headerLabel;
        }

        private void RefreshCourseAsset()
        {
            ObstacleCourseAsset asset;
            if (TryFindCourseAsset(out asset))
            {
                LoadAsset(asset);
            }
            else
            {
                _courseAsset = null;
            }
        }

        private void LoadAsset(ObstacleCourseAsset asset)
        {
            _courseAsset = asset;
        }

        private bool TryFindCourseAsset(out ObstacleCourseAsset asset)
        {
            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (GameObject gameObject in roots)
            {
                ObstacleCourseData d = gameObject.transform.GetComponentInChildren<ObstacleCourseData>(true);
                if (d != null)
                {
                    asset = d.asset;
                    _courseGameObject = gameObject;
                    return true;
                }
            }

            asset = null;
            return false;
        }

        private GameObject _tempPrefab = null;
        
        private void OnSceneGUI(SceneView sceneView)
        {
            if (_courseAsset == null) return;

            // Event.current houses information on scene view input this cycle
            Event current = Event.current;
            
            HandlePrefabPlacement(current);
        }

        private void HandlePrefabPlacement(Event current)
        {
            ShowPrefabOverlay();

            // Create tempPrefab when mouse enters window
            if (_prefabToCreate != null && current.type == EventType.MouseEnterWindow)
            {
                SceneView.lastActiveSceneView.Focus();
                _tempPrefab = Instantiate(_prefabToCreate);
                _tempPrefab.hideFlags = HideFlags.HideAndDontSave;
            }

            // Destroy tempPrefab when mouse leaves window
            if (_tempPrefab != null && current.type == EventType.MouseLeaveWindow)
            {
                DestroyImmediate(_tempPrefab);
                _tempPrefab = null;
            }
            
            // Destroy tempPrefab and deselect _prefabToCreate when esc pressed
            if (_tempPrefab != null && current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape)
            {
                ExitCreateMode();
            }

            // Move tempPrefab to raycasted position when mouse moves
            if (_tempPrefab != null && _prefabToCreate != null && current.type == EventType.MouseMove)
            {
                SceneView.lastActiveSceneView.Focus();
                
                RaycastHit hit;
                if (GetHitForScreenPosition(current.mousePosition, out hit, _courseAsset.creationMask))
                {
                    Quaternion newRotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
                    Vector3 newPosition = hit.point;
                    _tempPrefab.transform.SetPositionAndRotation(newPosition, newRotation);
                }
            }

            // Create new prefab from temp when creation button pressed (space for now)
            if (_tempPrefab != null && _prefabToCreate != null && _prefabTypeToCreate != PrefabTypes.None &&
                current.type == EventType.KeyDown && current.keyCode == KeyCode.Space)
            {
                RaycastHit hit;
                if (GetHitForScreenPosition(current.mousePosition, out hit, _courseAsset.creationMask))
                {
                    Quaternion newRotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
                    Vector3 newPosition = hit.point;
                    GameObject g = Instantiate(_prefabToCreate, newPosition, newRotation,
                        _courseGameObject.transform);
                    InitializeSceneObject(g, _prefabTypeToCreate);
                    
                    Undo.RegisterCreatedObjectUndo(g, $"Undo Place {_prefabToCreate}");
                }
            }
        }

        private void ExitCreateMode()
        {
            if (_tempPrefab != null)
            {
                DestroyImmediate(_tempPrefab);
            }
            _tempPrefab = null;
            _prefabToCreate = null;
        }

        private void InitializeSceneObject(GameObject gameObject, PrefabTypes prefabTypeToCreate)
        {
            switch (prefabTypeToCreate)
            {
                case PrefabTypes.checkpointPrefabs:
                    // parent to CourseManager
                    gameObject.transform.SetParent(courseBehaviour.transform);
                    // add to checkpoints object, probably at end of sequence
                    if (courseBehaviour != null)
                    {
                        var checkpoints = GetSceneCheckpoints();
                        if (checkpoints != null)
                        {
                            gameObject.name = $"Checkpoint {checkpoints.Length}";
                            Array.Resize(ref checkpoints, checkpoints.Length + 1);
                            checkpoints[checkpoints.Length - 1] = gameObject;
                            courseBehaviour.publicVariables.TrySetVariableValue(variable_checkPointsArray, checkpoints);
                        }
                    }

                    break;
                case PrefabTypes.powerupPrefabs:
                    if (playerModsManager != null)
                    {
                        // parent to PlayerModsManager
                        gameObject.transform.SetParent(playerModsManager.transform);

                        if (playerModsManager.publicVariables.TryGetVariableValue(variable_powerUps, out GameObject[] pups))
                        {
                            Array.Resize(ref pups, pups.Length + 1);
                            pups[pups.Length - 1] = gameObject;
                            playerModsManager.publicVariables.TrySetVariableValue(variable_powerUps, pups);
                        }
                    }

                    break;
            }
            
            ExitCreateMode();
            UnityEditor.Tools.current = Tool.Move;
            Selection.activeTransform = gameObject.transform;

            Refresh();
        }

        public static bool GetHitForScreenPosition(Vector2 position, out RaycastHit hit, LayerMask mask)
        {
            Camera cam = Camera.current;

            if (cam != null)
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(position);
                if (Physics.Raycast(ray, out hit, 100, mask))
                    if (hit.collider != null)
                        return true;
            }

            hit = new RaycastHit();
            return false;
        }

        private enum PrefabTypes
        {
            None,
            checkpointPrefabs,
            powerupPrefabs
        }
    } // class end
} // namespace end