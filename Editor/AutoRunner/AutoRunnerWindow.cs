using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using SaintsField.Editor.Core;
using SaintsField.Editor.Utils;
using SaintsField.Playa;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SaintsField.Editor.AutoRunner
{
    public class AutoRunnerWindow: SaintsEditorWindow
    {
        private const string EditorResourcePath = "SaintsField/AutoRunner.asset";

#if SAINTSFIELD_DEBUG
        [MenuItem("Saints/Auto Runner")]
#else
        [MenuItem("Window/Saints/Auto Runner")]
#endif
        public static void OpenWindow()
        {
            EditorWindow window = GetWindow<AutoRunnerWindow>(false, "SaintsField Auto Runner");
            window.Show();
        }

        public override Type EditorDrawerType => typeof(AutoRunnerEditor);

        // [Button]
        public override Object GetTarget()
        {
            AutoRunnerWindow autoRunnerWindow = EditorGUIUtility.Load(EditorResourcePath) as AutoRunnerWindow;
            Debug.Log($"load: {autoRunnerWindow}");
            return autoRunnerWindow == null
                ? this
                : autoRunnerWindow;
        }

        [Ordered, LeftToggle] public bool buildingScenes;

        [Ordered, ShowInInspector, PlayaShowIf(nameof(buildingScenes))]
        private static SceneAsset[] InBuildScenes => EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(each => AssetDatabase.LoadAssetAtPath<SceneAsset>(each.path))
            .ToArray();

        [Ordered] public SceneAsset[] sceneList = {};

        [Serializable]
        public struct FolderSearch
        {
            [AssetFolder]
            public string path;
            public string searchPattern;
            [ShowIf(nameof(searchPattern))]
            public SearchOption searchOption;

            public override string ToString()
            {
                return string.IsNullOrEmpty(searchPattern)
                    ? path
                    : $"{path}:{searchPattern}({searchOption})";
            }
        }

        [Ordered, RichLabel("$" + nameof(FolderSearchLabel))] public FolderSearch[] folderSearches = {};

        private string FolderSearchLabel(FolderSearch fs, int index) => string.IsNullOrEmpty(fs.path)
            ? $"Element {index}"
            : fs.ToString();

        private static IEnumerable<SerializedObject> GetSerializedObjectFromFolderSearch(FolderSearch folderSearch)
        {
            // var fullPath = Path.Join(Directory.GetCurrentDirectory(), folderSearch.path).Replace("/", "\\");
            Debug.Log($"#AutoRunner# Processing path {folderSearch.path}: {folderSearch.searchPattern}, {folderSearch.searchOption}");
            string[] listed = string.IsNullOrEmpty(folderSearch.searchPattern)
                ? Directory.GetFiles(folderSearch.path)
                : Directory.GetFiles(folderSearch.path, folderSearch.searchPattern, folderSearch.searchOption);
            foreach (string file in listed.Where(each => !each.EndsWith(".meta")).Select(each => each.Replace("\\", "/")))
            {
                // Debug.Log($"#AutoRunner# Processing {file}");
                Object obj = AssetDatabase.LoadAssetAtPath<Object>(file);
                if (obj == null)
                {
                    Debug.Log($"#AutoRunner# Skip null object {file} under folder {folderSearch.path}");
                    continue;
                }

                SerializedObject so;
                try
                {
                    so = new SerializedObject(obj);
                }
                catch (Exception e)
                {
                    Debug.Log($"#AutoRunner# Skip {obj} as it's not a valid object: {e}");
                    continue;
                }

                yield return so;
            }
        }

        private static IEnumerable<SerializedObject> GetSerializedObjectFromCurrentScene(string scenePath)
        {
            Debug.Log($"#AutoRunner# Processing {scenePath}");
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            GameObject[] rootGameObjects = scene.GetRootGameObjects();
            // Scene scene = SceneManager.GetActiveScene();
            // GameObject[] rootGameObjects = SceneManager.GetActiveScene().GetRootGameObjects();
            Debug.Log($"#AutoRunner# Scene {scene.path} has {rootGameObjects.Length} root game objects");
            foreach (GameObject rootGameObject in rootGameObjects)
            {
                foreach (Component comp in rootGameObject.transform.GetComponentsInChildren<Component>(true))
                {
                    SerializedObject so;
                    try
                    {
                        so = new SerializedObject(comp);
                    }
                    catch (Exception e)
                    {
                        Debug.Log($"#AutoRunner# Skip {comp} as it's not a valid object: {e}");
                        continue;
                    }

                    yield return so;
                }
            }
        }

        [Ordered, LeftToggle] public bool skipHiddenFields;

        private IReadOnlyDictionary<Type, IReadOnlyList<(bool isSaints, Type drawerType)>> _typeToDrawer;

        [Ordered, ReadOnly, ProgressBar(maxCallback: nameof(ResourceTotal)), BelowInfoBox("$" + nameof(_processingMessage))] public int processing;

        private string _processingMessage;

        private int ResourceTotal()
        {
            return (buildingScenes? InBuildScenes.Length: 0) + sceneList.Length + folderSearches.Length;
        }

        [Ordered, ShowInInspector, PlayaShowIf(nameof(_processedItemCount))] private int _processedItemCount;

        [Ordered, Button("Run!")]
        // ReSharper disable once UnusedMember.Local
        private IEnumerator RunAutoRunners()
        {
            _processedItemCount = 0;
            processing = 0;
            string[] scenePaths = sceneList
                .Select(AssetDatabase.GetAssetPath)
                .Concat(buildingScenes
                    ? EditorBuildSettings.scenes.Where(each => each.enabled).Select(each => each.path)
                    : Array.Empty<string>())
                .ToArray();

            List<(object, IEnumerable<SerializedObject>)> sceneSoIterations =
                new List<(object, IEnumerable<SerializedObject>)>();

            foreach (string scenePath in scenePaths)
            {
                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                sceneSoIterations.Add((scene, GetSerializedObjectFromCurrentScene(scenePath)));
                yield return null;
            }

            // IEnumerable<(object, IEnumerable<SerializedObject>)> sceneSoIterations = scenePaths.Select(each => (
            //     (object)AssetDatabase.LoadAssetAtPath<SceneAsset>(each),
            //     GetSerializedObjectFromScenePath(each)
            // ));
            List<(object, IEnumerable<SerializedObject>)> folderSoIterations =
                new List<(object, IEnumerable<SerializedObject>)>();
            foreach (FolderSearch folderSearch in folderSearches)
            {
                folderSoIterations.Add(($"{folderSearch.path}:{folderSearch.searchPattern}:{folderSearch.searchOption}", GetSerializedObjectFromFolderSearch(folderSearch)));
                yield return null;
            }
            // IEnumerable<(object, IEnumerable<SerializedObject>)> folderSoIterations = folderSearches.Select(each => (
            //     (object)$"{each.path}{each.searchPattern}",
            //     GetSerializedObjectFromFolderSearch(each)
            // ));

            // List<AutoRunnerResult> autoRunnerResults = new List<AutoRunnerResult>();
            results.Clear();
            foreach ((object target, IEnumerable<SerializedObject> serializedObjects) in sceneSoIterations.Concat(folderSoIterations))
            {
                foreach (SerializedObject so in serializedObjects)
                {
                    _processedItemCount++;
                    if(_processedItemCount % 100 == 0)
                    {
                        yield return null;
                    }
                    // Debug.Log($"#AutoRunner# Processing {so.targetObject}");
                    bool hasFixer = false;

                    SerializedProperty property = so.GetIterator();
                    while (property.NextVisible(true))
                    {
                        (SerializedUtils.FieldOrProp fieldOrProp, object parent) info;
                        try
                        {
                            info = SerializedUtils.GetFieldInfoAndDirectParent(property);
                        }
                        catch (Exception)
                        {
                            continue;
                        }
                        MemberInfo memberInfo = info.fieldOrProp.IsField
                            // ReSharper disable once RedundantCast
                            ? (MemberInfo)info.fieldOrProp.FieldInfo
                            : info.fieldOrProp.PropertyInfo;

                        PropertyAttribute[] saintsAttribute = memberInfo.GetCustomAttributes()
                            .OfType<PropertyAttribute>()
                            .Where(each => each is ISaintsAttribute)
                            .ToArray();

                        List<AutoRunnerResult> autoRunnerResults = new List<AutoRunnerResult>();
                        bool skipThisField = false;
                        foreach (PropertyAttribute saintsPropertyAttribute in saintsAttribute)
                        {
                            if (skipThisField)
                            {
                                break;
                            }

                            if (!_typeToDrawer.TryGetValue(saintsPropertyAttribute.GetType(), out IReadOnlyList<(bool isSaints, Type drawerType)> drawers))
                            {
                                continue;
                            }

                            foreach (Type drawerType in drawers.Where(each => each.isSaints).Select(each => each.drawerType))
                            {
                                SaintsPropertyDrawer saintsPropertyDrawer = (SaintsPropertyDrawer)Activator.CreateInstance(drawerType);

                                if(skipHiddenFields && saintsPropertyDrawer is IAutoRunnerSkipDrawer skipDrawer)
                                {
                                    if(skipDrawer.AutoRunnerSkip(property, memberInfo, info.parent))
                                    {
                                        Debug.Log($"#AutoRunner# skip {target}/{property.propertyPath}");
                                        autoRunnerResults.Clear();
                                        skipThisField = true;
                                        break;
                                    }
                                }

                                // ReSharper disable once InvertIf
                                if (saintsPropertyDrawer is IAutoRunnerFixDrawer autoRunnerDrawer)
                                {
                                    // Debug.Log($"{property.propertyPath}/{autoRunnerDrawer}");
                                    SerializedProperty prop = property.Copy();
                                    AutoRunnerFixerResult autoRunnerResult =
                                        autoRunnerDrawer.AutoRunFix(prop, memberInfo, info.parent);
                                    if(autoRunnerResult != null)
                                    {
                                        _processingMessage =
                                            $"Fixer found for {target}/{so.targetObject}: {autoRunnerResult}";
                                        Debug.Log($"#AutoRunner# {_processingMessage}");


                                        string mainTargetString;
                                        bool mainTargetIsAssetPath;
                                        if(target is string s)
                                        {
                                            mainTargetString = s;
                                            mainTargetIsAssetPath = false;
                                        }
                                        else if (target is Scene scene)
                                        {
                                            mainTargetString = scene.path;
                                            mainTargetIsAssetPath = true;
                                        }
                                        else
                                        {
                                            mainTargetString = AssetDatabase.GetAssetPath((Object) target);
                                            mainTargetIsAssetPath = true;
                                        }

                                        autoRunnerResults.Add(new AutoRunnerResult
                                        {
                                            FixerResult = autoRunnerResult,
                                            mainTargetString = mainTargetString,
                                            mainTargetIsAssetPath = mainTargetIsAssetPath,
                                            subTarget = so.targetObject,
                                            propertyPath = property.propertyPath,
                                            // SerializedProperty = prop,
                                            SerializedObject = so,
                                        });
                                    }
                                }
                            }
                        }
                        results.AddRange(autoRunnerResults);
                        if (autoRunnerResults.Count > 0)
                        {
                            hasFixer = true;
                        }
                    }

                    if (!hasFixer)
                    {
                        so.Dispose();
                    }
                }

                processing += 1;
            }
            EditorUtility.SetDirty(EditorInspectingTarget == null? this: EditorInspectingTarget);
            // results = autoRunnerResults.ToArray();
            _processingMessage = $"All done, {results.Count} found";
            Debug.Log($"#AutoRunner# {_processingMessage}");
        }

        private bool IsFromFile()
        {
            return EditorInspectingTarget != null &&
                   !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(EditorInspectingTarget));
        }

        [Ordered, Button("Save To Project"), PlayaHideIf(nameof(IsFromFile))]
        // ReSharper disable once UnusedMember.Local
        private void SaveToProject()
        {
            if (!Directory.Exists("Assets/Editor Default Resources"))
            {
                Debug.Log($"Create folder: Assets/Editor Default Resources");
                AssetDatabase.CreateFolder("Assets", "Editor Default Resources");
            }

            if (!Directory.Exists("Assets/Editor Default Resources/SaintsField"))
            {
                Debug.Log($"Create folder: Assets/Editor Default Resources/SaintsField");
                AssetDatabase.CreateFolder("Assets/Editor Default Resources", "SaintsField");
            }

            Debug.Log(
                $"Create saintsFieldConfig: Assets/Editor Default Resources/{EditorResourcePath}");
            AutoRunnerWindow copy = Instantiate(this);
            copy.results = new List<AutoRunnerResult>();
            AssetDatabase.CreateAsset(copy, $"Assets/Editor Default Resources/{EditorResourcePath}");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Reset target to saved file");
            EditorRefreshTarget();
            Selection.activeObject = EditorInspectingTarget;
        }

        // public AutoRunnerResult result = new AutoRunnerResult();
        [Ordered] public List<AutoRunnerResult> results = new List<AutoRunnerResult>();

        public override void OnEditorEnable()
        {
            EditorRefreshTarget();
            _typeToDrawer = SaintsPropertyDrawer.EnsureAndGetTypeToDrawers();
            processing = 0;
            _processedItemCount = 0;
            _processingMessage = null;
        }

        public override void OnEditorDestroy()
        {
            // Debug.Log(EditorInspectingTarget);
            // Debug.Log(((AutoRunnerWindow)EditorInspectingTarget).buildingScenes);
            // EditorUtility.SetDirty(EditorInspectingTarget);
            foreach (AutoRunnerResult autoRunnerResult in results)
            {
                try
                {
                    autoRunnerResult.SerializedObject.Dispose();
                }
                catch (Exception)
                {
                    // do nothing
                }
            }

            results = new List<AutoRunnerResult>();
        }
    }
}
