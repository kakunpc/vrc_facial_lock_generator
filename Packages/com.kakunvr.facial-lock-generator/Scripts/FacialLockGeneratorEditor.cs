using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using nadena.dev.modular_avatar.core;
using Unity.EditorCoroutines.Editor;
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

namespace kakunvr.FacialLockGenerator.Scripts
{
    public sealed class FacialLockGeneratorEditor : EditorWindow
    {
        private static string CreatePath = "Assets/kakunvr/FacialLockGenerator/Generated";
        private GameObject selectedGameObject;

        private Vector2 _scrollPosition = Vector2.zero;
        private List<FacialData> _facialList = new List<FacialData>();
        private ReorderableList _reorderableList;
        private const string AfkParamName = "AFK";
        private const string FacialLockIdParamName = "FacialLockId";

        [MenuItem("GameObject/kakunvr/Setup FacialLockGenerator", false, 0)]
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
            var w = (FacialLockGeneratorEditor)GetWindow(typeof(FacialLockGeneratorEditor));
            w.titleContent.text = "Setup FacialLockGenerator";
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
                    var startY = rect.y;

                    // フォルダ名の設定
                    rect.height = EditorGUIUtility.singleLineHeight;
                    rect.width = width - 50;

                    e.Folder = EditorGUI.TextField(rect, "フォルダ", e.Folder);
                    rect.y += EditorGUIUtility.singleLineHeight;

                    e.Name = EditorGUI.TextField(rect, "名前", e.Name);
                    var r = rect;
                    r.y = startY;
                    r.x = rect.x + rect.width;
                    r.width = 50;
                    r.height = EditorGUIUtility.singleLineHeight * 2;
                    if (GUI.Button(r, "Edit"))
                    {
                        EditBlendShape(e);
                    }
                },
                onAddDropdownCallback = OnAddDropdownCallback,
                elementHeight = EditorGUIUtility.singleLineHeight * 2
            };
        }

        private void OnAddDropdownCallback(Rect buttonRect, ReorderableList list)
        {
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("FXレイヤーから追加"), false,
                () => { AddAnimationClipWindow.Show(this, selectedGameObject); });

            menu.AddItem(new GUIContent("モデルのブレンドシェイプから追加"), false, () =>
            {
                // 追加してから編集
                var f = new FacialData();
                Add(f);
                EditBlendShape(f);
            });
            
            menu.AddItem(new GUIContent(".animで追加"), false, () =>
            {
                var path = EditorUtility.OpenFilePanelWithFilters("追加する.animを選択", Application.dataPath,
                    new[] { "anim", "anim" });
                if (!string.IsNullOrWhiteSpace(path))
                {
                    var facialData = new FacialData();
                    var blendShapeData = new List<BlendShapeData>();
                    var assetPath = path.Substring(path.IndexOf("Assets", StringComparison.Ordinal));
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
                    var curves = AnimationUtility.GetCurveBindings(clip);
                    foreach (var binding in curves)
                    {
                        AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                        var animT = selectedGameObject.transform.Find(binding.path)?.GetComponent<SkinnedMeshRenderer>();
                        if (animT == null) continue;

                        blendShapeData.Add(new BlendShapeData
                        {
                            Name = binding.propertyName.Substring("blendShape.".Length),
                            Target = animT,
                            Value = Mathf.RoundToInt(curve[curve.length - 1].value)
                        });
                    }

                    facialData.Name = Path.GetFileNameWithoutExtension(path);
                    facialData.BlendShapeData = blendShapeData;
                    Add(facialData);
                    EditBlendShape(facialData);
                }
            });
            
            menu.AddItem(new GUIContent(".animで追加（複数）"), false , () =>
            {
                var dir = EditorUtility.OpenFolderPanel("追加する.animを選択", Application.dataPath, "");
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    // 格納されてるフォルダごとにメニューを分ける？
                    var isFolder = EditorUtility.DisplayDialog("確認", "フォルダごとにメニューを作成しますか？", "作る", "作らない");
                    
                    // .animのみを取得
                    var files = Directory.GetFiles(dir, "*.anim", SearchOption.AllDirectories);
                    foreach (var path in files)
                    {
                        var facialData = new FacialData();
                        var blendShapeData = new List<BlendShapeData>();
                        var assetPath = path.Substring(path.IndexOf("Assets", StringComparison.Ordinal));
                        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
                        var curves = AnimationUtility.GetCurveBindings(clip);
                        foreach (var binding in curves)
                        {
                            AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                            var animT = selectedGameObject.transform.Find(binding.path)
                                ?.GetComponent<SkinnedMeshRenderer>();
                            if (animT == null) continue;

                            blendShapeData.Add(new BlendShapeData
                            {
                                Name = binding.propertyName.Substring("blendShape.".Length),
                                Target = animT,
                                Value = Mathf.RoundToInt(curve[curve.length - 1].value)
                            });
                        }
                        
                        if (isFolder)
                        {
                            // ファイルが有るフォルダ名を取得
                            var folder = Path.GetFileName(Path.GetDirectoryName(path));
                            facialData.Folder = folder;
                        }

                        facialData.Name = Path.GetFileNameWithoutExtension(path);
                        facialData.BlendShapeData = blendShapeData;
                        Add(facialData);
                    }
                }
            });
            
            menu.DropDown(buttonRect);
        }

        private void EditBlendShape(FacialData facialList)
        {
            EditBlendShapeWindow.Show(this, selectedGameObject, facialList);
        }

        public void Add(FacialData facialList)
        {
            _facialList.Add(facialList);
        }

        private void OnGUI()
        {
            GUILayout.Label("設定");

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField("ターゲット", selectedGameObject, typeof(GameObject), true);
            EditorGUI.EndDisabledGroup();

            GUILayout.Space(30);

            if (GUILayout.Button("以前のデータを反映"))
            {
                var filePath = EditorUtility.OpenFilePanelWithFilters("以前のデータを反映", CreatePath, new[] {"faciallist","asset"});
                if (!string.IsNullOrWhiteSpace(filePath))
                {
                    // Assets までのパスを削除
                    var assetPath = filePath.Substring(filePath.IndexOf("Assets", StringComparison.Ordinal));
                    var facialData = (FacialList)AssetDatabase.LoadAssetAtPath(assetPath, typeof(FacialList));
                    foreach (var data in facialData.FacialData)
                    {
                        // Targetを設定する
                        foreach (var blendShapeData in data.BlendShapeData)
                        {
                            var target = selectedGameObject.transform.Find(blendShapeData.TargetObjectName);
                            if (target == null)
                            {
                                Debug.LogWarning($"{blendShapeData.TargetObjectName}が見つかりません");
                                continue;
                            }

                            blendShapeData.Target = target.GetComponent<SkinnedMeshRenderer>();
                        }
                        _facialList.Add(data);
                    }
                }
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            _reorderableList.DoLayoutList();
            EditorGUILayout.EndScrollView();

            GUILayout.FlexibleSpace();

            var isOk = true;
            string errorMessage = "";
            if (_facialList.Count <= 0)
            {
                isOk = false;
                errorMessage = "表情データがありません";
            }
            else
            {
                foreach (var facialData in _facialList)
                {
                    if (string.IsNullOrEmpty(facialData.Name))
                    {
                        isOk = false;
                        errorMessage = "表情名が空です";
                        break;
                    }
                }
            }

            if (!string.IsNullOrEmpty(errorMessage))
            {
                GUILayout.BeginHorizontal("box");
                var errorIcon = EditorGUIUtility.IconContent("console.erroricon").image;
                EditorGUILayout.LabelField(new GUIContent(errorIcon), GUILayout.Height(errorIcon.height),
                    GUILayout.Width(errorIcon.width));
                GUILayout.Label(errorMessage);
                GUILayout.EndHorizontal();
            }

            EditorGUI.BeginDisabledGroup(!isOk);
            if (GUILayout.Button("作成"))
            {
                if (EditorUtility.DisplayDialog("確認", $"表情データを作成します。よろしいですか？", "作成する", "やっぱりやめる"))
                {
                    EditorCoroutineUtility.StartCoroutine(CreateFacialSettings(), this);
                }
            }

            EditorGUI.EndDisabledGroup();
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

        private IEnumerator CreateFacialSettings()
        {
            // 生成用のディレクトリを作成
            var date = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
            var dirName = $"{date}_{selectedGameObject.name.Replace("/", "").Replace("\\", "")}";
            var dirPath = Path.Combine(CreatePath, dirName);
            Directory.CreateDirectory(dirPath);

            // サムネイルの作成
            yield return EditorCoroutineUtility.StartCoroutine(CreateThumbnail(dirPath), this);
            
            // アニメーションファイルの作成
            CreateAnimationClip(dirPath);

            // Animatorの作成
            CreateAnimator(dirPath);

            // メニューの作成
            CreateMenu(dirPath);

            // モジュラーアバターの設定
            SetupModularAvatar(dirPath);

            // データの保存
            var facialList = CreateOrLoadScriptableObject<FacialList>(Path.Combine(dirPath, "faciallist.asset"));
            // 保存する前にTargetを名前に変換する
            foreach (var facialData in _facialList)
            {
                foreach (var blendShapeData in facialData.BlendShapeData)
                {
                    blendShapeData.TargetObjectName = blendShapeData.Target.name;
                    blendShapeData.Target = null;
                }
            }
            facialList.FacialData = _facialList;
            EditorUtility.SetDirty(facialList);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Info", "完了しました", "OK");
            Close();
        }

        private IEnumerator CreateThumbnail(string dirPath)
        {
            var thumbnailPath = Path.Combine(dirPath, "thumbnail");
            Directory.CreateDirectory(thumbnailPath);
            GameObject tempCamera = new GameObject("tempCamera");

            // 元々の表情を保存しておく
            var originalBlendShapeData = new List<BlendShapeData>();
            var skinMeshRenderers = selectedGameObject.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var skinnedMeshRenderer in skinMeshRenderers)
            {
                // ここがnullになってる時がある？
                if(skinnedMeshRenderer.sharedMesh == null) continue;
                
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
                    
                    yield return new EditorWaitForSeconds(0.01f);
                    
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
                    var fileName = $"{thumbnailPath}/facial_{i}.png";
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

            // 現在のデフォルトの表情を保存しておく
            var originalBlendShapeData = new List<BlendShapeData>();
            var skinMeshRenderers = selectedGameObject.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var skinnedMeshRenderer in skinMeshRenderers)
            {
                if(skinnedMeshRenderer.sharedMesh == null) continue;

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

                AssetDatabase.CreateAsset(clip, Path.Combine(animationPath, $"facial_{i}.anim"));
            }


            // リセット用のアニメーションを作成
            {
                var clip = new AnimationClip();
                clip.ClearCurves();
                foreach (var facialData in _facialList)
                {
                    foreach (var blendShapeData in facialData.BlendShapeData)
                    {
                        var original = originalBlendShapeData.Find(x =>
                            x.Target == blendShapeData.Target && x.Name == blendShapeData.Name);
                        // ない場合は無視
                        if (original == null) continue;
                        var curve = AnimationCurve.Constant(0, 0, original.Value);
                        var gameObjectPath = GetFullPath(blendShapeData.Target.transform)
                            .Substring((selectedGameObject.name + "/").Length);
                        clip.SetCurve(gameObjectPath, typeof(SkinnedMeshRenderer),
                            "blendShape." + blendShapeData.Name, curve);
                    }
                }

                AssetDatabase.CreateAsset(clip, Path.Combine(animationPath, "_reset.anim"));
                AssetDatabase.CreateAsset(new AnimationClip(), Path.Combine(animationPath, "_none.anim"));
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private void CreateAnimator(string dirPath)
        {
            var animatorPath = Path.Combine(dirPath, "animator");
            Directory.CreateDirectory(animatorPath);
            
            var animatorController = AnimatorController.CreateAnimatorControllerAtPath(Path.Combine(animatorPath, "animation.controller"));
            
            // Reset
            var resetLayer = new AnimatorControllerLayer
            {
                name = animatorController.MakeUniqueLayerName("FacialLocker_Reset"),
                blendingMode = AnimatorLayerBlendingMode.Override,
                defaultWeight = 1f,
                stateMachine = new AnimatorStateMachine()
            };
            resetLayer.stateMachine.name = resetLayer.name;
            resetLayer.stateMachine.hideFlags = HideFlags.HideInHierarchy;
            if (AssetDatabase.GetAssetPath(animatorController) != "")
            {
                AssetDatabase.AddObjectToAsset(resetLayer.stateMachine, AssetDatabase.GetAssetPath(animatorController));
            }
            animatorController.AddLayer(resetLayer);

            // Reset用のステート
            {
                var noneState = resetLayer.stateMachine.AddState(resetLayer.stateMachine.MakeUniqueStateName("none"),
                    new Vector3(100, 200, 0));
                noneState.writeDefaultValues = false;
                noneState.motion =
                    AssetDatabase.LoadAssetAtPath<AnimationClip>(Path.Combine(dirPath, "animation", "_none.anim"));
                var resetState = resetLayer.stateMachine.AddState(resetLayer.stateMachine.MakeUniqueStateName("reset"),
                    new Vector3(400, 200, 0));
                resetState.writeDefaultValues = false;
                resetState.motion =
                    AssetDatabase.LoadAssetAtPath<AnimationClip>(Path.Combine(dirPath, "animation", "_reset.anim"));
                var resetEndState = resetLayer.stateMachine.AddState(
                    resetLayer.stateMachine.MakeUniqueStateName("resetEnd"),
                    new Vector3(700, 200, 0));
                resetEndState.writeDefaultValues = false;
                resetEndState.motion =
                    AssetDatabase.LoadAssetAtPath<AnimationClip>(Path.Combine(dirPath, "animation", "_reset.anim"));

                resetLayer.stateMachine.defaultState = noneState;

                var transition = noneState.AddTransition(resetState);
                transition.hasExitTime = false;
                transition.duration = 0.1f;
                transition.AddCondition(AnimatorConditionMode.NotEqual, 0, FacialLockIdParamName);
                transition.AddCondition(AnimatorConditionMode.IfNot, 0, AfkParamName);

                transition = resetState.AddTransition(resetEndState);
                transition.hasExitTime = false;
                transition.duration = 0.1f;
                transition.AddCondition(AnimatorConditionMode.Equals, 0, FacialLockIdParamName);

                transition = resetState.AddTransition(resetEndState);
                transition.hasExitTime = false;
                transition.duration = 0.1f;
                transition.AddCondition(AnimatorConditionMode.If, 0, AfkParamName);

                transition = resetEndState.AddExitTransition();
                transition.hasExitTime = true;
                transition.exitTime = 0.75f;
                transition.hasFixedDuration = true;
                transition.duration = 0.1f;
            }


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
            p.duration = 0.1f;
            // p.offset = 0f;
            // p.exitTime = 0f;

            var p2 = layer.stateMachine.AddAnyStateTransition(defaultState);
            p2.AddCondition(AnimatorConditionMode.If, 1, AfkParamName);
            // p2.canTransitionToSelf = false;
            p2.duration = 0.1f;
            // p2.offset = 0f;
            // p2.exitTime = 0f;
            
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
                    Path.Combine(dirPath, "animation", $"facial_{i}.anim"));
                state.writeDefaultValues = false;

                // AddAnyStateTransition
                var transition = layer.stateMachine.AddAnyStateTransition(state);
                transition.AddCondition(AnimatorConditionMode.Equals, 1 + i, FacialLockIdParamName);
                transition.AddCondition(AnimatorConditionMode.IfNot, 0, AfkParamName);
                transition.canTransitionToSelf = false;
                transition.duration = 0.1f;
                // transition.offset = 0f;
                // transition.exitTime = 0f;
                
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
            
            // 各フォルダ用のメニューを作る
            var menuDic = new Dictionary<string, VRCExpressionsMenu>();
            var folders = _facialList.Where(x=> !string.IsNullOrEmpty(x.Folder)).Select(x => x.Folder).Distinct();
            menuDic.Add(string.Empty, menuAsset);
            foreach (var folder in folders)
            {
                if (string.IsNullOrEmpty(folder)) continue;
                var guid = GUID.Generate().ToString().Replace("-", "");
                menuDic.Add(folder,
                    CreateOrLoadScriptableObject<VRCExpressionsMenu>(Path.Combine(menuPath,
                        $"faciallocker_{folder}_{guid}.asset")));
            }

            // 登録する
            foreach (var menu in menuDic)
            {
                if (string.IsNullOrEmpty(menu.Key)) continue;
                menuAsset = AddMenuItem(menuAsset,
                    new VRCExpressionsMenu.Control()
                    {
                        name = menu.Key,
                        type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                        subMenu = menu.Value
                    },
                    menuPath
                );
            }

            for (var i = 0; i < _facialList.Count; ++i)
            {
                var facialData = _facialList[i];

                var addedMenu = menuDic[facialData.Folder];

                addedMenu = AddMenuItem(addedMenu,
                    new VRCExpressionsMenu.Control()
                    {
                        name = facialData.Name,
                        icon = AssetDatabase.LoadAssetAtPath<Texture2D>(Path.Combine(dirPath, "thumbnail",
                            $"facial_{i}.png")),
                        type = VRCExpressionsMenu.Control.ControlType.Toggle,
                        parameter = new VRCExpressionsMenu.Control.Parameter()
                        {
                            name = FacialLockIdParamName
                        },
                        value = i + 1
                    },
                    menuPath
                );
                menuDic[facialData.Folder] = addedMenu;
            }

            foreach (var menu in menuDic)
            {
                EditorUtility.SetDirty(menu.Value);
            }

            EditorUtility.SetDirty(menuAsset);

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
            var avatarName = selectedGameObject.name.Replace("/", "").Replace("\\", "");
            var modularAvatar = new GameObject($"MA_FaceLocker_{avatarName}");
            modularAvatar.transform.SetParent(selectedGameObject.transform);
            modularAvatar.transform.localPosition = Vector3.zero;
            modularAvatar.transform.localScale = Vector3.one;
            modularAvatar.transform.localRotation = Quaternion.identity;

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
            var prefabPath = Path.Combine(dirPath, $"{modularAvatar.name}.prefab");
            var prefab = PrefabUtility.SaveAsPrefabAsset(modularAvatar, prefabPath);
            DestroyImmediate(modularAvatar);
            var prefabObject = (GameObject)PrefabUtility.InstantiatePrefab(prefab, selectedGameObject.transform);
            prefabObject.transform.localPosition = Vector3.zero;
            prefabObject.transform.localScale = Vector3.one;
            prefabObject.transform.localRotation = Quaternion.identity;
            
            Selection.activeGameObject = prefabObject;
        }



        VRCExpressionsMenu AddMenuItem(VRCExpressionsMenu menu,
            VRCExpressionsMenu.Control control,
            string menuPath)
        {
            // すでに8個埋まっていたら新しいメニューを作る
            if (menu.controls.Count >= 8)
            {
                var guid = GUID.Generate().ToString().Replace("-", "");
                var newMenu = CreateOrLoadScriptableObject<VRCExpressionsMenu>(Path.Combine(menuPath,
                    $"faciallocker_{guid}.asset"));

                // 7個になるまで次のメニューに移動する
                while (menu.controls.Count > 7)
                {
                    var privItem = menu.controls[menu.controls.Count - 1];
                    menu.controls.RemoveAt(menu.controls.Count - 1);
                    newMenu.controls.Add(privItem);
                }

                // Nextのアイテムを追加
                menu.controls.Add(new VRCExpressionsMenu.Control()
                {
                    name = "Next",
                    icon = null,
                    type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                    subMenu = newMenu
                });
                EditorUtility.SetDirty(menu);

                menu = newMenu;
            }

            menu.controls.Add(control);
            return menu;
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
        private FacialLockGeneratorEditor FacialLockGeneratorEditor;
        private GameObject target;

        public static void Show(FacialLockGeneratorEditor editor, GameObject target)
        {
            var window = CreateInstance<AddAnimationClipWindow>();
            window.Initialize(editor, target);
            window.titleContent.text = "AddAnimationClipWindow";
            window.ShowAuxWindow();
        }

        private void Initialize(FacialLockGeneratorEditor editor, GameObject t)
        {
            FacialLockGeneratorEditor = editor;
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
                    FacialLockGeneratorEditor.Add(data);
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
        private FacialData _facialList;

        public static void Show(FacialLockGeneratorEditor editor, GameObject target, FacialData facialList)
        {
            var window = CreateInstance<EditBlendShapeWindow>();
            window.Initialize(editor, target, facialList);
            window.titleContent.text = "EditBlendShapeWindow";
            window.ShowAuxWindow();
        }

        private void Initialize(FacialLockGeneratorEditor editor, GameObject target, FacialData facialList)
        {
            _facialList = facialList;
            // targetからブレンドシェイプをすべて取得
            var skinnedMeshRenderers = target.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var meshRenderer in skinnedMeshRenderers)
            {
                var blendShapeData = new List<EditorBlendShapeData>();
                if (meshRenderer.sharedMesh == null) continue;
                
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
            foreach (var blendShape in facialList.BlendShapeData)
            {
                var rootBlendShape = _blendShapeData.FirstOrDefault(x => x.Target == blendShape.Target);
                var data = rootBlendShape?.BlendShapeList.FirstOrDefault(x => x.Name == blendShape.Name);
                if (data == null)
                {
                    Debug.LogWarning($"{blendShape.Target.name}の{blendShape.Name}が見つかりません");
                    continue;
                }

                data.IsUse = true;
                data.Value = blendShape.Value;
            }
        }

        private void OnGUI()
        {
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
                            var faceDataTarget = _facialList.BlendShapeData.Find(x =>
                                x.Target == blendShape.Target && x.Name == blendShapeData.Name);
                            if (faceDataTarget == null)
                            {
                                _facialList.BlendShapeData.Add(new BlendShapeData()
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
                            _facialList.BlendShapeData.RemoveAll(x =>
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