-- widget_template.lua
-- 模板：演示如何在 Lua 控件中添加元数据与实例 API

local lv = require("lvgl") -- lvgl 绑定在全局或通过 require 可用

local Widget = {}

-- 元数据：编辑器使用此表来生成属性面板
Widget.__widget_meta = {
  id = "template_widget",
  name = "Widget Template",
  description = "Template for creating widgets with metadata",
  schema_version = "1.0",
  version = "1.0",
  properties = {
    { name = "label", type = "string", default = "Label", label = "文本" },
    { name = "x", type = "number", default = 10, label = "X" },
    { name = "y", type = "number", default = 10, label = "Y" },
    { name = "width", type = "number", default = 100, label = "宽度" },
    { name = "height", type = "number", default = 40, label = "高度" },
  },
  events = { "clicked" },
}

-- 构造：必须返回实例表（或类实例）
-- parent: LVGL 父对象（画布或容器）
-- props: 初始属性表（可以是从编辑器保存的状态）
function Widget.new(parent, props)
  props = props or {}
  local instance = {}

  -- 默认值映射
  local defaults = {}
  for _, p in ipairs(Widget.__widget_meta.properties) do
    defaults[p.name] = p.default
  end

  -- 合并默认与传入
  instance.props = {}
  for k, v in pairs(defaults) do instance.props[k] = v end
  for k, v in pairs(props) do instance.props[k] = v end

  -- 创建 LVGL 对象（示例：一个容器或按钮）
  -- 这里仅做示意，实际用法请调用具体的 lv.* 创建函数
  instance.obj = lv.obj_create(parent)
  lv.obj_set_size(instance.obj, instance.props.width, instance.props.height)
  lv.obj_set_pos(instance.obj, instance.props.x, instance.props.y)

  -- 必要的实例方法：get/set 属性 / 序列化
  function instance.get_property(self, name)
    return self.props[name]
  end

  function instance.set_property(self, name, value)
    self.props[name] = value
    -- 将属性变化应用到 lv 对象，例如位置/尺寸/文本/颜色
    if name == "x" or name == "y" then
      lv.obj_set_pos(self.obj, self.props.x, self.props.y)
    elseif name == "width" or name == "height" then
      lv.obj_set_size(self.obj, self.props.width, self.props.height)
    end
    return true
  end

  function instance.get_properties(self)
    local out = {}
    for k, v in pairs(self.props) do out[k] = v end
    return out
  end

  function instance.apply_properties(self, props_table)
    for k, v in pairs(props_table) do
      self:set_property(k, v)
    end
    return true
  end

  function instance.to_state(self)
    return self:get_properties()
  end

  return instance
end

return Widget
