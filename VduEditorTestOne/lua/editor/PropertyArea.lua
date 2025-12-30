-- PropertyArea.lua
-- 浮动属性窗口：样式参考属性窗口，但不包含工具列表内容
local lv = require("lvgl")

-- 获取屏幕
local scr = lv.scr_act()

local PropertyArea = {}
PropertyArea.__index = PropertyArea

PropertyArea.__widget_meta = {
    id = "property_area",
    name = "属性窗口",
    description = "浮动属性窗口，显示所选控件属性",
    schema_version = "1.0",
    version = "1.0",
}
local selectedItem = nil
local selectedItems ={}
-- 构造函数
function PropertyArea.new(parent, props)
    props = props or {}
    local self = setmetatable({}, PropertyArea)
    
    -- 属性
    self.props = {
        x = props.x or 800,
        y = props.y or 50,
        width = props.width or 260,
        title_height = props.title_height or 28,
        item_height = props.item_height or 32,
        bg_color = props.bg_color or 0x2D2D2D,
        title_bg_color = props.title_bg_color or 0x3D3D3D,
        border_color = props.border_color or 0x555555,
        text_color = props.text_color or 0xFFFFFF,
        visible = props.visible or false,
        collapsed = props.collapsed or false,  -- 折叠状态
    }
    
    -- 保存父元素引用（屏幕）
    self._parent = parent
    
    -- 工具列表
    self._tools = props.tools or PropertyArea.DEFAULT_TOOLS
    
    -- 模块缓存
    self._loaded_modules = {}
    
    -- 事件监听器
    self._event_listeners = {}
    
    -- 标题栏拖拽状态
    self._drag_state = {
        is_dragging = false,
        start_x = 0,
        start_y = 0,
        start_mouse_x = 0,
        start_mouse_y = 0,
    }
    
    -- 工具拖拽状态（拖拽工具到画布）
    self._tool_drag_state = {
        is_dragging = false,
        tool = nil,
        module = nil,
        ghost = nil,  -- 拖拽时显示的幽灵预览
        start_mouse_x = 0,
        start_mouse_y = 0,
    }
    self._content_height = 200  -- 内容区域高度（可根据需要调整）
    -- 创建主容器（浮动窗口样式）
    self.container = lv.obj_create(parent)
    self.container:set_pos(self.props.x, self.props.y)
    self.container:set_size(self.props.width, self._content_height)
    self.container:set_style_bg_color(self.props.bg_color, 0)
    self.container:set_style_bg_opa(240, 0)  -- 略微透明
    self.container:set_style_radius(6, 0)
    self.container:set_style_border_width(1, 0)
    self.container:set_style_border_color(self.props.border_color, 0)
    self.container:set_style_shadow_width(8, 0)
    self.container:set_style_shadow_color(0x000000, 0)
    self.container:set_style_shadow_opa(100, 0)
    self.container:set_style_text_color(self.props.text_color, 0)
    self.container:set_style_pad_all(0, 0)
    self.container:remove_flag(lv.OBJ_FLAG_SCROLLABLE)
    self.container:remove_flag(lv.OBJ_FLAG_GESTURE_BUBBLE)
    self.container:clear_layout()
    
    -- 创建标题栏
    self:_create_title_bar()
    
    -- 创建内容区域
    self:_create_content_area()
    
    -- 如果初始状态是折叠的，则折叠
    if self.props.collapsed then
        self:_apply_collapsed_state()
    end
    
    return self
end

-- 创建标题栏
function PropertyArea:_create_title_bar()
    local this = self
    
    self.title_bar = lv.obj_create(self.container)
    self.title_bar:set_pos(0, 0)
    self.title_bar:set_size(self.props.width, self.props.title_height)
    self.title_bar:set_style_bg_color(self.props.title_bg_color, 0)
    self.title_bar:set_style_radius(6, 0)
    self.title_bar:set_style_border_width(0, 0)
    self.title_bar:set_style_text_color(self.props.text_color, 0)
    self.title_bar:set_style_pad_all(0, 0)
    self.title_bar:remove_flag(lv.OBJ_FLAG_SCROLLABLE)
    self.title_bar:clear_layout()
    
    -- 折叠按钮 (▼/▶)
    self.collapse_btn = lv.obj_create(self.title_bar)
    self.collapse_btn:set_size(20, 20)
    self.collapse_btn:set_pos(4, 4)
    self.collapse_btn:set_style_bg_color(0x505050, 0)
    self.collapse_btn:set_style_radius(3, 0)
    self.collapse_btn:set_style_border_width(0, 0)
    self.collapse_btn:set_style_pad_all(0, 0)
    self.collapse_btn:remove_flag(lv.OBJ_FLAG_SCROLLABLE)
    
    self.collapse_label = lv.label_create(self.collapse_btn)
    self.collapse_label:set_text(self.props.collapsed and "+" or "-")
    self.collapse_label:set_style_text_color(self.props.text_color, 0)
    self.collapse_label:center()
    
    -- 折叠按钮事件
    self.collapse_btn:add_event_cb(function(e)
        this:toggle_collapse()
    end, lv.EVENT_CLICKED, nil)
    
    -- 标题文本
    self.title_label = lv.label_create(self.title_bar)
    self.title_label:set_text("属性窗口")
    self.title_label:set_style_text_color(self.props.text_color, 0)
    self.title_label:align(lv.ALIGN_LEFT_MID, 28, 0)
    
    -- 隐藏按钮 (X)
    self.hide_btn = lv.obj_create(self.title_bar)
    self.hide_btn:set_size(20, 20)
    self.hide_btn:align(lv.ALIGN_RIGHT_MID, -4, 0)
    self.hide_btn:set_style_bg_color(0x555555, 0)
    self.hide_btn:set_style_radius(3, 0)
    self.hide_btn:set_style_border_width(0, 0)
    self.hide_btn:set_style_pad_all(0, 0)
    self.hide_btn:remove_flag(lv.OBJ_FLAG_SCROLLABLE)
    
    local hide_label = lv.label_create(self.hide_btn)
    hide_label:set_text("X")
    hide_label:set_style_text_color(self.props.text_color, 0)
    hide_label:center()
    
    -- 隐藏按钮事件
    self.hide_btn:add_event_cb(function(e)
        this:hide()
    end, lv.EVENT_CLICKED, nil)
    
    -- 标题栏拖拽事件
    self.title_bar:add_event_cb(function(e)
        this:_on_title_pressed()
    end, lv.EVENT_PRESSED, nil)
    
    self.title_bar:add_event_cb(function(e)
        this:_on_title_pressing()
    end, lv.EVENT_PRESSING, nil)
    
    self.title_bar:add_event_cb(function(e)
        this:_on_title_released()
    end, lv.EVENT_RELEASED, nil)
end

-- 创建内容区域
function PropertyArea:_create_content_area()
    self.content = lv.obj_create(self.container)
    self.content:set_pos(0, self.props.title_height)
    self.content:set_size(self.props.width, self._content_height)
    self.content:set_style_bg_opa(0, 0)
    self.content:set_style_border_width(0, 0)
    self.content:set_style_text_color(self.props.text_color, 0)
    self.content:set_style_pad_all(0, 0)
    self.content:remove_flag(lv.OBJ_FLAG_SCROLLABLE)
    self.content:clear_layout()
end

-- 标题栏按下事件
function PropertyArea:_on_title_pressed()
    local mouse_x = lv.get_mouse_x()
    local mouse_y = lv.get_mouse_y()
    
    self._drag_state.is_dragging = false
    self._drag_state.start_x = self.props.x
    self._drag_state.start_y = self.props.y
    self._drag_state.start_mouse_x = mouse_x
    self._drag_state.start_mouse_y = mouse_y
end

-- 标题栏拖动事件
function PropertyArea:_on_title_pressing()
    local mouse_x = lv.get_mouse_x()
    local mouse_y = lv.get_mouse_y()
    
    local delta_x = mouse_x - self._drag_state.start_mouse_x
    local delta_y = mouse_y - self._drag_state.start_mouse_y
    
    -- 检查是否开始拖拽
    if not self._drag_state.is_dragging then
        if math.abs(delta_x) > 3 or math.abs(delta_y) > 3 then
            self._drag_state.is_dragging = true
        else
            return
        end
    end
    
    -- 计算新位置
    local new_x = self._drag_state.start_x + delta_x
    local new_y = self._drag_state.start_y + delta_y
    
    -- 限制在屏幕范围内
    new_x = math.max(0, new_x)
    new_y = math.max(0, new_y)
    
    -- 更新位置
    self.props.x = new_x
    self.props.y = new_y
    self.container:set_pos(math.floor(new_x), math.floor(new_y))
end

-- 标题栏释放事件
function PropertyArea:_on_title_released()
    if self._drag_state.is_dragging then
        self:_emit("position_changed", self.props.x, self.props.y)
    end
    self._drag_state.is_dragging = false
end

-- 事件订阅
function PropertyArea:on(event_name, callback)
    if not self._event_listeners[event_name] then
        self._event_listeners[event_name] = {}
    end
    table.insert(self._event_listeners[event_name], callback)
end

-- 触发事件
function PropertyArea:_emit(event_name, ...)
    local listeners = self._event_listeners[event_name]
    if listeners then
        for _, cb in ipairs(listeners) do
            local ok, err = pcall(cb, self, ...)
            if not ok then
                print("[属性窗口] 事件回调错误:", err)
            end
        end
    end
end


-- 折叠/展开
function PropertyArea:toggle_collapse()
    self.props.collapsed = not self.props.collapsed
    self:_apply_collapsed_state()
    self:_emit("collapse_changed", self.props.collapsed)
end

-- 应用折叠状态
function PropertyArea:_apply_collapsed_state()
    if self.props.collapsed then
        -- 折叠：只显示标题栏
        self.content:add_flag(lv.OBJ_FLAG_HIDDEN)
        print(self.props.title_height)
        self.container:set_height(self.props.title_height)
        self.collapse_label:set_text("+")
    else
        -- 展开：显示全部
        self.content:remove_flag(lv.OBJ_FLAG_HIDDEN)
        print(self._content_height)
        self.container:set_height(self._content_height)
        self.collapse_label:set_text("-")
    end
end

-- 折叠
function PropertyArea:collapse()
    if not self.props.collapsed then
        self:toggle_collapse()
    end
end

-- 展开
function PropertyArea:expand()
    if self.props.collapsed then
        self:toggle_collapse()
    end
end

-- 是否折叠
function PropertyArea:is_collapsed()
    return self.props.collapsed
end

-- 显示属性窗口
function PropertyArea:show()
    self.props.visible = true
    self.container:remove_flag(lv.OBJ_FLAG_HIDDEN)
    self:_emit("visibility_changed", true)
    print("[属性窗口] 显示")
end

-- 隐藏属性窗口
function PropertyArea:hide()
    self.props.visible = false
    self.container:add_flag(lv.OBJ_FLAG_HIDDEN)
    self:_emit("visibility_changed", false)
    print("[属性窗口] 隐藏")
end

-- 切换显示/隐藏
function PropertyArea:toggle()
    if self.props.visible then
        self:hide()
    else
        self:show()
    end
end

-- 是否可见
function PropertyArea:is_visible()
    return self.props.visible
end

-- 设置位置
function PropertyArea:set_pos(x, y)
    self.props.x = x
    self.props.y = y
    self.container:set_pos(x, y)
end

-- 获取位置
function PropertyArea:get_pos()
    return self.props.x, self.props.y
end

-- 获取容器
function PropertyArea:get_container()
    return self.container
end

-- 添加自定义工具
function PropertyArea:add_tool(tool_def)
    table.insert(self._tools, tool_def)
end

-- 获取所有工具
function PropertyArea:get_tools()
    return self._tools
end

-- 检查是否正在拖拽工具
function PropertyArea:is_dragging_tool()
    return self._tool_drag_state.is_dragging
end

-- 获取当前拖拽的工具
function PropertyArea:get_dragging_tool()
    if self._tool_drag_state.is_dragging then
        return self._tool_drag_state.tool, self._tool_drag_state.module
    end
    return nil, nil
end
function PropertyArea:onSelectedItem(item)
    if item == nil then
        print("[属性窗口] 取消选中控件")
        selectedItem = nil
        selectedItems = {}
        return
    end
    
    -- item 可能是单个 widget_entry 或多个 widget_entries 列表
    if type(item) == "table" and item.instance then
        -- 单个选中
        selectedItem = item
        selectedItems = { item }
        
        local instance = item.instance
        if not instance then
            print("[属性窗口] 错误：选中项没有 instance")
            return
        end
        
        -- 调用实例的 get_properties 方法获取属性
        if instance.get_properties then
            local properties = instance:get_properties()
            print("[属性窗口] 选中控件属性:")
            for key, value in pairs(properties) do
                print("  " .. key .. " = " .. tostring(value))
            end
            return properties
        else
            print("[属性窗口] 错误：实例没有 get_properties 方法")
            return nil
        end
    elseif type(item) == "table" then
        -- 多个选中
        selectedItems = item
        selectedItem = nil
        
        print("[属性窗口] 多个控件已选中，共 " .. #item .. " 个")
        return nil
    end
end
return PropertyArea
