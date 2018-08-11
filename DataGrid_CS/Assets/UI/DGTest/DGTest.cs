using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using MogoEngine.UISystem;
using System.Collections.Generic;
using System;

public class DGTest : MonoBehaviour {

    public GameObject item1;
    public GameObject item2;

    public DataGrid dataGrid1;
    public DataGrid dataGrid2;


    private DataGrid dataGrid;
    List<int> itemData = new List<int>();

    void Awake()
    {
        Init();
    }

    private void Init()
    {
//        var  trans = GameObject.Find("ScrollPanel").GetComponent<RectTransform>();

        for (int i = 0; i < 10; i++)
        {
            itemData.Add(i);
        }
            
        if (dataGrid1 && item1)
            dataGrid1.SetItemsData(item1, itemData.Count, InitItem);
        if (dataGrid2 && item2)
            dataGrid2.SetItemsData(item2, itemData.Count, InitItem);
    }

    private void InitItem(ItemBase t, int index)
    {
        if (t && index < itemData.Count && index >= 0)
        {
            t.SetData(itemData[index].ToString());
        }
    }

}
