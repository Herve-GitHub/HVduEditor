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
    width = 180,
})

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
        print("切换网格显示")
    elseif item_id == "show_toolbox" then
        -- 切换工具箱显示/隐藏
        toolbox:toggle()
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
end)

canvas:on("widget_deselected", function(self, prev_widget)
    print("[画布] 取消选中")
end)

canvas:on("widget_deleted", function(self, widget_entry)
    print("[画布] 删除控件: " .. widget_entry.id)
end)

-- 工具箱事件处理
toolbox:on("tool_dropped", function(self, tool, module, x, y)
    print("[工具箱] 放置工具: " .. tool.name .. " @ (" .. x .. ", " .. y .. ")")
    
    local widget = canvas:handle_drop(module, x, y)
    if widget then
        print("[工具箱] 控件创建成功: " .. widget.id)
    end
end)

toolbox:on("visibility_changed", function(self, visible)
    print("[工具箱] 可见性变化: " .. tostring(visible))
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

local status_label = lv.label_create(status_bar)
status_label:set_text("就绪 | 画布: " .. WINDOW_WIDTH .. "x" .. (WINDOW_HEIGHT - MENUBAR_HEIGHT - STATUSBAR_HEIGHT) .. " | 网格: 20px")
status_label:set_style_text_color(0xFFFFFF, 0)
status_label:align(lv.ALIGN_LEFT_MID, 10, 0)

print("=== 编辑器初始化完成 ===")
print("菜单栏高度: " .. menu_bar:get_height())
print("画布区域: 全屏 (" .. WINDOW_WIDTH .. "x" .. (WINDOW_HEIGHT - MENUBAR_HEIGHT - STATUSBAR_HEIGHT) .. ")")
print("工具箱: 浮动在画布左上角")
