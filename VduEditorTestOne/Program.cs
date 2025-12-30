using LVGLSharp;
using LVGLSharp.Interop;
using LVGLSharp.Runtime.Windows;
using NLua;
using System.Runtime.InteropServices;
using System.Text;
using System.Collections.Concurrent;

namespace VduEditorTestOne
{
    internal unsafe class Program
    {
        static Win32Window? _window;
        static Lua? _lua;
        static List<EventCallbackData> _eventCallbacks = new();
        static List<TimerCallbackData> _timerCallbacks = new();
        static lv_style_t* defaultFontStyle = null;
        
        // 用于延迟处理事件的队列
        static ConcurrentQueue<Action> _pendingActions = new();
        
        static void Main(string[] args)
        {
            // 设置控制台输出编码为UTF-8，解决中文乱码问题
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;

            if (args.Length == 0)
            {
                Console.WriteLine("Usage: VduEditorTestOne <lua_script_path>");
                Console.WriteLine("Example: VduEditorTestOne lua/demo_button.lua");
                return;
            }

            string scriptPath = args[0];
            if (!File.Exists(scriptPath))
            {
                Console.WriteLine($"Error: Lua script not found: {scriptPath}");
                return;
            }

            // Initialize window
            _window = new Win32Window("LVGL Lua Demo", 1024, 810);
            _window.Init();
            var font = lv_obj_get_style_text_font(Win32Window.root, LV_PART_MAIN);
            defaultFontStyle = (lv_style_t*)NativeMemory.Alloc((nuint)sizeof(lv_style_t));
            NativeMemory.Clear(defaultFontStyle, (nuint)sizeof(lv_style_t));
            lv_style_init(defaultFontStyle);
            lv_style_set_text_font(defaultFontStyle, font);
            // Initialize Lua with LVGL bindings
            _lua = new Lua();
            _lua.State.Encoding = Encoding.UTF8;

            // Set up Lua package path for require
            string scriptDir = Path.GetDirectoryName(Path.GetFullPath(scriptPath)) ?? ".";
            string baseDir = Path.GetDirectoryName(scriptDir) ?? scriptDir;
            _lua.DoString($@"
                package.path = '{baseDir.Replace("\\", "/")}/?.lua;' .. package.path
                package.path = '{scriptDir.Replace("\\", "/")}/?.lua;' .. package.path
            ");

            // Register LVGL module that can be required
            RegisterLvglModule(_lua);

            // Run the Lua script
            try
            {
                _lua.DoFile(scriptPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lua Error: {ex.Message}");
            }

            // Start the main loop - 处理延迟事件
            _window.StartLoop(() => 
            { 
                ProcessPendingActions();
            });

            // Cleanup
            _lua.Dispose();
        }
        
        /// <summary>
        /// 在主循环中安全地处理延迟的事件回调
        /// </summary>
        static void ProcessPendingActions()
        {
            while (_pendingActions.TryDequeue(out var action))
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing pending action: {ex.Message}");
                }
            }
        }

        static void RegisterLvglModule(Lua lua)
        {
            // Create the lvgl module table
            lua.NewTable("_lvgl_module");

            // ========== EVENT constants ==========
            lua["_lvgl_module.EVENT_CLICKED"] = (int)LV_EVENT_CLICKED;
            lua["_lvgl_module.EVENT_SINGLE_CLICKED"] = (int)LV_EVENT_SINGLE_CLICKED;
            lua["_lvgl_module.EVENT_DOUBLE_CLICKED"] = (int)LV_EVENT_DOUBLE_CLICKED;
            lua["_lvgl_module.EVENT_PRESSED"] = (int)LV_EVENT_PRESSED;
            lua["_lvgl_module.EVENT_RELEASED"] = (int)LV_EVENT_RELEASED;
            lua["_lvgl_module.EVENT_LONG_PRESSED"] = (int)LV_EVENT_LONG_PRESSED;
            lua["_lvgl_module.EVENT_LONG_PRESSED_REPEAT"] = (int)LV_EVENT_LONG_PRESSED_REPEAT;
            lua["_lvgl_module.EVENT_VALUE_CHANGED"] = (int)LV_EVENT_VALUE_CHANGED;
            lua["_lvgl_module.EVENT_PRESSING"] = (int)LV_EVENT_PRESSING;
            lua["_lvgl_module.EVENT_SCROLL"] = (int)LV_EVENT_SCROLL;
            lua["_lvgl_module.EVENT_SCROLL_BEGIN"] = (int)LV_EVENT_SCROLL_BEGIN;
            lua["_lvgl_module.EVENT_SCROLL_END"] = (int)LV_EVENT_SCROLL_END;
            lua["_lvgl_module.EVENT_FOCUSED"] = (int)LV_EVENT_FOCUSED;
            lua["_lvgl_module.EVENT_DEFOCUSED"] = (int)LV_EVENT_DEFOCUSED;
            lua["_lvgl_module.EVENT_DRAW_MAIN"] = (int)LV_EVENT_DRAW_MAIN;

            // ========== ALIGN constants ==========
            lua["_lvgl_module.ALIGN_DEFAULT"] = (int)LV_ALIGN_DEFAULT;
            lua["_lvgl_module.ALIGN_TOP_LEFT"] = (int)LV_ALIGN_TOP_LEFT;
            lua["_lvgl_module.ALIGN_TOP_MID"] = (int)LV_ALIGN_TOP_MID;
            lua["_lvgl_module.ALIGN_TOP_RIGHT"] = (int)LV_ALIGN_TOP_RIGHT;
            lua["_lvgl_module.ALIGN_BOTTOM_LEFT"] = (int)LV_ALIGN_BOTTOM_LEFT;
            lua["_lvgl_module.ALIGN_BOTTOM_MID"] = (int)LV_ALIGN_BOTTOM_MID;
            lua["_lvgl_module.ALIGN_BOTTOM_RIGHT"] = (int)LV_ALIGN_BOTTOM_RIGHT;
            lua["_lvgl_module.ALIGN_LEFT_MID"] = (int)LV_ALIGN_LEFT_MID;
            lua["_lvgl_module.ALIGN_RIGHT_MID"] = (int)LV_ALIGN_RIGHT_MID;
            lua["_lvgl_module.ALIGN_CENTER"] = (int)LV_ALIGN_CENTER;
            lua["_lvgl_module.ALIGN_OUT_TOP_LEFT"] = (int)LV_ALIGN_OUT_TOP_LEFT;
            lua["_lvgl_module.ALIGN_OUT_TOP_MID"] = (int)LV_ALIGN_OUT_TOP_MID;
            lua["_lvgl_module.ALIGN_OUT_TOP_RIGHT"] = (int)LV_ALIGN_OUT_TOP_RIGHT;
            lua["_lvgl_module.ALIGN_OUT_BOTTOM_LEFT"] = (int)LV_ALIGN_OUT_BOTTOM_LEFT;
            lua["_lvgl_module.ALIGN_OUT_BOTTOM_MID"] = (int)LV_ALIGN_OUT_BOTTOM_MID;
            lua["_lvgl_module.ALIGN_OUT_BOTTOM_RIGHT"] = (int)LV_ALIGN_OUT_BOTTOM_RIGHT;
            lua["_lvgl_module.ALIGN_OUT_LEFT_TOP"] = (int)LV_ALIGN_OUT_LEFT_TOP;
            lua["_lvgl_module.ALIGN_OUT_LEFT_MID"] = (int)LV_ALIGN_OUT_LEFT_MID;
            lua["_lvgl_module.ALIGN_OUT_LEFT_BOTTOM"] = (int)LV_ALIGN_OUT_LEFT_BOTTOM;
            lua["_lvgl_module.ALIGN_OUT_RIGHT_TOP"] = (int)LV_ALIGN_OUT_RIGHT_TOP;
            lua["_lvgl_module.ALIGN_OUT_RIGHT_MID"] = (int)LV_ALIGN_OUT_RIGHT_MID;
            lua["_lvgl_module.ALIGN_OUT_RIGHT_BOTTOM"] = (int)LV_ALIGN_OUT_RIGHT_BOTTOM;

            // ========== TEXT ALIGN constants ==========
            lua["_lvgl_module.TEXT_ALIGN_LEFT"] = (int)lv_text_align_t.LV_TEXT_ALIGN_LEFT;
            lua["_lvgl_module.TEXT_ALIGN_CENTER"] = (int)lv_text_align_t.LV_TEXT_ALIGN_CENTER;
            lua["_lvgl_module.TEXT_ALIGN_RIGHT"] = (int)lv_text_align_t.LV_TEXT_ALIGN_RIGHT;
            lua["_lvgl_module.TEXT_ALIGN_AUTO"] = (int)lv_text_align_t.LV_TEXT_ALIGN_AUTO;

            // ========== OBJ FLAG constants ==========
            lua["_lvgl_module.OBJ_FLAG_SCROLLABLE"] = (int)LV_OBJ_FLAG_SCROLLABLE;
            lua["_lvgl_module.OBJ_FLAG_CLICKABLE"] = (int)LV_OBJ_FLAG_CLICKABLE;
            lua["_lvgl_module.OBJ_FLAG_HIDDEN"] = (int)LV_OBJ_FLAG_HIDDEN;
            lua["_lvgl_module.OBJ_FLAG_CHECKABLE"] = (int)LV_OBJ_FLAG_CHECKABLE;
            lua["_lvgl_module.OBJ_FLAG_PRESS_LOCK"] = (int)LV_OBJ_FLAG_PRESS_LOCK;
            lua["_lvgl_module.OBJ_FLAG_GESTURE_BUBBLE"] = (int)LV_OBJ_FLAG_GESTURE_BUBBLE;
            lua["_lvgl_module.OBJ_FLAG_SNAPPABLE"] = (int)LV_OBJ_FLAG_SNAPPABLE;
            lua["_lvgl_module.OBJ_FLAG_SCROLL_ON_FOCUS"] = (int)LV_OBJ_FLAG_SCROLL_ON_FOCUS;

            // ========== LAYOUT constants ==========
            lua["_lvgl_module.LAYOUT_NONE"] = (int)lv_layout_t.LV_LAYOUT_NONE;
            lua["_lvgl_module.LAYOUT_FLEX"] = (int)lv_layout_t.LV_LAYOUT_FLEX;
            lua["_lvgl_module.LAYOUT_GRID"] = (int)lv_layout_t.LV_LAYOUT_GRID;

            // ========== Other constants ==========
            lua["_lvgl_module.RADIUS_CIRCLE"] = 0x7FFF; // LV_RADIUS_CIRCLE

            // ========== Register object create functions ==========
            lua.RegisterFunction("_lvgl_module.scr_act", typeof(Program).GetMethod(nameof(LuaGetScreen)));
            lua.RegisterFunction("_lvgl_module.btn_create", typeof(Program).GetMethod(nameof(LuaBtnCreate)));
            lua.RegisterFunction("_lvgl_module.label_create", typeof(Program).GetMethod(nameof(LuaLabelCreate)));
            lua.RegisterFunction("_lvgl_module.obj_create", typeof(Program).GetMethod(nameof(LuaObjCreate)));
            lua.RegisterFunction("_lvgl_module.chart_create", typeof(Program).GetMethod(nameof(LuaChartCreate)));
            lua.RegisterFunction("_lvgl_module.slider_create", typeof(Program).GetMethod(nameof(LuaSliderCreate)));

            // ========== Register function-style API (for widget_template.lua compatibility) ==========
            lua.RegisterFunction("_lvgl_module.obj_set_size", typeof(Program).GetMethod(nameof(LuaObjSetSize)));
            lua.RegisterFunction("_lvgl_module.obj_set_pos", typeof(Program).GetMethod(nameof(LuaObjSetPos)));
            lua.RegisterFunction("_lvgl_module.obj_set_style_bg_color", typeof(Program).GetMethod(nameof(LuaObjSetStyleBgColor)));

            // ========== Event and timer functions ==========
            lua.RegisterFunction("_lvgl_module.obj_add_event_cb", typeof(Program).GetMethod(nameof(LuaObjAddEventCb)));
            lua.RegisterFunction("_lvgl_module.timer_create", typeof(Program).GetMethod(nameof(LuaTimerCreate)));
            lua.RegisterFunction("_lvgl_module.timer_delete", typeof(Program).GetMethod(nameof(LuaTimerDelete)));
            lua.RegisterFunction("_lvgl_module.event_get_code", typeof(Program).GetMethod(nameof(LuaEventGetCode)));

            // ========== Utility functions ==========
            lua.RegisterFunction("_lvgl_module.pct", typeof(Program).GetMethod(nameof(LuaPct)));
            lua.RegisterFunction("_lvgl_module.get_mouse_x", typeof(Program).GetMethod(nameof(LuaGetMouseX)));
            lua.RegisterFunction("_lvgl_module.get_mouse_y", typeof(Program).GetMethod(nameof(LuaGetMouseY)));

            // 重写Lua的print函数，确保UTF-8编码正确输出
            lua.DoString(@"
                local original_print = print
                print = function(...)
                    local args = {...}
                    local str_args = {}
                    for i, v in ipairs(args) do
                        str_args[i] = tostring(v)
                    end
                    original_print(table.concat(str_args, '\t'))
                end
            ");

            // Replace the preloaded module with our actual implementation
            lua.DoString(@"
                package.preload['lvgl'] = function()
                    return _lvgl_module
                end
                package.loaded['lvgl'] = _lvgl_module
            ");
        }

        // ========== Create functions ==========

        public static LvObjWrapper LuaGetScreen()
        {
            return new LvObjWrapper(Win32Window.root);
        }

        public static LvObjWrapper LuaBtnCreate(LvObjWrapper parent)
        {
            lv_obj_t* btn = lv_button_create(parent.Ptr);
            return new LvObjWrapper(btn);
        }

        public static LvObjWrapper LuaLabelCreate(LvObjWrapper parent)
        {
            lv_obj_t* label = lv_label_create(parent.Ptr);
            return new LvObjWrapper(label);
        }

        public static LvObjWrapper LuaObjCreate(LvObjWrapper parent)
        {
            lv_obj_t* obj = lv_obj_create(parent.Ptr);
            return new LvObjWrapper(obj);
        }

        public static LvChartWrapper LuaChartCreate(LvObjWrapper parent)
        {
            lv_obj_t* chart = lv_chart_create(parent.Ptr);
            return new LvChartWrapper(chart);
        }

        public static LvObjWrapper LuaSliderCreate(LvObjWrapper parent)
        {
            lv_obj_t* slider = lv_slider_create(parent.Ptr);
            return new LvObjWrapper(slider);
        }

        // ========== Function-style API (lv.obj_set_size, lv.obj_set_pos, etc.) ==========

        public static void LuaObjSetSize(LvObjWrapper obj, int width, int height)
        {
            obj.set_size(width, height);
        }

        public static void LuaObjSetPos(LvObjWrapper obj, int x, int y)
        {
            obj.set_pos(x, y);
        }

        public static void LuaObjSetStyleBgColor(LvObjWrapper obj, int color, int selector)
        {
            obj.set_style_bg_color(color, selector);
        }

        // ========== Utility functions ==========

        public static int LuaPct(int value)
        {
            return lv_pct(value);
        }

        /// <summary>
        /// 获取当前鼠标X坐标
        /// </summary>
        public static int LuaGetMouseX()
        {
            return Win32Window.MouseX;
        }

        /// <summary>
        /// 获取当前鼠标Y坐标
        /// </summary>
        public static int LuaGetMouseY()
        {
            return Win32Window.MouseY;
        }
        // ========== Timer functions ==========

        public static LvTimerWrapper LuaTimerCreate(LuaFunction callback, int periodMs)
        {
            var timerData = new TimerCallbackData { Callback = callback };
            _timerCallbacks.Add(timerData);
            GCHandle handle = GCHandle.Alloc(timerData);

            lv_timer_t* timer = lv_timer_create(&LuaTimerHandler, (uint)periodMs, (void*)GCHandle.ToIntPtr(handle));
            timerData.Timer = timer;
            return new LvTimerWrapper(timer, handle);
        }

        public static void LuaTimerDelete(LvTimerWrapper timerWrapper)
        {
            if (timerWrapper != null && timerWrapper.Ptr != null)
            {
                lv_timer_delete(timerWrapper.Ptr);
                if (timerWrapper.Handle.IsAllocated)
                {
                    var data = timerWrapper.Handle.Target as TimerCallbackData;
                    if (data != null)
                    {
                        _timerCallbacks.Remove(data);
                    }
                    timerWrapper.Handle.Free();
                }
            }
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        static void LuaTimerHandler(lv_timer_t* timer)
        {
            void* userData = lv_timer_get_user_data(timer);
            if (userData == null) return;

            GCHandle handle = GCHandle.FromIntPtr((IntPtr)userData);
            if (handle.Target is TimerCallbackData data)
            {
                // 将回调延迟到主循环中执行，记录日志以便排查阻塞
                var cb = data.Callback;
                _pendingActions.Enqueue(() =>
                {
                    Console.WriteLine($"[PendingAction][Timer] Start callback: {cb}");
                    try
                    {
                        cb.Call();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Lua timer callback error: {ex.Message}");
                    }
                    Console.WriteLine($"[PendingAction][Timer] End callback: {cb}");
                });
            }
        }

        // ========== Event functions ==========

        public static void LuaObjAddEventCb(LvObjWrapper obj, LuaFunction callback, int eventCode, object? userData)
        {
            var callbackData = new EventCallbackData 
            { 
                Callback = callback, 
                UserData = userData,
                ObjWrapper = obj
            };
            _eventCallbacks.Add(callbackData);
            GCHandle handle = GCHandle.Alloc(callbackData);

            lv_obj_add_event_cb(obj.Ptr, &LuaEventHandler, (lv_event_code_t)eventCode, (void*)GCHandle.ToIntPtr(handle));
        }

        public static int LuaEventGetCode(LvEventData eventData)
        {
            return eventData.get_code();
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        static void LuaEventHandler(lv_event_t* e)
        {
            void* userData = lv_event_get_user_data(e);
            if (userData == null) return;

            GCHandle handle = GCHandle.FromIntPtr((IntPtr)userData);
            if (handle.Target is EventCallbackData data)
            {
                // 捕获事件数据，延迟到主循环中安全执行
                int eventCode = (int)lv_event_get_code(e);
                IntPtr targetPtr = (IntPtr)lv_event_get_target(e);

                // Capture callback reference to avoid closure over 'data' which may be GC'd
                var cb = data.Callback;
                var user = data.UserData;

                // Enqueue an action that logs start/end and invokes the Lua callback
                _pendingActions.Enqueue(() =>
                {
                    Console.WriteLine($"[PendingAction][Event] Start - event:{eventCode} target:{targetPtr} callback:{cb}");
                    try
                    {
                        cb.Call(new LvEventData(eventCode, targetPtr));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Lua event callback error: {ex.Message}");
                    }
                    Console.WriteLine($"[PendingAction][Event] End - event:{eventCode} callback:{cb}");
                });
            }
        }

        class EventCallbackData
        {
            public LuaFunction Callback { get; set; } = null!;
            public object? UserData { get; set; }
            public LvObjWrapper? ObjWrapper { get; set; }
        }

        class TimerCallbackData
        {
            public LuaFunction Callback { get; set; } = null!;
            public lv_timer_t* Timer { get; set; }
        }
    }

    /// <summary>
    /// 延迟事件数据（用于在主循环中安全传递事件信息）
    /// </summary>
    public unsafe class LvEventData
    {
        private readonly int _eventCode;
        private readonly IntPtr _targetPtr;

        public LvEventData(int eventCode, IntPtr targetPtr)
        {
            _eventCode = eventCode;
            _targetPtr = targetPtr;
        }

        public int get_code()
        {
            return _eventCode;
        }

        public LvObjWrapper get_target()
        {
            return new LvObjWrapper((lv_obj_t*)_targetPtr);
        }
    }

    /// <summary>
    /// Wrapper class for lv_event_t*
    /// </summary>
    public unsafe class LvEventWrapper
    {
        private readonly lv_event_t* _ptr;

        public LvEventWrapper(lv_event_t* ptr)
        {
            _ptr = ptr;
        }

        public int get_code()
        {
            return (int)lv_event_get_code(_ptr);
        }

        public LvObjWrapper get_target()
        {
            return new LvObjWrapper((lv_obj_t*)lv_event_get_target(_ptr));
        }
    }

    /// <summary>
    /// Wrapper class for lv_timer_t*
    /// </summary>
    public unsafe class LvTimerWrapper
    {
        internal lv_timer_t* Ptr { get; }
        internal GCHandle Handle { get; }

        public LvTimerWrapper(lv_timer_t* ptr, GCHandle handle)
        {
            Ptr = ptr;
            Handle = handle;
        }
    }

    /// <summary>
    /// Wrapper for chart series pointer
    /// </summary>
    public unsafe class LvChartSeriesWrapper
    {
        internal lv_chart_series_t* Ptr { get; }

        public LvChartSeriesWrapper(lv_chart_series_t* ptr)
        {
            Ptr = ptr;
        }
    }

    /// <summary>
    /// Wrapper class for lv_obj_t* (chart specific)
    /// </summary>
    public unsafe class LvChartWrapper : LvObjWrapper
    {
        public LvChartWrapper(lv_obj_t* ptr) : base(ptr) { }

        public void set_type(int type)
        {
            lv_chart_set_type(Ptr, (lv_chart_type_t)type);
        }

        public void set_point_count(int count)
        {
            lv_chart_set_point_count(Ptr, (uint)count);
        }

        public void set_update_mode(int mode)
        {
            lv_chart_set_update_mode(Ptr, (lv_chart_update_mode_t)mode);
        }

        public void set_div_line_count(int hdiv, int vdiv)
        {
            lv_chart_set_div_line_count(Ptr, (byte)hdiv, (byte)vdiv);
        }

        public LvChartSeriesWrapper add_series(int color, int axis)
        {
            lv_color_t lvColor = lv_color_hex((uint)color);
            lv_chart_series_t* series = lv_chart_add_series(Ptr, lvColor, (lv_chart_axis_t)axis);
            return new LvChartSeriesWrapper(series);
        }

        public void set_range(int axis, int min, int max)
        {
            lv_chart_set_range(Ptr, (lv_chart_axis_t)axis, min, max);
        }

        public void set_next_value(LvChartSeriesWrapper series, int value)
        {
            lv_chart_set_next_value(Ptr, series.Ptr, value);
        }
    }

    /// <summary>
    /// Wrapper class for lv_obj_t* to expose LVGL objects to Lua
    /// </summary>
    public unsafe class LvObjWrapper
    {
        internal lv_obj_t* Ptr { get; }

        public LvObjWrapper(lv_obj_t* ptr)
        {
            Ptr = ptr;
        }

        // ========== 获取元素相关方法 ==========

        /// <summary>
        /// 获取子元素数量
        /// </summary>
        public int get_child_count()
        {
            return (int)lv_obj_get_child_count(Ptr);
        }

        /// <summary>
        /// 获取指定索引的子元素
        /// </summary>
        public LvObjWrapper? get_child(int index)
        {
            lv_obj_t* child = lv_obj_get_child(Ptr, index);
            return child != null ? new LvObjWrapper(child) : null;
        }

        /// <summary>
        /// 获取父元素
        /// </summary>
        public LvObjWrapper? get_parent()
        {
            lv_obj_t* parent = lv_obj_get_parent(Ptr);
            return parent != null ? new LvObjWrapper(parent) : null;
        }

        /// <summary>
        /// 获取所有子元素
        /// </summary>
        public List<LvObjWrapper> get_children()
        {
            var children = new List<LvObjWrapper>();
            int count = get_child_count();
            for (int i = 0; i < count; i++)
            {
                var child = get_child(i);
                if (child != null)
                {
                    children.Add(child);
                }
            }
            return children;
        }

        /// <summary>
        /// 递归获取所有后代元素
        /// </summary>
        public List<LvObjWrapper> get_all_descendants()
        {
            var descendants = new List<LvObjWrapper>();
            CollectDescendants(this, descendants);
            return descendants;
        }

        private static void CollectDescendants(LvObjWrapper obj, List<LvObjWrapper> list)
        {
            foreach (var child in obj.get_children())
            {
                list.Add(child);
                CollectDescendants(child, list);
            }
        }

        /// <summary>
        /// 获取元素在父元素中的索引
        /// </summary>
        public int get_index()
        {
            return lv_obj_get_index(Ptr);
        }

        /// <summary>
        /// 获取元素的X坐标
        /// </summary>
        public int get_x()
        {
            return lv_obj_get_x(Ptr);
        }

        /// <summary>
        /// 获取元素的Y坐标
        /// </summary>
        public int get_y()
        {
            return lv_obj_get_y(Ptr);
        }

        /// <summary>
        /// 获取元素的宽度
        /// </summary>
        public int get_width()
        {
            return lv_obj_get_width(Ptr);
        }

        /// <summary>
        /// 获取元素的高度
        /// </summary>
        public int get_height()
        {
            return lv_obj_get_height(Ptr);
        }

        /// <summary>
        /// 获取元素的内容宽度
        /// </summary>
        public int get_content_width()
        {
            return lv_obj_get_content_width(Ptr);
        }

        /// <summary>
        /// 获取元素的内容高度
        /// </summary>
        public int get_content_height()
        {
            return lv_obj_get_content_height(Ptr);
        }

        /// <summary>
        /// 检查元素是否可见
        /// </summary>
        public bool is_visible()
        {
            return lv_obj_is_visible(Ptr);
        }

        /// <summary>
        /// 检查元素是否有指定的标志
        /// </summary>
        public bool has_flag(int flag)
        {
            return lv_obj_has_flag(Ptr, (lv_obj_flag_t)flag);
        }

        /// <summary>
        /// 获取元素的状态
        /// </summary>
        public int get_state()
        {
            return lv_obj_get_state(Ptr);
        }

        // ========== 原有方法 ==========

        public void set_size(int width, int height)
        {
            lv_obj_set_size(Ptr, width, height);
        }

        public void set_width(int width)
        {
            lv_obj_set_width(Ptr, width);
        }

        public void set_height(int height)
        {
            lv_obj_set_height(Ptr, height);
        }

        public void set_pos(int x, int y)
        {
            lv_obj_set_pos(Ptr, x, y);
        }

        public void set_text(string text)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(text + "\0");
            fixed (byte* ptr = utf8)
            {
                lv_label_set_text(Ptr, ptr);
            }
        }

        public void center()
        {
            lv_obj_center(Ptr);
        }

        public void align(int alignType, int xOffset, int yOffset)
        {
            lv_obj_align(Ptr, (lv_align_t)alignType, xOffset, yOffset);
        }

        public void align_to(LvObjWrapper baseObj, int alignType, int xOffset, int yOffset)
        {
            lv_obj_align_to(Ptr, baseObj.Ptr, (lv_align_t)alignType, xOffset, yOffset);
        }

        public void delete()
        {
            lv_obj_delete(Ptr);
        }

        public void remove_flag(int flag)
        {
            lv_obj_remove_flag(Ptr, (lv_obj_flag_t)flag);
        }

        public void add_flag(int flag)
        {
            lv_obj_add_flag(Ptr, (lv_obj_flag_t)flag);
        }

        /// <summary>
        /// 设置布局类型 (0 = NONE, 1 = FLEX, 2 = GRID)
        /// </summary>
        public void set_layout(int layout)
        {
            lv_obj_set_layout(Ptr, (uint)layout);
        }

        /// <summary>
        /// 清除布局，恢复为无布局模式
        /// </summary>
        public void clear_layout()
        {
            lv_obj_set_layout(Ptr, (uint)lv_layout_t.LV_LAYOUT_NONE);
        }

        // ========== Style methods ==========

        public void set_style_bg_color(int color, int selector)
        {
            lv_color_t lvColor = lv_color_hex((uint)color);
            lv_obj_set_style_bg_color(Ptr, lvColor, (uint)selector);
        }

        public void set_style_bg_opa(int opa, int selector)
        {
            lv_obj_set_style_bg_opa(Ptr, (byte)opa, (uint)selector);
        }

        public void set_style_radius(int radius, int selector)
        {
            lv_obj_set_style_radius(Ptr, radius, (uint)selector);
        }

        public void set_style_border_width(int width, int selector)
        {
            lv_obj_set_style_border_width(Ptr, width, (uint)selector);
        }

        public void set_style_border_color(int color, int selector)
        {
            lv_color_t lvColor = lv_color_hex((uint)color);
            lv_obj_set_style_border_color(Ptr, lvColor, (uint)selector);
        }

        public void set_style_text_align(int align, int selector)
        {
            lv_obj_set_style_text_align(Ptr, (lv_text_align_t)align, (uint)selector);
        }

        public void set_style_transform_rotation(int angle, int selector)
        {
            lv_obj_set_style_transform_rotation(Ptr, angle, (uint)selector);
        }

        public void set_style_transform_pivot_x(int x, int selector)
        {
            lv_obj_set_style_transform_pivot_x(Ptr, x, (uint)selector);
        }

        public void set_style_transform_pivot_y(int y, int selector)
        {
            lv_obj_set_style_transform_pivot_y(Ptr, y, (uint)selector);
        }

        public void set_style_pad_all(int pad, int selector)
        {
            lv_obj_set_style_pad_all(Ptr, pad, (uint)selector);
        }

        public void set_style_pad_top(int pad, int selector)
        {
            lv_obj_set_style_pad_top(Ptr, pad, (uint)selector);
        }

        public void set_style_pad_bottom(int pad, int selector)
        {
            lv_obj_set_style_pad_bottom(Ptr, pad, (uint)selector);
        }

        public void set_style_pad_left(int pad, int selector)
        {
            lv_obj_set_style_pad_left(Ptr, pad, (uint)selector);
        }

        public void set_style_pad_right(int pad, int selector)
        {
            lv_obj_set_style_pad_right(Ptr, pad, (uint)selector);
        }

        public void set_style_text_color(int color, int selector)
        {
            lv_color_t lvColor = lv_color_hex((uint)color);
            lv_obj_set_style_text_color(Ptr, lvColor, (uint)selector);
        }

        public void set_style_shadow_width(int width, int selector)
        {
            lv_obj_set_style_shadow_width(Ptr, width, (uint)selector);
        }

        public void set_style_shadow_color(int color, int selector)
        {
            lv_color_t lvColor = lv_color_hex((uint)color);
            lv_obj_set_style_shadow_color(Ptr, lvColor, (uint)selector);
        }

        public void set_style_shadow_opa(int opa, int selector)
        {
            lv_obj_set_style_shadow_opa(Ptr, (byte)opa, (uint)selector);
        }

        public void add_event_cb(LuaFunction callback, int eventCode, object? userData)
        {
            Program.LuaObjAddEventCb(this, callback, eventCode, userData);
        }
    }
}
