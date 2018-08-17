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

        //if (dataGrid1 && item1)
        //{
        //    dataGrid1.SetItemsData(item1, itemData.Count, InitItem,9);
        //}
        if (dataGrid2 && item2)
        {
            dataGrid2.SetItemsData(item2, itemData.Count, InitItem, 8);
            dataGrid1 = dataGrid2;
        }
    }

    private void InitItem(ItemBase t, int index)
    {
        if (t && index < itemData.Count && index >= 0)
        {
            t.SetData(itemData[index].ToString());
        }
    }

    public void OnClickDataCountAdd1()
    {
        int value = UnityEngine.Random.Range(0, 100);
        int index = UnityEngine.Random.Range(0, itemData.Count - 1);
        itemData.Insert(index, value);
        Debug.LogErrorFormat("=======>UpdateCount , insert {0}  = {1}", index, value);
        dataGrid1.UpdateDataCount(itemData.Count);
    }

    public void OnClickDataCountreduce1()
    {
        int index = UnityEngine.Random.Range(0, itemData.Count - 1);
        itemData.RemoveAt(index);
        Debug.LogErrorFormat("--------->UpdateCount , remove {0}", index);
        dataGrid1.UpdateDataCount(itemData.Count);
    }

    public void OnClickUpdateOneItem()
    {
        int value = UnityEngine.Random.Range(0, 100) *-1;
        int index = UnityEngine.Random.Range(0, itemData.Count - 1);
        itemData[index] = value;
        Debug.LogErrorFormat("===========UpdateItem , update {0} = {1}", index, value);
        dataGrid1.UpdateItem(index);
    }


    public void OnClickGotoTop()
    {
        dataGrid1.ShowViewTop();
    }


    public void OnClickGotoBottom()
    {
        dataGrid1.ShowViewBottom();

    }

    public void OnClickGotoRandomItem()
    {
        int index = UnityEngine.Random.Range(0, itemData.Count - 1);
        Debug.LogError("====> show Item index = " + index);
        dataGrid1.ShowItem(index);
    }
}
