using UnityEngine;
using System.Collections;

namespace CaronteFX
{
  [System.Serializable]
  public class CRGOTmpData
  {
    public GameObject gameObject_;
    public Mesh       mesh_;
    public Mesh       tmp_Mesh_;
    public Vector3    localPosition_;
    public Quaternion localRotation_;
    public Vector3    localScale_;

    public CRGOTmpData(GameObject go)
    {
      gameObject_ = go;
      mesh_ = go.GetMeshFromMeshFilterOnly();

      if (mesh_ != null)
      {
        tmp_Mesh_ = UnityEngine.Object.Instantiate(mesh_);
      }
      else
      {
        tmp_Mesh_ = null;
      }

      Transform tr = go.transform;
      localPosition_ = tr.localPosition;
      localRotation_ = tr.localRotation;
      localScale_    = tr.localScale;
    }
  }
}