-- main_editor.lua
-- 主编辑器入口：整合菜单栏、画布、工具箱
local lv = require("lvgl")

-- 加载编辑器组件
local MenuBar = require("MenuBar")
local CanvasArea = require("CanvasArea")
local ToolsBox = require("tools_box")

-- 获取屏幕
local scr = lv.scr_act()

-- 重要：清除屏幕的 flex 布局（Win32Window 默认设置了 flex column 布局）
scr:clear_layout()

-- 设置屏幕背景色
scr:set_style_bg_color(0x1E1E1E, 0)

-- 禁用屏幕滚动，防止拖动时整个画面移动
scr:remove_flag(lv.OBJ_FLAG_SCROLLABLE)

-- 重置屏幕的 padding（Win32Window 默认设置了 10）
scr:set_style_pad_all(0, 0)

-- 设置默认文字颜色为白色
scr:set_style_text_color(0xFFFFFF, 0)

print("=== VDU 编辑器启动 ===")

-- 定义布局尺寸
local WINDOW_WIDTH = 1024
local WINDOW_HEIGHT = 768
local MENUBAR_HEIGHT = 36
local STATUSBAR_HEIGHT = 23

-- ========== 创建菜单栏 ==========
local menu_bar = MenuBar.new(scr, {
    x = 0,
    y = 0,
    width = WINDOW_WIDTH,
    height = MENUBAR_HEIGHT,
})

-- ========== 创建画布（占满整个工作区域）==========
local canvas = CanvasArea.new(scr, {
    x = 0,
    y = MENUBAR_HEIGHT,
    width = WINDOW_WIDTH,
    height = WINDOW_HEIGHT - MENUBAR_HEIGHT - STATUSBAR_HEIGHT,
    show_grid = true,
    snap_to_grid = true,
    grid_size = 20,
})

-- ========== 创建浮动工具箱（在画布上方）==========
local toolbox = ToolsBox.new(scr, {
    x = 10,
    y = MENUBAR_HEIGHT + 10,
    width = 130,
})

-- 创建属性窗口
local PropertyArea = require("PropertyArea")
local property_area = PropertyArea.new(scr, {
    x = 820,
    y = MENUBAR_HEIGHT + 10,
    width = 200,
    visible = false,
})

-- 同步菜单栏状态与画布/工具箱/属性窗口状态
menu_bar:set_state("show_grid", canvas:is_grid_visible())
menu_bar:set_state("snap_to_grid", canvas:is_snap_to_grid())
menu_bar:set_state("show_toolbox", toolbox:is_visible())
menu_bar:set_state("show_properties", property_area:is_visible())

-- 状态栏标签引用（后面会创建）
local status_label = nil

-- 更新状态栏
local function update_status_bar()
    if status_label then
        local grid_status = canvas:is_grid_visible() and "显示" or "隐藏"
        local snap_status = canvas:is_snap_to_grid() and "开启" or "关闭"
        status_label:set_text("就绪 | 网格: " .. grid_status .. " | 对齐: " .. snap_status .. " | 网格大小: " .. canvas.props.grid_size .. "px")
    end
end

-- 菜单事件处理
menu_bar:on("menu_action", function(self, menu_key, item_id)
    print("[菜单] " .. menu_key .. " -> " .. item_id)
    
    if item_id == "new" then
        canvas:clear()
        print("新建画布")
    elseif item_id == "save" then
        local state = canvas:export_state()
        print("保存画布状态:")
        for i, w in ipairs(state.widgets) do
            print("  - " .. w.type .. " @ (" .. w.props.x .. ", " .. w.props.y .. ")")
        end
    elseif item_id == "delete" then
        canvas:delete_selected()
    elseif item_id == "align_left" then
        canvas:align_selected("left")
    elseif item_id == "align_center" then
        canvas:align_selected("center_h")
    elseif item_id == "align_right" then
        canvas:align_selected("right")
    elseif item_id == "align_top" then
        canvas:align_selected("top")
    elseif item_id == "align_middle" then
        canvas:align_selected("center_v")
    elseif item_id == "align_bottom" then
        canvas:align_selected("bottom")
    elseif item_id == "show_grid" then
        -- 切换网格显示
        local new_state = canvas:toggle_grid()
        menu_bar:set_state("show_grid", new_state)
        print("网格显示: " .. tostring(new_state))
        update_status_bar()
    elseif item_id == "snap_to_grid" then
        -- 切换对齐到网格
        local new_state = canvas:toggle_snap_to_grid()
        menu_bar:set_state("snap_to_grid", new_state)
        print("对齐到网格: " .. tostring(new_state))
        update_status_bar()
    elseif item_id == "show_toolbox" then
        -- 切换工具箱显示/隐藏
        toolbox:toggle()
        menu_bar:set_state("show_toolbox", toolbox:is_visible())
    elseif item_id == "show_properties" then
        -- 切换属性窗口显示/隐藏
        property_area:toggle()
        menu_bar:set_state("show_properties", property_area:is_visible())
    elseif item_id == "exit" then
        print("退出编辑器")
    end
end)

-- 画布事件处理
canvas:on("widget_added", function(self, widget_entry)
    print("[画布] 添加控件: " .. widget_entry.id)
end)

canvas:on("widget_selected", function(self, widget_entry)
    print("[画布] 选中控件: " .. widget_entry.id)
    -- 获取所选控件的实例并调用其 get_properties，如果存在
    local inst = widget_entry.instance
    if inst and inst.get_properties then
        local ok, props = pcall(function() return inst:get_properties() end)
        if ok and props then
            property_area:show()
            property_area:set_properties(props)
        else
            print("[属性窗口] 获取属性失败: " .. tostring(props))
        end
    else
        print("[属性窗口] 选中控件没有 get_properties 方法")
    end
end)

canvas:on("widgets_selected", function(self, widget_entries)
    print("[画布] 多选控件: " .. #widget_entries .. " 个")
    for _, w in ipairs(widget_entries) do
        print("  - " .. w.id)
    end
end)

canvas:on("widget_deselected", function(self, prev_widget)
    print("[画布] 取消选中")
end)

canvas:on("widget_deleted", function(self, widget_entry)
    print("[画布] 删除控件: " .. widget_entry.id)
end)

canvas:on("widget_moved", function(self, widget_entry)
    print("[画布] 控件移动: " .. widget_entry.id)
end)

canvas:on("widgets_moved", function(self, widget_entries)
    print("[画布] 多个控件移动: " .. #widget_entries .. " 个")
end)

-- 工具箱拖放事件处理（新的拖拽方式）
toolbox:on("tool_drag_drop", function(self, tool, module, screen_x, screen_y)
    print("[工具箱] 拖放工具: " .. tool.name .. " 屏幕坐标: (" .. screen_x .. ", " .. screen_y .. ")")
    
    -- 将屏幕坐标转换为画布坐标
    local canvas_x = screen_x - canvas.props.x
    local canvas_y = screen_y - canvas.props.y
    
    print("[工具箱] 画布坐标: (" .. canvas_x .. ", " .. canvas_y .. ")")
    
    -- 检查是否在画布范围内
    if canvas_x >= 0 and canvas_x < canvas.props.width and
       canvas_y >= 0 and canvas_y < canvas.props.height then
        -- 在画布内，创建控件
        local widget = canvas:handle_drop(module, canvas_x, canvas_y)
        if widget then
            print("[工具箱] 控件创建成功: " .. widget.id)
            -- 自动选中新创建的控件
            canvas:select_widget(widget)
        end
    else
        print("[工具箱] 释放位置不在画布范围内，取消创建")
    end
end)

-- 兼容旧的点击放置方式
toolbox:on("tool_dropped", function(self, tool, module, x, y)
    print("[工具箱] 点击放置工具: " .. tool.name)
    
    -- 默认放置位置（画布中心附近）
    local default_x = 200
    local default_y = 150
    
    local widget = canvas:handle_drop(module, default_x, default_y)
    if widget then
        print("[工具箱] 控件创建成功: " .. widget.id)
        canvas:select_widget(widget)
    end
end)

-- 工具箱/属性窗口状态同步
toolbox:on("visibility_changed", function(self, visible)
    print("[工具箱] 可见性变化: " .. tostring(visible))
    menu_bar:set_state("show_toolbox", visible)
end)

property_area:on("visibility_changed", function(self, visible)
    print("[属性窗口] 可见性变化: " .. tostring(visible))
    menu_bar:set_state("show_properties", visible)
end)

-- ========== 状态栏 ==========
local status_bar = lv.obj_create(scr)
status_bar:set_pos(0, WINDOW_HEIGHT - STATUSBAR_HEIGHT)
status_bar:set_size(WINDOW_WIDTH, STATUSBAR_HEIGHT)
status_bar:set_style_bg_color(0x007ACC, 0)
status_bar:set_style_radius(0, 0)
status_bar:set_style_border_width(0, 0)
status_bar:set_style_pad_all(0, 0)
status_bar:remove_flag(lv.OBJ_FLAG_SCROLLABLE)
status_bar:clear_layout()

status_label = lv.label_create(status_bar)
status_label:set_style_text_color(0xFFFFFF, 0)
status_label:align(lv.ALIGN_LEFT_MID, 10, 0)

-- 初始化状态栏
update_status_bar()

print("=== 编辑器初始化完成 ===")
print("菜单栏高度: " .. menu_bar:get_height())
print("画布区域: 全屏 (" .. WINDOW_WIDTH .. "x" .. (WINDOW_HEIGHT - MENUBAR_HEIGHT - STATUSBAR_HEIGHT) .. ")")
print("工具箱: 浮动在画布左上角")
print("提示: 从工具箱拖拽控件到画布上释放即可创建")
