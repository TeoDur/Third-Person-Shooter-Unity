// ***********************************************************
//	Copyright 2016 Next Limit Technologies, http://www.nextlimit.com
//	All rights reserved.
//
//	THIS SOFTWARE IS PROVIDED 'AS IS' AND WITHOUT ANY EXPRESS OR
//	IMPLIED WARRANTIES, INCLUDING, WITHOUT LIMITATION, THE IMPLIED
//	WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE.
//
// ***********************************************************

using UnityEngine;
using UnityEditor;
using System.Collections;
using System.IO;
using System.Collections.Generic;

namespace CaronteFX
{
  [CanEditMultipleObjects]
  [CustomEditor(typeof(CRAnimation))]
  public class CRAnimationEditor : Editor
  {
    CRAnimation ac_;

    float  editorFrame_;
    double lastPreviewTime_ = 0;

    SerializedProperty decondeInGPUProp_;
    SerializedProperty bufferAllFramesProp_;
    SerializedProperty gpuFrameBufferSizeProp_;
    SerializedProperty overrideShaderForVertexAnimationProp_;
    SerializedProperty useDoubleSidedShaderProp_;
    SerializedProperty recomputeNormalsProp_;

    static Texture ic_logoCaronte_ = null;

    void OnEnable()
    {
      decondeInGPUProp_                     = serializedObject.FindProperty("decodeInGPU_");
      bufferAllFramesProp_                  = serializedObject.FindProperty("bufferAllFrames_");
      gpuFrameBufferSizeProp_               = serializedObject.FindProperty("gpuFrameBufferSize_");
      overrideShaderForVertexAnimationProp_ = serializedObject.FindProperty("overrideShaderForVertexAnimation_");
      useDoubleSidedShaderProp_             = serializedObject.FindProperty("useDoubleSidedShader_");
      recomputeNormalsProp_                 = serializedObject.FindProperty("recomputeNormals_");

      ac_ = (CRAnimation)target;
      editorFrame_ = Mathf.Max(ac_.LastReadFrame, 0);
      SetAnimationPreviewMode();

      LoadCaronteIcon();
    }

    void OnDisable()
    {

    }

    public void DrawAnimationFileType()
    {
      EditorGUI.BeginChangeCheck();
      var value = EditorGUILayout.EnumPopup("Animation file type", ac_.animationFileType);
      if ( EditorGUI.EndChangeCheck() )
      {
        Undo.RecordObject(ac_, "Change animation file type");
        ac_.animationFileType = (CRAnimation.AnimationFileType)value;
        EditorUtility.SetDirty( ac_ );
      }
    }

    public void ConvertCRAnimationAssetsToTextAssets()
    {  
      CRAnimationAsset activeCRAnimationAsset = ac_.activeAnimation;

      if (activeCRAnimationAsset != null)
      {
        string oldAssetPath;
        TextAsset textAsset = ConvertCRAnimationAssetToTextAsset(activeCRAnimationAsset, out oldAssetPath );

        ac_.activeAnimation = null;
        ac_.RemoveAnimation(activeCRAnimationAsset);
        AssetDatabase.DeleteAsset(oldAssetPath);

        ac_.AddAnimationAndSetActive(textAsset);
      }

      List<CRAnimationAsset> listCRAnimationAsset = ac_.listAnimations;
      List<TextAsset>        listTextAsset        = ac_.listAnimationsText;

      int lastAnimationAssets = listCRAnimationAsset.Count - 1;

      for (int i = lastAnimationAssets; i >= 0; i--)
      {
        CRAnimationAsset crAnimationAsset = listCRAnimationAsset[i];

        string oldAssetPath;
        TextAsset textAsset = ConvertCRAnimationAssetToTextAsset( crAnimationAsset, out oldAssetPath );
        
        listTextAsset.Add(textAsset);
        listCRAnimationAsset.RemoveAt(i);
        AssetDatabase.DeleteAsset(oldAssetPath);
      }

      listTextAsset.Reverse();

      AssetDatabase.Refresh();
      AssetDatabase.SaveAssets();
        
      EditorUtility.SetDirty(ac_);
    }


    private TextAsset ConvertCRAnimationAssetToTextAsset(CRAnimationAsset crAnimationAsset, out string oldAssetPath)
    {
      oldAssetPath = AssetDatabase.GetAssetPath(crAnimationAsset.GetInstanceID());
      int index = oldAssetPath.IndexOf(crAnimationAsset.name + ".asset");
      string assetDirectiory = oldAssetPath.Substring(0, index);

      string cacheFilePath = AssetDatabase.GenerateUniqueAssetPath(assetDirectiory + crAnimationAsset.name + ".bytes");

      FileStream fs = new FileStream(cacheFilePath, FileMode.Create);
      byte[] arrByte = crAnimationAsset.Bytes;
      fs.Write(arrByte, 0, arrByte.Length);
      fs.Close();

      AssetDatabase.Refresh();
      AssetDatabase.SaveAssets();

      TextAsset crAnimationText = (TextAsset)AssetDatabase.LoadAssetAtPath( cacheFilePath, typeof(TextAsset) );
      return crAnimationText;
    }


    public void ConvertTextAssetsToCRAnimationAssets()
    {
      TextAsset textAnimation = ac_.activeAnimationText;

      if (textAnimation != null)
      {
        string oldAssetPath;
        CRAnimationAsset crAnimationAsset = ConvertTextAssetToCRAnimationAsset(textAnimation, out oldAssetPath);

        ac_.activeAnimationText = null;
        ac_.RemoveAnimation(textAnimation);
        AssetDatabase.DeleteAsset(oldAssetPath);

        ac_.AddAnimationAndSetActive(crAnimationAsset);
      }

      List<CRAnimationAsset> listCRAnimationAsset = ac_.listAnimations;
      List<TextAsset>        listTextAsset        = ac_.listAnimationsText;

      int lastAnimationAssets = listTextAsset.Count - 1;

      for (int i = lastAnimationAssets; i >= 0; i--)
      {
        TextAsset textAsset = listTextAsset[i];

        string oldAssetPath;
        CRAnimationAsset crAnimationAsset = ConvertTextAssetToCRAnimationAsset( textAsset, out oldAssetPath );
        
        listTextAsset.RemoveAt(i);
        listCRAnimationAsset.Add(crAnimationAsset);

        AssetDatabase.DeleteAsset(oldAssetPath);
      }

      listCRAnimationAsset.Reverse();

      AssetDatabase.Refresh();
      AssetDatabase.SaveAssets();
      
      EditorUtility.SetDirty(ac_);
    }

    private CRAnimationAsset ConvertTextAssetToCRAnimationAsset(TextAsset textAsset, out string oldAssetPath)
    {
      oldAssetPath = AssetDatabase.GetAssetPath(textAsset.GetInstanceID());
      int index = oldAssetPath.IndexOf(textAsset.name + ".bytes");
      string assetDirectiory = oldAssetPath.Substring(0, index);

      string crAnimationFilePath = AssetDatabase.GenerateUniqueAssetPath(assetDirectiory + textAsset.name + ".asset");
      
      CRAnimationAsset crAnimationAsset = CRAnimationAsset.CreateInstance<CRAnimationAsset>();
      crAnimationAsset.Bytes = textAsset.bytes;
      AssetDatabase.CreateAsset( crAnimationAsset, crAnimationFilePath );

      return crAnimationAsset;
    }

    public void DrawAnimationFiles()
    {
      if (ac_.animationFileType == CRAnimation.AnimationFileType.CRAnimationAsset)
      {
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        var value = EditorGUILayout.ObjectField("Active animation", ac_.activeAnimation, typeof(CRAnimationAsset), false );
        if (EditorGUI.EndChangeCheck())
        {
          Undo.RecordObject(ac_, "Change active animation");
          ac_.activeAnimation = (CRAnimationAsset)value;
          EditorUtility.SetDirty( ac_ );
        }
        EditorGUILayout.EndHorizontal();

        CREditorUtils.DrawInspectorList("Animation tracks", ac_.listAnimations, ac_, "Set Active", ChangeToAnimationTrack );
        
        EditorGUI.BeginDisabledGroup(EditorApplication.isPlayingOrWillChangePlaymode);
        if ( GUILayout.Button("Convert animations to TextAssets") )
        {
          bool ok = EditorUtility.DisplayDialog("CaronteFX - Convert animations to TextAssets", "Proceed to conversion?", "Yes", "No" );
          if (ok)
          {
            ConvertCRAnimationAssetsToTextAssets();
          }

        }
        EditorGUI.EndDisabledGroup();

      }
      else if (ac_.animationFileType == CRAnimation.AnimationFileType.TextAsset)
      {
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        var value = EditorGUILayout.ObjectField("Active animation", ac_.activeAnimationText, typeof(TextAsset), false );
        if (EditorGUI.EndChangeCheck())
        {
          Undo.RecordObject(ac_, "Change active animation");
          ac_.activeAnimationText = (TextAsset)value;
          EditorUtility.SetDirty( ac_ );
        }
        EditorGUILayout.EndHorizontal();

        CREditorUtils.DrawInspectorList("Animation tracks", ac_.listAnimationsText, ac_, "Set Active", ChangeToAnimationTrack );
    
        if ( GUILayout.Button("Convert animations to CRAnimationAssets") )
        {
          bool ok = EditorUtility.DisplayDialog("CaronteFX - Convert animations to CRAnimationAssets", "Proceed to conversion?", "Yes", "No" );
          if (ok)
          {
            ConvertTextAssetsToCRAnimationAssets();
          }     
        }
      }    
    }

    private void ChangeToAnimationTrack(int trackIdx)
    {
      ac_.ChangeToAnimationTrack(trackIdx);
      EditorUtility.SetDirty(ac_);
    }

    public void DrawSpeed()
    {
      EditorGUI.BeginChangeCheck();
      var value = EditorGUILayout.FloatField("Speed", ac_.speed);
      if ( EditorGUI.EndChangeCheck() )
      {
        Undo.RecordObject(ac_, "Change animation file type");
        ac_.speed = value;
        EditorUtility.SetDirty( ac_ );
      }
    }

    public void DrawRepeatMode()
    {
      EditorGUI.BeginChangeCheck();
      var value = EditorGUILayout.EnumPopup("Repeat Mode", ac_.repeatMode);
      if ( EditorGUI.EndChangeCheck() )
      {
        Undo.RecordObject(ac_, "Change animation file type");
        ac_.repeatMode = (CRAnimation.RepeatMode)value;
        EditorUtility.SetDirty( ac_ );
      }
    }

    public void DrawIsPreviewInEditor()
    {
      EditorGUI.BeginChangeCheck();
      var value = EditorGUILayout.Toggle("Preview in editor", ac_.PreviewInEditor);
      if ( EditorGUI.EndChangeCheck() )
      {
        ac_.PreviewInEditor = value;
        SetAnimationPreviewMode();
      }
  }

    public void DrawSyncWithAnimator()
    {
      EditorGUI.BeginChangeCheck();
      var value = EditorGUILayout.ObjectField(new GUIContent("Sync with animator"), ac_.AnimatorSync, typeof(Animator), true);
      if (EditorGUI.EndChangeCheck())
      {
        Undo.RecordObject(ac_, "Change sync with animator");
        ac_.AnimatorSync = (Animator)value;
        EditorUtility.SetDirty(ac_);
      }
    }

    public void DrawStartTimeOffset()
    {
      EditorGUI.BeginChangeCheck();
      var value = EditorGUILayout.FloatField(new GUIContent("Start time offset"), ac_.StartTimeOffset );
      if (EditorGUI.EndChangeCheck())
      {
        Undo.RecordObject(ac_, "Change start time offset");
        ac_.StartTimeOffset = value;
        EditorUtility.SetDirty(ac_);
      }
    }

    void LoadCaronteIcon()
    {
      if (ic_logoCaronte_ == null)
      { 
        bool isUnityFree = !UnityEditorInternal.InternalEditorUtility.HasPro();
        if ( isUnityFree )
        {
          ic_logoCaronte_ = CREditorResource.LoadEditorTexture("cr_caronte_logo_free");
        }
        else
        {
          ic_logoCaronte_ = CREditorResource.LoadEditorTexture("cr_caronte_logo_pro");
        }
      }
    }

    public void DrawDecodeInGPU()
    {
      EditorGUILayout.PropertyField(decondeInGPUProp_, new GUIContent("Decode VA in GPU", "When this option is enabled vertex animations will be procesed in the GPU, this option requieres DX11 hardware level and a VertexAnimation Material."));
    }

    public void DrawBufferAllFrames()
    {
      EditorGUILayout.PropertyField(bufferAllFramesProp_, new GUIContent("Buffer all frames"));
    }

    public void DrawGPUBufferSize()
    {
      EditorGUILayout.PropertyField(gpuFrameBufferSizeProp_, new GUIContent("GPU buffer size","Number of frames that will be prebuffered to GPU for Vertex Animations") );
      gpuFrameBufferSizeProp_.intValue = Mathf.Clamp(gpuFrameBufferSizeProp_.intValue , 1, int.MaxValue);
    }

    public void DrawOverrideShaderForVA()
    {
      EditorGUILayout.PropertyField(overrideShaderForVertexAnimationProp_, new GUIContent("Override shader for VA", "When this option is enabled vertex animation objects will use the default VA shader, if it's disabled you must provide a material which support vertex animation when using GPU mode") );
    }

    private void DrawUseDoubleSidedShader()
    {
      EditorGUI.BeginDisabledGroup(!overrideShaderForVertexAnimationProp_.boolValue);
      EditorGUILayout.PropertyField(useDoubleSidedShaderProp_, new GUIContent("Use double sided shader", "When this option is enabled a double sided version of the standard material will be use for vertex animation.") );
      EditorGUI.EndDisabledGroup();
    }

    public void DrawRecomputeNormals(bool isPlayingOrWillChangePlaymode)
    {
      EditorGUILayout.PropertyField(recomputeNormalsProp_, new GUIContent("Recompute normals", "When this option is enabled, if the animation was baked with the option 'Save vertex systems', normals will be recomputed in each frame. \n\nThis option is very slow on CPU, use only in GPU mode.") );
      if (isPlayingOrWillChangePlaymode && ac_.CanRecomputeNormals && !ac_.DecodeInGPU && recomputeNormalsProp_.boolValue)
      {
        EditorGUILayout.HelpBox("Normal recomputation in CPU is very slow. It's strongly recommended to use GPU mode in combination with this option.", MessageType.Warning);
      }
    }

    public override void OnInspectorGUI()
    {
      serializedObject.Update();
      bool isPlayingOrWillChangePlaymode = EditorApplication.isPlayingOrWillChangePlaymode;

      Rect rect = GUILayoutUtility.GetRect(80f, 80f);
      GUI.DrawTexture(rect, ic_logoCaronte_, ScaleMode.ScaleToFit );
      CRGUIUtils.Splitter();

      if (Selection.gameObjects.Length > 1)
      {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        EditorGUI.BeginDisabledGroup(isPlayingOrWillChangePlaymode || ac_.PreviewInEditor || !SystemInfo.supportsComputeShaders);
        DrawDecodeInGPU();
        DrawBufferAllFrames();
        EditorGUI.BeginDisabledGroup(bufferAllFramesProp_.boolValue);
        DrawGPUBufferSize();
        EditorGUI.EndDisabledGroup();
        DrawOverrideShaderForVA();
        DrawUseDoubleSidedShader();
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.Space();
        DrawRecomputeNormals(isPlayingOrWillChangePlaymode);
        serializedObject.ApplyModifiedProperties();
        return;
      }
      EditorGUILayout.Space();
      EditorGUILayout.Space();   
      DrawAnimationFileType();
      DrawAnimationFiles();
      CRGUIUtils.Splitter();
      EditorGUILayout.Space();
      EditorGUILayout.Space();
      DrawDefaultInspector();
      EditorGUILayout.Space();
      if (CRCompressedPose.CanBeDecompressedByGPU() && !CRCompressedPose.CanBeDecompressedByCPU())
      {
        EditorGUILayout.HelpBox("Only GPU decoding of FiberCompression animations is allowed in this version of the plugin.", MessageType.Info);
      }
      else if ( CRCompressedPose.CanBeDecompressedByCPU() && ! CRCompressedPose.CanBeDecompressedByGPU())
      {
        EditorGUILayout.HelpBox("Only CPU decoding of FiberCompression animations is allowed in this version of the plugin.", MessageType.Info);
      }
      EditorGUI.BeginDisabledGroup(isPlayingOrWillChangePlaymode || ac_.PreviewInEditor || !SystemInfo.supportsComputeShaders );
      DrawDecodeInGPU();
      DrawBufferAllFrames();
      EditorGUI.EndDisabledGroup();
      EditorGUI.BeginDisabledGroup(!ac_.DecodeInGPU || isPlayingOrWillChangePlaymode || ac_.PreviewInEditor);
      EditorGUI.BeginDisabledGroup(bufferAllFramesProp_.boolValue);
      DrawGPUBufferSize();
      EditorGUI.EndDisabledGroup();
      DrawOverrideShaderForVA();
      DrawUseDoubleSidedShader();
      EditorGUI.EndDisabledGroup();
      EditorGUILayout.Space();
      DrawRecomputeNormals(isPlayingOrWillChangePlaymode);
      serializedObject.ApplyModifiedProperties();
      EditorGUILayout.Space();

      GameObject go = ac_.gameObject;
      PrefabType pType = PrefabUtility.GetPrefabType(go);
      bool isPrefab = pType == PrefabType.Prefab || pType ==PrefabType.PrefabInstance;
      bool gpuModeRequested = ac_.DecodeInGPU;

      EditorGUI.BeginDisabledGroup(isPlayingOrWillChangePlaymode || isPrefab);
      DrawIsPreviewInEditor();
      if (gpuModeRequested)
      {
        EditorGUILayout.HelpBox("GPU Vertex Animation decoding is not available in editor preview mode. Standard CPU decoding will be used.", MessageType.Info);
      }
      else if (isPrefab)
      {
        EditorGUILayout.HelpBox("Preview in editor is disabled on prefab and prefab instances due to performance reasons. Use play mode or break the prefab connection through the GameObject menu.", MessageType.Info);
      }
      EditorGUI.EndDisabledGroup();
        
      bool isPlayingOrPreviewInEditor = isPlayingOrWillChangePlaymode || ac_.PreviewInEditor;
      EditorGUI.BeginDisabledGroup( !isPlayingOrPreviewInEditor );
      EditorGUI.BeginChangeCheck();

      editorFrame_ = Mathf.Clamp(ac_.LastReadFrame, 0, ac_.LastFrame);
      if (ac_.interpolate)
      {
        editorFrame_ = EditorGUILayout.Slider(new GUIContent("Frame"), editorFrame_, 0, ac_.LastFrame);
      }
      else
      {
        editorFrame_ = EditorGUILayout.IntSlider(new GUIContent("Frame"), (int)editorFrame_, 0, ac_.LastFrame);
      }  
      if (EditorGUI.EndChangeCheck() && isPlayingOrPreviewInEditor )
      {   
        ac_.SetFrame(editorFrame_);      
        SceneView.RepaintAll();
      }

      EditorGUI.EndDisabledGroup();
      EditorGUILayout.LabelField(new GUIContent("Time"),             new GUIContent(ac_.AnimationTime.ToString("F3")) );
      EditorGUILayout.LabelField(new GUIContent("Frame Count"),      new GUIContent(ac_.FrameCount.ToString()) );
      EditorGUILayout.LabelField(new GUIContent("FPS"),              new GUIContent(ac_.Fps.ToString()) );
      EditorGUILayout.LabelField(new GUIContent("Animation Length"), new GUIContent(ac_.AnimationLength.ToString()) );
      EditorGUILayout.LabelField(new GUIContent("Compression type"), new GUIContent(GetCompressionTypeString()) );

      EditorGUILayout.Space();
      EditorGUI.BeginDisabledGroup(ac_.AnimatorSync != null);
      DrawStartTimeOffset();
      EditorGUI.EndDisabledGroup();
      DrawSyncWithAnimator();
      CRGUIUtils.Splitter();

      EditorGUI.BeginDisabledGroup(EditorApplication.isPlayingOrWillChangePlaymode);

      CRGUIUtils.Splitter();

      EditorGUILayout.Space();

      EditorGUI.EndDisabledGroup();

      if (isPlayingOrWillChangePlaymode)
      {
        Repaint();
      }
    }

    private string GetCompressionTypeString()
    {
      if (ac_.IsBoxCompression())
      {
        return "Box Compression";
      }
      else if (ac_.IsFiberCompression())
      {
        return "Fiber Compression";
      }
      else
      {
        return "None";
      }
    }

    private void SetAnimationPreviewMode()
    {
      if (!EditorApplication.isPlayingOrWillChangePlaymode)
      {
        if (ac_.PreviewInEditor && !ac_.IsPreviewing)
        {
          ac_.LoadAnimation(true);
          ac_.SetFrame(editorFrame_);
          EditorUtility.SetDirty(ac_);

          lastPreviewTime_ = EditorApplication.timeSinceStartup;
          EditorApplication.update -= UpdatePreview;
          EditorApplication.update += UpdatePreview;

          ac_.IsPreviewing = true;
        }
        else if ( !ac_.PreviewInEditor)
        {
          ClosePreviewMode();
        }
      }
    }

    private void ClosePreviewMode()
    {
      EditorApplication.update -= UpdatePreview;

      if (ac_ != null)
      {
        ac_.PreviewInEditor = false;
        ac_.IsPreviewing = false;
        ac_.CloseAnimation();
        EditorUtility.SetDirty(ac_);
      }
    }

    private void UpdatePreview()
    {
      if (ac_ == null)
      {
        ClosePreviewMode();
        return;
      }

      if (!EditorApplication.isPlayingOrWillChangePlaymode)
      {
        if (ac_.PreviewInEditor && ac_.IsPreviewing && ac_.isActiveAndEnabled)
        {
          double currentTime = EditorApplication.timeSinceStartup;
          float deltaTime = (float)(currentTime - lastPreviewTime_);

          lastPreviewTime_ = currentTime;
          ac_.Update(deltaTime);

          Repaint();

        }
        else if (!ac_.PreviewInEditor)
        {
          ClosePreviewMode();
        }
      }
      else
      {
        ClosePreviewMode();       
      }   
    }
  }
}

