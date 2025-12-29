-- PropertyArea.lua
-- 浮动属性窗口：样式参考工具箱，但不包含工具列表内容
local lv = require("lvgl")

local PropertyArea = {}
PropertyArea.__index = PropertyArea

PropertyArea.__widget_meta = {
    id = "property_area",
    name = "属性窗口",
    description = "浮动属性窗口，显示所选控件属性",
    schema_version = "1.0",
    version = "1.0",
}

function PropertyArea.new(parent, props)
    props = props or {}
    local self = setmetatable({}, PropertyArea)

    self.props = {
        x = props.x or 820,
        y = props.y or 50,
        width = props.width or 400,
        title_height = props.title_height or 28,
        content_height = props.content_height or 300,
        bg_color = props.bg_color or 0x2D2D2D,
        title_bg_color = props.title_bg_color or 0x3D3D3D,
        border_color = props.border_color or 0x555555,
        text_color = props.text_color or 0xFFFFFF,
        visible = props.visible ~= false,
        collapsed = props.collapsed or false,
    }

    self._parent = parent
    self._event_listeners = {}

    -- 拖拽状态
    self._drag_state = {
        is_dragging = false,
        start_x = 0,
        start_y = 0,
        start_mouse_x = 0,
        start_mouse_y = 0,
    }

    -- 主容器
    self.container = lv.obj_create(parent)
    self.container:set_pos(self.props.x, self.props.y)
    self.container:set_size(self.props.width, self.props.title_height + self.props.content_height)
    self.container:set_style_bg_color(self.props.bg_color, 0)
    self.container:set_style_bg_opa(240, 0)
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

    -- 标题栏
    self.title_bar = lv.obj_create(self.container)
    self.title_bar:set_pos(0, 0)
    self.title_bar:set_size(self.props.width, self.props.title_height)
    self.title_bar:set_style_bg_color(self.props.title_bg_color, 0)
    self.title_bar:set_style_radius(6, 0)
    self.title_bar:set_style_text_color(self.props.text_color, 0)
    self.title_bar:set_style_pad_all(0, 0)
    self.title_bar:remove_flag(lv.OBJ_FLAG_SCROLLABLE)
    self.title_bar:clear_layout()

    -- 标题文本
    self.title_label = lv.label_create(self.title_bar)
    self.title_label:set_text("属性窗口")
    self.title_label:set_style_text_color(self.props.text_color, 0)
    self.title_label:align(lv.ALIGN_LEFT_MID, 8, 0)

    -- 隐藏按钮
    self.hide_btn = lv.obj_create(self.title_bar)
    self.hide_btn:set_size(20, 20)
    self.hide_btn:align(lv.ALIGN_RIGHT_MID, -4, 0)
    self.hide_btn:set_style_bg_color(0x555555, 0)
    self.hide_btn:set_style_radius(3, 0)
    self.hide_btn:set_style_pad_all(0, 0)
    self.hide_btn:remove_flag(lv.OBJ_FLAG_SCROLLABLE)

    local hide_label = lv.label_create(self.hide_btn)
    hide_label:set_text("X")
    hide_label:set_style_text_color(self.props.text_color, 0)
    hide_label:center()

    local this = self
    self.hide_btn:add_event_cb(function(e)
        this:hide()
    end, lv.EVENT_CLICKED, nil)

    -- 标题栏拖拽支持
    self.title_bar:add_event_cb(function(e)
        this:_on_title_pressed()
    end, lv.EVENT_PRESSED, nil)
    self.title_bar:add_event_cb(function(e)
        this:_on_title_pressing()
    end, lv.EVENT_PRESSING, nil)
    self.title_bar:add_event_cb(function(e)
        this:_on_title_released()
    end, lv.EVENT_RELEASED, nil)

    -- 内容区域（为空，由使用者填充）
    self.content = lv.obj_create(self.container)
    self.content:set_pos(0, self.props.title_height)
    self.content:set_size(self.props.width, self.props.content_height)
    self.content:set_style_bg_opa(0, 0)
    self.content:set_style_pad_all(6, 0)
    self.content:remove_flag(lv.OBJ_FLAG_SCROLLABLE)
    self.content:clear_layout()

    if not self.props.visible then
        self.container:add_flag(lv.OBJ_FLAG_HIDDEN)
    end

    return self
end

function PropertyArea:_on_title_pressed()
    local mouse_x = lv.get_mouse_x()
    local mouse_y = lv.get_mouse_y()

    self._drag_state.is_dragging = false
    self._drag_state.start_x = self.props.x
    self._drag_state.start_y = self.props.y
    self._drag_state.start_mouse_x = mouse_x
    self._drag_state.start_mouse_y = mouse_y
end

function PropertyArea:_on_title_pressing()
    local mouse_x = lv.get_mouse_x()
    local mouse_y = lv.get_mouse_y()

    local delta_x = mouse_x - self._drag_state.start_mouse_x
    local delta_y = mouse_y - self._drag_state.start_mouse_y

    if not self._drag_state.is_dragging then
        if math.abs(delta_x) > 3 or math.abs(delta_y) > 3 then
            self._drag_state.is_dragging = true
        else
            return
        end
    end

    local new_x = self._drag_state.start_x + delta_x
    local new_y = self._drag_state.start_y + delta_y
    new_x = math.max(0, new_x)
    new_y = math.max(0, new_y)

    self.props.x = new_x
    self.props.y = new_y
    self.container:set_pos(math.floor(new_x), math.floor(new_y))
end

function PropertyArea:_on_title_released()
    self._drag_state.is_dragging = false
end

function PropertyArea:on(event_name, callback)
    if not self._event_listeners[event_name] then
        self._event_listeners[event_name] = {}
    end
    table.insert(self._event_listeners[event_name], callback)
end

function PropertyArea:_emit(event_name, ...)
    local listeners = self._event_listeners[event_name]
    if listeners then
        for _, cb in ipairs(listeners) do
            local ok, err = pcall(cb, self, ...)
            if not ok then
                print("[PropertyArea] 事件回调错误:", err)
            end
        end
    end
end

function PropertyArea:show()
    self.props.visible = true
    self.container:remove_flag(lv.OBJ_FLAG_HIDDEN)
    self:_emit("visibility_changed", true)
    print("[PropertyArea] 显示")
end

function PropertyArea:hide()
    self.props.visible = false
    self.container:add_flag(lv.OBJ_FLAG_HIDDEN)
    self:_emit("visibility_changed", false)
    print("[PropertyArea] 隐藏")
end

function PropertyArea:toggle()
    if self.props.visible then
        self:hide()
    else
        self:show()
    end
end

function PropertyArea:is_visible()
    return self.props.visible
end

function PropertyArea:set_pos(x, y)
    self.props.x = x
    self.props.y = y
    self.container:set_pos(x, y)
end

function PropertyArea:get_pos()
    return self.props.x, self.props.y
end

function PropertyArea:get_container()
    return self.container
end

-- 清空内容区域的子元素
function PropertyArea:clear_properties()
    local child_count = self.content:get_child_count()
    for i = child_count - 1, 0, -1 do
        local child = self.content:get_child(i)
        if child then
            pcall(function() child:delete() end)
        end
    end
end

-- 将属性表显示在属性窗口中（简单的只读展示）
function PropertyArea:set_properties(props_table)
    if not props_table then return end
    -- 清空现有内容
    self:clear_properties()

    local y_offset = 4
    local row_height = 22
    for k, v in pairs(props_table) do
        local row = lv.obj_create(self.content)
        row:set_pos(4, y_offset)
        row:set_size(self.props.width - 8, row_height)
        row:set_style_bg_opa(0, 0)
        row:set_style_pad_all(0, 0)
        row:remove_flag(lv.OBJ_FLAG_SCROLLABLE)
        row:clear_layout()

        local name_lbl = lv.label_create(row)
        name_lbl:set_text(tostring(k) .. ":")
        name_lbl:set_style_text_color(self.props.text_color, 0)
        name_lbl:align(lv.ALIGN_LEFT_MID, 4, 0)

        local val_lbl = lv.label_create(row)
        val_lbl:set_text(tostring(v))
        val_lbl:set_style_text_color(0xAAAAAA, 0)
        val_lbl:align(lv.ALIGN_RIGHT_MID, -4, 0)

        y_offset = y_offset + row_height
    end
end

return PropertyArea
