using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace CaronteFX
{
  public class CRAnimationsManager
  {
    Dictionary<TextAsset, byte[]> dictionaryTextAnimations = new Dictionary<TextAsset, byte[]>(); 

    private static CRAnimationsManager instance_;
    public static CRAnimationsManager Instance
    {
      get
      {
        if ( instance_ == null )
        {
          instance_ = new CRAnimationsManager();
        }
        return instance_;
      }
    }

    private CRAnimationsManager()
    {

    }

    public byte[] GetBytesFromAnimation(TextAsset animation)
    {
      if (!dictionaryTextAnimations.ContainsKey(animation))
      {
        dictionaryTextAnimations.Add(animation, animation.bytes);
      }

      return dictionaryTextAnimations[animation];
    }
  }
}

