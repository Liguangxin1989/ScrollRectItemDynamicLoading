﻿///
/// version 1 兼容之前版本，去掉多余的layout 选中，toggle 等 ，可以抽出分装一个组件 2018.0810
///
///version 2 继续优化 ，每次变化 只更新变化的item ,对水平移动进行测试
///
///version 3 增加 item 的增 删 改
/// 
///version 4 增加瞬移 （移动到底部，顶部，指定位置)
///
///
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
    /// 限制，item的 anchorspos 为 0 ,1
    /// </summary>
    [RequireComponent(typeof(ScrollRect))]
    public class DataGrid : MonoBehaviour
    {
        /// <summary>
        /// 移动方向
        /// </summary>
        enum MoveDict : byte
        {
            none = 0,
            /// <summary>
            /// 负方向移动
            /// </summary>
            leftorup = 1,
            /// <summary>
            /// 正方向移动.
            /// </summary>
            righrordown = 2,
            /// <summary>
            /// 没有移动 ,需要更新所有的item
            /// </summary>
            nomove = 3,
        }

        class IndexItem
        {
            public ItemBase item;
            public int index;
            public IndexItem(ItemBase item, int index)
            {
                this.item = item;
                this.index = index;
            }
        }

        private RectTransform m_content;
        private readonly List<IndexItem> m_items = new List<IndexItem>();

        private ScrollRect m_scrollRect;
        private RectTransform m_tranScrollRect;

        /// <summary>
        /// 可视区域内Item的数量（向上取整）
        /// </summary>
        private int m_viewItemCount;
        private bool m_isVertical;          //是否是垂直滚动方式，否则是水平滚动
        private int m_startIndex;           //数据数组渲染的起始下标
        /// <summary>
        /// 多缓存的数目 实际上实例化的数据量是 m_viewItemCount + CACHENUM
        /// </summary>
        const int CACHENUM = 1;

        //内容长度
        private float ContentSpace;
        //{
        //    get
        //    {
        //        return m_isVertical ? m_content.rect.height : m_content.rect.width;
        //    }
        //}
        //可见区域长度
        private float ViewSpace;
        //{
        //    get
        //    {
        //        return m_isVertical ? m_tranScrollRect.rect.height : m_tranScrollRect.rect.width;
        //    }
        //}
        /// <summary>
        /// 数据量个数
        /// </summary>
        private int _dataCount;
        /// <summary>
        /// item prefab
        /// </summary>
        private GameObject _item;

        /// <summary>
        /// item的size 如果有Gridlayout 应该把 Space 加进去
        /// </summary>
        Vector2 _itemSize = Vector2.zero;
        /// <summary>
        /// 每个Item的空间
        /// </summary>
        private float itemSpace;
        private bool _isEnable = false;

        private MoveDict _moveDict = MoveDict.none;

        public delegate void InitItemCallback(ItemBase t, int index);

        InitItemCallback _callback;
        bool isStart = false;

        void Start()
        {
            var go = gameObject;
            m_scrollRect = this.GetComponent<ScrollRect>();
            m_content = m_scrollRect.content;
            m_tranScrollRect = m_scrollRect.viewport == null ? this.transform as RectTransform : m_scrollRect.viewport;
            m_isVertical = m_scrollRect.vertical;
            ViewSpace = m_isVertical ? m_tranScrollRect.rect.height : m_tranScrollRect.rect.width;
            m_scrollRect.onValueChanged.AddListener(OnScroll);
            isStart = true;
            InitData();
        }

        /// <summary>
        /// 设置Item 相关的数据
        /// </summary>
        /// <param name="item"> item 的 prefab </param>
        /// <param name="count">item 的数目</param>
        /// <param name="callback"> item的 回调</param>
        public void SetItemsData(GameObject item, int count, InitItemCallback callback)
        {
            _dataCount = count;
            _item = item;
            _callback = callback;
            InitData();
        }

        void SetEnable()
        {
            _isEnable = _item != null && _item.GetComponent<ItemBase>() && _callback != null && _dataCount >0;

        }

        void InitData()
        {
            if (!isStart)
                return;
            SetItemSize();
            SetContentSize();
            SetCacheCount();
            m_startIndex = 0;
            SetEnable();
            UpdateView();
        }

        /// <summary>
        /// 计算Item的大小
        /// </summary>
        void SetItemSize()
        {
            if (_item)
            {
                var rectTrans = (_item.transform as RectTransform);
                _itemSize = rectTrans.rect.size;
                itemSpace = m_isVertical ? _itemSize.y : _itemSize.x;
            }
            ///TODO 如果有Gridlayout 应该把 Space 加进去

        }
        /// <summary>
        /// 计算Content的大小 
        /// content 的 不动边是由本身决定还是由 viewport 决定 ？  暂时由本身决定
        /// </summary>
        void SetContentSize()
        {
            if (m_content && itemSpace > 0)
            {
                float contentfit = itemSpace * _dataCount;
                if (m_isVertical)
                    m_content.sizeDelta = new Vector2(m_content.sizeDelta.x, contentfit);
                else
                    m_content.sizeDelta = new Vector2(contentfit, m_content.sizeDelta.y);

                ContentSpace = contentfit;
            }
        }
        /// <summary>
        /// 设置缓存数目
        /// </summary>
        void SetCacheCount()
        {
            if (ViewSpace > 0 && itemSpace > 0)
            {
                m_viewItemCount = Mathf.CeilToInt(ViewSpace / itemSpace);
                m_viewItemCount = m_viewItemCount + CACHENUM;
            }
        }

        /// <summary>
        /// 下一帧把指定项显示在最顶端并选中，这个比ResetScrollPosition保险，否则有些在UI一初始化完就执行的操作会不生效
        /// </summary>
        /// <param name="index"></param>
        public void ShowItemOnTop(int index)
        {

        }


        /// <summary>
        /// 重置滚动位置，
        /// </summary>
        /// <param name="top">true则跳转到顶部，false则跳转到底部</param>
        public void ResetScrollPosition(bool top = true)
        {
            int index = top ? 0 : m_viewItemCount - 1;
            // LoggerHelper.Error("len: "+index);
            ResetScrollPosition(index);
        }

        /// <summary>
        /// 重置滚动位置，如果同时还要赋值新的Data，请在赋值之前调用本方法
        /// </summary>
        public void ResetScrollPosition(int index)
        {
            var unitIndex = Mathf.Clamp(index, 0, _dataCount - 1);
            var value = (unitIndex * itemSpace) / (Mathf.Max(ViewSpace, ContentSpace - ViewSpace));
            value = Mathf.Clamp01(value);

            //特殊处理无法使指定条目置顶的情况——拉到最后
            if (unitIndex != index)
                value = 1;

            if (m_scrollRect)
            {
                if (m_isVertical)
                    m_scrollRect.verticalNormalizedPosition = 1 - value;
                else
                    m_scrollRect.horizontalNormalizedPosition = value;
            }

            m_startIndex = unitIndex;
            UpdateView();
        }

        private void OnScroll(Vector2 data)
        {
            if (!_isEnable)
                return;
            var tmp = (m_isVertical ? data.y : 1 - data.x);
            tmp = Mathf.Clamp01(tmp);
            var value = (ContentSpace - ViewSpace) * tmp;
            var start = ContentSpace - value - ViewSpace;
            var startIndex = Mathf.FloorToInt(start / itemSpace);
            //Debug.LogErrorFormat(" startindex = {0} , m_startIndex = {1}", startIndex, m_startIndex);
            startIndex = Mathf.Max(0, startIndex);

            if (startIndex != m_startIndex)
            {
                ///根据m_startIndex 与 startIndex 大小，判断玩家滑动的方向
                _moveDict = m_startIndex - startIndex < 0 ? MoveDict.righrordown : MoveDict.leftorup;
                m_startIndex = startIndex;
                UpdateView();

                _moveDict = MoveDict.none;
            }
        }

        /// <summary>
        /// 更新视图
        /// </summary>
        public void UpdateView()
        {
            if (!_isEnable)
                return;

            ///限制其范围
            m_startIndex = Mathf.Max(0, Mathf.Min(m_startIndex, _dataCount - 1));
            if (_moveDict == MoveDict.none || _moveDict == MoveDict.nomove)
                UpdateAllItems();
            else
            {
                UpdateChangeItems();
            }

        }
        /// <summary>
        /// Updates all items.
        /// </summary>
        void UpdateAllItems()
        {
            for (int i = 0; i < m_viewItemCount; i++)
            {
                var index = m_startIndex + i;
                if (index > _dataCount - 1 || index < 0)
                {
                    m_items[i].item.gameObject.SetActive(false);
                    continue;
                }
                if (i > m_items.Count - 1 || m_items[i] == null || m_items[i].item == null)
                {
                    ItemBase itembase;
                    var go = InitItem(out itembase);
                    if (go)
                    {
                        go.name = "item" + i;
                        if (m_items.Count - 1 < i)
                            m_items.Add(new IndexItem(itembase, index));
                        else
                        {
                            m_items[i].item = itembase;
                            m_items[i].index = index;
                        }
                    }
                }
                else
                {
                    m_items[i].index = index;
                }
                if (m_items[i] != null)
                {
                    UpdateItem(m_items[i]);
                }
                else
                {
                    m_items.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Updates the change items.
        /// </summary>
        void UpdateChangeItems()
        {
            if (m_viewItemCount != m_items.Count)
            {
                UpdateAllItems();
            }
            else
            {
                if (_moveDict == MoveDict.righrordown)
                {
                    int index = m_startIndex + m_viewItemCount-1; ///最后一个item的index
                    for (int i = 0; i < m_viewItemCount; i++)
                    {
                        if (!CheckItemIndex(m_items[i].index))
                        {
                            m_items[i].index = index--;
                            UpdateItem(m_items[i]);
                        }

                    }
                }
                else if (_moveDict == MoveDict.leftorup)
                {
                    int index = m_startIndex ; ///第一个item的index
                    for (int i = m_items.Count- 1; i >=0; i--)
                    {
                        if (!CheckItemIndex(m_items[i].index))
                        {
                            m_items[i].index = index++;
                            UpdateItem(m_items[i]);
                        }

                    }
                }
                /// 根据index 对list 进行重拍
                m_items.Sort((x ,y) => {
                    return x.index == y.index ? 0 : (x.index > y.index ? 1 : -1);
                } );
            }
        }

        void UpdateItem(IndexItem item)
        {
            if (item == null)
                return;
            if (item.item == null)
            {
                Debug.LogError("Item is null!");
                return;
            }
            if(!CheckItemIndex(item.index))
            {
                Debug.LogError("Item is index is not in view!");
                return;
            }
            UpdateItem(item.item, item.index);
        }

        void UpdateItem(ItemBase item , int index )
        {
            //Debug.LogErrorFormat(" item name = {0} , index = {1} , moveDict = {2} , startindex = {3} ,diffvalue = {4}" , item.gameObject.name , index ,_moveDict,m_startIndex ,diffValue);
            item.transform.localPosition = GetItemPos(index, item.transform.localPosition);
            item.gameObject.SetActive(true);
            if (_callback != null)
                _callback(item, index);
        }

        Vector3 GetItemPos( int index , Vector3 originPos )
        {
            if (m_isVertical)
            {
                originPos.y = index * itemSpace * -1;
            }
            else
            {
                originPos.x = index * itemSpace ;
            }
            return originPos;
        }
        bool CheckItemIndex(int index)
        {
            return index >= m_startIndex && index <= m_startIndex + m_viewItemCount-1;
        }

        /// <summary>
        /// Inits the item.
        /// </summary>
        /// <returns>The item.</returns>
        /// <param name="itembase">Itembase.</param>
        GameObject InitItem( out ItemBase itembase)
        {
            itembase = null;
            GameObject go = null;
            if (_item)
            {
                go = GameObject.Instantiate<GameObject>(_item, this.m_content);
                if(go)
                    itembase = go.GetComponent<ItemBase>();
            }
            return itembase == null ? null : go;
        }

    }
    /*    #region 链表

    class LinkedListItem 
    {
        public LinkedListItem pro;
        public int index;
        public ItemBase itembase;
        public LinkedListItem next;
    }

    class ItemlinkList 
    {
        public LinkedListItem head;
        public LinkedListItem tail;
        public int count ;

        HashSet<LinkedListItem> items = new HashSet<LinkedListItem>();

        public void AddItemToTail(LinkedListItem item)
        {
            if (items.Contains(item))
            {
                Debug.LogError("items contains the item");
                return;
            }
            if (head == null)
            {
                head = tail = item;
                item.pro = item.next = null;
            }
            else
            {
                tail.next = item;
                item.pro = tail;
                tail = item;

            }
            items.Add(item);
            count++;
        }

        public void AddItemToHead(LinkedListItem item)
        {
            if (items.Contains(item))
            {
                Debug.LogError("items contains the item");
                return;
            }
            if (head == null)
            {
                head = tail = item;
                item.next = item.pro = null;
            }
            else
            {
                head.pro = item;
                item.next = head;
                head = item;

            }
            items.Add(item);
            count++;
        }


        public void RemoveItem(LinkedListItem item)
        {
            if (!items.Contains(item))
            {
                Debug.LogError("items dont contains the item!");
                return;
            }

            if (item == head)
            {
                if (item.next == null)
                    head = tail = null;
                else
                {
                    head = item.next;
                    head.pro = null;
                }
            }
            else if (item == tail)
            {
                if (item.pro == null)
                    head = tail = null;
                else
                {
                    tail = item.pro;
                    tail.next = null;
                }
            }
            else
            {
                item.pro.next = item.next.pro;
                item.next.pro = item.pro.next;
            }
            count--;
            items.Remove(item);
        }

    }

    #endregion
    */
}