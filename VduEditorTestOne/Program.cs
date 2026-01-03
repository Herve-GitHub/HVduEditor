using LVGLSharp;
using LVGLSharp.Interop;
using LVGLSharp.Runtime.Windows;
using NLua;
using System.Diagnostics;
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
        
        // 用于延迟处理事件的队列（internal 以便 LvTextareaWrapper 可以访问）
        internal static ConcurrentQueue<Action> _pendingActions = new();
        
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
            _window = new Win32Window("LVGL Lua Demo", 1360, 810);
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
        
        // 标记是否正在处理 pending actions，防止嵌套调用
        static bool _isProcessingActions = false;
        
        // 用于跟踪连续空闲帧数，避免频繁检查
        static int _emptyFrameCount = 0;
        
        // 新增：用于事件节流的时间戳
        static long _lastEventProcessTime = 0;
        static readonly long _eventThrottleIntervalTicks = TimeSpan.FromMilliseconds(2).Ticks;

        /// <summary>
        /// 在主循环中安全地处理延迟的事件回调
        /// </summary>
        static void ProcessPendingActions()
        {
            // 防止嵌套调用导致的死循环
            if (_isProcessingActions)
            {
                return;
            }
            
            // 如果队列为空，快速返回
            if (_pendingActions.IsEmpty)
            {
                _emptyFrameCount++;
                return;
            }
            
            _emptyFrameCount = 0;
            _isProcessingActions = true;
            
            var startTime = Stopwatch.GetTimestamp();
            var maxProcessingTicks = TimeSpan.FromMilliseconds(10).Ticks; // 每帧最多处理 10ms
            
            try
            {
                int processedCount = 0;
                int maxActionsPerFrame = 50; // 增加每帧处理数量
                
                while (processedCount < maxActionsPerFrame && _pendingActions.TryDequeue(out var action))
                {
                    processedCount++;
                    try
                    {
                        action();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing pending action: {ex.Message}");
                    }
                    
                    // 检查是否超时
                    if (Stopwatch.GetTimestamp() - startTime > maxProcessingTicks)
                    {
                        break;
                    }
                }
                
                // 如果队列积压过多，输出警告
                int remaining = _pendingActions.Count;
                if (remaining > 200)
                {
                    Console.WriteLine($"[WARNING] Pending actions queue backlog: {remaining} actions waiting, processed: {processedCount}");
                }
            }
            finally
            {
                _isProcessingActions = false;
                _lastEventProcessTime = Stopwatch.GetTimestamp();
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

            // ========== STATE constants ==========
            lua["_lvgl_module.STATE_DEFAULT"] = (int)LV_STATE_DEFAULT;
            lua["_lvgl_module.STATE_CHECKED"] = (int)LV_STATE_CHECKED;
            lua["_lvgl_module.STATE_FOCUSED"] = (int)LV_STATE_FOCUSED;
            lua["_lvgl_module.STATE_EDITED"] = (int)LV_STATE_EDITED;
            lua["_lvgl_module.STATE_HOVERED"] = (int)LV_STATE_HOVERED;
            lua["_lvgl_module.STATE_PRESSED"] = (int)LV_STATE_PRESSED;
            lua["_lvgl_module.STATE_DISABLED"] = (int)LV_STATE_DISABLED;

            // ========== LAYOUT constants ==========
            lua["_lvgl_module.LAYOUT_NONE"] = (int)lv_layout_t.LV_LAYOUT_NONE;
            lua["_lvgl_module.LAYOUT_FLEX"] = (int)lv_layout_t.LV_LAYOUT_FLEX;
            lua["_lvgl_module.LAYOUT_GRID"] = (int)lv_layout_t.LV_LAYOUT_GRID;

            // ========== PART constants (for slider and other widgets) ==========
            lua["_lvgl_module.PART_MAIN"] = (int)LV_PART_MAIN;
            lua["_lvgl_module.PART_INDICATOR"] = (int)LV_PART_INDICATOR;
            lua["_lvgl_module.PART_KNOB"] = (int)LV_PART_KNOB;
            lua["_lvgl_module.PART_CURSOR"] = (int)LV_PART_CURSOR;
            lua["_lvgl_module.PART_ITEMS"] = (int)LV_PART_ITEMS;
            lua["_lvgl_module.PART_SCROLLBAR"] = (int)LV_PART_SCROLLBAR;
            
            // ========== ANIM constants ==========
            lua["_lvgl_module.ANIM_OFF"] = false;
            lua["_lvgl_module.ANIM_ON"] = true;

            // ========== Other constants ==========
            lua["_lvgl_module.RADIUS_CIRCLE"] = 0x7FFF; // LV_RADIUS_CIRCLE

            // ========== Register object create functions ==========
            lua.RegisterFunction("_lvgl_module.scr_act", typeof(Program).GetMethod(nameof(LuaGetScreen)));
            lua.RegisterFunction("_lvgl_module.btn_create", typeof(Program).GetMethod(nameof(LuaBtnCreate)));
            lua.RegisterFunction("_lvgl_module.label_create", typeof(Program).GetMethod(nameof(LuaLabelCreate)));
            lua.RegisterFunction("_lvgl_module.obj_create", typeof(Program).GetMethod(nameof(LuaObjCreate)));
            lua.RegisterFunction("_lvgl_module.chart_create", typeof(Program).GetMethod(nameof(LuaChartCreate)));
            lua.RegisterFunction("_lvgl_module.slider_create", typeof(Program).GetMethod(nameof(LuaSliderCreate)));
            lua.RegisterFunction("_lvgl_module.textarea_create", typeof(Program).GetMethod(nameof(LuaTextareaCreate)));

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

        public static LvTextareaWrapper LuaTextareaCreate(LvObjWrapper parent)
        {
            lv_obj_t* textarea = lv_textarea_create(parent.Ptr);
            var wrapper = new LvTextareaWrapper(textarea);
            Console.WriteLine($"[LuaTextareaCreate] Created LvTextareaWrapper, type: {wrapper.GetType().Name}");
            return wrapper;
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
            try
            {
                var timerData = new TimerCallbackData { Callback = callback };
                _timerCallbacks.Add(timerData);
                GCHandle handle = GCHandle.Alloc(timerData);

                lv_timer_t* timer = lv_timer_create(&LuaTimerHandler, (uint)periodMs, (void*)GCHandle.ToIntPtr(handle));
                timerData.Timer = timer;
                return new LvTimerWrapper(timer, handle);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] LuaTimerCreate failed: {ex.Message}");
                Console.WriteLine($"[ERROR] StackTrace: {ex.StackTrace}");
                throw;
            }
        }

        public static void LuaTimerDelete(LvTimerWrapper timerWrapper)
        {
            try
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
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] LuaTimerDelete failed: {ex.Message}");
                Console.WriteLine($"[ERROR] StackTrace: {ex.StackTrace}");
                throw;
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
                    //Console.WriteLine($"[PendingAction][Timer] Start callback: {cb}");
                    try
                    {
                        cb.Call();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Lua timer callback error: {ex.Message}");
                    }
                    //Console.WriteLine($"[PendingAction][Timer] End callback: {cb}");
                });
            }
        }

        // ========== Event functions ==========

        public static void LuaObjAddEventCb(LvObjWrapper obj, LuaFunction callback, int eventCode, object? userData)
        {
            try
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
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] LuaObjAddEventCb failed: {ex.Message}");
                Console.WriteLine($"[ERROR] StackTrace: {ex.StackTrace}");
                throw;
            }
        }

        public static int LuaEventGetCode(LvEventData eventData)
        {
            try
            {
                return eventData.get_code();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] LuaEventGetCode failed: {ex.Message}");
                Console.WriteLine($"[ERROR] StackTrace: {ex.StackTrace}");
                throw;
            }
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
                    try
                    {
                        cb.Call(new LvEventData(eventCode, targetPtr));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Lua event callback error: {ex.Message}");
                    }
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
    /// Wrapper class for lv_obj_t* (textarea specific)
    /// </summary>
    public unsafe class LvTextareaWrapper : LvObjWrapper
    {
        // 保存 accepted_chars 的 引用，防止被 GC 回收
        private byte[]? _acceptedCharsBuffer;
        private GCHandle _acceptedCharsHandle;
        
        public LvTextareaWrapper(lv_obj_t* ptr) : base(ptr) 
        {
            // 启用点击光标定位
            lv_textarea_set_cursor_click_pos(ptr, (c_bool1)1);
            
            // 默认状态下隐藏光标（透明）
            lv_obj_set_style_bg_opa(ptr, 0, (uint)LV_PART_CURSOR);
            
            // 聚焦状态下显示白色光标
            lv_color_t cursorColor = lv_color_hex(0xFFFFFF);
            lv_obj_set_style_bg_color(ptr, cursorColor, (uint)(LV_PART_CURSOR | LV_STATE_FOCUSED));
            lv_obj_set_style_bg_opa(ptr, 255, (uint)(LV_PART_CURSOR | LV_STATE_FOCUSED));
            
            // 自动添加到键盘输入组并注册事件（延迟执行，避免在事件处理过程中操作）
            var ptrCopy = ptr;
            Program._pendingActions.Enqueue(() =>
            {
                try {
                    // 添加到键盘输入组
                    lv_group_add_obj(Win32Window.key_inputGroup, ptrCopy);

                    // 添加聚焦事件回调，用于设置 IME 候选框位置，并自动进入编辑模式
                    lv_obj_add_event_cb(ptrCopy, &OnTextareaFocused, LV_EVENT_FOCUSED, null);

                    // 添加 PRESSED 事件回调，确保按下时立即将焦点设置到该 textarea
                    lv_obj_add_event_cb(ptrCopy, &OnTextareaPressed, LV_EVENT_PRESSED, null);

                    Console.WriteLine($"[LvTextareaWrapper] Auto-enabled keyboard input for textarea at 0x{(IntPtr)ptrCopy:X}");
                }
                catch (Exception e)
                {
                    Console.WriteLine($"[ERROR] Auto-enabled keyboard input {e.Message}");
                }
            });
        }
        
        /// <summary>
        /// 启用键盘输入支持（添加到输入组并注册事件）
        /// 注意：现在 textarea 在创建时会自动启用键盘输入，此方法保留为兼容性
        /// </summary>
        public void enable_keyboard_input()
        {
            // 由于构造函数已经自动启用键盘输入，此方法现在为空操作
            // 保留此方法是为了兼容已有的 Lua 代码
            Console.WriteLine($"[LvTextareaWrapper] enable_keyboard_input() called - already auto-enabled in constructor");
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        static void OnTextareaFocused(lv_event_t* e)
        {
            lv_obj_t* target = (lv_obj_t*)lv_event_get_target(e);
            Console.WriteLine($"[LvTextareaWrapper] Textarea FOCUSED at 0x{(IntPtr)target:X}");
            
            // 设置 IME 候选框位置
            lv_area_t area;
            lv_obj_get_coords(target, &area);
            int ime_x = area.x1;
            int ime_y = area.y2;
            
            IntPtr hIMC = Win32Api.ImmGetContext(Win32Api.g_hwnd);
            if (hIMC != IntPtr.Zero)
            {
                Win32Api.COMPOSITIONFORM compForm = new Win32Api.COMPOSITIONFORM();
                compForm.dwStyle = Win32Api.CFS_POINT;
                compForm.ptCurrentPos.x = ime_x;
                compForm.ptCurrentPos.y = ime_y;
                Win32Api.ImmSetCompositionWindow(hIMC, ref compForm);
                Win32Api.ImmReleaseContext(Win32Api.g_hwnd, hIMC);
            }
            
            // 自动进入编辑模式
            lv_group_set_editing(Win32Window.key_inputGroup, (c_bool1)1);
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        static void OnTextareaPressed(lv_event_t* e)
        {
            lv_obj_t* target = (lv_obj_t*)lv_event_get_target(e);
            Console.WriteLine($"[LvTextareaWrapper] Textarea PRESSED, focusing object at 0x{(IntPtr)target:X}");
            // 将焦点设置到被按下的 textarea
            lv_group_focus_obj(target);
            // 立即进入编辑模式
            lv_group_set_editing(Win32Window.key_inputGroup, (c_bool1)1);
        }

        /// <summary>
        /// 设置光标颜色
        /// </summary>
        public void set_cursor_color(int color)
        {
            lv_color_t lvColor = lv_color_hex((uint)color);
            lv_obj_set_style_bg_color(Ptr, lvColor, (uint)(LV_PART_CURSOR | LV_STATE_FOCUSED));
            lv_obj_set_style_bg_opa(Ptr, 255, (uint)(LV_PART_CURSOR | LV_STATE_FOCUSED));
        }

        /// <summary>
        /// 设置文本内容
        /// </summary>
        public new void set_text(string text)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(text + "\0");
            fixed (byte* ptr = utf8)
            {
                lv_textarea_set_text(Ptr, ptr);
            }
        }

        /// <summary>
        /// 获取文本内容
        /// </summary>
        public string get_text()
        {
            byte* textPtr = lv_textarea_get_text(Ptr);
            if (textPtr == null) return string.Empty;
            return Marshal.PtrToStringUTF8((IntPtr)textPtr) ?? string.Empty;
        }

        /// <summary>
        /// 设置单行模式
        /// </summary>
        public void set_one_line(bool enabled)
        {
            lv_textarea_set_one_line(Ptr, enabled ? (c_bool1)1 : (c_bool1)0);
        }

        /// <summary>
        /// 获取是否为单行模式
        /// </summary>
        public bool get_one_line()
        {
            return lv_textarea_get_one_line(Ptr) != 0;
        }

        /// <summary>
        /// 添加文本到 textarea
        /// </summary>
        public void add_text(string text)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(text + "\0");
            fixed (byte* ptr = utf8)
            {
                lv_textarea_add_text(Ptr, ptr);
            }
        }

        /// <summary>
        /// 添加单个字符到 textarea
        /// </summary>
        public void add_char(int c)
        {
            lv_textarea_add_char(Ptr, (uint)c);
        }

        /// <summary>
        /// 设置占位符文本
        /// </summary>
        public void set_placeholder_text(string text)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(text + "\0");
            fixed (byte* ptr = utf8)
            {
                lv_textarea_set_placeholder_text(Ptr, ptr);
            }
        }

        /// <summary>
        /// 获取占位符文本
        /// </summary>
        public string get_placeholder_text()
        {
            byte* textPtr = lv_textarea_get_placeholder_text(Ptr);
            if (textPtr == null) return string.Empty;
            return Marshal.PtrToStringUTF8((IntPtr)textPtr) ?? string.Empty;
        }

        /// <summary>
        /// 设置光标位置
        /// </summary>
        public void set_cursor_pos(int pos)
        {
            lv_textarea_set_cursor_pos(Ptr, pos);
        }

        /// <summary>
        /// 获取光标位置
        /// </summary>
        public int get_cursor_pos()
        {
            return (int)lv_textarea_get_cursor_pos(Ptr);
        }

        /// <summary>
        /// 设置是否启用点击光标定位
        /// </summary>
        public void set_cursor_click_pos(bool enabled)
        {
            lv_textarea_set_cursor_click_pos(Ptr, enabled ? (c_bool1)1 : (c_bool1)0);
        }

        /// <summary>
        /// 获取是否启用点击光标定位
        /// </summary>
        public bool get_cursor_click_pos()
        {
            return lv_textarea_get_cursor_click_pos(Ptr) != 0;
        }

        /// <summary>
        /// 设置密码模式
        /// </summary>
        public void set_password_mode(bool enabled)
        {
            lv_textarea_set_password_mode(Ptr, enabled ? (c_bool1)1 : (c_bool1)0);
        }

        /// <summary>
        /// 获取是否为密码模式
        /// </summary>
        public bool get_password_mode()
        {
            return lv_textarea_get_password_mode(Ptr) != 0;
        }

        /// <summary>
        /// 设置密码显示字符
        /// </summary>
        public void set_password_bullet(string bullet)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(bullet + "\0");
            fixed (byte* ptr = utf8)
            {
                lv_textarea_set_password_bullet(Ptr, ptr);
            }
        }

        /// <summary>
        /// 获取密码显示字符
        /// </summary>
        public string get_password_bullet()
        {
            byte* bulletPtr = lv_textarea_get_password_bullet(Ptr);
            if (bulletPtr == null) return string.Empty;
            return Marshal.PtrToStringUTF8((IntPtr)bulletPtr) ?? string.Empty;
        }

        /// <summary>
        /// 设置密码显示时间（毫秒）
        /// </summary>
        public void set_password_show_time(int time)
        {
            lv_textarea_set_password_show_time(Ptr, (uint)time);
        }

        /// <summary>
        /// 获取密码显示时间（毫秒）
        /// </summary>
        public int get_password_show_time()
        {
            return (int)lv_textarea_get_password_show_time(Ptr);
        }

        /// <summary>
        /// 设置接受的字符列表（需要保持缓冲区不被 GC 回收）
        /// </summary>
        public void set_accepted_chars(string chars)
        {
            // 释放之前的缓冲区
            if (_acceptedCharsHandle.IsAllocated)
            {
                _acceptedCharsHandle.Free();
            }
            
            // 创建新的缓冲区并固定它
            _acceptedCharsBuffer = Encoding.UTF8.GetBytes(chars + "\0");
            _acceptedCharsHandle = GCHandle.Alloc(_acceptedCharsBuffer, GCHandleType.Pinned);
            
            lv_textarea_set_accepted_chars(Ptr, (byte*)_acceptedCharsHandle.AddrOfPinnedObject());
        }

        /// <summary>
        /// 获取接受的字符列表
        /// </summary>
        public string get_accepted_chars()
        {
            byte* charsPtr = lv_textarea_get_accepted_chars(Ptr);
            if (charsPtr == null) return string.Empty;
            return Marshal.PtrToStringUTF8((IntPtr)charsPtr) ?? string.Empty;
        }

        /// <summary>
        /// 设置最大长度
        /// </summary>
        public void set_max_length(int length)
        {
            lv_textarea_set_max_length(Ptr, (uint)length);
        }

        /// <summary>
        /// 获取最大长度
        /// </summary>
        public int get_max_length()
        {
            return (int)lv_textarea_get_max_length(Ptr);
        }

        /// <summary>
        /// 设置插入替换文本
        /// </summary>
        public void set_insert_replace(string txt)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(txt + "\0");
            fixed (byte* ptr = utf8)
            {
                lv_textarea_set_insert_replace(Ptr, ptr);
            }
        }

        /// <summary>
        /// 设置是否启用文本选择
        /// </summary>
        public void set_text_selection(bool enabled)
        {
            lv_textarea_set_text_selection(Ptr, enabled ? (c_bool1)1 : (c_bool1)0);
        }

        /// <summary>
        /// 获取是否启用文本选择
        /// </summary>
        public bool get_text_selection()
        {
            return lv_textarea_get_text_selection(Ptr) != 0;
        }

        /// <summary>
        /// 检查是否有文本被选中
        /// </summary>
        public bool text_is_selected()
        {
            return lv_textarea_text_is_selected(Ptr) != 0;
        }

        /// <summary>
        /// 清除文本选择
        /// </summary>
        public void clear_selection()
        {
            lv_textarea_clear_selection(Ptr);
        }

        /// <summary>
        /// 删除光标前的字符
        /// </summary>
        public void delete_char()
        {
            lv_textarea_delete_char(Ptr);
        }

        /// <summary>
        /// 删除光标后的字符
        /// </summary>
        public void delete_char_forward()
        {
            lv_textarea_delete_char_forward(Ptr);
        }

        /// <summary>
        /// 光标向右移动
        /// </summary>
        public void cursor_right()
        {
            lv_textarea_cursor_right(Ptr);
        }

        /// <summary>
        /// 光标向左移动
        /// </summary>
        public void cursor_left()
        {
            lv_textarea_cursor_left(Ptr);
        }

        /// <summary>
        /// 光标向下移动
        /// </summary>
        public void cursor_down()
        {
            lv_textarea_cursor_down(Ptr);
        }

        /// <summary>
        /// 光标向上移动
        /// </summary>
        public void cursor_up()
        {
            lv_textarea_cursor_up(Ptr);
        }

        /// <summary>
        /// 设置文本对齐方式
        /// </summary>
        public void set_align(int align)
        {
            lv_textarea_set_align(Ptr, (lv_text_align_t)align);
        }

        /// <summary>
        /// 获取当前光标位置的字符
        /// </summary>
        public int get_current_char()
        {
            return (int)lv_textarea_get_current_char(Ptr);
        }

        /// <summary>
        /// 获取关联的 label 对象
        /// </summary>
        public LvObjWrapper get_label()
        {
            lv_obj_t* labelPtr = lv_textarea_get_label(Ptr);
            return new LvObjWrapper(labelPtr);
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
            try
            {
                lv_obj_set_size(Ptr, width, height);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] set_size failed: {ex.Message}");
                throw;
            }
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
            try
            {
                lv_obj_set_pos(Ptr, x, y);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] set_pos failed: {ex.Message}");
                throw;
            }
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
            try
            {
                lv_obj_remove_flag(Ptr, (lv_obj_flag_t)flag);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] remove_flag failed: {ex.Message}");
                throw;
            }
        }

        public void add_flag(int flag)
        {
            try
            {
                lv_obj_add_flag(Ptr, (lv_obj_flag_t)flag);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] add_flag failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 添加状态
        /// </summary>
        public void add_state(int state)
        {
            lv_obj_add_state(Ptr, (ushort)state);
        }

        /// <summary>
        /// 移除状态
        /// </summary>
        public void remove_state(int state)
        {
            lv_obj_remove_state(Ptr, (ushort)state);
        }

        /// <summary>
        /// 将对象添加到键盘输入组
        /// </summary>
        public void add_to_group()
        {
            lv_group_add_obj(Win32Window.key_inputGroup, Ptr);
        }

        /// <summary>
        /// 从键盘输入组移除对象
        /// </summary>
        public void remove_from_group()
        {
            lv_group_remove_obj(Ptr);
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
            try
            {
                lv_obj_set_layout(Ptr, (uint)lv_layout_t.LV_LAYOUT_NONE);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] clear_layout failed: {ex.Message}");
                throw;
            }
        }

        // ========== Style methods ==========

        public void set_style_bg_color(int color, int selector)
        {
            try
            {
                lv_color_t lvColor = lv_color_hex((uint)color);
                lv_obj_set_style_bg_color(Ptr, lvColor, (uint)selector);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] set_style_bg_color failed: {ex.Message}");
                throw;
            }
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

        // ========== Slider methods ==========
        
        /// <summary>
        /// 设置滑块范围（用于 slider）
        /// </summary>
        public void set_range(int min, int max)
        {
            lv_slider_set_range(Ptr, min, max);
        }
        
        /// <summary>
        /// 设置滑块值（用于 slider）
        /// </summary>
        public void set_value(int value, bool anim)
        {
            lv_slider_set_value(Ptr, value, anim);
        }
        
        /// <summary>
        /// 获取滑块值（用于 slider）
        /// </summary>
        public int get_value()
        {
            return lv_slider_get_value(Ptr);
        }

        public void add_event_cb(LuaFunction callback, int eventCode, object? userData)
        {
            try
            {
                Program.LuaObjAddEventCb(this, callback, eventCode, userData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] add_event_cb failed: {ex.Message}");
                throw;
            }
        }
    }
}
