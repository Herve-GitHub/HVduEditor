using LVGLSharp.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace VduEditor
{
    unsafe partial class Program
    {
        // 工具箱相关变量
        static lv_obj_t* toolbox;
        static lv_obj_t* toolboxHeader;
        static bool toolboxVisible = false;
        static lv_obj_t* btnShowToolbox;

        // 工具箱拖拽状态
        static bool isDraggingToolbox = false;
        static (int offX, int offY) toolboxDragOffset;

        // 从工具箱拖拽控件的状态
        static bool isDraggingFromToolbox = false;
        static lv_obj_t* previewObj = null;
        static WidgetMeta? currentDraggingMeta = null;

        // 工具箱按钮到元数据的映射（包括所有子对象）
        static Dictionary<nint, WidgetMeta> toolButtonMetaMap = new();
        
        // LVGL控件元数据列表（用于工具箱）
        static List<WidgetMeta> widgetMetaList = new();

        /// <summary>
        /// 创建浮动工具箱
        /// </summary>
        static void CreateFloatingToolbox()
        {
            if (toolbox == null)
            {
                toolbox = lv_obj_create(lv_layer_top());
                lv_obj_set_size(toolbox, 150, 650);
                lv_obj_set_pos(toolbox, 5, MENU_BAR_HEIGHT + 50);
                lv_obj_set_flex_flow(toolbox, LV_FLEX_FLOW_COLUMN);
                lv_obj_set_style_pad_all(toolbox, 10, 0);
                lv_obj_set_style_pad_gap(toolbox, 8, 0);
                lv_obj_set_style_bg_color(toolbox, lv_color_hex(0xF5F5F5), 0);
                lv_obj_set_style_border_color(toolbox, lv_color_hex(0x2196F3), 0);
                lv_obj_set_style_border_width(toolbox, 2, 0);
                lv_obj_set_style_radius(toolbox, 8, 0);
                lv_obj_set_style_shadow_width(toolbox, 15, 0);
                lv_obj_set_style_shadow_color(toolbox, lv_color_hex(0x000000), 0);
                lv_obj_set_style_shadow_opa(toolbox, 80, 0);
                lv_obj_set_scrollbar_mode(toolbox, LV_SCROLLBAR_MODE_AUTO);
                // 禁用滚动吸附效果
                lv_obj_set_scroll_snap_x(toolbox, LV_SCROLL_SNAP_NONE);
                lv_obj_set_scroll_snap_y(toolbox, LV_SCROLL_SNAP_NONE);
                ApplyDefaultFontStyle(toolbox);

                // 标题栏（可拖拽区域）
                toolboxHeader = lv_obj_create(toolbox);
                lv_obj_set_size(toolboxHeader, 125, 30);
                lv_obj_set_style_bg_color(toolboxHeader, lv_color_hex(0x2196F3), 0);
                lv_obj_set_style_radius(toolboxHeader, 4, 0);
                lv_obj_set_style_pad_all(toolboxHeader, 0, 0);
                lv_obj_set_scrollbar_mode(toolboxHeader, LV_SCROLLBAR_MODE_OFF);
                lv_obj_remove_flag(toolboxHeader, LV_OBJ_FLAG_SCROLLABLE);
                // 设置按下时的颜色变化提示可拖拽
                lv_obj_set_style_bg_color(toolboxHeader, lv_color_hex(0x1976D2), LV_STATE_PRESSED);

                var toolboxLabel = lv_label_create(toolboxHeader);
                fixed (byte* utf8Ptr = Encoding.UTF8.GetBytes("≡ 工具箱\0"))
                    lv_label_set_text(toolboxLabel, utf8Ptr);
                lv_obj_center(toolboxLabel);
                lv_obj_set_style_text_color(toolboxLabel, lv_color_hex(0xFFFFFF), 0);
                ApplyDefaultFontStyle(toolboxLabel);

                // 为标题栏添加拖拽事件
                lv_obj_add_event(toolboxHeader, &OnToolboxHeaderDrag, lv_event_code_t.LV_EVENT_ALL, null);

                // 添加工具箱按钮
                AddToolboxButtons();

                // 初始隐藏
                lv_obj_add_flag(toolbox, LV_OBJ_FLAG_HIDDEN);
            }
        }

        /// <summary>
        /// 添加工具箱按钮
        /// </summary>
        static void AddToolboxButtons()
        {
            // 添加一些默认的LVGL控件
            var defaultWidgets = new List<WidgetMeta>
            {
                new WidgetMeta
                {
                    Id = "custom_button",
                    Name = "按钮",
                    Description = "基本按钮控件",
                    Properties = new List<PropertyDef>
                    {
                        new PropertyDef { Name = "label", Type = PropertyType.String, Default = "按钮", Label = "文本" },
                        new PropertyDef { Name = "x", Type = PropertyType.Number, Default = 0, Label = "X" },
                        new PropertyDef { Name = "y", Type = PropertyType.Number, Default = 0, Label = "Y" },
                        new PropertyDef { Name = "width", Type = PropertyType.Number, Default = 100, Label = "宽度" },
                        new PropertyDef { Name = "height", Type = PropertyType.Number, Default = 40, Label = "高度" }
                    }
                },
                new WidgetMeta
                {
                    Id = "label",
                    Name = "标签",
                    Description = "文本标签控件",
                    Properties = new List<PropertyDef>
                    {
                        new PropertyDef { Name = "text", Type = PropertyType.String, Default = "标签", Label = "文本" },
                        new PropertyDef { Name = "x", Type = PropertyType.Number, Default = 0, Label = "X" },
                        new PropertyDef { Name = "y", Type = PropertyType.Number, Default = 0, Label = "Y" },
                        new PropertyDef { Name = "width", Type = PropertyType.Number, Default = 80, Label = "宽度" },
                        new PropertyDef { Name = "height", Type = PropertyType.Number, Default = 30, Label = "高度" }
                    }
                },
                new WidgetMeta
                {
                    Id = "panel",
                    Name = "面板",
                    Description = "容器面板控件",
                    Properties = new List<PropertyDef>
                    {
                        new PropertyDef { Name = "x", Type = PropertyType.Number, Default = 0, Label = "X" },
                        new PropertyDef { Name = "y", Type = PropertyType.Number, Default = 0, Label = "Y" },
                        new PropertyDef { Name = "width", Type = PropertyType.Number, Default = 200, Label = "宽度" },
                        new PropertyDef { Name = "height", Type = PropertyType.Number, Default = 150, Label = "高度" }
                    }
                },
                new WidgetMeta
                {
                    Id = "valve",
                    Name = "阀门",
                    Description = "旋转阀门控件",
                    Properties = new List<PropertyDef>
                    {
                        new PropertyDef { Name = "x", Type = PropertyType.Number, Default = 0, Label = "X" },
                        new PropertyDef { Name = "y", Type = PropertyType.Number, Default = 0, Label = "Y" },
                        new PropertyDef { Name = "size", Type = PropertyType.Number, Default = 100, Label = "尺寸" }
                    }
                },
                new WidgetMeta
                {
                    Id = "trend_chart",
                    Name = "趋势图",
                    Description = "数据趋势图表",
                    Properties = new List<PropertyDef>
                    {
                        new PropertyDef { Name = "x", Type = PropertyType.Number, Default = 0, Label = "X" },
                        new PropertyDef { Name = "y", Type = PropertyType.Number, Default = 0, Label = "Y" },
                        new PropertyDef { Name = "width", Type = PropertyType.Number, Default = 300, Label = "宽度" },
                        new PropertyDef { Name = "height", Type = PropertyType.Number, Default = 200, Label = "高度" }
                    }
                }
            };

            widgetMetaList.AddRange(defaultWidgets);

            // 为每个控件创建工具箱按钮
            foreach (var meta in widgetMetaList)
            {
                CreateToolboxButton(meta);
            }
        }

        /// <summary>
        /// 创建工具箱按钮
        /// </summary>
        static void CreateToolboxButton(WidgetMeta meta)
        {
            var btn = lv_btn_create(toolbox);
            lv_obj_set_size(btn, 125, 35);
            lv_obj_set_style_bg_color(btn, lv_color_hex(0xFFFFFF), 0);
            lv_obj_set_style_bg_color(btn, lv_color_hex(0xE3F2FD), LV_STATE_PRESSED);
            lv_obj_set_style_border_color(btn, lv_color_hex(0xCCCCCC), 0);
            lv_obj_set_style_border_width(btn, 1, 0);
            lv_obj_set_style_radius(btn, 4, 0);
            lv_obj_set_style_shadow_width(btn, 0, 0);

            var label = lv_label_create(btn);
            fixed (byte* utf8Ptr = Encoding.UTF8.GetBytes(meta.Name + "\0"))
                lv_label_set_text(label, utf8Ptr);
            lv_obj_center(label);
            lv_obj_set_style_text_color(label, lv_color_hex(0x333333), 0);
            ApplyDefaultFontStyle(label);

            // 存储按钮和标签到元数据映射
            toolButtonMetaMap[(nint)btn] = meta;
            toolButtonMetaMap[(nint)label] = meta;

            // 添加拖拽事件
            lv_obj_add_event(btn, &OnToolboxItemDrag, lv_event_code_t.LV_EVENT_ALL, null);
        }

        /// <summary>
        /// 工具箱项目拖拽事件处理
        /// </summary>
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static unsafe void OnToolboxItemDrag(lv_event_t* e)
        {
            var code = lv_event_get_code(e);
            var target = (lv_obj_t*)lv_event_get_target(e);
            lv_indev_t* indev = lv_indev_get_act();
            if (indev == null) return;

            lv_point_t p;
            lv_indev_get_point(indev, &p);

            // 获取按钮对应的元数据
            if (!toolButtonMetaMap.TryGetValue((nint)target, out var meta))
                return;

            if (code == lv_event_code_t.LV_EVENT_PRESSED)
            {
                // 开始从工具箱拖拽
                isDraggingFromToolbox = true;
                currentDraggingMeta = meta;

                // 创建预览对象
                previewObj = lv_obj_create(lv_layer_top());
                lv_obj_set_size(previewObj, 80, 30);
                lv_obj_set_pos(previewObj, p.x - 40, p.y - 15);
                lv_obj_set_style_bg_color(previewObj, lv_color_hex(0x2196F3), 0);
                lv_obj_set_style_bg_opa(previewObj, 180, 0);
                lv_obj_set_style_radius(previewObj, 4, 0);
                lv_obj_set_style_border_width(previewObj, 0, 0);
                lv_obj_remove_flag(previewObj, LV_OBJ_FLAG_CLICKABLE);
                lv_obj_remove_flag(previewObj, LV_OBJ_FLAG_SCROLLABLE);

                var previewLabel = lv_label_create(previewObj);
                fixed (byte* utf8Ptr = Encoding.UTF8.GetBytes(meta.Name + "\0"))
                    lv_label_set_text(previewLabel, utf8Ptr);
                lv_obj_center(previewLabel);
                lv_obj_set_style_text_color(previewLabel, lv_color_hex(0xFFFFFF), 0);
                ApplyDefaultFontStyle(previewLabel);
            }
            else if (code == lv_event_code_t.LV_EVENT_PRESSING)
            {
                // 拖拽中，更新预览位置
                if (isDraggingFromToolbox && previewObj != null)
                {
                    lv_obj_set_pos(previewObj, p.x - 40, p.y - 15);
                }
            }
            else if (code == lv_event_code_t.LV_EVENT_RELEASED)
            {
                // 释放时，检查是否在画布区域内
                if (isDraggingFromToolbox && currentDraggingMeta != null)
                {
                    // 删除预览对象
                    if (previewObj != null)
                    {
                        lv_obj_del(previewObj);
                        previewObj = null;
                    }

                    // 检查是否在画布区域内
                    lv_area_t canvasCoords;
                    lv_obj_get_coords(canvasArea, &canvasCoords);

                    if (p.x >= canvasCoords.x1 && p.x <= canvasCoords.x2 &&
                        p.y >= canvasCoords.y1 && p.y <= canvasCoords.y2)
                    {
                        // 计算相对于画布的坐标
                        int relX = p.x - canvasCoords.x1 - 40;
                        int relY = p.y - canvasCoords.y1 - 15;
                        if (relX < 0) relX = 10;
                        if (relY < 0) relY = 10;

                        // 在画布上创建控件
                        var widget = CreateCanvasWidget(currentDraggingMeta, relX, relY);
                        SelectWidget(widget);

                        Console.WriteLine($"在画布上创建控件: {currentDraggingMeta.Name} at ({relX}, {relY})");
                    }

                    isDraggingFromToolbox = false;
                    currentDraggingMeta = null;
                }
            }
        }

        /// <summary>
        /// 工具箱标题栏拖拽事件处理
        /// </summary>
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static unsafe void OnToolboxHeaderDrag(lv_event_t* e)
        {
            var code = lv_event_get_code(e);
            lv_indev_t* indev = lv_indev_get_act();
            if (indev == null) return;

            lv_point_t p;
            lv_indev_get_point(indev, &p);

            if (code == lv_event_code_t.LV_EVENT_PRESSED)
            {
                // 开始拖拽，记录鼠标相对于面板左上角的偏移
                isDraggingToolbox = true;
                int panelX = lv_obj_get_x(toolbox);
                int panelY = lv_obj_get_y(toolbox);
                toolboxDragOffset = (p.x - panelX, p.y - panelY);
            }
            else if (code == lv_event_code_t.LV_EVENT_PRESSING)
            {
                // 拖拽中，更新面板位置
                if (isDraggingToolbox)
                {
                    int newX = p.x - toolboxDragOffset.offX;
                    int newY = p.y - toolboxDragOffset.offY;

                    // 限制在屏幕范围内
                    int panelW = lv_obj_get_width(toolbox);
                    int panelH = lv_obj_get_height(toolbox);
                    if (newX < 0) newX = 0;
                    if (newY < 0) newY = 0;
                    if (newX > mianWindowsSize.Width - panelW) newX = mianWindowsSize.Width - panelW;
                    if (newY > mianWindowsSize.Height - panelH) newY = mianWindowsSize.Height - panelH;

                    lv_obj_set_pos(toolbox, newX, newY);
                }
            }
            else if (code == lv_event_code_t.LV_EVENT_RELEASED)
            {
                // 结束拖拽
                isDraggingToolbox = false;
            }
        }

        /// <summary>
        /// 切换工具箱显示/隐藏
        /// </summary>
        static void ToggleToolbox()
        {
            toolboxVisible = !toolboxVisible;
            if (toolboxVisible)
            {
                lv_obj_remove_flag(toolbox, LV_OBJ_FLAG_HIDDEN);
            }
            else
            {
                // 调整到默认位置
                lv_obj_set_pos(toolbox, 5, MENU_BAR_HEIGHT + 50);
                lv_obj_add_flag(toolbox, LV_OBJ_FLAG_HIDDEN);
            }
        }
    }
}
