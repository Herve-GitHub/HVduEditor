using LVGLSharp;
using LVGLSharp.Interop;
using LVGLSharp.Runtime.Windows;
using NLua;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace VduEditor
{
    /// <summary>
    /// 菜单栏管理
    /// 包含文件、编辑、对齐、视图等菜单项
    /// </summary>
    unsafe partial class Program
    {
        // 菜单栏高度
        const int MENU_BAR_HEIGHT = 35;

        // 菜单栏对象
        static lv_obj_t* menuBar;

        // 当前打开的下拉菜单（用于显示子菜单）
        static lv_obj_t* activeDropdown = null;

        // 菜单按钮对象
        static lv_obj_t* menuFileBtn;
        static lv_obj_t* menuEditBtn;
        static lv_obj_t* menuAlignBtn;
        static lv_obj_t* menuViewBtn;

        /// <summary>
        /// 创建菜单栏
        /// </summary>
        static void CreateMenuBar()
        {
            // 创建菜单栏容器
            menuBar = lv_obj_create(root);
            lv_obj_set_size(menuBar, mianWindowsSize.Width - 20, MENU_BAR_HEIGHT);
            lv_obj_set_pos(menuBar, 0, 0);
            lv_obj_set_style_bg_color(menuBar, lv_color_hex(0x37474F), 0);  // 深灰色
            lv_obj_set_style_border_width(menuBar, 0, 0);
            lv_obj_set_style_radius(menuBar, 0, 0);
            lv_obj_set_style_pad_all(menuBar, 0, 0);
            lv_obj_set_style_pad_left(menuBar, 0, 0);
            lv_obj_set_style_pad_gap(menuBar, 0, 0);
            lv_obj_set_flex_flow(menuBar, LV_FLEX_FLOW_ROW);
            lv_obj_set_flex_align(menuBar, LV_FLEX_ALIGN_START, LV_FLEX_ALIGN_CENTER, LV_FLEX_ALIGN_CENTER);
            lv_obj_set_scrollbar_mode(menuBar, LV_SCROLLBAR_MODE_OFF);
            lv_obj_remove_flag(menuBar, LV_OBJ_FLAG_SCROLLABLE);

            // 创建菜单按钮
            menuFileBtn = CreateMenuButton("文件(F)");
            lv_obj_add_event(menuFileBtn, &OnFileMenuClick, lv_event_code_t.LV_EVENT_CLICKED, null);

            menuEditBtn = CreateMenuButton("编辑(E)");
            lv_obj_add_event(menuEditBtn, &OnEditMenuClick, lv_event_code_t.LV_EVENT_CLICKED, null);

            menuAlignBtn = CreateMenuButton("对齐(A)");
            lv_obj_add_event(menuAlignBtn, &OnAlignMenuClick, lv_event_code_t.LV_EVENT_CLICKED, null);

            menuViewBtn = CreateMenuButton("视图(V)");
            lv_obj_add_event(menuViewBtn, &OnViewMenuClick, lv_event_code_t.LV_EVENT_CLICKED, null);
        }

        /// <summary>
        /// 创建菜单按钮
        /// </summary>
        static lv_obj_t* CreateMenuButton(string text)
        {
            var btn = lv_btn_create(menuBar);
            lv_obj_set_size(btn, LV_SIZE_CONTENT, 30);
            lv_obj_set_style_min_width(btn, 70, 0);
            lv_obj_set_style_bg_color(btn, lv_color_hex(0x37474F), 0);
            lv_obj_set_style_bg_color(btn, lv_color_hex(0x546E7A), LV_STATE_PRESSED);
            lv_obj_set_style_bg_color(btn, lv_color_hex(0x455A64), LV_STATE_FOCUSED);
            lv_obj_set_style_shadow_width(btn, 0, 0);
            lv_obj_set_style_radius(btn, 0, 0);
            lv_obj_set_style_pad_left(btn, 10, 0);
            lv_obj_set_style_pad_right(btn, 10, 0);

            var label = lv_label_create(btn);
            fixed (byte* utf8Ptr = Encoding.UTF8.GetBytes(text + "\0"))
                lv_label_set_text(label, utf8Ptr);
            lv_obj_center(label);
            lv_obj_set_style_text_color(label, lv_color_hex(0xFFFFFF), 0);
            ApplyDefaultFontStyle(label);

            return btn;
        }

        /// <summary>
        /// 关闭当前打开的下拉菜单
        /// </summary>
        static void CloseActiveDropdown()
        {
            if (activeDropdown != null)
            {
                lv_obj_del(activeDropdown);
                activeDropdown = null;
            }
        }

        /// <summary>
        /// 创建下拉菜单
        /// </summary>
        static lv_obj_t* CreateDropdownMenu(lv_obj_t* parentBtn, (string text, string shortcut, Action? action)[] items)
        {
            CloseActiveDropdown();

            // 获取按钮位置
            int btnX = lv_obj_get_x(parentBtn);
            int btnY = lv_obj_get_y(parentBtn) + lv_obj_get_height(parentBtn);

            // 计算菜单高度
            int itemHeight = 32;
            int menuHeight = items.Length * itemHeight + 10;

            // 创建下拉菜单容器
            var dropdown = lv_obj_create(lv_layer_top());
            lv_obj_set_size(dropdown, 200, menuHeight);
            lv_obj_set_pos(dropdown, btnX, btnY + MENU_BAR_HEIGHT);
            lv_obj_set_style_bg_color(dropdown, lv_color_hex(0xFFFFFF), 0);
            lv_obj_set_style_border_color(dropdown, lv_color_hex(0xCCCCCC), 0);
            lv_obj_set_style_border_width(dropdown, 1, 0);
            lv_obj_set_style_radius(dropdown, 4, 0);
            lv_obj_set_style_shadow_width(dropdown, 8, 0);
            lv_obj_set_style_shadow_color(dropdown, lv_color_hex(0x000000), 0);
            lv_obj_set_style_shadow_opa(dropdown, 60, 0);
            lv_obj_set_style_pad_all(dropdown, 5, 0);
            lv_obj_set_style_pad_gap(dropdown, 2, 0);
            lv_obj_set_flex_flow(dropdown, LV_FLEX_FLOW_COLUMN);
            lv_obj_set_scrollbar_mode(dropdown, LV_SCROLLBAR_MODE_OFF);
            lv_obj_remove_flag(dropdown, LV_OBJ_FLAG_SCROLLABLE);

            // 创建菜单项
            for (int i = 0; i < items.Length; i++)
            {
                var item = items[i];
                if (item.text == "-")
                {
                    // 分隔线
                    var separator = lv_obj_create(dropdown);
                    lv_obj_set_size(separator, 180, 1);
                    lv_obj_set_style_bg_color(separator, lv_color_hex(0xE0E0E0), 0);
                    lv_obj_set_style_border_width(separator, 0, 0);
                    lv_obj_set_style_pad_all(separator, 0, 0);
                }
                else
                {
                    CreateMenuItem(dropdown, item.text, item.shortcut, item.action);
                }
            }

            activeDropdown = dropdown;

            // 点击其他地方关闭菜单
            lv_obj_add_event(dropdown, &OnDropdownFocusLost, lv_event_code_t.LV_EVENT_DEFOCUSED, null);

            return dropdown;
        }

        /// <summary>
        /// 创建菜单项
        /// </summary>
        static lv_obj_t* CreateMenuItem(lv_obj_t* parent, string text, string shortcut, Action? action)
        {
            var item = lv_obj_create(parent);
            lv_obj_set_size(item, 180, 28);
            lv_obj_set_style_bg_color(item, lv_color_hex(0xFFFFFF), 0);
            lv_obj_set_style_bg_color(item, lv_color_hex(0xE3F2FD), LV_STATE_FOCUSED);
            lv_obj_set_style_bg_color(item, lv_color_hex(0xBBDEFB), LV_STATE_PRESSED);
            lv_obj_set_style_border_width(item, 0, 0);
            lv_obj_set_style_radius(item, 2, 0);
            lv_obj_set_style_pad_left(item, 10, 0);
            lv_obj_set_style_pad_right(item, 10, 0);
            lv_obj_set_scrollbar_mode(item, LV_SCROLLBAR_MODE_OFF);
            lv_obj_remove_flag(item, LV_OBJ_FLAG_SCROLLABLE);
            lv_obj_add_flag(item, LV_OBJ_FLAG_CLICKABLE);

            // 菜单文本
            var label = lv_label_create(item);
            fixed (byte* utf8Ptr = Encoding.UTF8.GetBytes(text + "\0"))
                lv_label_set_text(label, utf8Ptr);
            lv_obj_align(label, LV_ALIGN_LEFT_MID, 0, 0);
            lv_obj_set_style_text_color(label, lv_color_hex(0x333333), 0);
            ApplyDefaultFontStyle(label);

            // 快捷键提示
            if (!string.IsNullOrEmpty(shortcut))
            {
                var shortcutLabel = lv_label_create(item);
                fixed (byte* utf8Ptr = Encoding.UTF8.GetBytes(shortcut + "\0"))
                    lv_label_set_text(shortcutLabel, utf8Ptr);
                lv_obj_align(shortcutLabel, LV_ALIGN_RIGHT_MID, 0, 0);
                lv_obj_set_style_text_color(shortcutLabel, lv_color_hex(0x888888), 0);
                ApplyDefaultFontStyle(shortcutLabel);
            }

            // 存储动作并添加点击事件
            if (action != null)
            {
                StoreMenuAction(item, action);
                lv_obj_add_event(item, &OnMenuItemClick, lv_event_code_t.LV_EVENT_CLICKED, null);
            }

            return item;
        }

        // 菜单动作映射
        static Dictionary<nint, Action> menuActionMap = new();
        static void StoreMenuAction(lv_obj_t* item, Action action) => menuActionMap[(nint)item] = action;
        static Action? GetMenuAction(lv_obj_t* item) => menuActionMap.GetValueOrDefault((nint)item);

        /// <summary>
        /// 菜单项点击事件
        /// </summary>
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static unsafe void OnMenuItemClick(lv_event_t* e)
        {
            var item = (lv_obj_t*)lv_event_get_target(e);
            var action = GetMenuAction(item);
            CloseActiveDropdown();
            action?.Invoke();
        }

        /// <summary>
        /// 下拉菜单失去焦点事件
        /// </summary>
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static unsafe void OnDropdownFocusLost(lv_event_t* e)
        {
            // 延迟关闭，避免点击菜单项时立即关闭
        }

        #region 文件菜单

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static unsafe void OnFileMenuClick(lv_event_t* e)
        {
            var items = new (string, string, Action?)[]
            {
                ("新建工程", "Ctrl+N", OnNewProject),
                ("打开工程", "Ctrl+O", OnOpenProject),
                ("-", "", null),
                ("保存工程", "Ctrl+S", OnSaveProject),
                ("另存为...", "Ctrl+Shift+S", OnSaveProjectAs),
                ("-", "", null),
                ("关闭工程", "", OnCloseProject),
                ("-", "", null),
                ("退出", "Alt+F4", OnExit)
            };
            CreateDropdownMenu(menuFileBtn, items);
        }

        static void OnNewProject()
        {
            Console.WriteLine("新建工程");
            ShowMessage("新建工程", "功能开发中...");
        }

        static void OnOpenProject()
        {
            Console.WriteLine("打开工程");
            ShowMessage("打开工程", "功能开发中...");
        }

        static void OnSaveProject()
        {
            Console.WriteLine("保存工程");
            ShowMessage("保存工程", "功能开发中...");
        }

        static void OnSaveProjectAs()
        {
            Console.WriteLine("另存为");
            ShowMessage("另存为", "功能开发中...");
        }

        static void OnCloseProject()
        {
            Console.WriteLine("关闭工程");
            // 清空画布上的所有控件
            ClearCanvas();
            ShowMessage("关闭工程", "已关闭当前工程");
        }

        static void OnExit()
        {
            Console.WriteLine("退出程序");
            Environment.Exit(0);
        }

        /// <summary>
        /// 清空画布
        /// </summary>
        static void ClearCanvas()
        {
            // 清空控件实例
            foreach (var kvp in widgetInstances)
            {
                lv_obj_del((lv_obj_t*)kvp.Key);
            }
            widgetInstances.Clear();
            SelectWidget(null);
        }

        #endregion

        #region 编辑菜单

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static unsafe void OnEditMenuClick(lv_event_t* e)
        {
            var items = new (string, string, Action?)[]
            {
                ("撤销", "Ctrl+Z", OnUndo),
                ("重做", "Ctrl+Y", OnRedo),
                ("-", "", null),
                ("剪切", "Ctrl+X", OnCut),
                ("复制", "Ctrl+C", OnCopy),
                ("粘贴", "Ctrl+V", OnPaste),
                ("-", "", null),
                ("删除", "Delete", OnDelete),
                ("全选", "Ctrl+A", OnSelectAll)
            };
            CreateDropdownMenu(menuEditBtn, items);
        }

        static void OnUndo()
        {
            Console.WriteLine("撤销");
            ShowMessage("撤销", "功能开发中...");
        }

        static void OnRedo()
        {
            Console.WriteLine("重做");
            ShowMessage("重做", "功能开发中...");
        }

        static void OnCut()
        {
            Console.WriteLine("剪切");
            if (selectedWidget != null)
            {
                OnCopy();
                OnDelete();
            }
        }

        // 剪贴板（存储复制的控件信息）
        static (WidgetMeta? meta, Dictionary<string, object>? values) clipboard;

        static void OnCopy()
        {
            Console.WriteLine("复制");
            if (selectedWidget != null)
            {
                clipboard = (selectedWidget.Meta, new Dictionary<string, object>(selectedWidget.Values));
                ShowMessage("复制", $"已复制控件: {selectedWidget.Meta.Name}");
            }
        }

        static void OnPaste()
        {
            Console.WriteLine("粘贴");
            if (clipboard.meta != null)
            {
                // 在当前选中位置偏移一点粘贴
                int x = 50, y = 50;
                if (selectedWidget != null)
                {
                    x = lv_obj_get_x(selectedWidget.Obj) + 20;
                    y = lv_obj_get_y(selectedWidget.Obj) + 20;
                }

                var newWidget = CreateCanvasWidget(clipboard.meta, x, y);

                // 复制属性值
                if (clipboard.values != null)
                {
                    foreach (var kvp in clipboard.values)
                    {
                        if (kvp.Key != "x" && kvp.Key != "y")
                        {
                            newWidget.Values[kvp.Key] = kvp.Value;
                            ApplyPropertyToWidget(newWidget, kvp.Key, kvp.Value);
                        }
                    }
                }

                SelectWidget(newWidget);
                ShowMessage("粘贴", $"已粘贴控件: {clipboard.meta.Name}");
            }
        }

        static void OnDelete()
        {
            Console.WriteLine("删除");
            if (selectedWidget != null)
            {
                var name = selectedWidget.Meta.Name;
                widgetInstances.Remove((nint)selectedWidget.Obj);
                lv_obj_del(selectedWidget.Obj);
                SelectWidget(null);
                ShowMessage("删除", $"已删除控件: {name}");
            }
        }

        static void OnSelectAll()
        {
            Console.WriteLine("全选");
            ShowMessage("全选", "功能开发中...");
        }

        #endregion

        #region 对齐菜单

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static unsafe void OnAlignMenuClick(lv_event_t* e)
        {
            var items = new (string, string, Action?)[]
            {
                ("左对齐", "", OnAlignLeft),
                ("右对齐", "", OnAlignRight),
                ("顶部对齐", "", OnAlignTop),
                ("底部对齐", "", OnAlignBottom),
                ("-", "", null),
                ("水平居中", "", OnAlignCenterH),
                ("垂直居中", "", OnAlignCenterV),
                ("-", "", null),
                ("水平等距分布", "", OnDistributeH),
                ("垂直等距分布", "", OnDistributeV)
            };
            CreateDropdownMenu(menuAlignBtn, items);
        }

        static void OnAlignLeft()
        {
            Console.WriteLine("左对齐");
            if (selectedWidget != null)
            {
                lv_obj_set_x(selectedWidget.Obj, 10);
                SyncWidgetToProperties(selectedWidget);
            }
        }

        static void OnAlignRight()
        {
            Console.WriteLine("右对齐");
            if (selectedWidget != null)
            {
                int canvasW = lv_obj_get_width(canvasArea);
                int objW = lv_obj_get_width(selectedWidget.Obj);
                lv_obj_set_x(selectedWidget.Obj, canvasW - objW - 10);
                SyncWidgetToProperties(selectedWidget);
            }
        }

        static void OnAlignTop()
        {
            Console.WriteLine("顶部对齐");
            if (selectedWidget != null)
            {
                lv_obj_set_y(selectedWidget.Obj, 10);
                SyncWidgetToProperties(selectedWidget);
            }
        }

        static void OnAlignBottom()
        {
            Console.WriteLine("底部对齐");
            if (selectedWidget != null)
            {
                int canvasH = lv_obj_get_height(canvasArea);
                int objH = lv_obj_get_height(selectedWidget.Obj);
                lv_obj_set_y(selectedWidget.Obj, canvasH - objH - 10);
                SyncWidgetToProperties(selectedWidget);
            }
        }

        static void OnAlignCenterH()
        {
            Console.WriteLine("水平居中");
            if (selectedWidget != null)
            {
                int canvasW = lv_obj_get_width(canvasArea);
                int objW = lv_obj_get_width(selectedWidget.Obj);
                lv_obj_set_x(selectedWidget.Obj, (canvasW - objW) / 2);
                SyncWidgetToProperties(selectedWidget);
            }
        }

        static void OnAlignCenterV()
        {
            Console.WriteLine("垂直居中");
            if (selectedWidget != null)
            {
                int canvasH = lv_obj_get_height(canvasArea);
                int objH = lv_obj_get_height(selectedWidget.Obj);
                lv_obj_set_y(selectedWidget.Obj, (canvasH - objH) / 2);
                SyncWidgetToProperties(selectedWidget);
            }
        }

        static void OnDistributeH()
        {
            Console.WriteLine("水平等距分布");
            ShowMessage("水平等距分布", "需要选择多个控件，功能开发中...");
        }

        static void OnDistributeV()
        {
            Console.WriteLine("垂直等距分布");
            ShowMessage("垂直等距分布", "需要选择多个控件，功能开发中...");
        }

        #endregion

        #region 视图菜单

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static unsafe void OnViewMenuClick(lv_event_t* e)
        {
            var items = new (string, string, Action?)[]
            {
                ("显示工具箱", "F1", OnShowToolbox),
                ("显示属性面板", "F2", OnShowProperty),
                ("-", "", null),
                ("显示网格", "", OnToggleGrid),
                ("对齐到网格", "", OnToggleSnapGrid)
            };
            CreateDropdownMenu(menuViewBtn, items);
        }

        static void OnShowToolbox()
        {
            ToggleToolbox();
        }

        static void OnShowProperty()
        {
            // 暂未实现
        }

        static bool showGrid = false;
        static void OnToggleGrid()
        {
            showGrid = !showGrid;
            Console.WriteLine($"显示网格: {showGrid}");
            ShowMessage("显示网格", showGrid ? "已开启" : "已关闭");
        }

        static bool snapToGrid = false;
        static void OnToggleSnapGrid()
        {
            snapToGrid = !snapToGrid;
            Console.WriteLine($"对齐到网格: {snapToGrid}");
            ShowMessage("对齐到网格", snapToGrid ? "已开启" : "已关闭");
        }

        #endregion

        #region 消息提示

        /// <summary>
        /// 显示消息提示
        /// </summary>
        static void ShowMessage(string title, string message)
        {
            // 创建消息框
            var msgBox = lv_obj_create(lv_layer_top());
            lv_obj_set_size(msgBox, 280, 100);
            lv_obj_align(msgBox, LV_ALIGN_TOP_RIGHT, -20, 50);
            lv_obj_set_style_bg_color(msgBox, lv_color_hex(0x323232), 0);
            lv_obj_set_style_radius(msgBox, 8, 0);
            lv_obj_set_style_shadow_width(msgBox, 10, 0);
            lv_obj_set_style_shadow_color(msgBox, lv_color_hex(0x000000), 0);
            lv_obj_set_style_shadow_opa(msgBox, 80, 0);
            lv_obj_set_style_pad_all(msgBox, 15, 0);
            lv_obj_set_scrollbar_mode(msgBox, LV_SCROLLBAR_MODE_OFF);
            lv_obj_remove_flag(msgBox, LV_OBJ_FLAG_SCROLLABLE);
            lv_obj_remove_flag(msgBox, LV_OBJ_FLAG_CLICKABLE);

            // 标题
            var titleLabel = lv_label_create(msgBox);
            fixed (byte* utf8Ptr = Encoding.UTF8.GetBytes(title + "\0"))
                lv_label_set_text(titleLabel, utf8Ptr);
            lv_obj_set_pos(titleLabel, 0, 0);
            lv_obj_set_style_text_color(titleLabel, lv_color_hex(0x4CAF50), 0);
            ApplyDefaultFontStyle(titleLabel);

            // 消息内容
            var msgLabel = lv_label_create(msgBox);
            fixed (byte* utf8Ptr = Encoding.UTF8.GetBytes(message + "\0"))
                lv_label_set_text(msgLabel, utf8Ptr);
            lv_obj_set_pos(msgLabel, 0, 30);
            lv_obj_set_style_text_color(msgLabel, lv_color_hex(0xFFFFFF), 0);
            ApplyDefaultFontStyle(msgLabel);

            // 2秒后自动关闭
            StoreMessageBox(msgBox);
            // 使用定时器延迟删除
            var timer = lv_timer_create(&OnMessageTimeout, 2000, (void*)(nint)msgBox);
            lv_timer_set_repeat_count(timer, 1);
        }

        static HashSet<nint> messageBoxes = new();
        static void StoreMessageBox(lv_obj_t* box) => messageBoxes.Add((nint)box);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static unsafe void OnMessageTimeout(lv_timer_t* timer)
        {
            var box = (lv_obj_t*)lv_timer_get_user_data(timer);
            if (messageBoxes.Contains((nint)box))
            {
                messageBoxes.Remove((nint)box);
                lv_obj_del(box);
            }
        }

        #endregion
    }
}
