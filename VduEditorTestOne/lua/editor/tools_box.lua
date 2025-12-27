-- tools_box.lua
-- 浮动工具箱：悬浮在画布上方
local lv = require("lvgl")

local ToolsBox = {}
ToolsBox.__index = ToolsBox

ToolsBox.__widget_meta = {
    id = "tools_box",
    name = "Tools Box",
    description = "浮动工具箱，悬浮在画布上方",
    schema_version = "1.0",
    version = "1.0",
}

-- 默认工具列表
ToolsBox.DEFAULT_TOOLS = {
    { id = "button", name = "按钮", icon = "BTN", module_path = "widgets.button" },
    { id = "valve", name = "阀门", icon = "VLV", module_path = "widgets.valve" },
    { id = "trend_chart", name = "趋势图", icon = "CHT", module_path = "widgets.trend_chart" },
}

-- 构造函数
function ToolsBox.new(parent, props)
    props = props or {}
    local self = setmetatable({}, ToolsBox)
    
    -- 属性
    self.props = {
        x = props.x or 10,
        y = props.y or 50,
        width = props.width or 180,
        title_height = props.title_height or 28,
        item_height = props.item_height or 45,
        bg_color = props.bg_color or 0x2D2D2D,
        title_bg_color = props.title_bg_color or 0x3D3D3D,
        border_color = props.border_color or 0x555555,
        text_color = props.text_color or 0xFFFFFF,
        visible = props.visible ~= false,
    }
    
    -- 保存父元素引用（屏幕）
    self._parent = parent
    
    -- 工具列表
    self._tools = props.tools or ToolsBox.DEFAULT_TOOLS
    
    -- 模块缓存
    self._loaded_modules = {}
    
    -- 事件监听器
    self._event_listeners = {}
    
    -- 计算高度
    local content_height = #self._tools * self.props.item_height
    local total_height = self.props.title_height + content_height + 8
    
    -- 创建主容器（浮动窗口样式）
    self.container = lv.obj_create(parent)
    self.container:set_pos(self.props.x, self.props.y)
    self.container:set_size(self.props.width, total_height)
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
    
    -- 标题文本
    self.title_label = lv.label_create(self.title_bar)
    self.title_label:set_text("工具箱")
    self.title_label:set_style_text_color(self.props.text_color, 0)
    self.title_label:align(lv.ALIGN_LEFT_MID, 8, 0)
    
    -- 隐藏按钮 (X)
    local this = self
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
    
    -- 创建内容区域
    self.content = lv.obj_create(self.container)
    self.content:set_pos(0, self.props.title_height)
    self.content:set_size(self.props.width, content_height + 8)
    self.content:set_style_bg_opa(0, 0)
    self.content:set_style_border_width(0, 0)
    self.content:set_style_text_color(self.props.text_color, 0)
    self.content:set_style_pad_all(0, 0)
    self.content:remove_flag(lv.OBJ_FLAG_SCROLLABLE)
    self.content:clear_layout()
    
    -- 创建工具项
    self:_create_tool_items()
    
    return self
end

-- 事件订阅
function ToolsBox:on(event_name, callback)
    if not self._event_listeners[event_name] then
        self._event_listeners[event_name] = {}
    end
    table.insert(self._event_listeners[event_name], callback)
end

-- 触发事件
function ToolsBox:_emit(event_name, ...)
    local listeners = self._event_listeners[event_name]
    if listeners then
        for _, cb in ipairs(listeners) do
            local ok, err = pcall(cb, self, ...)
            if not ok then
                print("[工具箱] 事件回调错误:", err)
            end
        end
    end
end

-- 创建工具项
function ToolsBox:_create_tool_items()
    local y_offset = 4
    
    for i, tool in ipairs(self._tools) do
        self:_create_tool_item(tool, y_offset)
        y_offset = y_offset + self.props.item_height
    end
end

-- 创建单个工具项
function ToolsBox:_create_tool_item(tool, y_offset)
    local item_width = self.props.width - 8
    local item_height = self.props.item_height - 4
    
    local item_container = lv.obj_create(self.content)
    item_container:set_pos(4, y_offset)
    item_container:set_size(item_width, item_height)
    item_container:set_style_bg_color(0x404040, 0)
    item_container:set_style_radius(4, 0)
    item_container:set_style_border_width(0, 0)
    item_container:set_style_text_color(self.props.text_color, 0)
    item_container:set_style_pad_all(0, 0)
    item_container:remove_flag(lv.OBJ_FLAG_SCROLLABLE)
    item_container:remove_flag(lv.OBJ_FLAG_GESTURE_BUBBLE)
    item_container:clear_layout()
    
    -- 图标区域
    local icon_box = lv.obj_create(item_container)
    icon_box:set_pos(4, 4)
    icon_box:set_size(32, 32)
    icon_box:set_style_bg_color(0x505050, 0)
    icon_box:set_style_radius(4, 0)
    icon_box:set_style_border_width(0, 0)
    icon_box:set_style_text_color(self.props.text_color, 0)
    icon_box:set_style_pad_all(0, 0)
    icon_box:remove_flag(lv.OBJ_FLAG_SCROLLABLE)
    icon_box:remove_flag(lv.OBJ_FLAG_CLICKABLE)
    
    local icon_label = lv.label_create(icon_box)
    icon_label:set_text(tool.icon or "?")
    icon_label:set_style_text_color(self.props.text_color, 0)
    icon_label:center()
    
    -- 名称
    local name_label = lv.label_create(item_container)
    name_label:set_text(tool.name)
    name_label:set_style_text_color(self.props.text_color, 0)
    name_label:align(lv.ALIGN_LEFT_MID, 42, 0)
    
    -- 点击事件：直接创建控件到画布
    local this = self
    local tool_ref = tool
    
    item_container:add_event_cb(function(e)
        this:_on_tool_clicked(tool_ref)
    end, lv.EVENT_CLICKED, nil)
    
    return item_container
end

-- 工具项点击 - 直接在画布创建控件
function ToolsBox:_on_tool_clicked(tool)
    print("[工具箱] 点击工具: " .. tool.name)
    
    -- 加载模块
    local module = self:_load_module(tool.module_path)
    if module then
        -- 触发工具放置事件
        local drop_x = 300
        local drop_y = 200
        print("[工具箱] 放置工具: " .. tool.name .. " @ (" .. drop_x .. ", " .. drop_y .. ")")
        self:_emit("tool_dropped", tool, module, drop_x, drop_y)
    end
end

-- 加载工具模块
function ToolsBox:_load_module(module_path)
    if self._loaded_modules[module_path] then
        return self._loaded_modules[module_path]
    end
    
    local ok, module = pcall(require, module_path)
    if ok then
        self._loaded_modules[module_path] = module
        print("[工具箱] 模块加载成功: " .. module_path)
        return module
    else
        print("[工具箱] 加载模块失败: " .. module_path .. " - " .. tostring(module))
        return nil
    end
end

-- 显示工具箱
function ToolsBox:show()
    self.props.visible = true
    self.container:remove_flag(lv.OBJ_FLAG_HIDDEN)
    self:_emit("visibility_changed", true)
    print("[工具箱] 显示")
end

-- 隐藏工具箱
function ToolsBox:hide()
    self.props.visible = false
    self.container:add_flag(lv.OBJ_FLAG_HIDDEN)
    self:_emit("visibility_changed", false)
    print("[工具箱] 隐藏")
end

-- 切换显示/隐藏
function ToolsBox:toggle()
    if self.props.visible then
        self:hide()
    else
        self:show()
    end
end

-- 是否可见
function ToolsBox:is_visible()
    return self.props.visible
end

-- 设置位置
function ToolsBox:set_pos(x, y)
    self.props.x = x
    self.props.y = y
    self.container:set_pos(x, y)
end

-- 获取容器
function ToolsBox:get_container()
    return self.container
end

-- 添加自定义工具
function ToolsBox:add_tool(tool_def)
    table.insert(self._tools, tool_def)
end

-- 获取所有工具
function ToolsBox:get_tools()
    return self._tools
end

return ToolsBox
