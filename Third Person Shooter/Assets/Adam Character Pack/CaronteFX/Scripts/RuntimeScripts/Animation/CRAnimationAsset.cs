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
using System.Collections;

namespace CaronteFX
{
  /// <summary>
  /// Simple animation asset to use instead of a TextAsset (which copies memory on access).
  /// </summary>
  public class CRAnimationAsset : ScriptableObject 
  {
    [SerializeField]
    private byte[] bytes;
    public byte[] Bytes
    {
      get { return bytes; }
      set { bytes = value; }
    }
  }
}

