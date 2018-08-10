using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Item : ItemBase
{
    public Text text;


    public override void SetData(string data)
    {
        if (string.IsNullOrEmpty(data))
            text.text = data;
    }

}
