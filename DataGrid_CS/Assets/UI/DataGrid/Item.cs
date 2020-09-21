using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Item : ItemBase
{
    public Text text;


    public override void SetData(string data)
    {
        if (!string.IsNullOrEmpty(data))
            text.text = data;
    }
    /// <summary>
    /// 设置此item滑动方向上的Width
    /// </summary>
    /// <returns></returns>
    internal override float Width()
    {
        return 80;
    }
}
