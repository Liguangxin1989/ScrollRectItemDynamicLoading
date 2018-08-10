//using MogoEngine.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using MogoEngine.UISystem;
namespace MogoEngine.UISystem
{
    /// <summary>
    /// 数据列表渲染组件，Item缓存，支持无限循环列表，即用少量的Item实现大量的列表项显示
    /// </summary>
    public class DataGrid : MonoBehaviour
    {
        [HideInInspector]
        public bool useLoopItems = false;           //是否使用无限循环列表，对于列表项中OnDataSet方法执行消耗较大时不宜使用，因为OnDataSet方法会在滚动的时候频繁调用


        private RectTransform m_content;
        private object[] m_data;
        private GameObject m_goItemRender;
        private Type m_itemRenderType;
        private readonly List<ItemRender> m_items = new List<ItemRender>();
        private object m_selectedData;
        private LayoutGroup m_LayoutGroup;
        private RectOffset m_oldPadding;

        //下面的属性会需要父对象上有ScrollRect组件
        private ScrollRect m_scrollRect;    //父对象上的，不一定存在
        private RectTransform m_tranScrollRect;
        private int m_itemSpace;          //每个Item的空间
        private int m_viewItemCount;        //可视区域内Item的数量（向上取整）
        private bool m_isVertical;          //是否是垂直滚动方式，否则是水平滚动
        private int m_startIndex;           //数据数组渲染的起始下标
        private string m_itemClickSound = "";//AudioConst.btnClick;

        Vector2 Resolution = new Vector2(1242, 2208);

        //内容长度
        private float ContentSpace
        {
            get
            {
                return m_isVertical ? m_content.sizeDelta.y : m_content.sizeDelta.x;
            }
        }
        //可见区域长度
        private float ViewSpace
        {
            get
            {
                return m_isVertical ? (m_tranScrollRect.sizeDelta.y + Resolution.y) : (m_tranScrollRect.sizeDelta.x + Resolution.x);
            }
        }
        //约束常量（固定的行（列）数）
        private int ConstraintCount
        {
            get
            {
                return m_LayoutGroup == null ? 1 : ((m_LayoutGroup is GridLayoutGroup) ? (m_LayoutGroup as GridLayoutGroup).constraintCount : 1);
            }
        }
        //数据量个数
        private int DataCount
        {
            get
            {
                return m_data == null ? 0 : m_data.Length;
            }
        }
        //缓存数量
        private int CacheCount
        {
            get
            {
                return ConstraintCount + DataCount % ConstraintCount;
            }
        }
        //缓存单元的行（列）数
        private int CacheUnitCount
        {
            get
            {
                return m_LayoutGroup == null ? 1 : Mathf.CeilToInt((float)CacheCount / ConstraintCount);
            }
        }
        //数据单元的行（列）数
        private int DataUnitCount
        {
            get
            {
                return m_LayoutGroup == null ? DataCount : Mathf.CeilToInt((float)DataCount / ConstraintCount);
            }
        }



        void Awake()
        {
            var go = gameObject;
            var trans = transform;
            m_LayoutGroup = GetComponentInChildren<LayoutGroup>();
            m_content = m_LayoutGroup.gameObject.GetComponent<RectTransform>();
            if (m_LayoutGroup != null)
                m_oldPadding = m_LayoutGroup.padding;

            m_scrollRect = trans.GetComponentInParent<ScrollRect>();
            if (m_scrollRect != null && m_LayoutGroup != null)
            {
                m_tranScrollRect = m_scrollRect.GetComponent<RectTransform>();
                m_isVertical = m_scrollRect.vertical;
                var layoutgroup = m_LayoutGroup as GridLayoutGroup;
                if (layoutgroup != null)
                {
                    m_itemSpace = (int)(m_isVertical ? (layoutgroup.cellSize.y + layoutgroup.spacing.y) : (layoutgroup.cellSize.x + layoutgroup.spacing.x));
                    m_viewItemCount = Mathf.CeilToInt(ViewSpace / m_itemSpace);
                }
            }
            else
            {
                Debug.LogError("scrollRect is null or verticalLayoutGroup is null");
            }
        }

        void Start()
        {
            if (m_scrollRect != null)
            {
                if (useLoopItems)
                    m_scrollRect.onValueChanged.AddListener(OnScroll);
            }
        }

        /// <summary>
        /// 设置渲染项
        /// </summary>
        /// <param name="goItemRender"></param>
        /// <param name="itemRenderType"></param>
        public void SetItemRender(GameObject goItemRender, Type itemRenderType)
        {
            m_goItemRender = goItemRender;
            m_itemRenderType = itemRenderType.IsSubclassOf(typeof(ItemRender)) ? itemRenderType : null;
            LayoutElement le = goItemRender.GetComponent<LayoutElement>();
            var layoutGroup = m_LayoutGroup as HorizontalOrVerticalLayoutGroup;
            if (le != null && layoutGroup != null)
            {
                if (m_tranScrollRect == null)
                {
                    m_scrollRect = transform.GetComponentInParent<ScrollRect>();
                    m_tranScrollRect = m_scrollRect.GetComponent<RectTransform>();
                }
                m_itemSpace = (int)(le.preferredHeight + (int)layoutGroup.spacing);
                m_viewItemCount = Mathf.CeilToInt(ViewSpace / m_itemSpace);
            }
        }
        /// <summary>
        /// 数据项
        /// </summary>
        public object[] Data
        {
            set
            {
                m_data = value;
                UpdateView();
            }
            get { return m_data; }
        }

        public List<ItemRender> ItemRenders
        {
            get { return m_items; }
        }

        public void Remove(object item)
        {
            if (item == null || Data == null)
            {
                return;
            }
            List<object> newList = new List<object>(Data);
            if (newList.Contains(item))
            {
                newList.Remove(item);
            }
            Data = newList.ToArray();
        }

        /// <summary>
        /// 当前选择的数据项
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T SelectedData<T>()
        {
            return (T)m_selectedData;
        }

        /// <summary>
        /// 下一帧把指定项显示在最顶端并选中，这个比ResetScrollPosition保险，否则有些在UI一初始化完就执行的操作会不生效
        /// </summary>
        /// <param name="index"></param>
        public void ShowItemOnTop(int index)
        {
            //TimerManager.SetTimeOut(0.01f, () =>
            //{
            //    if (m_data.Length > index)
            //        SelectItem(m_data[index]);
            //    ResetScrollPosition(index);
            //});
        }

        public void ShowItemOnTop(object data)
        {
            if (m_data == null || m_data.Length == 0)
            {
                return;
            }
            var target_index = -1;
            for (int i = 0; i < m_data.Length; i++)
            {
                if (m_data[i] == data)
                {
                    target_index = i;
                    break;
                }
            }
            if (target_index != -1)
            {
                //TimerManager.SetTimeOut(0.01f, () =>
                //{
                //    SelectItem(m_data[target_index]);
                //    ResetScrollPosition(target_index);
                //});
            }
        }

        /// <summary>
        /// 重置滚动位置，
        /// </summary>
        /// <param name="top">true则跳转到顶部，false则跳转到底部</param>
        public void ResetScrollPosition(bool top = true)
        {
            if (m_data == null)
                return;
            int index = top ? 0 : m_data.Length;
            // LoggerHelper.Error("len: "+index);
            ResetScrollPosition(index);
        }

        /// <summary>
        /// 重置滚动位置，如果同时还要赋值新的Data，请在赋值之前调用本方法
        /// </summary>
        public void ResetScrollPosition(int index)
        {
            if (m_data == null)
                return;
            var unitIndex = Mathf.Clamp(index / ConstraintCount, 0, DataUnitCount - m_viewItemCount > 0 ? DataUnitCount - m_viewItemCount : 0);
            var value = (unitIndex * m_itemSpace) / (Mathf.Max(ViewSpace, ContentSpace - ViewSpace));
            value = Mathf.Clamp01(value);

            //特殊处理无法使指定条目置顶的情况——拉到最后
            if (unitIndex != index / ConstraintCount)
                value = 1;

            if (m_scrollRect)
            {
                if (m_isVertical)
                    m_scrollRect.verticalNormalizedPosition = 1 - value;
                else
                    m_scrollRect.horizontalNormalizedPosition = value;
            }

            m_startIndex = unitIndex * ConstraintCount;
            UpdateView();
        }

        /// <summary>
        /// 更新视图
        /// </summary>
        public void UpdateView()
        {
            if (useLoopItems)
            {
                if (m_data != null)
                    m_startIndex = Mathf.Max(0, Mathf.Min(m_startIndex / ConstraintCount, DataUnitCount - m_viewItemCount - CacheUnitCount)) * ConstraintCount;
                var frontSpace = m_startIndex / ConstraintCount * m_itemSpace;
                var behindSpace = Mathf.Max(0, m_itemSpace * (DataUnitCount - CacheUnitCount) - frontSpace - (m_itemSpace * m_viewItemCount));
                if (m_isVertical)
                    m_LayoutGroup.padding = new RectOffset(m_oldPadding.left, m_oldPadding.right, frontSpace, behindSpace);
                else
                    m_LayoutGroup.padding = new RectOffset(frontSpace, behindSpace, m_oldPadding.top, m_oldPadding.bottom);
            }
            else
                m_startIndex = 0;

            if (m_goItemRender == null || m_itemRenderType == null || m_data == null || m_content == null)
                return;

            int itemLength = useLoopItems ? m_viewItemCount * ConstraintCount + CacheCount : m_data.Length;
            itemLength = Mathf.Min(itemLength, m_data.Length);
            //LoggerHelper.Error("len: "+itemLength);
            for (int i = itemLength; i < m_items.Count; i++)
            {
                Destroy(m_items[i].gameObject);
                m_items[i] = null;
            }
            for (int i = m_items.Count - 1; i >= 0; i--)
            {
                if (m_items[i] == null)
                    m_items.RemoveAt(i);
            }

            for (int i = 0; i < itemLength; i++)
            {
                var index = m_startIndex + i;
                if (index >= m_data.Length || index < 0)
                    continue;
                if (i < m_items.Count)
                {
                    m_items[i].SetData(m_data[index]);

                }
                else
                {
                    var go = Instantiate(m_goItemRender) as GameObject;
                    go.name = m_goItemRender.name +"_"+index;
                    go.transform.SetParent(m_content, false);
                    go.SetActive(true);
                    var script = go.AddComponent(m_itemRenderType) as ItemRender;
                    if (!go.activeInHierarchy)
                        script.Awake();
                    script.SetData(m_data[index]);
                    script.m_owner = this;
                    m_items.Add(script);
                }
            }
        }

        private void OnScroll(Vector2 data)
        {
            //if (m_canvas != null && m_canvas.pixelPerfect)
            //    m_canvas.pixelPerfect = false;
            var value = (ContentSpace - ViewSpace) * (m_isVertical ? data.y : 1 - data.x);
            var start = ContentSpace - value - ViewSpace;
            var startIndex = Mathf.FloorToInt(start / m_itemSpace) * ConstraintCount;
            startIndex = Mathf.Max(0, startIndex);

            if (startIndex != m_startIndex)
            {
                m_startIndex = startIndex;
                UpdateView();
            }
        }

        void Destroy()
        {
            m_items.Clear();
        }

    }
}