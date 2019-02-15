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
using UnityEngine.Events;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using CaronteFX.AnimationFlags;

namespace CaronteFX
{

  using CROInfo         = Tuple5<Transform, int, int, int, int>;
  using TGOFrameData    = Tuple2<Transform, CRGOKeyframe>;
  using TVisibilityData = Tuple2<Transform, Vector2>;
  using TGOHeaderData   = Tuple4<string, int, int, List<string>>;

  [System.Serializable]
  public class CRCollisionEvent : UnityEvent<CRCollisionEvInfo>
  {

  }


  [AddComponentMenu("CaronteFX/Animation")]
  public class CRAnimation : MonoBehaviour, IAnimatorExporter
  {
    [System.Serializable]
    public enum RepeatMode
    {
      Loop,
      Clamp,
      PingPong,
    };

    [System.Serializable]
    public enum AnimationFileType
    {
      CRAnimationAsset,
      TextAsset,
    };
    
    [HideInInspector]
    public AnimationFileType animationFileType = AnimationFileType.CRAnimationAsset;
    [HideInInspector]
    public CRAnimationAsset activeAnimation = null;
    [HideInInspector]
    public List<CRAnimationAsset> listAnimations = new List<CRAnimationAsset>();
    [HideInInspector]
    public TextAsset activeAnimationText = null;
    [HideInInspector]
    public List<TextAsset> listAnimationsText = new List<TextAsset>();

    public float                  speed               = 1.0f;
    public RepeatMode             repeatMode          = RepeatMode.Loop;
    public CRCollisionEvent       collisionEvent      = null;
    public bool                   animate             = true;
    public bool                   interpolate         = false;
    public bool                   doRuntimeNullChecks = false;

    public byte[] Bytes
    {
      get { return binaryAnim_; }
    }

    public float AnimationTime
    {
      get { return time_; }
    }

    public int FrameCount
    {
      get { return frameCount_; }
    }

    public float FrameTime
    {
      get { return frameTime_; }
    }

    public int Fps
    {
      get { return fps_; }
    }

    public float AnimationLength
    {
      get { return animationLength_; }
    }

    public int LastReadFrame
    {
      get { return lastReadFrame_; }
    }

    public int LastFrame
    {
      get { return lastFrame_; }
    }
   
    public bool PreviewInEditor
    {
      get { return previewInEditor_;  }
      set { previewInEditor_ = value;  }
    }

    public bool IsPreviewing
    {
      get { return isPreviewing_; }
      set { isPreviewing_ = value; }
    }

    public Animator AnimatorSync
    {
      get { return animatorSync_; }
      set { animatorSync_ = value; }
    }

    public float StartTimeOffset
    {
      get { return startTimeOffset_; }
      set { startTimeOffset_ = value; }
    }

    public bool DecodeInGPU
    {
      get { return decodeInGPU_;  }
      set { decodeInGPU_ = value; }
    }

    public bool CanRecomputeNormals
    {
      get { return vertexLocalSystems_; }
    }

    private float time_     = 0.0f;
    private int frameCount_ = 0;

    private float frameTime_ = 0.0f;
    private int fps_         = 0;

    private float animationLength_ = 0.0f;
    private int lastFrame_ = 0;

    private int      nGameObjects_;
    private string[] arrGOPath_;
    private int      nEmitters_ = 0;
    private string[] arrEmitterName_;

    private CRAnimationAsset animationLastLoaded_;
    private TextAsset animationLastLoadedText_;

    private int   lastReadFrame_      = -1;
    private float lastReadFloatFrame_ = -1;

    [NonSerialized]
    private EFileHeaderFlags fileHeaderFlags_ = EFileHeaderFlags.NONE;

    [SerializeField, HideInInspector]
    private List<CRGOTmpData> listGOTmpData_ = new List<CRGOTmpData>();
    [SerializeField, HideInInspector]
    private bool previewInEditor_ = false;
    [SerializeField, HideInInspector]
    private bool isPreviewing_ = false;
    [SerializeField, HideInInspector]
    private Animator animatorSync_ = null;
    [SerializeField, HideInInspector]
    private float startTimeOffset_ = 0.0f;

    [SerializeField, HideInInspector]
    private bool decodeInGPU_ = false;
    [SerializeField, HideInInspector]
    private bool bufferAllFrames_ = false;

    [SerializeField, HideInInspector]
    [Range(1, 3)]
    private int gpuFrameBufferSize_ = 1;
    [SerializeField, HideInInspector]
    private bool overrideShaderForVertexAnimation_ = true;
    [SerializeField, HideInInspector]
    private bool useDoubleSidedShader_ = true;
    [SerializeField, HideInInspector]
    private bool recomputeNormals_ = true;

    private Shader vertexAnimShader = null;
    private ComputeShader cShaderPositions_     = null;
    private ComputeShader cShaderNormals_       = null;
    private ComputeShader cShaderInterpolation_ = null;

    private CRGPUBufferer gpuBufferer_;

    private byte[] binaryAnim_;

    private int bCursor1_ = 0;
    private int bCursor2_ = 0;

    private double timeInternal_ = 0.0;

    private class CRGOInfo
    {
      public Transform tr_;
      public int vertexCount_;
      public int bytesOffset_;
      public int boneIdxBegin_;
      public int boneIdxEnd_;
      public int boneCount_;

      public CRGOInfo(Transform tr, int vertexCount, int bytesOffset, int boneIdxBegin, int boneIdxEnd)
      {
        tr_ = tr;

        vertexCount_  = vertexCount;
        bytesOffset_  = bytesOffset;
        boneIdxBegin_ = boneIdxBegin;
        boneIdxEnd_   = boneIdxEnd;

        boneCount_ = boneIdxEnd_ - boneIdxBegin_;
      }
    }

    private CRGOInfo[]  arrGOInfo_;
    private Transform[] arrBoneTr_;

    private BitArray      arrSkipObject_;
    private BitArray      arrIsBone_;
    private Vector2[]     arrVisibilityInterval_;
    private Mesh[]        arrMesh_;
    private CRGPUBuffer[] arrCRGPUBuffer_;

    private CRDefinition[]     arrDefinition_;
    private CRCompressedPose[] arrCompressedPose_;

    private CRVertexDataCache[] arrVertexDataCache_;

    private long[]      arrFrameOffsets_;
    private int[]       arrCacheIndex_;
    private Vector3[][] arrVertex3Cache1_;
    private Vector3[][] arrVertex3Cache2_;
    private Vector4[][] arrVertex4Cache_;
    private bool        interpolationModeActive_;

    private int  binaryVersion_;
    private bool vertexCompression_;
    private bool fiberCompression_;
    private bool boxCompression_;
    private bool vertexLocalSystems_;
    private bool tangentsData_;
    private bool alignedData_ = false;

    private bool internalPaused_;
    private bool internalUsingGpu_;
    private int  internalGPUBufferSize_;

    private bool GPUModeRequested
    {
      get { return decodeInGPU_ && SystemInfo.supportsComputeShaders;  }
    }

    private Dictionary<int, int> dictVertexCountCacheIdx_ = new Dictionary<int, int>();
    private List<int>            listVertexCacheCount_    = new List<int>();
    private HashSet<GameObject>  setVertexAnimatedGO_     = new HashSet<GameObject>();

    private CRCollisionEvInfo ceInfo = new CRCollisionEvInfo();

    delegate void ReadFrameDel(float frame);
    private ReadFrameDel readFrameDel_ = new ReadFrameDel( (frame) => {} );

    const float vecQuantz = 1.0f / 127.0f;
    const float posQuantz = 1.0f / 65535.0f;

    void Awake()
    {
      LoadAnimation(false);
    }

    void Start()
    {
      ReadFrameAtCurrentTime();
    }

    void OnDestroy()
    {
      CloseAnimation();
    }

    public void AddAnimationAndSetActive(CRAnimationAsset animationAsset)
    {
      AddAnimation(animationAsset);

      activeAnimation = animationAsset;
      animationFileType = AnimationFileType.CRAnimationAsset;
    }

    public void AddAnimation(CRAnimationAsset animationAsset)
    {
      if ( !listAnimations.Contains(animationAsset) )
      {
        listAnimations.Add(animationAsset);
      }
    }

    public void RemoveAnimation(CRAnimationAsset animationAsset)
    {
      listAnimations.Remove(animationAsset);
    }

    public void AddAnimationAndSetActive(TextAsset textAsset)
    {
      AddAnimation(textAsset);

      activeAnimationText = textAsset;
      animationFileType = AnimationFileType.TextAsset;
    }
   
    public void AddAnimation(TextAsset textAsset)
    {
      if (!listAnimationsText.Contains(textAsset))
      {
        listAnimationsText.Add(textAsset);
      }
    }

    public void RemoveAnimation(TextAsset textAsset)
    {
      listAnimationsText.Remove(textAsset);
    }

    public void LoadAnimation(bool fromEditor)
    {
      //first close the current animation
      CloseAnimation();

      bool isCRAnimationAsset = animationFileType == AnimationFileType.CRAnimationAsset;
      bool isTextAsset        = animationFileType == AnimationFileType.TextAsset;

      if ( isCRAnimationAsset && activeAnimation     == null ||
           isTextAsset        && activeAnimationText == null )
      {
        return;
      }
  
      byte[] animBytes = null;
      if (isCRAnimationAsset)
      {
        animBytes            = activeAnimation.Bytes;
        animationLastLoaded_ = activeAnimation;
      }
      else if (isTextAsset)
      {
        CRAnimationsManager animationsManager = CRAnimationsManager.Instance;
        animBytes = animationsManager.GetBytesFromAnimation(activeAnimationText);
        animationLastLoadedText_ = activeAnimationText;
      }

      using (MemoryStream ms = new MemoryStream(animBytes, false) )
      {
        if (ms != null)
        {
          using (BinaryReader br = new BinaryReader(ms) )
          {
            if (br != null)
            {
              LoadAnimationCommon(br, fromEditor);

              if (fiberCompression_ && !internalUsingGpu_ && !CRCompressedPose.CanBeDecompressedByCPU())
              {
                CloseAnimation();
                return;
              }

              if (binaryVersion_ < 5 )
              {
                LoadAnimationV0(br, fromEditor);
              }
              if (binaryVersion_ == 5)
              {
                LoadAnimationV5(br, fromEditor);
              }
              else if (binaryVersion_ == 6)
              {
                LoadAnimationV6(br, fromEditor);
              }
              else if (binaryVersion_ == 7 ||
                       binaryVersion_ == 8 )
              {
                LoadAnimationV78(br, fromEditor);
              }

              binaryAnim_          = animBytes;
              lastReadFrame_       = -1;
              lastReadFloatFrame_  = -1;

              timeInternal_ = startTimeOffset_;
            }

          } // BinaryReader

        } //MemoryStream
      }
    }

    public void CloseAnimation()
    {
      binaryAnim_      = null;

      time_            = 0.0f;
      frameCount_      = 0;
      frameTime_       = 0.0f;
      fps_             = 0;
      animationLength_ = 0.0f;
      lastFrame_       = 0;

      boxCompression_   = false;
      fiberCompression_ = false;
      alignedData_      = false;

      ClearGOTmpData();
      ClearGPUBuffers();
    }

    private void ClearGOTmpData()
    {
      foreach (CRGOTmpData goTmpData in listGOTmpData_)
      {
        GameObject go = goTmpData.gameObject_;

        if (go != null)
        {
          go.transform.localPosition = goTmpData.localPosition_;
          go.transform.localRotation = goTmpData.localRotation_;
          go.transform.localScale    = goTmpData.localScale_;

          Mesh mesh = goTmpData.mesh_;
          if (mesh != null)
          {
            MeshFilter mf = go.GetComponent<MeshFilter>();
            if (mf != null)
            {
              mf.sharedMesh = mesh;
            }
          }
        }

        Mesh tmpMesh = goTmpData.tmp_Mesh_;
        if (tmpMesh != null)
        {
          UnityEngine.Object.DestroyImmediate(tmpMesh);
        }
      }
      listGOTmpData_.Clear();
    }

    private void ClearGPUBuffers()
    {
      if (arrCRGPUBuffer_ != null)
      {
        for (int i = 0; i < nGameObjects_; i++)
        {
          if (arrCRGPUBuffer_[i] != null)
          {
            CRGPUBuffer crGPUBuffer = arrCRGPUBuffer_[i];
            if (crGPUBuffer != null)
            {
              crGPUBuffer.Clear();
            }
          }
        }
      }
    }

    private void LoadAnimationCommon(BinaryReader br, bool fromEditor)
    {
      binaryVersion_ = br.ReadInt32();

      interpolationModeActive_ = interpolate;

      vertexCompression_ = br.ReadBoolean();
      tangentsData_      = br.ReadBoolean();
      frameCount_        = br.ReadInt32();
      fps_               = br.ReadInt32();
      nGameObjects_      = br.ReadInt32();

      if (binaryVersion_ >= 8)
      {
        fileHeaderFlags_ = (EFileHeaderFlags)br.ReadUInt32();
      }
      else
      {
        fileHeaderFlags_ = EFileHeaderFlags.NONE;
        if (vertexCompression_)
        {
          fileHeaderFlags_ |= EFileHeaderFlags.BOXCOMPRESSION;
        }
      }

      alignedData_        = fileHeaderFlags_.FlagCheck(EFileHeaderFlags.ALIGNEDDATA);
      boxCompression_     = fileHeaderFlags_.FlagCheck(EFileHeaderFlags.BOXCOMPRESSION);
      fiberCompression_   = fileHeaderFlags_.FlagCheck(EFileHeaderFlags.FIBERCOMPRESSION);
      vertexLocalSystems_ = fileHeaderFlags_.FlagCheck(EFileHeaderFlags.VERTEXLOCALSYSTEMS);

      lastFrame_       = Mathf.Max(frameCount_ - 1, 0);

      animationLength_ = (float)lastFrame_ / (float)fps_;
      frameTime_       = 1.0f / (float)fps_;

      arrGOPath_             = new string[nGameObjects_];
      arrGOInfo_             = new CRGOInfo[nGameObjects_];
      arrSkipObject_         = new BitArray(nGameObjects_, false);
      arrIsBone_             = new BitArray(nGameObjects_, false);
      arrVisibilityInterval_ = new Vector2[nGameObjects_];
      arrMesh_               = new Mesh[nGameObjects_];
      arrCRGPUBuffer_        = new CRGPUBuffer[nGameObjects_];

      if (fiberCompression_)
      {
        arrDefinition_      = new CRDefinition[nGameObjects_];
        arrCompressedPose_  = new CRCompressedPose[nGameObjects_];
      }

      if (vertexLocalSystems_)
      {
        arrVertexDataCache_ = new CRVertexDataCache[nGameObjects_];
      }

      arrCacheIndex_         = new int[nGameObjects_];

      dictVertexCountCacheIdx_.Clear();
      listVertexCacheCount_   .Clear();

      internalUsingGpu_      = false;
      internalGPUBufferSize_ = gpuFrameBufferSize_;

      if (GPUModeRequested && !fromEditor)
      {
        internalUsingGpu_ = true;
        if (bufferAllFrames_)
        {
          internalGPUBufferSize_ = frameCount_;
        }

        gpuBufferer_ = new CRGPUBufferer(internalGPUBufferSize_ + 1);
        if (useDoubleSidedShader_)
        {
          vertexAnimShader = (Shader)Resources.Load("Standard VA (double sided)");
        }
        else
        {
          vertexAnimShader = (Shader)Resources.Load("Standard VA");
        }

      }

      AssignReadFrameDelegate();
    }

    private void LoadAnimationV0(BinaryReader br, bool fromEditor)
    {
      for (int i = 0; i < nGameObjects_; i++)
      {
        string relativePath = br.ReadString();
        int vertexCount     = br.ReadInt32();
        int boneCount       = 0;

        arrGOPath_[i] = relativePath;
 
        CreateCacheIdx(i, vertexCount);
        int offsetBytesSize = CalculateStreamOffsetSize( vertexCount, boneCount, 0 );

        Transform tr = transform.Find(relativePath); 
        arrGOInfo_[i] = new CRGOInfo(tr, vertexCount, offsetBytesSize, 0, 0);
        
        if ( tr == null || 
           ( tr != null && ( vertexCount > 0 && !tr.gameObject.HasMesh() ) ) )
        {
          arrSkipObject_[i] = true;
          continue;
        }
  
        AssignGameObject(fromEditor, null, null, null, ref arrGOInfo_[i], ref arrMesh_[i], ref arrCRGPUBuffer_[i]);
        
      } //for GameObjects...

      CreateCaches();

      arrFrameOffsets_ = new long[frameCount_];
      for (int i = 0; i < frameCount_; i++)
      {
        arrFrameOffsets_[i] = br.ReadInt64();
      }
    }

    private void LoadAnimationV5(BinaryReader br, bool fromEditor)
    {
      for (int i = 0; i < nGameObjects_; i++)
      {
        string relativePath = br.ReadString();
        int vertexCount     = br.ReadInt32();
        
        arrGOPath_[i] = relativePath;
     
        CreateCacheIdx(i, vertexCount);
        int offsetBytesSize = CalculateStreamOffsetSize( vertexCount, 0, 0 );

        Transform tr  = transform.Find(relativePath);
        arrGOInfo_[i] = new CRGOInfo(tr, vertexCount, offsetBytesSize, 0, 0);

        if ( tr == null || 
           ( tr != null && ( vertexCount > 0 && !tr.gameObject.HasMesh() ) ) )
        {
          arrSkipObject_[i] = true;
          continue;
        }

        arrIsBone_[i] = ( vertexCount == 0 && !tr.gameObject.HasMesh() );
        arrVisibilityInterval_[i] = Vector2.zero;


        AssignGameObject(fromEditor, null, null, null, ref arrGOInfo_[i], ref arrMesh_[i], ref arrCRGPUBuffer_[i]);
        
      } //for GameObjects...


      nEmitters_ = br.ReadInt32();
      arrEmitterName_ = new string[nEmitters_];
      for (int i = 0; i < nEmitters_; i++)
      {
        arrEmitterName_[i] = br.ReadString();
      }

      CreateCaches();

      arrFrameOffsets_ = new long[frameCount_];
      for (int i = 0; i < frameCount_; i++)
      {
        arrFrameOffsets_[i] = br.ReadInt64();
      }
    }

    private void LoadAnimationV6(BinaryReader br, bool fromEditor)
    {
      for (int i = 0; i < nGameObjects_; i++)
      {
        string relativePath = br.ReadString();
        int vertexCount     = br.ReadInt32();
        Vector2 v           = new Vector2( br.ReadSingle(), br.ReadSingle() );

        arrGOPath_[i] = relativePath;
     
        CreateCacheIdx(i, vertexCount);
        int offsetBytesSize = CalculateStreamOffsetSize( vertexCount, 0, 0 );

        Transform tr  = transform.Find(relativePath);
        arrGOInfo_[i] = new CRGOInfo(tr, vertexCount, offsetBytesSize, 0, 0);

        if ( tr == null || 
           ( tr != null && ( vertexCount > 0 && !tr.gameObject.HasMesh() ) ) )
        {
          arrSkipObject_[i] = true;
          continue;
        }

        arrIsBone_[i] = ( vertexCount == 0 && !tr.gameObject.HasMesh() );
        arrVisibilityInterval_[i] = v;


        AssignGameObject(fromEditor, null, null, null, ref arrGOInfo_[i], ref arrMesh_[i], ref arrCRGPUBuffer_[i]);
                
      } //for GameObjects...


      nEmitters_ = br.ReadInt32();
      arrEmitterName_ = new string[nEmitters_];
      for (int i = 0; i < nEmitters_; i++)
      {
        arrEmitterName_[i] = br.ReadString();
      }

      CreateCaches();

      arrFrameOffsets_ = new long[frameCount_];
      for (int i = 0; i < frameCount_; i++)
      {
        arrFrameOffsets_[i] = br.ReadInt64();
      }
    }

    private void LoadAnimationV78(BinaryReader br, bool fromEditor)
    {
      int currentBoneIndex = 0;
      List<Transform> listBonesTransform = new List<Transform>();

      for (int i = 0; i < nGameObjects_; i++)
      {
        string relativePath = br.ReadString();
        int vertexCount     = br.ReadInt32();
        int boneCount       = br.ReadInt32();

        Vector2 v = new Vector2( br.ReadSingle(), br.ReadSingle() );

        arrGOPath_[i] = relativePath;
     
        CreateCacheIdx(i, vertexCount);

        int boneIdxBegin = currentBoneIndex;
        currentBoneIndex += boneCount;
        int boneIdxEnd   = currentBoneIndex;;

        CRDefinition definition = null;
        CRCompressedPose compressedPose = null;
        CRVertexDataCache vertexDataCache = null;
        int compressedPoseBytesOffset = 0;

        if (vertexCount > 0)
        {
          if (fiberCompression_)
          {
            definition = new CRDefinition();
            definition.Init(br, internalUsingGpu_);
            arrDefinition_[i] = definition;

            compressedPose = new CRCompressedPose(definition.GetNumberOfFibers(), internalUsingGpu_);
            arrCompressedPose_[i] = compressedPose;
            compressedPoseBytesOffset = compressedPose.GetBytesOffset(definition);
          }

          if (vertexLocalSystems_)
          {
            vertexDataCache = new CRVertexDataCache(vertexCount);
            vertexDataCache.Load(br);
            arrVertexDataCache_[i] = vertexDataCache;
          }
        }

        int offsetBytesSize = CalculateStreamOffsetSize( vertexCount, boneCount, compressedPoseBytesOffset );
        Transform tr  = transform.Find(relativePath);

        if (boneCount > 0)
        {
          arrGOInfo_[i] = new CRGOInfo( tr, vertexCount, offsetBytesSize, boneIdxBegin, boneIdxEnd );
        }
        else
        {
          arrGOInfo_[i] = new CRGOInfo( tr, vertexCount, offsetBytesSize, 0, 0 );
        }
        
        for (int j = 0; j < boneCount; j++)
        {
          string boneRelativePath = br.ReadString();
          Transform boneTr  = transform.Find(boneRelativePath);
          listBonesTransform.Add(boneTr);
        }
       
        if ( tr == null || 
           ( tr != null && ( vertexCount > 0 && !tr.gameObject.HasMesh() ) ) )
        {
          arrSkipObject_[i] = true;
          continue;
        }

        arrIsBone_[i] = ( vertexCount == 0 && !tr.gameObject.HasMesh() );
        arrVisibilityInterval_[i] = v;

        AssignGameObject(fromEditor, definition, compressedPose, vertexDataCache, ref arrGOInfo_[i], ref arrMesh_[i], ref arrCRGPUBuffer_[i]); 
       
      } //for GameObjects...

      arrBoneTr_ = listBonesTransform.ToArray();
      if (fromEditor)
      {
        foreach(Transform tr in arrBoneTr_)
        {
          if (tr != null)
          {
            CRGOTmpData boneTmpData = new CRGOTmpData(tr.gameObject);
            listGOTmpData_.Add(boneTmpData);
          }
        }     
      }

      nEmitters_ = br.ReadInt32();
      arrEmitterName_ = new string[nEmitters_];
      for (int i = 0; i < nEmitters_; i++)
      {
        arrEmitterName_[i] = br.ReadString();
      }

      CreateCaches();

      arrFrameOffsets_ = new long[frameCount_];
      for (int i = 0; i < frameCount_; i++)
      {
        arrFrameOffsets_[i] = br.ReadInt64();
      }
    }

    public bool IsVertexAnimated(GameObject go)
    {
      return ( setVertexAnimatedGO_.Contains(go) );
    }

    private bool ReadBoolean(ref int cursor)
    {
      int offset = cursor;
      cursor += sizeof(bool);
      return BitConverter.ToBoolean(binaryAnim_, offset);
    }

    private string ReadString(ref int cursor)
    {
      string str = BitConverter.ToString(binaryAnim_, cursor);
      cursor += str.Length * (sizeof(char) + 1);
      return str;
    }

    private byte ReadByte(ref int cursor)
    {
      return CRBinaryReader.ReadByteFromArrByte(binaryAnim_, ref cursor);
    }

    private SByte ReadSByte(ref int cursor)
    {
      return CRBinaryReader.ReadSByteFromArrByte(binaryAnim_, ref cursor);
    }

    private UInt16 ReadUInt16(ref int cursor)
    {
      return (CRBinaryReader.ReadUInt16FromArrByte(binaryAnim_, ref cursor) );
    }  

    private Int32 ReadInt32(ref int cursor)
    {
      return (CRBinaryReader.ReadInt32FromArrByte(binaryAnim_, ref cursor) );
    }  


    private Int64 ReadInt64(ref int cursor)
    {
      return (CRBinaryReader.ReadInt64FromArrByte(binaryAnim_, ref cursor) );
    }


    private float ReadSingle(ref int cursor)
    {
      return CRBinaryReader.ReadSingleFromArrByte(binaryAnim_, ref cursor);
    }

    private void SetCursorAt(Int64 bytesOffset, ref int cursor)
    {
      cursor = (int)bytesOffset;
    }

    private void AdvanceCursor(Int64 bytesOffset, ref int cursor)
    {
      cursor += (int)bytesOffset;
    }

    private void AdvanceCursorIfExists(long offsetBytesSize, bool exists)
    {
      if (exists)
      {
        if (alignedData_)
        {
          AdvanceCursorPadding4(ref bCursor1_);
        }
        AdvanceCursor(offsetBytesSize, ref bCursor1_);
      }
    }

    private void AdvanceCursorSkipMesh(int vertexCount, bool isCompressed, bool hasTangents, ref int cursor)
    {
      if (isCompressed)
      {
        AdvanceCursor(vertexCount * sizeof(UInt16) * 3, ref cursor);
        AdvanceCursor(vertexCount * sizeof(SByte) * 3, ref cursor);

        if (hasTangents)
        {
          AdvanceCursor(vertexCount * sizeof(SByte) * 4, ref cursor);
        }
      }
      else
      {
        AdvanceCursor(vertexCount * sizeof(float) * 3, ref cursor);
        AdvanceCursor(vertexCount * sizeof(float) * 3, ref cursor);
        if (hasTangents)
        {
          AdvanceCursor(vertexCount * sizeof(float) * 4, ref cursor);
        }
      }
    }


    private void AdvanceCursorsIfExists(long offsetBytesSize, bool existPrev, bool existNext )
    {
      if (existPrev)
      {
        if (alignedData_)
        {
          AdvanceCursorPadding4(ref bCursor1_);
        }
        AdvanceCursor(offsetBytesSize, ref bCursor1_);
      }
      if (existNext)
      {
        if (alignedData_)
        {
          AdvanceCursorPadding4(ref bCursor2_);
        }
        AdvanceCursor(offsetBytesSize, ref bCursor2_);
      }
    }

    private void AdvanceCursorPadding4(ref int cursor)
    {
      int padding = cursor % 4;      
      if (padding != 0)
      {
        AdvanceCursor(4 - padding, ref cursor);
      }
    }

    private void AssignGameObject( bool fromEditor, CRDefinition definition, CRCompressedPose compressedPose, CRVertexDataCache vertexDataCache, 
                                   ref CRGOInfo goData, ref Mesh mesh, ref CRGPUBuffer crGPUBuffer)
    {
      Transform tr = goData.tr_;
      GameObject go = tr.gameObject;

      int vertexCount = goData.vertexCount_;
      int boneCount = goData.boneCount_;

      if (tr != null)
      {
        if (vertexCount > 0 || boneCount > 0)
        {
          setVertexAnimatedGO_.Add(go);
        }
      }

      if (fromEditor)
      {
        CRGOTmpData goTmpData = new CRGOTmpData(go);
        listGOTmpData_.Add(goTmpData);
      }

      if (vertexCount > 0)
      {
        if (fromEditor)
        {
          Mesh tmpMesh = listGOTmpData_[listGOTmpData_.Count - 1].tmp_Mesh_;
          MeshFilter mf = go.GetComponent<MeshFilter>();
          mf.sharedMesh = tmpMesh;
          mesh = tmpMesh;
        }
        else
        {
          mesh = go.GetMeshInstance();
        }

        if (internalUsingGpu_)
        {
          Renderer rn = go.GetComponent<Renderer>();
          if (rn != null)
          {
            SetGPUBuffersAndTextures(fromEditor, vertexCount, definition, compressedPose, vertexDataCache, rn, ref crGPUBuffer); 
          }

        }
        tr.localScale = Vector3.one;
      }
    }

    private void SetGPUBuffersAndTextures(bool fromEditor, int vertexCount, CRDefinition definition, CRCompressedPose compressedPose, 
                                          CRVertexDataCache vertexDataCache, Renderer rn, ref CRGPUBuffer crGPUBuffer)
    {
      crGPUBuffer = new CRGPUBuffer(internalGPUBufferSize_, vertexCount, vertexCompression_, boxCompression_, fiberCompression_, 
                                    definition, compressedPose, vertexLocalSystems_, vertexDataCache);

      if (overrideShaderForVertexAnimation_)
      {
        Material[] arrMaterial = rn.materials;
        if (arrMaterial != null)
        {
          int nMaterial = arrMaterial.Length;
          for (int i = 0; i < nMaterial; i++)
          {
            Material mat = arrMaterial[i];
            if (mat != null)
            {
              mat.shader = vertexAnimShader;
              arrMaterial[i].SetTexture("_PositionsTex", crGPUBuffer.PositionTexture);
              arrMaterial[i].SetTexture("_NormalsTex", crGPUBuffer.NormalTexture);
              arrMaterial[i].SetFloat("_useSampler", 1.0f); 
            }
   
          }
          rn.materials = arrMaterial;
        }
      }
      else
      {
        MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        mpb.SetTexture("_PositionsTex", crGPUBuffer.PositionTexture);
        mpb.SetTexture("_NormalsTex", crGPUBuffer.NormalTexture);
        mpb.SetFloat("_useSampler", 1.0f);
        rn.SetPropertyBlock(mpb);
      }
    }

    private void AssignReadFrameDelegate()
    {
      if ( binaryVersion_ == 4 )
      {
        readFrameDel_ = ReadFrameV4;
      }
      else if ( binaryVersion_ == 5 )
      {
        readFrameDel_ = ReadFrameV5;     
      }
      else if (binaryVersion_ == 6)
      {
        if (interpolate)
        {
          readFrameDel_ = ReadFrameInterpolatedV6;
        }
        else
        {
          readFrameDel_ = ReadFrameV6;
        }
      }
      else if (binaryVersion_ == 7 ||
               binaryVersion_ == 8)
      {
        if (interpolate)
        {
          readFrameDel_ = ReadFrameInterpolatedV78;
        }
        else
        {
          readFrameDel_ = ReadFrameV78;
        }
      }

      if (internalUsingGpu_)
      {
        AssignComputeShaders();
      }

    }

    private void AssignComputeShaders()
    {
      cShaderInterpolation_ = (ComputeShader)Resources.Load("CRInterpolationCS");

      if (vertexLocalSystems_)
      {
        cShaderNormals_ = (ComputeShader)Resources.Load("CRVertexNormalsFastUpdaterCS");
      }

      if (vertexCompression_)
      {
        if (boxCompression_)
        {
          cShaderPositions_ = (ComputeShader)Resources.Load("CRVertexAnimationBoxCS");
        }
        else if (fiberCompression_)
        {
          cShaderPositions_ = (ComputeShader)Resources.Load("CRVertexAnimationFiberCS");
        }
      }
      else
      {
        cShaderPositions_ = (ComputeShader)Resources.Load("CRVertexAnimationCS");
      }
    }

    private int CalculateStreamOffsetSize( int vertexCount, int boneCount, int compressedPoseBytesOffset )
    {
      const int nLocationComponents = 3;
      const int nRotationComponents = 4;

      const int nBoneTranslationComponents = 3;
      const int nBoneRotationComponents    = 4;
      const int nBoneScaleComponents       = 3;

      const int nBoxComponents = 6;
      const int nPositionComponents = 3;
      const int nNormalComponents   = 3;
      const int nTangentComponents  = 4;

      const int nFloatBytes    = sizeof(float);
      const int nUInt16Bytes   = sizeof(UInt16);
      const int nSByteByes     = sizeof(sbyte);

      int bytesAdvance = nFloatBytes * (nLocationComponents + nRotationComponents);

      if (boneCount > 0)
      {
        bytesAdvance += boneCount * ( nFloatBytes * (nBoneTranslationComponents + nBoneRotationComponents + nBoneScaleComponents) );
      }

      if (vertexCount > 0)
      {
        if ( vertexCompression_ )
        { 
          if (fiberCompression_)
          {
            bytesAdvance += compressedPoseBytesOffset;
          }
          else
          {
            bytesAdvance += nFloatBytes * nBoxComponents + (nUInt16Bytes * nPositionComponents + nSByteByes * nNormalComponents ) * vertexCount;
            if (tangentsData_)
            {
              bytesAdvance += nSByteByes * nTangentComponents * vertexCount;
            }    
          }
        } 
        else
        {
          bytesAdvance += nFloatBytes * nBoxComponents + ( nFloatBytes * ( nPositionComponents + nNormalComponents ) ) * vertexCount;
          if (tangentsData_)
          {
            bytesAdvance += nFloatBytes * nTangentComponents * vertexCount;
          }
        }
      }
 
      return bytesAdvance;
    }


    private void CreateCacheIdx(int gameObjectIdx, int vertexCount)
    {
      if (vertexCount > 0)
      {
        if (!dictVertexCountCacheIdx_.ContainsKey(vertexCount))
        {
          listVertexCacheCount_.Add(vertexCount);
          dictVertexCountCacheIdx_[vertexCount] = listVertexCacheCount_.Count - 1;
        }
        arrCacheIndex_[gameObjectIdx] = dictVertexCountCacheIdx_[vertexCount];
      }
      else
      {
        arrCacheIndex_[gameObjectIdx] = -1;
      }
    }

    private void CreateCaches()
    {
      int nCaches = listVertexCacheCount_.Count;

      arrVertex3Cache1_ = new Vector3[nCaches][];
      arrVertex3Cache2_ = new Vector3[nCaches][];
      arrVertex4Cache_  = new Vector4[nCaches][];
      
      for (int i = 0; i < nCaches; i++)
      {
        arrVertex3Cache1_[i] = new Vector3[listVertexCacheCount_[i]];
        
        if (tangentsData_ || internalUsingGpu_)
        {
          arrVertex4Cache_[i] = new Vector4[listVertexCacheCount_[i]];
        }

        if (!internalUsingGpu_)
        {
          arrVertex3Cache2_[i] = new Vector3[listVertexCacheCount_[i]];
        }
      }
    }

    private void CheckLoadAnimationChanged()
    {
      bool isCRAnimationAsset = animationFileType == AnimationFileType.CRAnimationAsset;
      bool isTextAsset = animationFileType == AnimationFileType.TextAsset;

      if ( (isCRAnimationAsset && activeAnimation != animationLastLoaded_)  ||
           (isTextAsset && activeAnimationText != animationLastLoadedText_))
      {
        LoadAnimation(false);
      }
    }

    void Update()
    {
      CheckLoadAnimationChanged();

      if (animate && !internalPaused_ && binaryAnim_ != null)
      {
        timeInternal_ += Time.deltaTime  * speed;

        if (animatorSync_ != null)
        {
          AnimatorStateInfo asi = animatorSync_.GetCurrentAnimatorStateInfo(0);
          timeInternal_ = asi.normalizedTime * asi.length;
        }

        switch (repeatMode)
        {
          case RepeatMode.Loop:
            time_ = Mathf.Repeat((float)timeInternal_, animationLength_);
            break;
          case RepeatMode.PingPong:
            time_ = Mathf.PingPong((float)timeInternal_, animationLength_);
            break;
          case RepeatMode.Clamp:
            time_ = Mathf.Clamp((float)timeInternal_, 0.0f, animationLength_);
            break;
        }

        ReadFrameAtCurrentTime();
      }
    }

    void OnApplicationPause(bool pauseStatus) 
    {
      internalPaused_ = pauseStatus;
    }

    public void SetFrame( float frame )
    {
      float time = frame / (float)fps_;
      SetTime( time );
    }
 
    public void SetTime( float time )
    {
      timeInternal_ = time;
      time_         = time;

      ReadFrameAtCurrentTime();
    }

    public void Update(float time)
    {
      if (animate)
      {
        timeInternal_ += time * speed;

        switch (repeatMode)
        {
          case RepeatMode.Loop:
            time_ = Mathf.Repeat( (float)timeInternal_, animationLength_ );
            break;
          case RepeatMode.PingPong:
            time_ = Mathf.PingPong( (float)timeInternal_, animationLength_ );
            break;
          case RepeatMode.Clamp:
            time_ = Mathf.Clamp( (float)timeInternal_, 0.0f, animationLength_ );
            break;
        }

        ReadFrameAtCurrentTime();
      }
    }

    public bool IsBoxCompression()
    {
      return boxCompression_;
    }

    public bool IsFiberCompression()
    {
      return fiberCompression_;
    }

    private void ReadFrameAtCurrentTime()
    {
      if (interpolate != interpolationModeActive_)
      {
          AssignReadFrameDelegate();
          interpolationModeActive_ = interpolate;
      }

      if (binaryAnim_ != null)
      {
        float floatFrame = Mathf.Clamp(time_ * fps_, 0f, (float)lastFrame_);
        readFrameDel_(floatFrame);
      }
    }

    private void SetVisibility(Transform tr, bool isBone, bool isVisible)
    {
      GameObject go = tr.gameObject;

      if (isBone)
      {
        if ( isVisible && go.activeInHierarchy )
        { 
          tr.localScale = Vector3.one;
        }
        else
        {
          tr.localScale = Vector3.zero;
        }
      }
      else
      {
        go.SetActive(isVisible);      
      }
    }

    private void ReadFrameV4(float frame)
    {
      int nearFrame = (int)Mathf.RoundToInt(frame);

      if ( lastReadFrame_ == nearFrame )
      {
        return;
      }

      SetCursorAt(arrFrameOffsets_[nearFrame], ref bCursor1_);

      for ( int i = 0; i < nGameObjects_; i++ )
      {
        CRGOInfo goInfo = arrGOInfo_[i];

        Transform tr        = goInfo.tr_;
        int vertexCount     = goInfo.vertexCount_;
        int offsetBytesSize = goInfo.bytesOffset_;

        EGOKeyFrameFlags flags = (EGOKeyFrameFlags)ReadByte(ref bCursor1_);

        bool isVisible = ( flags & EGOKeyFrameFlags.VISIBLE ) == EGOKeyFrameFlags.VISIBLE;

        if ( tr == null || (vertexCount > 0 && arrMesh_[i] == null) )
        {
          if ( isVisible )
          {
            AdvanceCursor(offsetBytesSize, ref bCursor1_);
          }
          continue;
        }

        tr.gameObject.SetActive( isVisible );
          
        if (isVisible)
        {
          ReadRQ(tr, ref bCursor1_);

          if (vertexCount > 0)
          {  
            Mesh mesh    = arrMesh_[i];
            int cacheIdx = arrCacheIndex_[i];
            ReadMeshVerticesCPU(mesh, cacheIdx, vertexCount);
          }

        } //isVisible

      } //forGameobjects

      lastReadFrame_ = nearFrame;
    }

    private void ReadFrameV5(float frame)
    {
      int nearFrame = (int)Mathf.RoundToInt(frame);

      if ( lastReadFrame_ == nearFrame )
      {
        return;
      }

      long frameOffset = arrFrameOffsets_[nearFrame];

      SetCursorAt(frameOffset, ref bCursor1_);
      for ( int i = 0; i < nGameObjects_; i++ )
      {
        CRGOInfo goInfo = arrGOInfo_[i];

        Transform tr        = goInfo.tr_;
        int vertexCount     = goInfo.vertexCount_;
        int offsetBytesSize = goInfo.bytesOffset_;

        EGOKeyFrameFlags flags = (EGOKeyFrameFlags)ReadByte(ref bCursor1_);

        bool isVisible = ( flags & EGOKeyFrameFlags.VISIBLE ) == EGOKeyFrameFlags.VISIBLE;
        bool skipGameObject = arrSkipObject_[i];

        if ( skipGameObject )
        {
          if (isVisible)
          {
            AdvanceCursor(offsetBytesSize, ref bCursor1_);
          }
          continue;
        }

        if ( doRuntimeNullChecks )
        {
          bool isGONull   = tr == null;
          bool isMeshNull = (vertexCount > 0) && (arrMesh_[i] == null);

          if ( isGONull || isMeshNull ) 
          {
            if (isVisible)
            {
              AdvanceCursor(offsetBytesSize, ref bCursor1_);
            }     
            continue;
          }      
        }

        SetVisibility(tr, arrIsBone_[i], isVisible);

        if (isVisible)
        {
          ReadRQ(tr, ref bCursor1_);

          if (vertexCount > 0)
          {  
            Mesh mesh    = arrMesh_[i];
            int cacheIdx = arrCacheIndex_[i];
            ReadMeshVerticesCPU(mesh, cacheIdx, vertexCount);
          }

        } //isVisible

      } //forGameobjects

      ReadEvents(ref bCursor1_);

      lastReadFrame_ = nearFrame;
    }

    private void ReadFrameV6(float frame)
    {
      int nearFrame = (int)Mathf.RoundToInt(frame);

      if ( lastReadFrame_ == nearFrame )
      {
        return;
      }

      long frameOffset = arrFrameOffsets_[nearFrame];
      SetCursorAt(frameOffset, ref bCursor1_);

      for (int i = 0; i < nGameObjects_; i++)
      {
        CRGOInfo goInfo = arrGOInfo_[i];

        Transform tr        = goInfo.tr_;
        int vertexCount     = goInfo.vertexCount_;
        int offsetBytesSize = goInfo.bytesOffset_;

        EGOKeyFrameFlags flagsnear = (EGOKeyFrameFlags)ReadByte(ref bCursor1_);

        bool isVisible = (flagsnear & EGOKeyFrameFlags.VISIBLE) == EGOKeyFrameFlags.VISIBLE;
        bool isGhost   = (flagsnear & EGOKeyFrameFlags.GHOST)   == EGOKeyFrameFlags.GHOST;

        bool exists = isVisible || isGhost;
        
        bool skipGameObject = arrSkipObject_[i];
        if (skipGameObject)
        {
          AdvanceCursorIfExists(offsetBytesSize, exists);
          continue;
        }

        if (doRuntimeNullChecks)
        {
          bool isGONull = tr == null;
          bool isMeshNull = (vertexCount > 0) && (arrMesh_[i] == null);

          if (isGONull || isMeshNull)
          {
            AdvanceCursorIfExists(offsetBytesSize, exists);
            continue;
          }
        }
    
        Vector2 visibleTimeInterval = arrVisibilityInterval_[i];

        bool isInsideVisibleTimeInterval = (exists) && 
                                           ( time_ >= visibleTimeInterval.x && time_ < visibleTimeInterval.y );

        SetVisibility(tr, arrIsBone_[i], isInsideVisibleTimeInterval);

        if (isVisible)
        { 
          ReadRQ(tr, ref bCursor1_);

          if (vertexCount > 0)
          {
            Mesh mesh = arrMesh_[i];
            int cacheIdx = arrCacheIndex_[i];
            ReadMeshVerticesCPU(mesh, cacheIdx, vertexCount); 
          }
        }
        else if (isGhost)
        {
          AdvanceCursor(offsetBytesSize, ref bCursor1_);
        }
      } //forGameobjects

      ReadEvents(ref bCursor1_);
      lastReadFrame_ = nearFrame;
    }

    private void ReadFrameInterpolatedV6(float frame)
    {
      if ( lastReadFloatFrame_ == frame )
      {
        return;
      }

      int prevFrame = (int)frame;
      int nextFrame = Mathf.Min(prevFrame + 1, lastFrame_);

      float t = frame - prevFrame;

      long prevFrameOffset = arrFrameOffsets_[prevFrame];
      long nextFrameOffset = arrFrameOffsets_[nextFrame];

      SetCursorAt(prevFrameOffset, ref bCursor1_);
      SetCursorAt(nextFrameOffset, ref bCursor2_);

      for (int i = 0; i < nGameObjects_; i++)
      {
        CRGOInfo goInfo = arrGOInfo_[i];

        Transform tr        = goInfo.tr_;
        int vertexCount     = goInfo.vertexCount_;
        int offsetBytesSize = goInfo.bytesOffset_;

        EGOKeyFrameFlags flagsPrev = (EGOKeyFrameFlags)ReadByte(ref bCursor1_);
        EGOKeyFrameFlags flagsNext = (EGOKeyFrameFlags)ReadByte(ref bCursor2_);

        bool visiblePrev = (flagsPrev & EGOKeyFrameFlags.VISIBLE) == EGOKeyFrameFlags.VISIBLE;
        bool ghostPrev   = (flagsPrev & EGOKeyFrameFlags.GHOST)   == EGOKeyFrameFlags.GHOST;

        bool existsPrev = visiblePrev || ghostPrev;

        bool visibleNext = (flagsNext & EGOKeyFrameFlags.VISIBLE) == EGOKeyFrameFlags.VISIBLE;
        bool ghostNext   = (flagsNext & EGOKeyFrameFlags.GHOST)   == EGOKeyFrameFlags.GHOST;
  
        bool existsNext  = visibleNext || ghostNext;

        bool skipGameObject = arrSkipObject_[i];

        if (skipGameObject)
        {
          AdvanceCursorsIfExists(offsetBytesSize, existsPrev, existsNext);
          continue;
        }

        if (doRuntimeNullChecks)
        {
          bool isGONull = tr == null;
          bool isMeshNull = (vertexCount > 0) && (arrMesh_[i] == null);

          if (isGONull || isMeshNull)
          {
            AdvanceCursorsIfExists(offsetBytesSize, existsPrev, existsNext);
            continue;
          }
        }
   
        Vector2 visibleTimeInterval = arrVisibilityInterval_[i];

        bool isInsideVisibleTimeInterval = (existsPrev && existsNext) && 
                                            ( time_ >= visibleTimeInterval.x && time_ < visibleTimeInterval.y );

        SetVisibility(tr, arrIsBone_[i], isInsideVisibleTimeInterval);
    
        if (!isInsideVisibleTimeInterval)
        {
          AdvanceCursorsIfExists(offsetBytesSize, existsPrev, existsNext);
        }
        else
        {
          float tAux = t;

          if (ghostPrev && visibleNext)
          { 
            float min = visibleTimeInterval.x;
            float max = nextFrame * frameTime_;
            tAux = (time_ - min) / (max - min);
          }
          else if (ghostNext && visiblePrev)
          {
            float min = prevFrame * frameTime_;
            float max = visibleTimeInterval.y;
            tAux = (time_ - min) / (max - min);
          }
          else if (ghostPrev && ghostNext)
          {
            float min = visibleTimeInterval.x;
            float max = visibleTimeInterval.y;
            tAux = (time_ - min) / (max - min);
          }

          ReadRQ(tAux, tr, ref bCursor1_, ref bCursor2_);
          if (vertexCount > 0)
          {
            Mesh mesh = arrMesh_[i];
            int cacheIdx = arrCacheIndex_[i];
            ReadMeshVerticesCPU(tAux, mesh, cacheIdx, vertexCount); 
          }
        }

      } //forGameobjects

      if ( t < 0.5f )
      {
        if (lastReadFrame_ != prevFrame)
        {
          ReadEvents(ref bCursor1_);
          lastReadFrame_ = prevFrame;
        }
      }
      else
      {
        if (lastReadFrame_ != nextFrame)
        {
          ReadEvents(ref bCursor2_);
          lastReadFrame_ = nextFrame;
        }   
      }
  
      lastReadFloatFrame_ = frame;
    }

    private void ReadFrameV78(float frame)
    {
      int nearFrame = (int)Mathf.RoundToInt(frame);

      if ( lastReadFrame_ == nearFrame )
      {
        return;
      }

      if (internalUsingGpu_)
      {
        BufferGPUFrames(nearFrame, internalGPUBufferSize_);
      }

      long frameOffset = arrFrameOffsets_[nearFrame];
      SetCursorAt(frameOffset, ref bCursor1_);

      ReadFrameV78(time_, nearFrame);

      ReadEvents(ref bCursor1_);
      lastReadFrame_ = nearFrame;
    }

    public void ReadFrameV78(float time, int nearFrame)
    {
      for (int i = 0; i < nGameObjects_; i++)
      {
        CRGOInfo goInfo = arrGOInfo_[i];

        Transform tr        = goInfo.tr_;
        int vertexCount     = goInfo.vertexCount_;
        int offsetBytesSize = goInfo.bytesOffset_;
        int boneIdxBegin    = goInfo.boneIdxBegin_;
        int boneIdxEnd      = goInfo.boneIdxEnd_;
        int boneCount       = goInfo.boneCount_;

        EGOKeyFrameFlags flagsnear = (EGOKeyFrameFlags)binaryAnim_[bCursor1_];
        bCursor1_ += sizeof(byte);

        bool isVisible = (flagsnear & EGOKeyFrameFlags.VISIBLE) == EGOKeyFrameFlags.VISIBLE;
        bool isGhost   = (flagsnear & EGOKeyFrameFlags.GHOST)   == EGOKeyFrameFlags.GHOST;

        bool exists = isVisible || isGhost;

        bool skipGameObject = arrSkipObject_[i];
        if (skipGameObject)
        {
          AdvanceCursorIfExists(offsetBytesSize, exists);
          continue;
        }

        if (doRuntimeNullChecks)
        {
          bool isGONull = tr == null;
          bool isMeshNull = (vertexCount > 0) && (arrMesh_[i] == null);

          if (isGONull || isMeshNull)
          {
            AdvanceCursorIfExists(offsetBytesSize, exists);
            continue;
          }
        }

        Vector2 visibleTimeInterval = arrVisibilityInterval_[i];

        bool isInsideVisibleTimeInterval = (exists) &&
                                           (time >= visibleTimeInterval.x && time < visibleTimeInterval.y);


        GameObject go = tr.gameObject;

        if (arrIsBone_[i])
        {
          if (isInsideVisibleTimeInterval && go.activeInHierarchy)
          {
            tr.localScale = Vector3.one;
          }
          else
          {
            tr.localScale = Vector3.zero;
          }
        }
        else
        {
          go.SetActive(isInsideVisibleTimeInterval);
        }

        if (!isInsideVisibleTimeInterval)
        {
          AdvanceCursorIfExists(offsetBytesSize, exists);
        }
        else
        {
          if (alignedData_)
          {
            AdvanceCursorPadding4(ref bCursor1_);
          }

          Vector3 r1;
          Quaternion q1;
         
          r1.x = ReadSingle(ref bCursor1_);
          r1.y = ReadSingle(ref bCursor1_);
          r1.z = ReadSingle(ref bCursor1_);
    
          q1.x = ReadSingle(ref bCursor1_);
          q1.y = ReadSingle(ref bCursor1_);
          q1.z = ReadSingle(ref bCursor1_);
          q1.w = ReadSingle(ref bCursor1_);

          tr.localPosition = r1;
          tr.localRotation = q1;

          if (vertexCount > 0)
          {
            Mesh mesh = arrMesh_[i];
            int cacheIdx = arrCacheIndex_[i];

            if (internalUsingGpu_)
            {
              CRGPUBuffer gpuBuffer = arrCRGPUBuffer_[i];
              int bufferFrame = gpuBufferer_.GetBufferFrame(nearFrame);
              if (fiberCompression_)
              {
                CRDefinition definition = arrDefinition_[i];
                CRCompressedPose compressedPose = arrCompressedPose_[i];
                ReadMeshVerticesFiberGPU(bufferFrame, mesh, definition, compressedPose, gpuBuffer);
              }
              else
              {
                ReadMeshVerticesGPU(bufferFrame, mesh, vertexCount, gpuBuffer);
              }

              if (vertexLocalSystems_ && recomputeNormals_)
              {
                RecomputeNormalsGPU(vertexCount, gpuBuffer);
              }
            }
            else
            {
              if (fiberCompression_)
              {
                CRDefinition     definition     = arrDefinition_[i];
                CRCompressedPose compressedPose = arrCompressedPose_[i];
                ReadMeshVerticesFiberCPU(mesh, definition, compressedPose, cacheIdx, vertexCount);
              }
              else
              {
                ReadMeshVerticesCPU(mesh, cacheIdx, vertexCount);
              }

              if (vertexLocalSystems_ && recomputeNormals_)
              {
                CRVertexDataCache vertexDataCache = arrVertexDataCache_[i];
                RecomputeNormalsCPU(mesh, cacheIdx, vertexDataCache);
              }
            }
          }
          else if (boneCount > 0)
          {
            CRBinaryReader.ReadArrByteToArrBone(binaryAnim_, ref bCursor1_, arrBoneTr_, boneIdxBegin, boneIdxEnd);
          }
        }
      } //forGameobjects
    }

   private void ReadFrameInterpolatedV78(float frame)
   {
      if ( lastReadFloatFrame_ == frame )
      {
        return;
      }

      int prevFrame = (int)frame;
      int nextFrame = Mathf.Min(prevFrame + 1, lastFrame_);

      float t = frame - prevFrame;

      if (internalUsingGpu_)
      {      
        BufferGPUFrames(prevFrame, internalGPUBufferSize_);
        BufferGPUFrames(nextFrame, internalGPUBufferSize_);
      }

      long prevFrameOffset = arrFrameOffsets_[prevFrame];
      long nextFrameOffset = arrFrameOffsets_[nextFrame];

      SetCursorAt(prevFrameOffset, ref bCursor1_);
      SetCursorAt(nextFrameOffset, ref bCursor2_);
 
      ReadFrameInterpolatedV78(time_, t, prevFrame, nextFrame);

      if ( t < 0.5f )
      {
        if (lastReadFrame_ != prevFrame)
        {
          ReadEvents(ref bCursor1_);
          lastReadFrame_ = prevFrame;
        }
      }
      else
      {
        if (lastReadFrame_ != nextFrame)
        {
          ReadEvents(ref bCursor2_);
          lastReadFrame_ = nextFrame;
        }   
      }
  
      lastReadFloatFrame_ = frame;
    }

    public void ReadFrameInterpolatedV78(float time, float t, int prevFrame, int nextFrame)
    {
      for (int i = 0; i < nGameObjects_; i++)
      {
        CRGOInfo goInfo = arrGOInfo_[i];

        Transform tr        = goInfo.tr_;
        int vertexCount     = goInfo.vertexCount_;
        int offsetBytesSize = goInfo.bytesOffset_;
        int boneIdxBegin    = goInfo.boneIdxBegin_;
        int boneIdxEnd      = goInfo.boneIdxEnd_;
        int boneCount       = goInfo.boneCount_;

        EGOKeyFrameFlags flagsPrev = (EGOKeyFrameFlags)binaryAnim_[bCursor1_];
        bCursor1_ += sizeof(byte);

        EGOKeyFrameFlags flagsNext = (EGOKeyFrameFlags)binaryAnim_[bCursor2_];
        bCursor2_ += sizeof(byte);

        bool visiblePrev = (flagsPrev & EGOKeyFrameFlags.VISIBLE) == EGOKeyFrameFlags.VISIBLE;
        bool ghostPrev   = (flagsPrev & EGOKeyFrameFlags.GHOST) == EGOKeyFrameFlags.GHOST;

        bool existsPrev = visiblePrev || ghostPrev;

        bool visibleNext = (flagsNext & EGOKeyFrameFlags.VISIBLE) == EGOKeyFrameFlags.VISIBLE;
        bool ghostNext   = (flagsNext & EGOKeyFrameFlags.GHOST) == EGOKeyFrameFlags.GHOST;

        bool existsNext = visibleNext || ghostNext;

        bool skipGameObject = arrSkipObject_[i];

        if (skipGameObject)
        {
          AdvanceCursorsIfExists(offsetBytesSize, existsPrev, existsNext);
          continue;
        }

        if (doRuntimeNullChecks)
        {
          bool isGONull = tr == null;
          bool isMeshNull = (vertexCount > 0) && (arrMesh_[i] == null);

          if (isGONull || isMeshNull)
          {
            AdvanceCursorsIfExists(offsetBytesSize, existsPrev, existsNext);
            continue;
          }
        }

        Vector2 visibleTimeInterval = arrVisibilityInterval_[i];

        bool isInsideVisibleTimeInterval = (existsPrev && existsNext) &&
                                           (time >= visibleTimeInterval.x && time < visibleTimeInterval.y);

        GameObject go = tr.gameObject;

        if (arrIsBone_[i])
        {
          if (isInsideVisibleTimeInterval && go.activeInHierarchy)
          {
            tr.localScale = Vector3.one;
          }
          else
          {
            tr.localScale = Vector3.zero;
          }
        }
        else
        {
          go.SetActive(isInsideVisibleTimeInterval);
        }

        if (!isInsideVisibleTimeInterval)
        {
          AdvanceCursorsIfExists(offsetBytesSize, existsPrev, existsNext);
        }
        else
        {
          if (alignedData_)
          {
            AdvanceCursorPadding4(ref bCursor1_);
            AdvanceCursorPadding4(ref bCursor2_);
          }
   
          Vector3 r1;
          Quaternion q1;

          r1.x = ReadSingle(ref bCursor1_);
          r1.y = ReadSingle(ref bCursor1_);
          r1.z = ReadSingle(ref bCursor1_);
    
          q1.x = ReadSingle(ref bCursor1_);
          q1.y = ReadSingle(ref bCursor1_);
          q1.z = ReadSingle(ref bCursor1_);
          q1.w = ReadSingle(ref bCursor1_);

          Vector3 r2;
          Quaternion q2;

          r2.x = ReadSingle(ref bCursor2_);
          r2.y = ReadSingle(ref bCursor2_);
          r2.z = ReadSingle(ref bCursor2_);

          q2.x = ReadSingle(ref bCursor2_);
          q2.y = ReadSingle(ref bCursor2_);
          q2.z = ReadSingle(ref bCursor2_);
          q2.w = ReadSingle(ref bCursor2_);    

          float tAux = t;

          if (ghostPrev && visibleNext)
          {
            float min = visibleTimeInterval.x;
            float max = nextFrame * frameTime_;
            tAux = (time - min) / (max - min);
          }
          else if (ghostNext && visiblePrev)
          {
            float min = prevFrame * frameTime_;
            float max = visibleTimeInterval.y;
            tAux = (time - min) / (max - min);
          }
          else if (ghostPrev && ghostNext)
          {
            float min = visibleTimeInterval.x;
            float max = visibleTimeInterval.y;
            tAux = (time - min) / (max - min);
          }

          Vector3 rInterpolated = Vector3.LerpUnclamped(r1, r2, tAux);
          Vector3 rCorrection = Vector3.LerpUnclamped((r1 - rInterpolated), (r2 - rInterpolated), tAux);

          tr.localPosition = rInterpolated;
          tr.localRotation = Quaternion.SlerpUnclamped(q1, q2, tAux);

          if (vertexCount > 0)
          {
            Mesh mesh = arrMesh_[i];
            int cacheIdx = arrCacheIndex_[i];

            if (internalUsingGpu_)
            {
              CRGPUBuffer gpuBuffer = arrCRGPUBuffer_[i];

              int bufferFrame1 = gpuBufferer_.GetBufferFrame(prevFrame);
              int bufferFrame2 = gpuBufferer_.GetBufferFrame(nextFrame);

              if (fiberCompression_)
              {
                CRDefinition definition = arrDefinition_[i];
                CRCompressedPose compressedPose = arrCompressedPose_[i];
                ReadMeshVerticesFiberGPU(tAux, bufferFrame1, bufferFrame2, mesh, definition, compressedPose, gpuBuffer);
              }
              else
              {
                ReadMeshVerticesGPU(tAux, bufferFrame1, bufferFrame2, mesh, vertexCount, gpuBuffer);
              }

              if (vertexLocalSystems_ && recomputeNormals_)
              {
                RecomputeNormalsGPU(vertexCount, gpuBuffer);
              }
            }
            else
            {
              if (fiberCompression_)
              {
                CRDefinition     definition     = arrDefinition_[i];
                CRCompressedPose compressedPose = arrCompressedPose_[i];

                ReadMeshVerticesFiberCPU(tAux, mesh, definition, compressedPose, cacheIdx, vertexCount);
              }
              else
              {
                ReadMeshVerticesCPU(tAux, mesh, cacheIdx, vertexCount);
              }

              if (vertexLocalSystems_ && recomputeNormals_)
              {
                CRVertexDataCache vertexDataCache = arrVertexDataCache_[i];
                RecomputeNormalsCPU(mesh, cacheIdx, vertexDataCache);
              }
            }
          }
          else if (boneCount > 0)
          {
            CRBinaryReader.ReadArrByteToArrBone(binaryAnim_, ref bCursor1_, ref bCursor2_, tAux, arrBoneTr_, boneIdxBegin, boneIdxEnd, rCorrection);
          }
        }

      } //forGameobjects
    }

    private void BufferGPUFrames(int frame, int nFramesToBuffer)
    {
      if (gpuBufferer_.GetNumberOfFramesBuffered() >= frameCount_)
      {
        return;
      }

      int iFrame = frame;
      int nFramesBuffered = 0;

      while (nFramesBuffered < nFramesToBuffer)
      {
        if (iFrame > lastFrame_)
        {
          iFrame = 0;
        }
        if (iFrame < 0)
        {
          iFrame = lastFrame_;
        }

        if (!gpuBufferer_.IsFrameBuffered(iFrame))
        {
          BufferGPUFrameV78(iFrame);
        }

        if (speed > 0)
        {
          iFrame++;
        }
        else
        {
          iFrame--;
        }

        nFramesBuffered++;

        if (nFramesBuffered > frameCount_)
        {
          break;
        }
      }
    }

    private void BufferGPUFrameV78(int frame)
    {
      gpuBufferer_.AddFrameToBuffer(frame);
      int bufferFrame = gpuBufferer_.GetBufferFrame(frame);

      long frameOffset = arrFrameOffsets_[frame];
      SetCursorAt(frameOffset, ref bCursor1_);

      for (int i = 0; i < nGameObjects_; i++)
      {
        CRGOInfo goInfo = arrGOInfo_[i];

        Transform tr        = goInfo.tr_;
        int vertexCount     = goInfo.vertexCount_;
        int offsetBytesSize = goInfo.bytesOffset_;

        EGOKeyFrameFlags flagsnear = (EGOKeyFrameFlags)ReadByte(ref bCursor1_);

        bool isVisible = (flagsnear & EGOKeyFrameFlags.VISIBLE) == EGOKeyFrameFlags.VISIBLE;
        bool isGhost   = (flagsnear & EGOKeyFrameFlags.GHOST)   == EGOKeyFrameFlags.GHOST;

        bool exists = isVisible || isGhost;

        bool skipGameObject = arrSkipObject_[i];
        if (skipGameObject || vertexCount == 0)
        {
          AdvanceCursorIfExists(offsetBytesSize, exists);
          continue;
        }

        if (doRuntimeNullChecks)
        {
          bool isGONull = tr == null;
          bool isMeshNull = (vertexCount > 0) && (arrMesh_[i] == null);

          if (isGONull || isMeshNull)
          {
            AdvanceCursorIfExists(offsetBytesSize, exists);
            continue;
          }
        }

        Vector2 visibleTimeInterval = arrVisibilityInterval_[i];
        bool isInsideVisibleTimeInterval = (exists) &&
                                           (time_ >= visibleTimeInterval.x && time_ < visibleTimeInterval.y);

        if (isInsideVisibleTimeInterval)
        {
          if (alignedData_)
          {
            AdvanceCursorPadding4(ref bCursor1_);
          }

          //RQ
          AdvanceCursor(28, ref bCursor1_);
        
          CRGPUBuffer crGPUBuffer = arrCRGPUBuffer_[i];
          crGPUBuffer.BufferFrameMesh(bufferFrame, vertexCompression_, boxCompression_, fiberCompression_, tangentsData_, binaryAnim_, ref bCursor1_);
        }
        else if (exists)
        {
          AdvanceCursor(offsetBytesSize, ref bCursor1_);
        }
      } //forGameobjects
    }

    private void ReadRQ( Transform tr, ref int cursor )
    {
      Vector3 r1;
      Quaternion q1;

      r1.x = ReadSingle(ref cursor);
      r1.y = ReadSingle(ref cursor);
      r1.z = ReadSingle(ref cursor);

      q1.x = ReadSingle(ref cursor);
      q1.y = ReadSingle(ref cursor);
      q1.z = ReadSingle(ref cursor);
      q1.w = ReadSingle(ref cursor);

      tr.localPosition = r1;
      tr.localRotation = q1;
    }

    private void ReadRQ( float t, Transform tr, ref int cursor1, ref int cursor2 )
    {
        Vector3 r1;
        Quaternion q1;

        r1.x = ReadSingle(ref cursor1);
        r1.y = ReadSingle(ref cursor1);
        r1.z = ReadSingle(ref cursor1);

        q1.x = ReadSingle(ref cursor1);
        q1.y = ReadSingle(ref cursor1);
        q1.z = ReadSingle(ref cursor1);
        q1.w = ReadSingle(ref cursor1);

        Vector3    r2;
        Quaternion q2;

        r2.x = ReadSingle(ref cursor2);
        r2.y = ReadSingle(ref cursor2);
        r2.z = ReadSingle(ref cursor2);

        q2.x = ReadSingle(ref cursor2);
        q2.y = ReadSingle(ref cursor2);
        q2.z = ReadSingle(ref cursor2);
        q2.w = ReadSingle(ref cursor2);

        tr.localPosition = Vector3.LerpUnclamped(r1, r2, t);
        tr.localRotation = Quaternion.SlerpUnclamped(q1, q2, t);
    }
  

    private void ReadMeshVerticesCPU(Mesh mesh, int cacheIdx, int vertexCount)
    {
      Vector3 boundsMin;
      boundsMin.x = ReadSingle(ref bCursor1_);
      boundsMin.y = ReadSingle(ref bCursor1_);
      boundsMin.z = ReadSingle(ref bCursor1_);

      Vector3 boundsMax;
      boundsMax.x = ReadSingle(ref bCursor1_);
      boundsMax.y = ReadSingle(ref bCursor1_);
      boundsMax.z = ReadSingle(ref bCursor1_);

      Vector3[] vector3cache = arrVertex3Cache1_[cacheIdx];
      Vector3 boundSize = (boundsMax - boundsMin) * (posQuantz);
      
      if (boxCompression_)
      {
        CRBinaryReader.ReadArrByteDecompToArrPosition(binaryAnim_, ref bCursor1_, vector3cache, 0, vertexCount, boundsMin, boundSize);
        mesh.vertices = vector3cache;

        CRBinaryReader.ReadArrByteDecompToArrNormal(binaryAnim_, ref bCursor1_, vector3cache, 0, vertexCount);
        mesh.normals  = vector3cache;

        if (tangentsData_)
        {
          Vector4[] vector4cache = arrVertex4Cache_[cacheIdx];

          CRBinaryReader.ReadArrByteDecompToArrTangent(binaryAnim_, ref bCursor1_, vector4cache, 0, vertexCount);
          mesh.tangents = vector4cache;
        }
      }
      else
      {
        CRBinaryReader.ReadArrByteToArrVector3(binaryAnim_, ref bCursor1_, vector3cache, 0, vertexCount);
        mesh.vertices = vector3cache;

        CRBinaryReader.ReadArrByteToArrVector3(binaryAnim_, ref bCursor1_, vector3cache, 0, vertexCount);
        mesh.normals = vector3cache;

        if (tangentsData_)
        {
          Vector4[] vector4cache = arrVertex4Cache_[cacheIdx];

          CRBinaryReader.ReadArrByteToArrVector4(binaryAnim_, ref bCursor1_, vector4cache, 0, vertexCount);
          mesh.tangents = vector4cache;
        }
      }

      Bounds bounds = mesh.bounds;
      bounds.SetMinMax(boundsMin, boundsMax);
      mesh.bounds = bounds;
    }

    private void ReadMeshVerticesCPU(float t, Mesh mesh, int cacheIdx, int vertexCount)
    {
      Vector3[] vector3cache = arrVertex3Cache1_[cacheIdx];

      Vector3 boundsMin1;
      boundsMin1.x = ReadSingle(ref bCursor1_);
      boundsMin1.y = ReadSingle(ref bCursor1_);
      boundsMin1.z = ReadSingle(ref bCursor1_);

      Vector3 boundsMax1;
      boundsMax1.x = ReadSingle(ref bCursor1_);
      boundsMax1.y = ReadSingle(ref bCursor1_);
      boundsMax1.z = ReadSingle(ref bCursor1_);

      Vector3 boundSize1 = (boundsMax1 - boundsMin1) * (posQuantz);

      Vector3 boundsMin2;
      boundsMin2.x = ReadSingle(ref bCursor2_);
      boundsMin2.y = ReadSingle(ref bCursor2_);
      boundsMin2.z = ReadSingle(ref bCursor2_);

      Vector3 boundsMax2;
      boundsMax2.x = ReadSingle(ref bCursor2_);
      boundsMax2.y = ReadSingle(ref bCursor2_);
      boundsMax2.z = ReadSingle(ref bCursor2_);
 
      Vector3 boundSize2 = (boundsMax2 - boundsMin2) * (posQuantz);

      if (boxCompression_)
      {
        CRBinaryReader.ReadArrByteDecompLerpToArrPosition(binaryAnim_, ref bCursor1_, ref bCursor2_, t, vector3cache, 0, vertexCount,
                                                          boundsMin1, boundSize1, boundsMin2, boundSize2 );
        mesh.vertices = vector3cache;

        CRBinaryReader.ReadArrByteDecompLerpToArrNormal(binaryAnim_, ref bCursor1_, ref bCursor2_, t, vector3cache, 0, vertexCount);
        mesh.normals = vector3cache;

        if (tangentsData_)
        {

          Vector4[] vector4cache = arrVertex4Cache_[cacheIdx];

          CRBinaryReader.ReadArrByteDecompLerpToArrTangent(binaryAnim_, ref bCursor1_, ref bCursor2_, t, vector4cache, 0, vertexCount);
          mesh.tangents = vector4cache;
        }
      }
      else
      {
        CRBinaryReader.ReadArrByteLerpToArrVector3(binaryAnim_, ref bCursor1_, ref bCursor2_, t, vector3cache, 0, vertexCount);
        mesh.vertices = vector3cache;

        CRBinaryReader.ReadArrByteLerpToArrVector3(binaryAnim_, ref bCursor1_, ref bCursor2_, t, vector3cache, 0, vertexCount);
        mesh.normals = vector3cache;

        if (tangentsData_)
        {
          Vector4[] vector4cache = arrVertex4Cache_[cacheIdx];
          CRBinaryReader.ReadArrByteLerpToArrVector4(binaryAnim_, ref bCursor1_, ref bCursor2_, t, vector4cache, 0, vertexCount);
          mesh.tangents = vector4cache;
        }
      }

      Vector3 v3_1 = Vector3.LerpUnclamped(boundsMin1, boundsMin2, t);
      Vector3 v3_2 = Vector3.LerpUnclamped(boundsMax1, boundsMax2, t);
      Bounds bounds = mesh.bounds;
      bounds.SetMinMax(v3_1, v3_2);
      mesh.bounds = bounds;
    }

    private void ReadMeshVerticesFiberCPU(Mesh mesh, CRDefinition definition, CRCompressedPose compressedPose, int cacheIdx, int vertexCount)
    {
      compressedPose.Load(binaryAnim_, ref bCursor1_, definition);

      Vector3[] vector3cache = arrVertex3Cache1_[cacheIdx];
      compressedPose.DecompressPositions(vector3cache, definition);
      mesh.vertices = vector3cache;

      Bounds bounds = mesh.bounds;
      bounds.SetMinMax(compressedPose.Box.min_, compressedPose.Box.max_);
      mesh.bounds = bounds;
    }

    private void ReadMeshVerticesFiberCPU(float t, Mesh mesh, CRDefinition definition, CRCompressedPose compressedPose, int cacheIdx, int vertexCount)
    {
      Vector3[] vector3cache = arrVertex3Cache1_[cacheIdx];

      compressedPose.Load(binaryAnim_, ref bCursor1_, definition);

      CRBox3 box3Frame1 = compressedPose.Box;
      compressedPose.DecompressPositions(vector3cache, definition);

      compressedPose.Load(binaryAnim_, ref bCursor2_, definition);

      CRBox3 box3Frame2 = compressedPose.Box;
      compressedPose.DecompressInterpolatePositions(t, vector3cache, definition);

      mesh.vertices = vector3cache;

      Vector3 minInterpolated = Vector3.LerpUnclamped(box3Frame1.min_, box3Frame2.min_, t);
      Vector3 maxInterpolated = Vector3.LerpUnclamped(box3Frame1.max_, box3Frame2.max_, t);

      Bounds bounds = mesh.bounds;
      bounds.SetMinMax(minInterpolated, maxInterpolated);
      mesh.bounds = bounds;
    }

    private void ReadMeshVerticesGPU(int bufferFrame, Mesh mesh, int vertexCount, CRGPUBuffer gpuBuffer)
    {
      cShaderPositions_.SetInt("vertexCount", vertexCount);

      CRBox3 box3 = CRBox3.CreateLoad(binaryAnim_, ref bCursor1_);

      Bounds bounds = mesh.bounds;
      bounds.SetMinMax(box3.min_, box3.max_);
      mesh.bounds = bounds;

      if (boxCompression_)
      {
        cShaderPositions_.SetVector("boundsMin", new Vector4(box3.min_.x, box3.min_.y, box3.min_.z));
        cShaderPositions_.SetVector("boundsMax", new Vector4(box3.max_.x, box3.max_.y, box3.max_.z));
      }

      cShaderPositions_.SetBuffer(0, "positionBuffer", gpuBuffer.GetPositionBuffer(bufferFrame));
      cShaderPositions_.SetTexture(0, "positionTexture", gpuBuffer.PositionTexture);

      cShaderPositions_.SetBuffer(0, "normalBuffer", gpuBuffer.GetNormalBuffer(bufferFrame));
      cShaderPositions_.SetTexture(0, "normalTexture", gpuBuffer.NormalTexture);

      AdvanceCursorSkipMesh(vertexCount, vertexCompression_, tangentsData_, ref bCursor1_);

      cShaderPositions_.Dispatch(0, 1, gpuBuffer.PositionTexture.height, 1);
    }

    private void ReadMeshVerticesGPU(float t, int bufferFrame1, int bufferFrame2, Mesh mesh, int vertexCount, CRGPUBuffer gpuBuffer)
    {
      cShaderPositions_.SetInt("vertexCount", vertexCount);
      
      CRBox3 box3Frame1 = new CRBox3();
      CRBox3 box3Frame2 = new CRBox3();

      //pos1
      {
        box3Frame1.Load(binaryAnim_, ref bCursor1_);

        if (boxCompression_)
        {
          cShaderPositions_.SetVector("boundsMin", new Vector4(box3Frame1.min_.x, box3Frame1.min_.y, box3Frame1.min_.z));
          cShaderPositions_.SetVector("boundsMax", new Vector4(box3Frame1.max_.x, box3Frame1.max_.y, box3Frame1.max_.z));
        }

        cShaderPositions_.SetBuffer(0, "positionBuffer", gpuBuffer.GetPositionBuffer(bufferFrame1));
        cShaderPositions_.SetTexture(0, "positionTexture", gpuBuffer.PositionInterpolationTexture1);

        cShaderPositions_.SetBuffer(0, "normalBuffer", gpuBuffer.GetNormalBuffer(bufferFrame1));
        cShaderPositions_.SetTexture(0, "normalTexture", gpuBuffer.NormalInterpolationTexture1);

        AdvanceCursorSkipMesh(vertexCount, vertexCompression_, tangentsData_, ref bCursor1_);

        cShaderPositions_.Dispatch(0, 1, gpuBuffer.PositionTexture.height, 1);
      }

      //pos2
      {
        box3Frame2.Load(binaryAnim_, ref bCursor2_);

        if (boxCompression_)
        {
          cShaderPositions_.SetVector("boundsMin", new Vector4(box3Frame2.min_.x, box3Frame2.min_.y, box3Frame2.min_.z));
          cShaderPositions_.SetVector("boundsMax", new Vector4(box3Frame2.max_.x, box3Frame2.max_.y, box3Frame2.max_.z));
        }

        cShaderPositions_.SetBuffer(0, "positionBuffer", gpuBuffer.GetPositionBuffer(bufferFrame2));
        cShaderPositions_.SetTexture(0, "positionTexture", gpuBuffer.PositionInterpolationTexture2);

        cShaderPositions_.SetBuffer(0, "normalBuffer", gpuBuffer.GetNormalBuffer(bufferFrame2));
        cShaderPositions_.SetTexture(0, "normalTexture", gpuBuffer.NormalInterpolationTexture2);

        AdvanceCursorSkipMesh(vertexCount, vertexCompression_, tangentsData_, ref bCursor2_);

        cShaderPositions_.Dispatch(0, 1, gpuBuffer.PositionTexture.height, 1);
      }

      //interpolate
      {
        Vector3 minInterpolated = Vector3.LerpUnclamped(box3Frame1.min_, box3Frame2.min_, t);
        Vector3 maxInterpolated = Vector3.LerpUnclamped(box3Frame1.max_, box3Frame2.max_, t);

        Bounds bounds = mesh.bounds;
        bounds.SetMinMax(minInterpolated, maxInterpolated);
        mesh.bounds = bounds;

        cShaderInterpolation_.SetFloat("t", t);

        cShaderInterpolation_.SetTexture(0, "texture1", gpuBuffer.PositionInterpolationTexture1);
        cShaderInterpolation_.SetTexture(0, "texture2", gpuBuffer.PositionInterpolationTexture2);
        cShaderInterpolation_.SetTexture(0, "textureOutput", gpuBuffer.PositionTexture);

        cShaderInterpolation_.Dispatch(0, 1, gpuBuffer.PositionTexture.height, 1);

        cShaderInterpolation_.SetTexture(0, "texture1", gpuBuffer.NormalInterpolationTexture1);
        cShaderInterpolation_.SetTexture(0, "texture2", gpuBuffer.NormalInterpolationTexture2);
        cShaderInterpolation_.SetTexture(0, "textureOutput", gpuBuffer.NormalTexture);

        cShaderInterpolation_.Dispatch(0, 1, gpuBuffer.NormalTexture.height, 1);
      }

    }

    private void ReadMeshVerticesFiberGPU(int bufferFrame, Mesh mesh, CRDefinition definition, CRCompressedPose compressedPose, 
                                          CRGPUBuffer gpuBuffer)
    {
      int nFibers = definition.GetNumberOfFibers();
      cShaderPositions_.SetInt("ufon", nFibers);

      CRBox3 box3 = CRBox3.CreateLoad(binaryAnim_, ref bCursor1_);

      Bounds bounds = mesh.bounds;
      bounds.SetMinMax(box3.min_, box3.max_);
      mesh.bounds = bounds;

      cShaderPositions_.SetVector("ufom", new Vector4(box3.min_.x, box3.min_.y, box3.min_.z));
      cShaderPositions_.SetVector("ufoM", new Vector4(box3.max_.x, box3.max_.y, box3.max_.z));

      cShaderPositions_.SetBuffer(0, "ufo3", gpuBuffer.GetDefinitionBuffer());
      cShaderPositions_.SetBuffer(0, "ufo2", gpuBuffer.GetPositionBuffer(bufferFrame));

      cShaderPositions_.SetTexture(0, "ufo0", gpuBuffer.PositionTexture);

      compressedPose.AdvanceCursorSkipPose(ref bCursor1_, definition);

      int threadGroupSize = Mathf.CeilToInt((float)nFibers / 32.0f);
      cShaderPositions_.Dispatch(0, threadGroupSize, 1, 1);
    }

    private void ReadMeshVerticesFiberGPU(float t, int bufferFrame1, int bufferFrame2, Mesh mesh, CRDefinition definition, CRCompressedPose compressedPose, 
                                          CRGPUBuffer gpuBuffer)
    {
      int nFibers = definition.GetNumberOfFibers();
      cShaderPositions_.SetInt("ufon", nFibers);
      cShaderPositions_.SetBuffer(0, "ufo3", gpuBuffer.GetDefinitionBuffer());

      CRBox3 box3Frame1 = new CRBox3();
      CRBox3 box3Frame2 = new CRBox3();

      //positions1
      {
        box3Frame1.Load(binaryAnim_, ref bCursor1_);

        cShaderPositions_.SetVector("ufom", new Vector4(box3Frame1.min_.x, box3Frame1.min_.y, box3Frame1.min_.z));
        cShaderPositions_.SetVector("ufoM", new Vector4(box3Frame1.max_.x, box3Frame1.max_.y, box3Frame1.max_.z));

        cShaderPositions_.SetBuffer(0, "ufo2", gpuBuffer.GetPositionBuffer(bufferFrame1));
        cShaderPositions_.SetTexture(0, "ufo0", gpuBuffer.PositionInterpolationTexture1);

        compressedPose.AdvanceCursorSkipPose(ref bCursor1_, definition);

        int threadGroupSize = Mathf.CeilToInt((float)nFibers / 32.0f);
        cShaderPositions_.Dispatch(0, threadGroupSize, 1, 1);
      }

      //positions2
      {
        box3Frame2.Load(binaryAnim_, ref bCursor2_);

        cShaderPositions_.SetVector("ufom", new Vector4(box3Frame2.min_.x, box3Frame2.min_.y, box3Frame2.min_.z));
        cShaderPositions_.SetVector("ufoM", new Vector4(box3Frame2.max_.x, box3Frame2.max_.y, box3Frame2.max_.z));

        cShaderPositions_.SetBuffer(0, "ufo2", gpuBuffer.GetPositionBuffer(bufferFrame2));
        cShaderPositions_.SetTexture(0, "ufo0", gpuBuffer.PositionInterpolationTexture2);

        compressedPose.AdvanceCursorSkipPose(ref bCursor2_, definition);

        int threadGroupSize = Mathf.CeilToInt((float)nFibers / 32.0f);
        cShaderPositions_.Dispatch(0, threadGroupSize, 1, 1);
      }

      //interpolate
      {
        Vector3 minInterpolated = Vector3.LerpUnclamped(box3Frame1.min_, box3Frame2.min_, t);
        Vector3 maxInterpolated = Vector3.LerpUnclamped(box3Frame1.max_, box3Frame2.max_, t);

        Bounds bounds = mesh.bounds;
        bounds.SetMinMax(minInterpolated, maxInterpolated);
        mesh.bounds = bounds;

        cShaderInterpolation_.SetFloat("t", t);
        cShaderInterpolation_.SetTexture(0, "texture1", gpuBuffer.PositionInterpolationTexture1);
        cShaderInterpolation_.SetTexture(0, "texture2", gpuBuffer.PositionInterpolationTexture2);
        cShaderInterpolation_.SetTexture(0, "textureOutput", gpuBuffer.PositionTexture);

        cShaderInterpolation_.Dispatch(0, 1, gpuBuffer.PositionTexture.height, 1);
      }
    }

    private void RecomputeNormalsCPU(Mesh mesh, int cacheIdx, CRVertexDataCache vertexDataCache)
    {
      Vector3[] arrVertexCache = arrVertex3Cache1_[cacheIdx];
      Vector3[] arrNormalCache = arrVertex3Cache2_[cacheIdx];
      CRNormalCalculator.CalculateNormals(arrVertexCache, vertexDataCache.Cache, arrNormalCache);
      mesh.normals = arrNormalCache;
    }

    private void RecomputeNormalsGPU(int vertexCount, CRGPUBuffer gpuBuffer)
    {
      cShaderNormals_.SetInt("vc", vertexCount);
      cShaderNormals_.SetTexture(0, "ufo0", gpuBuffer.PositionTexture);
      cShaderNormals_.SetTexture(0, "ufo1", gpuBuffer.NormalTexture);

      cShaderNormals_.SetBuffer(0, "ufo5", gpuBuffer.GetVertexDataBuffer() );

      int threadGroupSize = Mathf.CeilToInt((float)vertexCount / 32.0f);
      cShaderNormals_.Dispatch(0, threadGroupSize, 1, 1);
    }

    private void ReadEvents(ref int cursor)
    {
      int nEvents = ReadInt32(ref cursor);
      for (int i = 0; i < nEvents; i++)
      {
        int idEmitter = ReadInt32(ref cursor);
        ceInfo.emitterName_ = arrEmitterName_[idEmitter];

        int idBodyA = ReadInt32(ref cursor);
        int idBodyB = ReadInt32(ref cursor);

        Transform trA = arrGOInfo_[idBodyA].tr_;
        Transform trB = arrGOInfo_[idBodyB].tr_;

        if (trA != null)
        {
          ceInfo.GameObjectA = trA.gameObject;
        }
        else
        {
          ceInfo.GameObjectA = null;
        }

        if (trB != null)
        {
          ceInfo.GameObjectB = trB.gameObject;
        }
        else
        {
          ceInfo.GameObjectB = null;
        }

        Matrix4x4 m_LOCAL_to_WORLD = transform.localToWorldMatrix;

        ceInfo.position_.x = ReadSingle(ref cursor);
        ceInfo.position_.y = ReadSingle(ref cursor);
        ceInfo.position_.z = ReadSingle(ref cursor);

        ceInfo.position_ = m_LOCAL_to_WORLD.MultiplyPoint3x4(ceInfo.position_);

        ceInfo.velocityA_.x = ReadSingle(ref cursor);
        ceInfo.velocityA_.y = ReadSingle(ref cursor);
        ceInfo.velocityA_.x = ReadSingle(ref cursor);     

        ceInfo.velocityA_ = m_LOCAL_to_WORLD.MultiplyVector(ceInfo.velocityA_);

        ceInfo.velocityB_.x = ReadSingle(ref cursor);
        ceInfo.velocityB_.y = ReadSingle(ref cursor);
        ceInfo.velocityB_.x = ReadSingle(ref cursor);

        ceInfo.velocityB_ = m_LOCAL_to_WORLD.MultiplyVector(ceInfo.velocityB_);

        ceInfo.relativeSpeed_N_ = ReadSingle(ref cursor);
        ceInfo.relativeSpeed_T_ = ReadSingle(ref cursor);

        ceInfo.relativeP_N_ = ReadSingle(ref cursor);
        ceInfo.relativeP_T_ = ReadSingle(ref cursor);

        collisionEvent.Invoke(ceInfo);
      }
    }

    #region IAnimatorExporter interface

    public void InitAnimationBaking(out int nFrames, out int fps, out float deltaTimeFrame, out float animationLength)
    {
      LoadAnimation(true);

      nFrames         = frameCount_;
      fps             = fps_;
      deltaTimeFrame  = frameTime_;
      animationLength = animationLength_;
    }

    public void FinishAnimationBaking()
    {
      CloseAnimation();
    }

    public void ChangeToAnimationTrack(int trackIdx)
    {
      bool isCRAnimationAsset = animationFileType == AnimationFileType.CRAnimationAsset;
      bool isTextAsset        = animationFileType == AnimationFileType.TextAsset;

      if ( isCRAnimationAsset )
      {
        if ( trackIdx < listAnimations.Count && trackIdx >= 0 )
        {
          activeAnimation = listAnimations[trackIdx];
        }
        else
        {
          activeAnimation = null;
        }      
      }
      else if ( isTextAsset && (trackIdx < listAnimationsText.Count ) )
      {
        if ( trackIdx < listAnimationsText.Count && trackIdx >= 0 )
        {
          activeAnimationText = listAnimationsText[trackIdx];
        }
        else
        {
          activeAnimationText = null;
        }     
      }
    }
    
    public string GetActiveAnimationName()
    {
      string name = string.Empty;
      if (animationFileType == AnimationFileType.CRAnimationAsset && activeAnimation != null)
      {
        name = activeAnimation.name;
      }
      else if (animationFileType == AnimationFileType.TextAsset && activeAnimationText != null)
      {
        name = activeAnimationText.name;
      }

      return name;
    }

    public int GetActiveAnimationTrackIdx()
    {
      bool isCRAnimationAsset = animationFileType == AnimationFileType.CRAnimationAsset;
      bool isTextAsset        = animationFileType == AnimationFileType.TextAsset;

      int trackIdx = -1;
      if ( isCRAnimationAsset && activeAnimation != null )
      {
        trackIdx = listAnimations.FindIndex( (animationasset) => { return animationasset == activeAnimation; } );
      }
      else if ( isTextAsset && activeAnimationText != null )
      {
        trackIdx = listAnimationsText.FindIndex( (animationassetText) => { return animationassetText = activeAnimationText; } );
      }

      return trackIdx;
    }

    public int GetAnimationTrackCount()
    {
      bool isCRAnimationAsset = animationFileType == AnimationFileType.CRAnimationAsset;

      if ( isCRAnimationAsset )
      {
        return listAnimations.Count;
      }
      else
      {
        return listAnimationsText.Count;
      }
    }

    public void GetGOHeaderData(List<TGOHeaderData> listGOHeaderData)
    {
      listGOHeaderData.Clear();

      for (int i = 0; i < nGameObjects_; i++)
      {
        CRGOInfo goInfo = arrGOInfo_[i];
           
        Transform tr        = goInfo.tr_;
        if (tr == null)
        {
          continue;
        }

        int vertexCount     = goInfo.vertexCount_;
        int boneIdxBegin    = goInfo.boneIdxBegin_;
        int boneIdxEnd      = goInfo.boneIdxEnd_;
        int boneCount       = goInfo.boneCount_;

        GameObject go = tr.gameObject;
        string goRelativePath = go.GetRelativePathTo(this.gameObject);

        List<string> listBoneRelativePath = new List<string>();
        for (int j = boneIdxBegin; j < boneIdxEnd; j++)
        {
          Transform boneTr = arrBoneTr_[j];
          if (boneTr != null)
          {
            GameObject boneGO = boneTr.gameObject;
            string boneRelativePath = boneGO.GetRelativePathTo(this.gameObject);
            listBoneRelativePath.Add(boneRelativePath);
          }
          else
          {
            listBoneRelativePath.Add("CRNotFoundBonePath");
          }
        }  
        
        listGOHeaderData.Add(new TGOHeaderData(goRelativePath, vertexCount, boneCount, listBoneRelativePath));   
      }
    }

    public void GetVisibilityData(List<TVisibilityData> listVisibilityData)
    {
      listVisibilityData.Clear();

      for (int i = 0; i < nGameObjects_; i++)
      {
        CRGOInfo goInfo = arrGOInfo_[i];

        Transform tr = goInfo.tr_;
        if (tr != null)
        {
          Vector2 visibilityInterval = arrVisibilityInterval_[i];
          listVisibilityData.Add( new TVisibilityData(tr, visibilityInterval) );
        }
      }
    }

    public void GetFrameGOData( int frame, List<TGOFrameData> listFrameData )
    {
      listFrameData.Clear();
      SetFrame(frame);

      List<Transform>  listBoneTransform   = new List<Transform>();
      List<Vector3>    listBoneTranslation = new List<Vector3>();
      List<Quaternion> listBoneRotation    = new List<Quaternion>();
      List<Vector3>    listBoneScale       = new List<Vector3>();

      for (int i = 0; i < nGameObjects_; i++)
      {
        CRGOInfo goInfo     = arrGOInfo_[i];    
        Transform tr        = goInfo.tr_;

        bool skipGameObject = arrSkipObject_[i];
        if (tr == null || skipGameObject)
        {
          continue;
        }

        int vertexCount     = goInfo.vertexCount_;
        int boneIdxBegin    = goInfo.boneIdxBegin_;
        int boneIdxEnd      = goInfo.boneIdxEnd_;
        int boneCount       = goInfo.boneCount_;

        CRGOKeyframe goKeyframe = new CRGOKeyframe(vertexCount, boneCount);
        listFrameData.Add( new TGOFrameData(tr, goKeyframe) );

        GameObject go = tr.gameObject;

        bool isActive = go.activeSelf;
        if (arrIsBone_[i])
        {
          Vector3 localScale = tr.localScale;
          isActive = localScale != Vector3.zero;
        }

        goKeyframe.SetBodyKeyframe( isActive, tr.localPosition, tr.localRotation);

        if (isActive)
        {
          if (vertexCount > 0)
          {
            Mesh mesh = go.GetMesh();
            if (mesh != null)
            {
              goKeyframe.SetVertexKeyframe(mesh.vertices, mesh.normals, mesh.tangents);
            }
          }
          else if (boneCount > 0)
          {
            GetFrameBonesData(boneIdxBegin, boneIdxEnd, listBoneTransform, listBoneTranslation, listBoneRotation, listBoneScale );
            goKeyframe.SetBonesTransform(listBoneTransform.ToArray());
            goKeyframe.SetBonesKeyframe(listBoneTranslation.ToArray(), listBoneRotation.ToArray(), listBoneScale.ToArray());
          }
        }
      } //forGameobjects
    }


    private void GetFrameBonesData(int boneIdxBegin, int boneIdxEnd, List<Transform> listBoneTransform, List<Vector3> listBoneTranslation, List<Quaternion> listBoneRotation, List<Vector3> listBoneScale)
    {
      listBoneTransform.Clear();
      listBoneTranslation.Clear();
      listBoneRotation.Clear();
      listBoneScale.Clear();

      for (int i = boneIdxBegin; i < boneIdxEnd; i++)
      {
        Transform tr = arrBoneTr_[i];

        listBoneTransform.Add( tr );
        if ( tr != null)
        {
          listBoneTranslation.Add( tr.localPosition );
          listBoneRotation.Add( tr.localRotation );
          listBoneScale.Add( tr.localScale );
        }
        else
        {
          listBoneTranslation.Add( Vector3.zero );
          listBoneRotation.Add( Quaternion.identity );
          listBoneScale.Add( Vector3.one );
        }
      }   
    }

    #endregion
  }
}
