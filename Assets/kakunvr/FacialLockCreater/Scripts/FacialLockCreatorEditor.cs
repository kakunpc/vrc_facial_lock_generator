using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditorInternal;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDKBase;
using AnimatorController = UnityEditor.Animations.AnimatorController;
using AnimatorControllerLayer = UnityEditor.Animations.AnimatorControllerLayer;
using AnimatorControllerParameter = UnityEngine.AnimatorControllerParameter;
using AnimatorControllerParameterType = UnityEngine.AnimatorControllerParameterType;
using AnimatorLayerBlendingMode = UnityEditor.Animations.AnimatorLayerBlendingMode;

namespace kakunvr.FacialLockCreater.Scripts
{
    public sealed class FacialLockCreatorEditor : EditorWindow
    {
        private static string CreatePath = "Assets/kakunvr/FacialLockCreater/Generated";
        private GameObject selectedGameObject;

        private Vector2 _scrollPosition = Vector2.zero;
        private List<FacialData> _facialList = new List<FacialData>();
        private ReorderableList _reorderableList;
        private const string AfkParamName = "AFK";
        private const string FacialLockIdParamName = "FacialLockId";

        [MenuItem("GameObject/kakunvr/Setup FacialLockCreator", false, 0)]
        public static void Create()
        {
            var gameObject = Selection.activeGameObject;
            try
            {
                var avatarDescriptor = gameObject.GetComponent<VRCAvatarDescriptor>();

                if (avatarDescriptor == null)
                {
                    throw new EditorException("VRCAvatarDescriptorが見つかりません");
                }

                CreateWindow(gameObject);
            }
            catch (EditorException e)
            {
                EditorUtility.DisplayDialog("Error", e.Message, "OK");
            }
        }

        private static void CreateWindow(GameObject gameObject)
        {
            var w = (FacialLockCreatorEditor)GetWindow(typeof(FacialLockCreatorEditor));
            w.titleContent.text = "Setup FacialLockCreator";
            w.SetGameObject(gameObject);
        }

        private void SetGameObject(GameObject gameObject)
        {
            selectedGameObject = gameObject;

            _facialList.Clear();
            _reorderableList = new ReorderableList(_facialList, typeof(FacialData),
                true,
                true,
                true,
                true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "追加する表情一覧"),
                drawElementCallback = (rect, index, active, focused) =>
                {
                    var e = _facialList[index];

                    var width = rect.width;

                    rect.height = EditorGUIUtility.singleLineHeight;
                    rect.width = width - 50;
                    e.Name = EditorGUI.TextField(rect, "", e.Name);
                    var r = rect;
                    r.x = rect.x + rect.width;
                    r.width = 50;
                    if (GUI.Button(r, "Edit"))
                    {
                        EditBlendShape(e);
                    }
                },
                onAddDropdownCallback = OnAddDropdownCallback
            };
        }

        private void OnAddDropdownCallback(Rect buttonRect, ReorderableList list)
        {
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("アニメーションクリップから追加"), false,
                () => { AddAnimationClipWindow.Show(this, selectedGameObject); });

            menu.AddItem(new GUIContent("ブレンドシェイプから追加"), false, () =>
            {
                // 追加してから編集
                var f = new FacialData();
                Add(f);
                EditBlendShape(f);
            });
            menu.DropDown(buttonRect);
        }

        private void EditBlendShape(FacialData facialData)
        {
            EditBlendShapeWindow.Show(this, selectedGameObject, facialData);
        }

        public void Add(FacialData facialData)
        {
            _facialList.Add(facialData);
        }

        private void OnGUI()
        {
            GUILayout.Label("設定");

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField("ターゲット", selectedGameObject, typeof(GameObject), true);
            EditorGUI.EndDisabledGroup();

            GUILayout.Space(30);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            _reorderableList.DoLayoutList();
            EditorGUILayout.EndScrollView();


            GUILayout.FlexibleSpace();
            if (GUILayout.Button("作成"))
            {
                if (EditorUtility.DisplayDialog("確認", $"表情データを作成します。よろしいですか？", "作成する", "やっぱりやめる"))
                {
                    CreateFacialSettings();
                }
            }
        }

        private void ApplyBlendShape(List<BlendShapeData> blendShapeDatas)
        {
            foreach (var blendShape in blendShapeDatas)
            {
                var skinnedMeshRenderer = blendShape.Target;
                var mesh = skinnedMeshRenderer.sharedMesh;
                var blendShapeIndex = mesh.GetBlendShapeIndex(blendShape.Name);
                skinnedMeshRenderer.SetBlendShapeWeight(blendShapeIndex, blendShape.Value);
            }
        }

        private void CreateFacialSettings()
        {
            // 生成用のディレクトリを作成
            var dirName = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
            var dirPath = Path.Combine(CreatePath, dirName);
            Directory.CreateDirectory(dirPath);

            // サムネイルの作成
            CreateThumbnail(dirPath);

            // アニメーションファイルの作成
            CreateAnimationClip(dirPath);

            // Animatorの作成
            CreateAnimator(dirPath);

            // メニューの作成
            CreateMenu(dirPath);

            // モジュラーアバターの設定
            SetupModularAvatar(dirPath);

            EditorUtility.DisplayDialog("Info", "完了しました", "OK");
            Close();
        }


        private void CreateThumbnail(string dirPath)
        {
            var thumbnailPath = Path.Combine(dirPath, "thumbnail");
            Directory.CreateDirectory(thumbnailPath);
            GameObject tempCamera = new GameObject("tempCamera");

            // 元々の表情を保存しておく
            var originalBlendShapeData = new List<BlendShapeData>();
            var skinMeshRenderers = selectedGameObject.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var skinnedMeshRenderer in skinMeshRenderers)
            {
                for (int i = 0; i < skinnedMeshRenderer.sharedMesh.blendShapeCount; i++)
                {
                    originalBlendShapeData.Add(new BlendShapeData()
                    {
                        Target = skinnedMeshRenderer,
                        Name = skinnedMeshRenderer.sharedMesh.GetBlendShapeName(i),
                        Value = (int)skinnedMeshRenderer.GetBlendShapeWeight(i)
                    });
                }
            }

            try
            {
                var cam = tempCamera.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.Depth;
                cam.orthographic = true;
                cam.orthographicSize = 0.1f;
                var width = 256;
                var height = 256;
                var renderTexture = new RenderTexture(width, height, 32);
                cam.targetTexture = renderTexture;

                // アバターの高さを取得して、カメラの高さを合わせる
                var avatar = selectedGameObject.GetComponent<VRCAvatarDescriptor>();
                var p = tempCamera.transform.position;
                p.z = 10;
                p.y = avatar.ViewPosition.y;
                p.x = selectedGameObject.transform.position.x;
                tempCamera.transform.position = p;
                tempCamera.transform.rotation = Quaternion.Euler(0, 180, 0);

                // サムネイルの撮影
                for (var i = 0; i < _facialList.Count; ++i)
                {
                    var facialData = _facialList[i];
                    EditorUtility.DisplayProgressBar("サムネイルを生成中", $"{facialData.Name}のサムネイルを生成中",
                        (float)i / _facialList.Count);

                    // Reset
                    ApplyBlendShape(originalBlendShapeData);

                    // 表情を適応
                    ApplyBlendShape(facialData.BlendShapeData);

                    cam.Render();

                    var tempRt = RenderTexture.GetTemporary(width, height);

                    Graphics.Blit(cam.targetTexture, tempRt);

                    // リサイズ
                    var preRt = RenderTexture.active;
                    RenderTexture.active = tempRt;

                    var ret = new Texture2D(width, height);
                    ret.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    ret.Apply();

                    var texture2D = new Texture2D(width, height, TextureFormat.ARGB32, false);
                    texture2D.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    texture2D.Apply();
                    var fileName = $"{thumbnailPath}/{facialData.Name}.png";
                    File.WriteAllBytes(fileName, texture2D.EncodeToPNG());

                    RenderTexture.active = preRt;
                    RenderTexture.ReleaseTemporary(tempRt);

                    AssetDatabase.Refresh();
                    // 書き込んだファイルのtextureImporterからALPHAを有効にする
                    var importer = AssetImporter.GetAtPath(fileName) as TextureImporter;
                    if (importer != null) importer.alphaIsTransparency = true;
                }
                
                // 最後にNoneを作成する
                {
                    // Reset
                    ApplyBlendShape(originalBlendShapeData);

                    cam.Render();

                    var tempRt = RenderTexture.GetTemporary(width, height);

                    Graphics.Blit(cam.targetTexture, tempRt);

                    // リサイズ
                    var preRt = RenderTexture.active;
                    RenderTexture.active = tempRt;

                    var ret = new Texture2D(width, height);
                    ret.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    ret.Apply();

                    var texture2D = new Texture2D(width, height, TextureFormat.ARGB32, false);
                    texture2D.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    texture2D.Apply();
                    var fileName = $"{thumbnailPath}/_DefaultFace.png";
                    File.WriteAllBytes(fileName, texture2D.EncodeToPNG());

                    RenderTexture.active = preRt;
                    RenderTexture.ReleaseTemporary(tempRt);

                    AssetDatabase.Refresh();
                    // 書き込んだファイルのtextureImporterからALPHAを有効にする
                    var importer = AssetImporter.GetAtPath(fileName) as TextureImporter;
                    if (importer != null) importer.alphaIsTransparency = true;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                DestroyImmediate(tempCamera);
                // 表情を戻す
                ApplyBlendShape(originalBlendShapeData);
            }
        }

        private void CreateAnimationClip(string dirPath)
        {
            var animationPath = Path.Combine(dirPath, "animation");
            Directory.CreateDirectory(animationPath);

            for (var i = 0; i < _facialList.Count; ++i)
            {
                var facialData = _facialList[i];
                var clip = new AnimationClip();
                clip.ClearCurves();
                foreach (var blendShapeData in facialData.BlendShapeData)
                {
                    var curve = AnimationCurve.Constant(0, 0, blendShapeData.Value);
                    var gameObjectPath = GetFullPath(blendShapeData.Target.transform)
                        .Substring((selectedGameObject.name + "/").Length);
                    clip.SetCurve(gameObjectPath, typeof(SkinnedMeshRenderer),
                        "blendShape." + blendShapeData.Name, curve);
                }
                
                AssetDatabase.CreateAsset(clip, Path.Combine(animationPath, $"{facialData.Name}.anim"));
            }
            
            
            AssetDatabase.CreateAsset(new AnimationClip(), Path.Combine(animationPath, "_none.anim"));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private void CreateAnimator(string dirPath)
        {
            var animatorPath = Path.Combine(dirPath, "animator");
            Directory.CreateDirectory(animatorPath);
            
            var animatorController = AnimatorController.CreateAnimatorControllerAtPath(Path.Combine(animatorPath, "animation.controller"));
            var layer = new AnimatorControllerLayer
            {
                name = animatorController.MakeUniqueLayerName("FacialLocker"),
                blendingMode = AnimatorLayerBlendingMode.Override,
                defaultWeight = 1f,
                stateMachine = new AnimatorStateMachine()
            };
            layer.stateMachine.name = layer.name;
            layer.stateMachine.hideFlags = HideFlags.HideInHierarchy;

            if (AssetDatabase.GetAssetPath(animatorController) != "")
            {
                AssetDatabase.AddObjectToAsset(layer.stateMachine, AssetDatabase.GetAssetPath(animatorController));
            }
            animatorController.AddLayer(layer);
            // baseを削除
            animatorController.RemoveLayer(0);

            var defaultState = layer.stateMachine.AddState(
                layer.stateMachine.MakeUniqueStateName("NoneState"), new Vector3(0, 200, 0));

            defaultState.writeDefaultValues = false;
            defaultState.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(Path.Combine(dirPath, "animation", "_none.anim"));
            var defaultVrcAnimatorTrackingControl = defaultState.AddStateMachineBehaviour<VRCAnimatorTrackingControl>();
            defaultVrcAnimatorTrackingControl.trackingHead = VRC_AnimatorTrackingControl.TrackingType.NoChange;
            defaultVrcAnimatorTrackingControl.trackingLeftHand = VRC_AnimatorTrackingControl.TrackingType.NoChange;
            defaultVrcAnimatorTrackingControl.trackingRightHand = VRC_AnimatorTrackingControl.TrackingType.NoChange;
            defaultVrcAnimatorTrackingControl.trackingHip = VRC_AnimatorTrackingControl.TrackingType.NoChange;
            defaultVrcAnimatorTrackingControl.trackingLeftFoot = VRC_AnimatorTrackingControl.TrackingType.NoChange;
            defaultVrcAnimatorTrackingControl.trackingRightFoot = VRC_AnimatorTrackingControl.TrackingType.NoChange;
            defaultVrcAnimatorTrackingControl.trackingLeftFingers = VRC_AnimatorTrackingControl.TrackingType.NoChange;
            defaultVrcAnimatorTrackingControl.trackingRightFingers = VRC_AnimatorTrackingControl.TrackingType.NoChange;
            defaultVrcAnimatorTrackingControl.trackingEyes = VRC_AnimatorTrackingControl.TrackingType.Tracking;
            defaultVrcAnimatorTrackingControl.trackingMouth = VRC_AnimatorTrackingControl.TrackingType.Tracking;

            var p = layer.stateMachine.AddAnyStateTransition(defaultState);
            p.AddCondition(AnimatorConditionMode.Equals, 0, FacialLockIdParamName);
            p.canTransitionToSelf = false;
            p.duration = 0f;
            p.offset = 0f;
            p.exitTime = 0f;

            var p2 = layer.stateMachine.AddAnyStateTransition(defaultState);
            p2.AddCondition(AnimatorConditionMode.If, 1, AfkParamName);
            // p2.canTransitionToSelf = false;
            p2.duration = 0f;
            p2.offset = 0f;
            p2.exitTime = 0f;
            
            layer.stateMachine.defaultState = defaultState;
            layer.stateMachine.anyStatePosition = new Vector3(0, 400, 0);
            animatorController.AddParameter(new AnimatorControllerParameter
                { name = "AFK", type = AnimatorControllerParameterType.Bool, defaultFloat = 0 });
            
            animatorController.AddParameter(new AnimatorControllerParameter
                { name = "FacialLockId", type = AnimatorControllerParameterType.Int, defaultFloat = 0 });

            for (var i = 0; i < _facialList.Count; ++i)
            {
                var facialData = _facialList[i];
                var state = layer.stateMachine.AddState(
                    layer.stateMachine.MakeUniqueStateName(
                        facialData.Name), new Vector3(300 + 30 * i, 200 + 100 * i, 0));
                state.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    Path.Combine(dirPath, "animation", $"{facialData.Name}.anim"));
                state.writeDefaultValues = false;

                // AddAnyStateTransition
                var transition = layer.stateMachine.AddAnyStateTransition(state);
                transition.AddCondition(AnimatorConditionMode.Equals, 1 + i, FacialLockIdParamName);
                transition.AddCondition(AnimatorConditionMode.IfNot, 0, AfkParamName);
                transition.canTransitionToSelf = false;
                transition.duration = 0f;
                transition.offset = 0f;
                transition.exitTime = 0f;
                
                var trackingControl = state.AddStateMachineBehaviour<VRCAnimatorTrackingControl>();
                trackingControl.trackingHead = VRC_AnimatorTrackingControl.TrackingType.NoChange;
                trackingControl.trackingLeftHand = VRC_AnimatorTrackingControl.TrackingType.NoChange;
                trackingControl.trackingRightHand = VRC_AnimatorTrackingControl.TrackingType.NoChange;
                trackingControl.trackingHip = VRC_AnimatorTrackingControl.TrackingType.NoChange;
                trackingControl.trackingLeftFoot = VRC_AnimatorTrackingControl.TrackingType.NoChange;
                trackingControl.trackingRightFoot = VRC_AnimatorTrackingControl.TrackingType.NoChange;
                trackingControl.trackingLeftFingers = VRC_AnimatorTrackingControl.TrackingType.NoChange;
                trackingControl.trackingRightFingers = VRC_AnimatorTrackingControl.TrackingType.NoChange;
                trackingControl.trackingEyes = VRC_AnimatorTrackingControl.TrackingType.Animation;
                trackingControl.trackingMouth = VRC_AnimatorTrackingControl.TrackingType.Tracking;
                EditorUtility.SetDirty(state);
                
            }
            EditorUtility.SetDirty(defaultState);
            EditorUtility.SetDirty(animatorController);

            AssetDatabase.SaveAssets();
        }

        private void CreateMenu(string dirPath)
        {
            var menuPath = Path.Combine(dirPath, "menu");
            Directory.CreateDirectory(menuPath);
            
            // メニューを作る
            var menuAsset = CreateOrLoadScriptableObject<VRCExpressionsMenu>(Path.Combine(menuPath, "faciallocker.asset"));
            var firstMenu = menuAsset;
            
            // 初期化用のものを登録する
            menuAsset.controls.Add(new VRCExpressionsMenu.Control()
            {
                name = "_Default",
                icon = AssetDatabase.LoadAssetAtPath<Texture2D>(Path.Combine(dirPath, "thumbnail", "_DefaultFace.png")),
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                parameter = new VRCExpressionsMenu.Control.Parameter()
                {
                    name = FacialLockIdParamName
                },
                value = 0
            });
            EditorUtility.SetDirty(menuAsset);
            
            int menuIndex = 0;

            for (var i = 0; i < _facialList.Count; ++i)
            {
                var facialData = _facialList[i];
                // 7つまで埋まってて、残り2つ以上あるならメニューを作る
                if (menuAsset.controls.Count >= 7 && i < _facialList.Count - 1)
                {
                    var newMenu  = CreateOrLoadScriptableObject<VRCExpressionsMenu>(Path.Combine(menuPath,
                        $"faciallocker_{menuIndex}.asset"));
                    menuIndex++;
                    menuAsset.controls.Add(new VRCExpressionsMenu.Control()
                    {
                        name = "Next",
                        icon = null,
                        type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                        subMenu = newMenu
                    });
                    
                    menuAsset = newMenu;
                    EditorUtility.SetDirty(newMenu);
                }

                menuAsset.controls.Add(new VRCExpressionsMenu.Control()
                {
                    name = facialData.Name,
                    icon = AssetDatabase.LoadAssetAtPath<Texture2D>(Path.Combine(dirPath, "thumbnail",
                        $"{facialData.Name}.png")),
                    type = VRCExpressionsMenu.Control.ControlType.Toggle,
                    parameter = new VRCExpressionsMenu.Control.Parameter()
                    {
                        name = FacialLockIdParamName
                    },
                    value = i + 1
                });
            }

            // MA登録用のRootメニューを作る
            var menuRootAsset =
                CreateOrLoadScriptableObject<VRCExpressionsMenu>(Path.Combine(menuPath, "menuroot_ma.asset"));

            menuRootAsset.controls.Add(new VRCExpressionsMenu.Control
            {
                name = "Facial Lock",
                icon = null,
                type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                subMenu = firstMenu
            });
            EditorUtility.SetDirty(menuRootAsset);
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private void SetupModularAvatar(string dirPath)
        {
            var modularAvatar = new GameObject("MA_FaceLocker");
            modularAvatar.transform.SetParent(selectedGameObject.transform);

            var facialLocker = modularAvatar.AddComponent<ModularAvatarParameters>();
            facialLocker.parameters.Add(new ParameterConfig()
            {
                defaultValue = 0,
                nameOrPrefix = FacialLockIdParamName,
                internalParameter = true,
                isPrefix = false,
                syncType = ParameterSyncType.Int,
                localOnly = false,
                saved = false
            });

            var animator = modularAvatar.AddComponent<ModularAvatarMergeAnimator>();
            animator.animator =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(Path.Combine(dirPath, "animator",
                    "animation.controller"));
            animator.deleteAttachedAnimator = true;
            animator.layerType = VRCAvatarDescriptor.AnimLayerType.FX;
            animator.pathMode = MergeAnimatorPathMode.Absolute;
            animator.matchAvatarWriteDefaults = true;

            var menuInstaller = modularAvatar.AddComponent<ModularAvatarMenuInstaller>();
            menuInstaller.menuToAppend = AssetDatabase.LoadAssetAtPath<VRCExpressionsMenu>(Path.Combine(dirPath,
                "menu", "menuroot_ma.asset"));

            // 使い回せるようにprefab化する
            var prefabPath = Path.Combine(dirPath, "MA_FaceLocker.prefab");
            var prefab = PrefabUtility.SaveAsPrefabAsset(modularAvatar, prefabPath);
            DestroyImmediate(modularAvatar);
            PrefabUtility.InstantiatePrefab(prefab, selectedGameObject.transform);
        }

        private static string GetFullPath(Transform t)
        {
            var path = t.name;
            var parent = t.parent;
            while (parent)
            {
                path = $"{parent.name}/{path}";
                parent = parent.parent;
            }

            return path;
        }
        
        private T CreateOrLoadScriptableObject<T>(string path) where T : ScriptableObject
        {
            var resultAsset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (resultAsset == null)
            {
                var obj = CreateInstance<T>();
                AssetDatabase.CreateAsset(obj, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                resultAsset = AssetDatabase.LoadAssetAtPath<T>(path);
            }

            return resultAsset;
        }
    }

    public class AddAnimationClipWindow : EditorWindow
    {
        private readonly string[] _defaultFacialList =
        {
            "fist",
            "peace",
            "open",
            "gun",
            "point",
            "rocknroll",
            "thumbsup",
            "idle"
        };


        private Dictionary<string, bool> _facialList = new Dictionary<string, bool>();

        private Vector2 _scrollPosition = Vector2.zero;
        private FacialLockCreatorEditor facialLockCreatorEditor;
        private GameObject target;

        public static void Show(FacialLockCreatorEditor editor, GameObject target)
        {
            var window = CreateInstance<AddAnimationClipWindow>();
            window.Initialize(editor, target);
            window.titleContent.text = "AddAnimationClipWindow";
            window.ShowUtility();
        }

        private void Initialize(FacialLockCreatorEditor editor, GameObject t)
        {
            facialLockCreatorEditor = editor;
            target = t;

            // FXレイヤーに保存されているアニメーションクリップを取得
            var avatarDescriptor = target.GetComponent<VRCAvatarDescriptor>();
            var fxAnimationLayer =
                avatarDescriptor.baseAnimationLayers.First(x => x.type == VRCAvatarDescriptor.AnimLayerType.FX);

            if (fxAnimationLayer.isDefault) return;

            if (fxAnimationLayer.animatorController != null)
            {
                foreach (var clip in fxAnimationLayer.animatorController.animationClips)
                {
                    var path = AssetDatabase.GetAssetPath(clip);

                    bool isDefault = false;
                    foreach (var df in _defaultFacialList)
                    {
                        if (Path.GetFileNameWithoutExtension(path)
                                .Replace("_", "")
                                .Replace("-", "")
                                .Replace(" ", "")
                                .ToUpper()
                                .IndexOf(df.ToUpper(), StringComparison.Ordinal) != -1)
                        {
                            isDefault = true;
                            break;
                        }
                    }

                    _facialList[path] = isDefault;
                }
            }
        }

        private void OnGUI()
        {
            GUILayout.Label("アニメーションクリップから追加");
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            GUILayout.BeginVertical("box");

            var listKeys = _facialList.Keys.ToArray();
            foreach (var key in listKeys)
            {
                var state = _facialList[key];

                GUILayout.BeginHorizontal();

                state = GUILayout.Toggle(state, Path.GetFileNameWithoutExtension(key));

                GUILayout.FlexibleSpace();

                GUILayout.EndHorizontal();
                _facialList[key] = state;
            }

            GUILayout.EndVertical();

            EditorGUILayout.EndScrollView();

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("追加"))
            {
                foreach (var facial in _facialList)
                {
                    if (!facial.Value) continue;

                    // Clipから解析してブレンドシェイプに落とし込む
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(facial.Key);
                    var curves = AnimationUtility.GetCurveBindings(clip);
                    var blendShapeData = new List<BlendShapeData>();

                    foreach (var binding in curves)
                    {
                        AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                        var animT = target.transform.Find(binding.path)?.GetComponent<SkinnedMeshRenderer>();
                        if (animT == null) continue;

                        blendShapeData.Add(new BlendShapeData
                        {
                            Name = binding.propertyName.Substring("blendShape.".Length),
                            Target = animT,
                            Value = Mathf.RoundToInt(curve[curve.length - 1].value)
                        });
                    }

                    var data = new FacialData
                    {
                        Name = Path.GetFileNameWithoutExtension(facial.Key),
                        BlendShapeData = blendShapeData
                    };
                    facialLockCreatorEditor.Add(data);
                }

                Close();
            }
        }
    }

    public class EditBlendShapeWindow : EditorWindow
    {
        class EditorBlendShapeData
        {
            public bool IsUse;
            public string Name;
            public int Value;
        }

        class RootBlendShapeData
        {
            public List<EditorBlendShapeData> BlendShapeList = new List<EditorBlendShapeData>();
            public bool IsOpen = false;
            public SkinnedMeshRenderer Target = null;
        }

        private List<RootBlendShapeData> _blendShapeData = new List<RootBlendShapeData>();

        private Vector2 _scrollPosition = Vector2.zero;
        private FacialData _facialData;

        public static void Show(FacialLockCreatorEditor editor, GameObject target, FacialData facialData)
        {
            var window = CreateInstance<EditBlendShapeWindow>();
            window.Initialize(editor, target, facialData);
            window.titleContent.text = "EditBlendShapeWindow";
            window.ShowUtility();
        }

        private void Initialize(FacialLockCreatorEditor editor, GameObject target, FacialData facialData)
        {
            _facialData = facialData;
            // targetからブレンドシェイプをすべて取得
            var skinnedMeshRenderers = target.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var meshRenderer in skinnedMeshRenderers)
            {
                var blendShapeData = new List<EditorBlendShapeData>();
                for (int i = 0; i < meshRenderer.sharedMesh.blendShapeCount; i++)
                {
                    blendShapeData.Add(new EditorBlendShapeData()
                    {
                        IsUse = false,
                        Name = meshRenderer.sharedMesh.GetBlendShapeName(i),
                        Value = 0
                    });
                }

                _blendShapeData.Add(new RootBlendShapeData()
                {
                    Target = meshRenderer,
                    BlendShapeList = blendShapeData,
                    IsOpen = false
                });
            }

            // facialDataに従って値を適応
            foreach (var blendShape in facialData.BlendShapeData)
            {
                var rootBlendShape = _blendShapeData.First(x => x.Target == blendShape.Target);
                var data = rootBlendShape.BlendShapeList.First(x => x.Name == blendShape.Name);
                data.IsUse = true;
                data.Value = blendShape.Value;
            }
        }

        private void OnGUI()
        {
            GUILayout.Label("アニメーションクリップから追加");
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            foreach (var blendShape in _blendShapeData)
            {
                blendShape.IsOpen = EditorGUILayout.Foldout(blendShape.IsOpen, blendShape.Target.name);
                if (blendShape.IsOpen)
                {
                    GUILayout.BeginVertical("box");
                    EditorGUILayout.ObjectField("ターゲット", blendShape.Target, typeof(SkinnedMeshRenderer), true);


                    foreach (var blendShapeData in blendShape.BlendShapeList)
                    {
                        GUILayout.BeginHorizontal("box");

                        bool b = blendShapeData.IsUse;
                        b = GUILayout.Toggle(b, "");

                        int sliderValue = 0;
                        if (b)
                        {
                            sliderValue = blendShapeData.Value;
                        }

                        EditorGUI.BeginDisabledGroup(!b);

                        sliderValue = EditorGUILayout.IntSlider(blendShapeData.Name, sliderValue, 0, 100);

                        EditorGUI.EndDisabledGroup();

                        GUILayout.EndHorizontal();

                        // 値の更新
                        blendShapeData.IsUse = b;
                        blendShapeData.Value = sliderValue;

                        // FaceDataへ反映
                        if (b)
                        {
                            var faceDataTarget = _facialData.BlendShapeData.Find(x =>
                                x.Target == blendShape.Target && x.Name == blendShapeData.Name);
                            if (faceDataTarget == null)
                            {
                                _facialData.BlendShapeData.Add(new BlendShapeData()
                                {
                                    Target = blendShape.Target,
                                    Name = blendShapeData.Name,
                                    Value = blendShapeData.Value
                                });
                            }
                            else
                            {
                                faceDataTarget.Value = blendShapeData.Value;
                            }
                        }
                        else
                        {
                            // 一致するものを削除
                            _facialData.BlendShapeData.RemoveAll(x =>
                                x.Target == blendShape.Target && x.Name == blendShapeData.Name);
                        }
                    }

                    GUILayout.EndVertical();
                }
            }

            EditorGUILayout.EndScrollView();
        }
    }
}