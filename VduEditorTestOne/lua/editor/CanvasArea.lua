-- CanvasArea.lua
-- 画布区域：组态软件的设计画布，支持拖拽移动控件
local lv = require("lvgl")

local CanvasArea = {}
CanvasArea.__index = CanvasArea

CanvasArea.__widget_meta = {
    id = "canvas_area",
    name = "Canvas Area",
    description = "组态编辑器画布，支持拖拽放置和移动控件",
    schema_version = "1.0",
    version = "1.0",
}

-- 构造函数
function CanvasArea.new(parent, props)
    props = props or {}
    local self = setmetatable({}, CanvasArea)
    
    -- 属性
    self.props = {
        x = props.x or 0,
        y = props.y or 40,
        width = props.width or 800,
        height = props.height or 600,
        bg_color = props.bg_color or 0x1E1E1E,
        grid_color = props.grid_color or 0x2A2A2A,
        grid_size = props.grid_size or 20,
        show_grid = props.show_grid ~= false,
        snap_to_grid = props.snap_to_grid ~= false,
    }
    
    -- 放置的控件列表
    self._widgets = {}
    
    -- 选中的控件
    self._selected_widget = nil
    self._selection_box = nil
    
    -- 拖拽状态
    self._drag_state = {
        is_dragging = false,
        widget_entry = nil,
        start_widget_x = 0,
        start_widget_y = 0,
        start_mouse_x = 0,
        start_mouse_y = 0,
        last_x = 0,
        last_y = 0,
    }
    
    -- 事件监听器
    self._event_listeners = {}
    
    -- 创建画布容器
    self.container = lv.obj_create(parent)
    self.container:set_pos(self.props.x, self.props.y)
    self.container:set_size(self.props.width, self.props.height)
    self.container:set_style_bg_color(self.props.bg_color, 0)
    self.container:set_style_radius(0, 0)
    self.container:set_style_border_width(1, 0)
    self.container:set_style_border_color(0x3C3C3C, 0)
    self.container:set_style_pad_all(0, 0)
    self.container:remove_flag(lv.OBJ_FLAG_SCROLLABLE)
    self.container:remove_flag(lv.OBJ_FLAG_GESTURE_BUBBLE)
    self.container:clear_layout()
    
    -- 绘制网格
    if self.props.show_grid then
        self:_draw_grid()
    end
    
    -- 画布点击事件（取消选中）
    local this = self
    self.container:add_event_cb(function(e)
        if not this._drag_state.is_dragging then
            this:deselect()
        end
    end, lv.EVENT_CLICKED, nil)
    
    return self
end

-- 事件订阅方法
function CanvasArea:on(event_name, callback)
    if not self._event_listeners[event_name] then
        self._event_listeners[event_name] = {}
    end
    table.insert(self._event_listeners[event_name], callback)
end

-- 触发事件
function CanvasArea:_emit(event_name, ...)
    local listeners = self._event_listeners[event_name]
    if listeners then
        for _, cb in ipairs(listeners) do
            local ok, err = pcall(cb, self, ...)
            if not ok then
                print("[CanvasArea] event callback error:", err)
            end
        end
    end
end

-- 绘制网格
function CanvasArea:_draw_grid()
    local grid_size = self.props.grid_size
    local width = self.props.width
    local height = self.props.height
    
    -- 垂直线
    for x = grid_size, width - 1, grid_size do
        local line = lv.obj_create(self.container)
        line:set_pos(x, 0)
        line:set_size(1, height)
        line:set_style_bg_color(self.props.grid_color, 0)
        line:set_style_bg_opa(128, 0)
        line:set_style_radius(0, 0)
        line:set_style_border_width(0, 0)
        line:remove_flag(lv.OBJ_FLAG_CLICKABLE)
    end
    
    -- 水平线
    for y = grid_size, height - 1, grid_size do
        local line = lv.obj_create(self.container)
        line:set_pos(0, y)
        line:set_size(width, 1)
        line:set_style_bg_color(self.props.grid_color, 0)
        line:set_style_bg_opa(128, 0)
        line:set_style_radius(0, 0)
        line:set_style_border_width(0, 0)
        line:remove_flag(lv.OBJ_FLAG_CLICKABLE)
    end
end

-- 对齐到网格
function CanvasArea:snap_position(x, y)
    if not self.props.snap_to_grid then
        return math.floor(x), math.floor(y)
    end
    local grid = self.props.grid_size
    -- 确保 x, y 是数字
    x = x or 0
    y = y or 0
    local snapped_x = math.floor((x + grid / 2) / grid) * grid
    local snapped_y = math.floor((y + grid / 2) / grid) * grid
    -- 确保不为负数
    snapped_x = math.max(0, snapped_x)
    snapped_y = math.max(0, snapped_y)
    return snapped_x, snapped_y
end

-- 递归禁用所有子元素的事件响应（设计模式）
function CanvasArea:_disable_widget_events(obj)
    -- 禁用点击、滚动等交互
    obj:remove_flag(lv.OBJ_FLAG_CLICKABLE)
    obj:remove_flag(lv.OBJ_FLAG_SCROLLABLE)
    obj:remove_flag(lv.OBJ_FLAG_CHECKABLE)
    obj:remove_flag(lv.OBJ_FLAG_SCROLL_ON_FOCUS)
    
    -- 递归处理所有子元素
    local child_count = obj:get_child_count()
    for i = 0, child_count - 1 do
        local child = obj:get_child(i)
        if child then
            self:_disable_widget_events(child)
        end
    end
end

-- 添加控件到画布
function CanvasArea:add_widget(widget_module, props)
    props = props or {}
    
    -- 对齐到网格
    local x, y = self:snap_position(props.x or 100, props.y or 100)
    props.x = x
    props.y = y
    
    -- 设计模式：强制禁用自动更新
    props.design_mode = true
    props.auto_update = false
    
    -- 创建控件实例
    local widget_instance = widget_module.new(self.container, props)
    
    -- 获取控件的主要对象
    local main_obj = widget_instance.btn or widget_instance.container or widget_instance.obj or widget_instance.chart
    
    -- 停止控件的定时器（如果有）
    if widget_instance.stop then
        widget_instance:stop()
    end
    
    -- 禁用控件自身的事件响应（设计模式）
    if main_obj then
        self:_disable_widget_events(main_obj)
    end
    
    -- 记录控件信息
    local widget_entry = {
        id = self:_generate_id(),
        module = widget_module,
        instance = widget_instance,
        props = props,
    }
    
    table.insert(self._widgets, widget_entry)
    
    -- 直接在控件上注册事件（不使用覆盖层）
    self:_setup_widget_drag_events(widget_entry)
    
    -- 触发事件
    self:_emit("widget_added", widget_entry)
    
    return widget_entry
end

-- 设置控件拖拽事件（直接在控件上）
function CanvasArea:_setup_widget_drag_events(widget_entry)
    local instance = widget_entry.instance
    local main_obj = instance.btn or instance.container or instance.obj or instance.chart
    if not main_obj then return end
    
    local this = self
    
    -- 重新启用控件的点击能力（用于拖拽）
    main_obj:add_flag(lv.OBJ_FLAG_CLICKABLE)
    
    -- 按下事件 - 开始拖拽
    main_obj:add_event_cb(function(e)
        this:_on_widget_pressed(widget_entry)
    end, lv.EVENT_PRESSED, nil)
    
    -- 拖动事件 - 移动控件
    main_obj:add_event_cb(function(e)
        this:_on_widget_pressing(widget_entry)
    end, lv.EVENT_PRESSING, nil)
    
    -- 释放事件 - 结束拖拽
    main_obj:add_event_cb(function(e)
        this:_on_widget_released(widget_entry)
    end, lv.EVENT_RELEASED, nil)
    
    -- 点击事件 - 选中控件（只在非拖拽时触发）
    main_obj:add_event_cb(function(e)
        -- 短暂延迟检查，确保 released 事件已处理
        if not this._drag_state.is_dragging then
            this:select_widget(widget_entry)
        end
    end, lv.EVENT_CLICKED, nil)
    
    print("[画布] 控件事件已注册: " .. widget_entry.id)
end

-- 控件按下事件
function CanvasArea:_on_widget_pressed(widget_entry)
    local instance = widget_entry.instance
    local main_obj = instance.btn or instance.container or instance.obj or instance.chart
    if not main_obj then return end
    
    -- 获取当前鼠标位置
    local mouse_x = lv.get_mouse_x()
    local mouse_y = lv.get_mouse_y()
    
    -- 获取控件当前位置
    local widget_x = main_obj:get_x()
    local widget_y = main_obj:get_y()
    
    -- 记录拖拽开始状态
    self._drag_state.is_dragging = false  -- 还没开始移动
    self._drag_state.widget_entry = widget_entry
    self._drag_state.start_widget_x = widget_x
    self._drag_state.start_widget_y = widget_y
    self._drag_state.start_mouse_x = mouse_x
    self._drag_state.start_mouse_y = mouse_y
    self._drag_state.last_x = widget_x
    self._drag_state.last_y = widget_y
    
    print("[画布] 按下控件: " .. widget_entry.id .. " 鼠标: (" .. tostring(mouse_x) .. ", " .. tostring(mouse_y) .. ") 控件: (" .. widget_x .. ", " .. widget_y .. ")")
end

-- 控件拖动事件
function CanvasArea:_on_widget_pressing(widget_entry)
    if self._drag_state.widget_entry ~= widget_entry then return end
    
    local instance = widget_entry.instance
    local main_obj = instance.btn or instance.container or instance.obj or instance.chart
    if not main_obj then return end
    
    -- 获取当前鼠标位置
    local current_mouse_x = lv.get_mouse_x()
    local current_mouse_y = lv.get_mouse_y()
    
    -- 计算移动增量（相对于初始位置）
    local delta_x = current_mouse_x - self._drag_state.start_mouse_x
    local delta_y = current_mouse_y - self._drag_state.start_mouse_y
    
    -- 检查是否真的开始移动（避免微小抖动）
    if not self._drag_state.is_dragging then
        if math.abs(delta_x) > 3 or math.abs(delta_y) > 3 then
            self._drag_state.is_dragging = true
            -- 选中正在拖拽的控件
            self:select_widget(widget_entry)
            print("[画布] 开始拖拽: " .. widget_entry.id)
        else
            return
        end
    end
    
    -- 计算新位置（基于初始控件位置 + 鼠标移动量）
    local new_x = self._drag_state.start_widget_x + delta_x
    local new_y = self._drag_state.start_widget_y + delta_y
    
    -- 限制在画布范围内
    local w = main_obj:get_width()
    local h = main_obj:get_height()
    new_x = math.max(0, math.min(new_x, self.props.width - w))
    new_y = math.max(0, math.min(new_y, self.props.height - h))
    
    -- 取整
    new_x = math.floor(new_x)
    new_y = math.floor(new_y)
    
    -- 记录最后位置
    self._drag_state.last_x = new_x
    self._drag_state.last_y = new_y
    
    -- 移动控件
    main_obj:set_pos(new_x, new_y)
    
    -- 更新选择框位置
    if self._selection_box and self._selected_widget == widget_entry then
        self._selection_box:set_pos(new_x - 2, new_y - 2)
    end
end

-- 控件释放事件
function CanvasArea:_on_widget_released(widget_entry)
    if self._drag_state.widget_entry ~= widget_entry then return end
    
    local was_dragging = self._drag_state.is_dragging
    local instance = widget_entry.instance
    local main_obj = instance.btn or instance.container or instance.obj or instance.chart
    
    if was_dragging and main_obj then
        -- 使用记录的最后位置进行网格对齐
        local final_x = self._drag_state.last_x
        local final_y = self._drag_state.last_y
        
        -- 对齐到网格
        local snapped_x, snapped_y = self:snap_position(final_x, final_y)
        
        -- 限制在画布范围内
        local w = main_obj:get_width()
        local h = main_obj:get_height()
        snapped_x = math.max(0, math.min(snapped_x, self.props.width - w))
        snapped_y = math.max(0, math.min(snapped_y, self.props.height - h))
        
        -- 设置最终位置
        main_obj:set_pos(snapped_x, snapped_y)
        
        -- 更新控件属性
        if instance.props then
            instance.props.x = snapped_x
            instance.props.y = snapped_y
        end
        widget_entry.props.x = snapped_x
        widget_entry.props.y = snapped_y
        
        -- 更新选择框 - 重新创建以确保位置正确
        if self._selected_widget == widget_entry then
            self:_update_selection_box(widget_entry)
        end
        
        print("[画布] 拖拽结束: " .. widget_entry.id .. " 最终位置: (" .. snapped_x .. ", " .. snapped_y .. ")")
        self:_emit("widget_moved", widget_entry)
    end
    
    -- 重置拖拽状态
    self._drag_state.is_dragging = false
    self._drag_state.widget_entry = nil
end

-- 更新选择框位置
function CanvasArea:_update_selection_box(widget_entry)
    if not self._selection_box then return end
    
    local instance = widget_entry.instance
    local main_obj = instance.btn or instance.container or instance.obj or instance.chart
    if not main_obj then return end
    
    local x = main_obj:get_x()
    local y = main_obj:get_y()
    local w = main_obj:get_width()
    local h = main_obj:get_height()
    
    -- 更新选择框位置和大小
    self._selection_box:set_pos(x - 2, y - 2)
    self._selection_box:set_size(w + 4, h + 4)
    
    -- 更新四个角的手柄位置
    local handle_size = 8
    local handle_positions = {
        { x = -handle_size/2, y = -handle_size/2 },
        { x = w - handle_size/2, y = -handle_size/2 },
        { x = -handle_size/2, y = h - handle_size/2 },
        { x = w - handle_size/2, y = h - handle_size/2 },
    }
    
    local child_count = self._selection_box:get_child_count()
    for i = 0, child_count - 1 do
        local handle = self._selection_box:get_child(i)
        if handle and handle_positions[i + 1] then
            handle:set_pos(handle_positions[i + 1].x, handle_positions[i + 1].y)
        end
    end
end

-- 生成唯一ID
function CanvasArea:_generate_id()
    return "widget_" .. os.time() .. "_" .. math.random(1000, 9999)
end

-- 选中控件
function CanvasArea:select_widget(widget_entry)
    -- 取消之前的选中
    self:deselect()
    
    self._selected_widget = widget_entry
    
    -- 创建选择框
    self:_create_selection_box(widget_entry)
    
    -- 触发事件
    self:_emit("widget_selected", widget_entry)
end

-- 取消选中
function CanvasArea:deselect()
    if self._selection_box then
        self._selection_box:delete()
        self._selection_box = nil
    end
    
    local prev_selected = self._selected_widget
    self._selected_widget = nil
    
    if prev_selected then
        self:_emit("widget_deselected", prev_selected)
    end
end

-- 创建选择框
function CanvasArea:_create_selection_box(widget_entry)
    local instance = widget_entry.instance
    local main_obj = instance.btn or instance.container or instance.obj or instance.chart
    if not main_obj then return end
    
    local x = main_obj:get_x()
    local y = main_obj:get_y()
    local w = main_obj:get_width()
    local h = main_obj:get_height()
    
    -- 创建选择框容器
    self._selection_box = lv.obj_create(self.container)
    self._selection_box:set_pos(x - 2, y - 2)
    self._selection_box:set_size(w + 4, h + 4)
    self._selection_box:set_style_bg_opa(0, 0)
    self._selection_box:set_style_border_width(2, 0)
    self._selection_box:set_style_border_color(0x007ACC, 0)
    self._selection_box:set_style_radius(2, 0)
    -- 选择框不可点击，让事件穿透到下面的控件
    self._selection_box:remove_flag(lv.OBJ_FLAG_CLICKABLE)
    self._selection_box:remove_flag(lv.OBJ_FLAG_SCROLLABLE)
    
    -- 创建调整大小的手柄（四个角）- 也不可点击
    local handle_size = 8
    local handle_positions = {
        { x = -handle_size/2, y = -handle_size/2 },
        { x = w - handle_size/2, y = -handle_size/2 },
        { x = -handle_size/2, y = h - handle_size/2 },
        { x = w - handle_size/2, y = h - handle_size/2 },
    }
    
    for _, pos in ipairs(handle_positions) do
        local handle = lv.obj_create(self._selection_box)
        handle:set_pos(pos.x, pos.y)
        handle:set_size(handle_size, handle_size)
        handle:set_style_bg_color(0x007ACC, 0)
        handle:set_style_radius(1, 0)
        handle:set_style_border_width(0, 0)
        handle:remove_flag(lv.OBJ_FLAG_CLICKABLE)
    end
end

-- 删除选中的控件
function CanvasArea:delete_selected()
    if not self._selected_widget then return end
    
    local widget_entry = self._selected_widget
    local instance = widget_entry.instance
    
    -- 删除LVGL对象
    local main_obj = instance.btn or instance.container or instance.obj or instance.chart
    if main_obj then
        main_obj:delete()
    end
    
    -- 从列表中移除
    for i, w in ipairs(self._widgets) do
        if w.id == widget_entry.id then
            table.remove(self._widgets, i)
            break
        end
    end
    
    -- 取消选中
    self:deselect()
    
    -- 触发事件
    self:_emit("widget_deleted", widget_entry)
end

-- 获取所有控件
function CanvasArea:get_widgets()
    return self._widgets
end

-- 获取选中的控件
function CanvasArea:get_selected()
    return self._selected_widget
end

-- 获取容器
function CanvasArea:get_container()
    return self.container
end

-- 导出画布状态（用于保存）
function CanvasArea:export_state()
    local state = {
        widgets = {}
    }
    
    for _, w in ipairs(self._widgets) do
        local widget_state = {
            id = w.id,
            type = w.module.__widget_meta and w.module.__widget_meta.id or "unknown",
            props = w.instance:to_state()
        }
        table.insert(state.widgets, widget_state)
    end
    
    return state
end

-- 清空画布
function CanvasArea:clear()
    for _, w in ipairs(self._widgets) do
        local instance = w.instance
        
        -- 删除控件
        local main_obj = instance.btn or instance.container or instance.obj or instance.chart
        if main_obj then
            main_obj:delete()
        end
    end
    
    self._widgets = {}
    self:deselect()
    
    self:_emit("canvas_cleared")
end

-- 切换网格显示
function CanvasArea:toggle_grid()
    self.props.show_grid = not self.props.show_grid
    self:_refresh_grid()
    return self.props.show_grid
end

-- 设置网格显示状态
function CanvasArea:set_show_grid(show)
    if self.props.show_grid ~= show then
        self.props.show_grid = show
        self:_refresh_grid()
    end
end

-- 获取网格显示状态
function CanvasArea:is_grid_visible()
    return self.props.show_grid
end

-- 切换对齐到网格
function CanvasArea:toggle_snap_to_grid()
    self.props.snap_to_grid = not self.props.snap_to_grid
    return self.props.snap_to_grid
end

-- 设置对齐到网格状态
function CanvasArea:set_snap_to_grid(snap)
    self.props.snap_to_grid = snap
end

-- 获取对齐到网格状态
function CanvasArea:is_snap_to_grid()
    return self.props.snap_to_grid
end

-- 刷新网格显示
function CanvasArea:_refresh_grid()
    -- 删除现有网格线（网格线是没有 CLICKABLE 标志的子元素）
    local children_to_delete = {}
    local child_count = self.container:get_child_count()
    
    for i = 0, child_count - 1 do
        local child = self.container:get_child(i)
        if child then
            -- 检查是否是网格线（宽度为1或高度为1，且不可点击）
            local w = child:get_width()
            local h = child:get_height()
            if (w == 1 or h == 1) and not child:has_flag(lv.OBJ_FLAG_CLICKABLE) then
                table.insert(children_to_delete, child)
            end
        end
    end
    
    -- 删除网格线
    for _, child in ipairs(children_to_delete) do
        child:delete()
    end
    
    -- 如果需要显示网格，重新绘制
    if self.props.show_grid then
        self:_draw_grid()
    end
end

-- 从工具箱接收放置
function CanvasArea:handle_drop(widget_module, drop_x, drop_y)
    -- 直接使用传入的坐标作为画布内坐标
    local canvas_x = drop_x
    local canvas_y = drop_y
    
    -- 限制在画布范围内
    canvas_x = math.max(0, math.min(canvas_x, self.props.width - 50))
    canvas_y = math.max(0, math.min(canvas_y, self.props.height - 50))
    
    -- 添加控件
    return self:add_widget(widget_module, { x = canvas_x, y = canvas_y })
end

-- 对齐操作
function CanvasArea:align_selected(align_type)
    if not self._selected_widget then return end
    
    local instance = self._selected_widget.instance
    local main_obj = instance.btn or instance.container or instance.obj or instance.chart
    if not main_obj then return end
    
    local w = main_obj:get_width()
    local h = main_obj:get_height()
    local new_x, new_y = main_obj:get_x(), main_obj:get_y()
    
    if align_type == "center_h" then
        new_x = math.floor((self.props.width - w) / 2)
    elseif align_type == "center_v" then
        new_y = math.floor((self.props.height - h) / 2)
    elseif align_type == "left" then
        new_x = 0
    elseif align_type == "right" then
        new_x = self.props.width - w
    elseif align_type == "top" then
        new_y = 0
    elseif align_type == "bottom" then
        new_y = self.props.height - h
    end
    
    new_x, new_y = self:snap_position(new_x, new_y)
    main_obj:set_pos(new_x, new_y)
    
    if instance.props then
        instance.props.x = new_x
        instance.props.y = new_y
    end
    self._selected_widget.props.x = new_x
    self._selected_widget.props.y = new_y
    
    -- 更新选择框
    if self._selection_box then
        self._selection_box:set_pos(new_x - 2, new_y - 2)
    end
    
    self:_emit("widget_moved", self._selected_widget)
end

return CanvasArea
