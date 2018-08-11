///
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
        private RectTransform m_content;
        private readonly List<ItemBase> m_items = new List<ItemBase>();

        //下面的属性会需要父对象上有ScrollRect组件
        private ScrollRect m_scrollRect;    //父对象上的，不一定存在
        private RectTransform m_tranScrollRect;

        /// <summary>
        /// 可视区域内Item的数量（向上取整）
        /// </summary>
        private int m_viewItemCount;        
        private bool m_isVertical;          //是否是垂直滚动方式，否则是水平滚动
        private int m_startIndex;           //数据数组渲染的起始下标
        /// <summary>
        /// 多缓存的数目 实际上实例化的数据量是 m_viewItemCount + 3
        /// </summary>
        const int CACHENUM = 3;

        //内容长度
        private float ContentSpace
        {
            get
            {
                return m_isVertical ? m_content.rect.height : m_content.rect.width;
            }
        }
        //可见区域长度
        private float ViewSpace
        {
            get
            {
                return m_isVertical ? m_tranScrollRect.rect.height : m_tranScrollRect.rect.width;
            }
        }
        /// <summary>
        /// 缓存的个数
        /// </summary>
        int _cacheCount;
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
        //每个Item的空间
        private float itemSpace
        {
             get { return  m_isVertical ? _itemSize.y : _itemSize.x; }
        } 

        private bool _isEnable = false;

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
            _isEnable = item != null && item.GetComponent<ItemBase>() && callback != null;
            if (_isEnable)
            {
                _dataCount = count;
                _item = item;
                _callback = callback;
                InitData();
            }
            else
            {
                Debug.LogError(" 参数不对!!");
            }
        }

        void InitData()
        {
            if (!isStart)
                return;
            SetItemSize();
            SetContentSize();
            SetCacheCount();
            m_startIndex = 0;
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
            }
        }
        /// <summary>
        /// 设置缓存数目
        /// </summary>
        void SetCacheCount()
        {
            if(ViewSpace >0 && itemSpace >0)
            {
                m_viewItemCount = Mathf.CeilToInt(ViewSpace / itemSpace);
                _cacheCount = m_viewItemCount + CACHENUM;
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
            int index = top ? 0 : _cacheCount - 1;
            // LoggerHelper.Error("len: "+index);
            ResetScrollPosition(index);
        }

        /// <summary>
        /// 重置滚动位置，如果同时还要赋值新的Data，请在赋值之前调用本方法
        /// </summary>
        public void ResetScrollPosition(int index)
        {
            var unitIndex = Mathf.Clamp(index , 0,_dataCount-1);
            var value = (unitIndex * itemSpace) / (Mathf.Max(ViewSpace, ContentSpace - ViewSpace));
            value = Mathf.Clamp01(value);

            //特殊处理无法使指定条目置顶的情况——拉到最后
            if (unitIndex != index )
                value = 1;

            if (m_scrollRect)
            {
                if (m_isVertical)
                    m_scrollRect.verticalNormalizedPosition = 1 - value;
                else
                    m_scrollRect.horizontalNormalizedPosition = value;
            }

            m_startIndex = unitIndex ;
            UpdateView();
        }

        private void OnScroll(Vector2 data)
        {
            var value = (ContentSpace - ViewSpace) * (m_isVertical ? data.y : 1 - data.x);
            var start = ContentSpace - value - ViewSpace;
            var startIndex = Mathf.FloorToInt(start / itemSpace);
            startIndex = Mathf.Max(0, startIndex);

            if (startIndex != m_startIndex)
            {
                m_startIndex = startIndex;
                UpdateView();
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

            for (int i = 0; i < _cacheCount; i++)
            {
                var index = m_startIndex + i;
                if (index > _dataCount - 1 || index < 0)
                {
                    m_items[i].gameObject.SetActive(false);
                    continue;
                }
                if (i > m_items.Count - 1 || m_items[i] == null)
                {
                    ItemBase itembase;
                    var go = InitItem(out itembase);
                    if (go)
                    {
                        go.name = "item" + i;
                        if (m_items.Count - 1 < i)
                            m_items.Add(itembase);
                        else
                            m_items[i] = itembase;
                    }
                }
                if (m_items[i] != null)
                {
                    if (_callback != null)
                        _callback(m_items[i], index);

                    m_items[i].transform.localPosition = GetItemPos(index, m_items[i].transform.localPosition);
                    m_items[i].gameObject.SetActive(true);
                }
                else
                {
                    m_items.RemoveAt(i);
                }
            }
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
}