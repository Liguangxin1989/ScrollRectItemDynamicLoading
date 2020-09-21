///
/// version 1 兼容之前版本，去掉多余的layout 选中，toggle 等 ，可以抽出分装一个组件 2018.0810
///
///version 2 继续优化 ，每次变化 只更新变化的item ,对水平移动进行测试
///
///version 3 增加 item 的增 删 改
/// 
///version 4 增加瞬移 （移动到底部，顶部，指定位置)
///
///version 5 增加多个Tab页面共用一个DataGrid 时候，来回切换时候出现的bug
/// 
///

using System;
using System.Collections;
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
        #region 变量
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
            rightordown = 2,
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
        [SerializeField]
        private ScrollRect m_scrollRect;
        private RectTransform m_tranScrollRect;

        /// <summary>
        /// 可视区域内Item的数量（向上取整）
        /// </summary>
        private int m_viewItemCount;
        /// <summary>
        /// 是否是垂直滚动方式，否则是水平滚动
        /// </summary>
        private bool m_isVertical;
        /// <summary>
        /// 数据数组渲染的起始下标(就是view 显示的第一个下标)
        /// </summary>
        private int m_startIndex;
        /// <summary>
        /// Content 中显示的第一个下标 与 m_startIndex 并不相同
        /// </summary>
        private int _contentStartIndex;
        /// <summary>
        /// 多缓存的数目 实际上实例化的数据量是 m_viewItemCount + CACHENUM
        /// 设置越多，缓存的越多，在高速移动的时候就不会因为加载不及时而不显示
        /// 目前设置为2 目的是为了 view 区域的top 和bottom 都多余一个，这样基本满足高速加载时候加载不及时的问题
        /// </summary>
        const int CACHENUM = 2;
        /// <summary>
        /// 内容长度
        /// </summary>
        private float ContentSpace;
        /// <summary>
        /// 可见区域长度
        /// </summary>
        private float ViewSpace;
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
        /// <summary>
        /// 回调函数
        /// </summary>
        InitItemCallback _callback;

        bool isInit = false;
        /// <summary>
        /// 是否为第一次初始化scrollRect，初始化的时候，必须重新刷新
        /// </summary>
        bool isFirst = true;
        #endregion
        void Init()
        {
            var go = gameObject;
            m_scrollRect = this.GetComponent<ScrollRect>();
            m_content = m_scrollRect.content;
            m_tranScrollRect = m_scrollRect.viewport == null ? this.transform as RectTransform : m_scrollRect.viewport;
            m_isVertical = m_scrollRect.vertical;
            ViewSpace = m_isVertical ? m_tranScrollRect.rect.height : m_tranScrollRect.rect.width;
            m_scrollRect.onValueChanged.AddListener(OnScroll);
            isInit = true;
        }
        /// <summary>
        /// 设置Item 相关的数据
        /// </summary>
        /// <param name="item"> item 的 prefab </param>
        /// <param name="count">item 的数目</param>
        /// <param name="callback"> item的 回调</param>
        /// <param name="initIndex"> 初始时候的位置</param>
        public void SetItemsData(GameObject item, int count, InitItemCallback callback, int initIndex = 0)
        {
            _dataCount = count;
            _item = item;
            _callback = callback;
            m_startIndex = initIndex;
            if (!isInit)
                Init();
            InitData();
        }

        void SetEnable()
        {
            _isEnable = _item != null && _item.GetComponent<ItemBase>() && _callback != null && _dataCount > 0;
        }

        void InitData()
        {
            if (!isInit)
                return;
            isFirst = true;
            SetItemSize();
            SetContentSize();
            SetCacheCount();
            SetEnable();
            StartCoroutine(IgnoreInertia());
            ShowItem(m_startIndex);
        }
        /// <summary>
        /// 在切换scroll中数据的时候，如果此时scrollRect还在滑动，或者ScrollRect的content变化很大，会在切换的时候出现滑动，用此协程取消在初始化时候的滑动
        /// </summary>
        /// <returns></returns>
        IEnumerator IgnoreInertia()
        {
            m_scrollRect.inertia = false;
            m_scrollRect.movementType = ScrollRect.MovementType.Clamped;
            yield return null;
            yield return null;
            m_scrollRect.inertia = true;
            m_scrollRect.movementType = ScrollRect.MovementType.Elastic;
        }

        /// <summary>
        /// 计算Item的大小
        /// </summary>
        void SetItemSize()
        {
            if (_item)
            {
                itemSpace = _item.GetComponent<ItemBase>().Width();
            }
            ///TODO 如果有Gridlayout 应该把 Space 加进去

        }
        /// <summary>
        /// 计算Content的大小  Content的变化会触发ValueChange的回调
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
                ///+1 是很重要的！ 为什么 ？你猜！
                m_viewItemCount = m_viewItemCount +1+ CACHENUM;
            }
        }
        /// <summary>
        /// 更新玩家数目
        /// </summary>
        /// <param name="dataCount"></param>
        public void UpdateDataCount(int dataCount)
        {
            if(dataCount != _dataCount)
            {
                _dataCount = dataCount;
                SetContentSize();
                UpdateView();
            }
        }
        /// <summary>
        /// 更新某个item
        /// </summary>
        /// <param name="index"></param>
        public void UpdateItem(int index)
        {
            for (int i = 0; i < m_items.Count; i++)
            {
                if(m_items[i].index == index)
                {
                    UpdateItem(m_items[i]);
                    break;
                }
            }
        }

        public void ShowViewTop()
        {
            if (m_scrollRect)
            {
                if (m_isVertical)
                    m_scrollRect.verticalNormalizedPosition = 1;
                else
                    m_scrollRect.horizontalNormalizedPosition = 0;
            }
        }

        public void ShowViewBottom()
        {
            if (m_scrollRect)
            {
                if (m_isVertical)
                    m_scrollRect.verticalNormalizedPosition = 0;
                else
                    m_scrollRect.horizontalNormalizedPosition = 1;
            }
        }

        public void ShowItem(int index)
        {
            if (index < 0 || (_dataCount >0 && index > _dataCount - 1))
            {
                Debug.LogError("ERROR: the index out of range!!");
                return;
            }
            if (_isEnable)
            {
                float value =( index * itemSpace )/ (ContentSpace-ViewSpace);

                value = Mathf.Max(0, Mathf.Min(value, 1));
                if (m_scrollRect)
                {
                    if (m_isVertical)
                    {
                        m_scrollRect.verticalNormalizedPosition = value; ///修改scrollRect 的进度条，防止在切换Tab的时候，因为value一样而不调用scroll的bug
                        m_scrollRect.verticalNormalizedPosition = 1 - value;
                    }
                    else
                    {
                        m_scrollRect.horizontalNormalizedPosition =1- value;
                        m_scrollRect.horizontalNormalizedPosition = value;
                    }
                }
            }
            else
            {
                Debug.LogError(" The Scroll view cant enable");
            }
        }

        #region Cure
        /// <summary>
        /// scrollRect滑动变化的回调
        /// </summary>
        /// <param name="data"></param>
        private void OnScroll(Vector2 data)
        {
            if (!_isEnable)
                return;
            var tmp = (m_isVertical ? data.y : 1 - data.x);
            tmp = Mathf.Clamp01(tmp);
            var value = (ContentSpace - ViewSpace) * tmp;
            var start = ContentSpace - value - ViewSpace;
            ///计算移动后的startIndex
            var startIndex = Mathf.FloorToInt(start / itemSpace);
            startIndex = Mathf.Max(0, startIndex);

            if (startIndex != m_startIndex || isFirst)
            {
                ///根据m_startIndex 与 startIndex 大小，判断玩家滑动的方向
                if (isFirst)
                    _moveDict = MoveDict.nomove;
                else
                    _moveDict = m_startIndex - startIndex < 0 ? MoveDict.rightordown : MoveDict.leftorup;
                m_startIndex = startIndex;
                UpdateView();

                _moveDict = MoveDict.none;
            }
            isFirst = false;
        }
        /// <summary>
        /// 更新视图
        /// </summary>
        void UpdateView()
        {
            if (!_isEnable)
                return;
            ///限制其范围
            m_startIndex = Mathf.Max(0, Mathf.Min(m_startIndex, _dataCount - 1));
            ///更新下标
            UpdateContentStartIndex();
            UpdateItems();
        }

        HashSet<int> itemindexs = new HashSet<int>();

        void UpdateContentStartIndex()
        {
            _contentStartIndex = m_startIndex - (CACHENUM / 2);
            int endIndex = _contentStartIndex + m_viewItemCount - 1;
            ///检测 end范围
            endIndex = Mathf.Min(endIndex, _dataCount - 1);
            _contentStartIndex = endIndex - m_viewItemCount + 1;
            ///检测 start 范围
            _contentStartIndex = Math.Max(_contentStartIndex, 0);
            endIndex = _contentStartIndex + m_viewItemCount - 1;
        }

        void InitiItemIndexList()
        {
            int endIndex = _contentStartIndex + m_viewItemCount - 1;
            itemindexs.Clear();
            for (int i = _contentStartIndex; i <= endIndex; i++)
            {
                itemindexs.Add(i);
            }
        }
        List<IndexItem> tmp = new List<IndexItem>();
        void UpdateItems()
        {
            InitiItemIndexList();
            tmp.Clear();
            var maxCount = Mathf.Max(m_viewItemCount, m_items.Count);
            for (int i = 0; i < maxCount; i++)
            {
                if(i >= m_viewItemCount)
                {
                    m_items[i].item.gameObject.SetActive(false);
                    continue;
                }

                ///init item
                if (i > m_items.Count-1 || m_items[i] == null || m_items[i].item == null)
                {
                    ItemBase itembase;
                    var go = InitItem(out itembase);
                    if (go)
                    {
                        go.name = "item" + i;
                        if (m_items.Count - 1 < i)
                            m_items.Add(new IndexItem(itembase, -1));
                        else
                        {
                            m_items[i].item = itembase;
                            m_items[i].index = -1;
                        }
                    }
                    else
                    {
                        Debug.LogError(" init item Error!");
                        continue;
                    }
                }
                else
                {
                    m_items[i].item.gameObject.SetActive(true);
                }
                ///滑动的时候才进行 不在content的item 更新， 不是因为滑动而更新的 需要全部更新
                if (itemindexs.Contains(m_items[i].index) && (_moveDict == MoveDict.leftorup || _moveDict == MoveDict.rightordown))
                {
                    itemindexs.Remove(m_items[i].index);
                }
                else
                {
                    tmp.Add(m_items[i]);
                }
            }

            int index = 0;
            if (itemindexs.Count != tmp.Count)
            {
                Debug.LogErrorFormat("itemindexs count = {0}  . tmp count = {1}", itemindexs.Count, tmp.Count);
                return;
            }
            foreach (var item in itemindexs)
            {
                tmp[index].index = item;
                UpdateItem(tmp[index]);
                index++;
            }
        }

        /// <summary>
        ///  item 是否在Content 中显示
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>

        bool ItemIsShow(int index)
        {
            return index >= _contentStartIndex && index <= _contentStartIndex + m_viewItemCount - 1;
        }
        #endregion
        #region UpdateItem

        void UpdateItem(IndexItem item)
        {
            if (item == null)
                return;
            if (item.item == null)
            {
                Debug.LogError("Item is null!");
                return;
            }
            UpdateItem(item.item, item.index);
        }

        void UpdateItem(ItemBase item, int index)
        {
            item.transform.localPosition = GetItemPos(index, item.transform.localPosition);
            bool isVisible = index < _dataCount && index >= 0;
            item.gameObject.SetActive(isVisible);
            if (_callback != null && isVisible)
                _callback(item, index);
        }

        Vector3 GetItemPos(int index, Vector3 originPos)
        {
            if (m_isVertical)
            {
                originPos.y = index * itemSpace * -1;
            }
            else
            {
                originPos.x = index * itemSpace;
            }
            return originPos;
        }
        /// <summary>
        /// Inits the item.
        /// </summary>
        /// <returns>The item.</returns>
        /// <param name="itembase">Itembase.</param>
        GameObject InitItem(out ItemBase itembase)
        {
            itembase = null;
            GameObject go = null;
            if (_item)
            {
                go = GameObject.Instantiate<GameObject>(_item, this.m_content);
                if (go)
                    itembase = go.GetComponent<ItemBase>();
            }
            return itembase == null ? null : go;
        }
        #endregion
    }
}