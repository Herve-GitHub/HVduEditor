using LVGLSharp;
using LVGLSharp.Interop;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace VduEditor
{
    /// <summary>
    /// 画布上的控件实例
    /// </summary>
    unsafe class WidgetInstance
    {
        public lv_obj_t* Obj { get; set; }
        public WidgetMeta Meta { get; set; }
        public Dictionary<string, object> Values { get; set; } = new();
        public string Description { get; set; } = "";

        public WidgetInstance(lv_obj_t* obj, WidgetMeta meta)
        {
            Obj = obj;
            Meta = meta;
            Description = meta.Description;
            foreach (var prop in meta.Properties)
            {
                if (prop.Default != null)
                    Values[prop.Name] = prop.Default;
            }
        }
    }

    /// <summary>
    /// 画布区域管理
    /// 负责画布的创建、控件的放置、选择、拖拽等功能
    /// </summary>
    unsafe partial class Program
    {
        // 画布对象
        static lv_obj_t* canvasArea;

        // 当前选中的控件
        static WidgetInstance? selectedWidget = null;

        // 画布上控件的拖拽偏移
        static Dictionary<nint, (int offX, int offY)> dragOffsets = new();

        // 控件实例字典
        static Dictionary<nint, WidgetInstance> widgetInstances = new();

        /// <summary>
        /// 创建画布区域
        /// </summary>
        static void CreateCanvasArea()
        {
            canvasArea = lv_obj_create(root);
            lv_obj_set_size(canvasArea, 1010, 890);  // 减小高度，为菜单栏腾出空间
            lv_obj_set_pos(canvasArea, 5, MENU_BAR_HEIGHT + 5);  // 菜单栏下方
            lv_obj_set_style_bg_color(canvasArea, lv_color_hex(0xFFFFFF), 0);
            lv_obj_set_style_border_color(canvasArea, lv_color_hex(0x888888), 0);
            lv_obj_set_style_border_width(canvasArea, 2, 0);
            lv_obj_set_scrollbar_mode(canvasArea, LV_SCROLLBAR_MODE_OFF);
            lv_obj_remove_flag(canvasArea, LV_OBJ_FLAG_SCROLLABLE);

            var canvasLabel = lv_label_create(canvasArea);
            fixed (byte* utf8Ptr = Encoding.UTF8.GetBytes("画布\0"))
                lv_label_set_text(canvasLabel, utf8Ptr);
            lv_obj_set_pos(canvasLabel, 5, 5);
            lv_obj_set_style_text_color(canvasLabel, lv_color_hex(0xCCCCCC), 0);
            ApplyDefaultFontStyle(canvasLabel);

            // 添加画布点击事件处理器（取消选中）
            lv_obj_add_event(canvasArea, &CanvasClickHandler, lv_event_code_t.LV_EVENT_CLICKED, null);
        }

        /// <summary>
        /// 画布点击事件处理
        /// 点击画布空白区域时取消选中
        /// </summary>
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static unsafe void CanvasClickHandler(lv_event_t* e)
        {
            var target = lv_event_get_target(e);
            if (target == canvasArea) SelectWidget(null);
        }

        /// <summary>
        /// 选中控件
        /// </summary>
        /// <param name="widget">要选中的控件，传入 null 表示取消选中</param>
        static void SelectWidget(WidgetInstance? widget)
        {
            // 取消之前选中控件的高亮
            if (selectedWidget != null)
                lv_obj_set_style_outline_width(selectedWidget.Obj, 0, 0);

            selectedWidget = widget;

            if (widget != null)
            {
                // 添加选中高亮
                lv_obj_set_style_outline_width(widget.Obj, 2, 0);
                lv_obj_set_style_outline_color(widget.Obj, lv_color_hex(0x2196F3), 0);
                // 同步控件属性到属性面板
                SyncWidgetToProperties(widget);
            }
        }

        /// <summary>
        /// 同步控件当前状态到属性值字典
        /// </summary>
        static void SyncWidgetToProperties(WidgetInstance widget)
        {
            widget.Values["x"] = lv_obj_get_x(widget.Obj);
            widget.Values["y"] = lv_obj_get_y(widget.Obj);
            widget.Values["width"] = lv_obj_get_width(widget.Obj);
            widget.Values["height"] = lv_obj_get_height(widget.Obj);
        }

        /// <summary>
        /// 在画布上创建控件
        /// </summary>
        /// <param name="meta">控件元数据</param>
        /// <param name="x">X坐标</param>
        /// <param name="y">Y坐标</param>
        /// <returns>创建的控件实例</returns>
        static WidgetInstance CreateCanvasWidget(WidgetMeta meta, int x, int y)
        {
            lv_obj_t* obj;
            bool isButton = meta.Id.ToLower().Contains("button");

            if (isButton)
            {
                obj = lv_btn_create(canvasArea);
                var defaultWidth = Convert.ToInt32(meta.Properties.FirstOrDefault(p => p.Name == "width")?.Default ?? 100);
                var defaultHeight = Convert.ToInt32(meta.Properties.FirstOrDefault(p => p.Name == "height")?.Default ?? 40);
                lv_obj_set_size(obj, defaultWidth, defaultHeight);

                var label = lv_label_create(obj);
                var labelText = (meta.Properties.FirstOrDefault(p => p.Name == "label")?.Default?.ToString() ?? meta.Name) + "\0";
                fixed (byte* utf8Ptr = Encoding.UTF8.GetBytes(labelText))
                    lv_label_set_text(label, utf8Ptr);
                lv_obj_center(label);
                ApplyDefaultFontStyle(label);
            }
            else
            {
                var defaultSize = Convert.ToInt32(meta.Properties.FirstOrDefault(p => p.Name == "size")?.Default ?? 100);
                var defaultWidth = Convert.ToInt32(meta.Properties.FirstOrDefault(p => p.Name == "width")?.Default ?? defaultSize);
                var defaultHeight = Convert.ToInt32(meta.Properties.FirstOrDefault(p => p.Name == "height")?.Default ?? defaultSize);

                obj = lv_obj_create(canvasArea);
                lv_obj_set_size(obj, defaultWidth, defaultHeight);
                lv_obj_set_scrollbar_mode(obj, LV_SCROLLBAR_MODE_OFF);
                lv_obj_remove_flag(obj, LV_OBJ_FLAG_SCROLLABLE);

                uint bgColor = meta.Id switch
                {
                    "valve" => 0xFF5722,
                    "trend_chart" => 0x2196F3,
                    _ => 0xE0E0E0
                };
                lv_obj_set_style_bg_color(obj, lv_color_hex(bgColor), 0);
                lv_obj_set_style_radius(obj, 8, 0);

                var label = lv_label_create(obj);
                fixed (byte* utf8Ptr = Encoding.UTF8.GetBytes(meta.Name + "\0"))
                    lv_label_set_text(label, utf8Ptr);
                lv_obj_center(label);
                lv_obj_set_style_text_color(label, lv_color_hex(0xFFFFFF), 0);
                ApplyDefaultFontStyle(label);
            }

            lv_obj_set_pos(obj, x, y);

            // 创建控件实例并添加到字典
            var widget = new WidgetInstance(obj, meta);
            widget.Values["x"] = x;
            widget.Values["y"] = y;
            widgetInstances[(nint)obj] = widget;

            // 添加拖拽事件
            lv_obj_add_event(obj, &CanvasObjDragHandler, lv_event_code_t.LV_EVENT_ALL, null);

            return widget;
        }

        /// <summary>
        /// 画布上控件的拖拽事件处理
        /// </summary>
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static unsafe void CanvasObjDragHandler(lv_event_t* e)
        {
            var code = lv_event_get_code(e);
            var obj = (lv_obj_t*)lv_event_get_target(e);
            lv_indev_t* indev = lv_indev_get_act();
            if (indev == null) return;
            lv_point_t p; lv_indev_get_point(indev, &p);

            if (code == lv_event_code_t.LV_EVENT_PRESSED)
            {
                // 开始拖拽，记录偏移
                int objX = lv_obj_get_x(obj);
                int objY = lv_obj_get_y(obj);
                lv_area_t ca; lv_obj_get_coords(canvasArea, &ca);
                int mouseRelX = p.x - ca.x1;
                int mouseRelY = p.y - ca.y1;
                dragOffsets[(nint)obj] = (mouseRelX - objX, mouseRelY - objY);

                // 选中该控件
                if (widgetInstances.TryGetValue((nint)obj, out var widget))
                    SelectWidget(widget);
            }
            else if (code == lv_event_code_t.LV_EVENT_PRESSING)
            {
                // 拖拽中，更新位置
                if (dragOffsets.TryGetValue((nint)obj, out var off))
                {
                    lv_area_t ca; lv_obj_get_coords(canvasArea, &ca);
                    int relX = p.x - ca.x1 - off.offX;
                    int relY = p.y - ca.y1 - off.offY;

                    // 限制在画布范围内
                    if (relX < 0) relX = 0;
                    if (relY < 0) relY = 0;
                    int objW = lv_obj_get_width(obj);
                    int objH = lv_obj_get_height(obj);
                    int canvasW = lv_obj_get_width(canvasArea);
                    int canvasH = lv_obj_get_height(canvasArea);
                    if (relX > canvasW - objW - 10) relX = canvasW - objW - 10;
                    if (relY > canvasH - objH - 10) relY = canvasH - objH - 10;

                    lv_obj_set_pos(obj, relX, relY);
                }
            }
            else if (code == lv_event_code_t.LV_EVENT_RELEASED)
            {
                // 拖拽结束，更新属性面板
                dragOffsets.Remove((nint)obj);
                if (selectedWidget != null && selectedWidget.Obj == obj)
                {
                    SyncWidgetToProperties(selectedWidget);
                }
            }
        }

        /// <summary>
        /// 将属性值应用到画布上的控件
        /// </summary>
        /// <param name="widget">要更新的控件实例</param>
        /// <param name="propName">属性名称</param>
        /// <param name="value">新的属性值</param>
        static void ApplyPropertyToWidget(WidgetInstance widget, string propName, object value)
        {
            switch (propName)
            {
                // ===== 位置属性 =====
                case "x":
                    lv_obj_set_x(widget.Obj, Convert.ToInt32(value));
                    break;
                case "y":
                    lv_obj_set_y(widget.Obj, Convert.ToInt32(value));
                    break;

                // ===== 尺寸属性 =====
                case "width":
                    lv_obj_set_width(widget.Obj, Convert.ToInt32(value));
                    break;
                case "height":
                    lv_obj_set_height(widget.Obj, Convert.ToInt32(value));
                    break;
                case "size":
                    var size = Convert.ToInt32(value);
                    lv_obj_set_size(widget.Obj, size, size);
                    break;

                // ===== 颜色属性 =====
                case "bg_color":
                case "background_color":
                case "color":
                    var bgColor = ParseHexColor(value?.ToString() ?? "#FFFFFF");
                    lv_obj_set_style_bg_color(widget.Obj, bgColor, 0);
                    break;
                case "text_color":
                case "font_color":
                    var textColor = ParseHexColor(value?.ToString() ?? "#000000");
                    lv_obj_set_style_text_color(widget.Obj, textColor, 0);
                    UpdateChildLabelsColor(widget.Obj, textColor);
                    break;
                case "border_color":
                    var borderColor = ParseHexColor(value?.ToString() ?? "#CCCCCC");
                    lv_obj_set_style_border_color(widget.Obj, borderColor, 0);
                    break;

                // ===== 边框属性 =====
                case "border_width":
                    lv_obj_set_style_border_width(widget.Obj, Convert.ToInt32(value), 0);
                    break;
                case "radius":
                case "border_radius":
                    lv_obj_set_style_radius(widget.Obj, Convert.ToInt32(value), 0);
                    break;

                // ===== 透明度属性 =====
                case "opacity":
                case "opa":
                    lv_obj_set_style_opa(widget.Obj, (byte)Convert.ToInt32(value), 0);
                    break;
                case "bg_opacity":
                case "bg_opa":
                    lv_obj_set_style_bg_opa(widget.Obj, (byte)Convert.ToInt32(value), 0);
                    break;

                // ===== 内边距属性 =====
                case "padding":
                case "pad":
                    var pad = Convert.ToInt32(value);
                    lv_obj_set_style_pad_all(widget.Obj, pad, 0);
                    break;
                case "pad_top":
                    lv_obj_set_style_pad_top(widget.Obj, Convert.ToInt32(value), 0);
                    break;
                case "pad_bottom":
                    lv_obj_set_style_pad_bottom(widget.Obj, Convert.ToInt32(value), 0);
                    break;
                case "pad_left":
                    lv_obj_set_style_pad_left(widget.Obj, Convert.ToInt32(value), 0);
                    break;
                case "pad_right":
                    lv_obj_set_style_pad_right(widget.Obj, Convert.ToInt32(value), 0);
                    break;

                // ===== 文本/标签属性 =====
                case "label":
                case "text":
                    UpdateWidgetLabel(widget.Obj, value?.ToString() ?? "");
                    break;

                // ===== 可见性属性 =====
                case "visible":
                    if (Convert.ToBoolean(value))
                        lv_obj_remove_flag(widget.Obj, LV_OBJ_FLAG_HIDDEN);
                    else
                        lv_obj_add_flag(widget.Obj, LV_OBJ_FLAG_HIDDEN);
                    break;

                // ===== 交互/点击属性 =====
                case "enabled":
                case "clickable":
                    if (Convert.ToBoolean(value))
                        lv_obj_add_flag(widget.Obj, LV_OBJ_FLAG_CLICKABLE);
                    else
                        lv_obj_remove_flag(widget.Obj, LV_OBJ_FLAG_CLICKABLE);
                    break;

                default:
                    Console.WriteLine($"未处理的属性: {propName} = {value}");
                    break;
            }
        }

        /// <summary>
        /// 更新控件内部文本标签文本
        /// </summary>
        static void UpdateWidgetLabel(lv_obj_t* obj, string text)
        {
            var childCount = lv_obj_get_child_count(obj);
            for (int i = 0; i < childCount; i++)
            {
                var child = lv_obj_get_child(obj, i);
                if (child != null)
                {
                    fixed (byte* utf8Ptr = Encoding.UTF8.GetBytes(text + "\0"))
                    {
                        lv_label_set_text(child, utf8Ptr);
                    }
                    return;
                }
            }
        }

        /// <summary>
        /// 更新控件的所有子标签的文本颜色
        /// </summary>
        static void UpdateChildLabelsColor(lv_obj_t* obj, lv_color_t color)
        {
            var childCount = lv_obj_get_child_count(obj);
            for (int i = 0; i < childCount; i++)
            {
                var child = lv_obj_get_child(obj, i);
                if (child != null)
                {
                    lv_obj_set_style_text_color(child, color, 0);
                }
            }
        }

        /// <summary>
        /// 解析十六进制颜色字符串为 LVGL 颜色
        /// </summary>
        static lv_color_t ParseHexColor(string hex)
        {
            try
            {
                hex = hex.TrimStart('#');
                if (hex.Length == 3)
                {
                    hex = $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}";
                }
                if (hex.Length == 6)
                {
                    var r = Convert.ToByte(hex.Substring(0, 2), 16);
                    var g = Convert.ToByte(hex.Substring(2, 2), 16);
                    var b = Convert.ToByte(hex.Substring(4, 2), 16);
                    return lv_color_make(r, g, b);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"颜色解析失败: {hex}, 错误: {ex.Message}");
            }
            return lv_color_hex(0xFFFFFF);
        }
    }
}
