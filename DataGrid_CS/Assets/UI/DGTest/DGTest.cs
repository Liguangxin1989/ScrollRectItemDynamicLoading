using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using MogoEngine.UISystem;
using System.Collections.Generic;
using System;

public class DGTest : MonoBehaviour {

    public GameObject item;
    private DataGrid dataGrid;
    List<int> itemData = new List<int>();

    void Awake()
    {
        Init();
    }

    private void Init()
    {
        var  trans = GameObject.Find("ScrollPanel").GetComponent<RectTransform>();

        for (int i = 0; i < 10000; i++)
        {
            itemData.Add(i);
        }

        dataGrid = trans.GetComponent<DataGrid>();
        dataGrid.SetItemsData(item, itemData.Count, InitItem);
    }

    private void InitItem(ItemBase t, int index)
    {
        if (t && index < itemData.Count && index >= 0)
        {
            t.SetData(itemData[index].ToString());
        }
    }

}
