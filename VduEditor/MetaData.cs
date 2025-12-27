using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VduEditor
{
    /// <summary>
    /// 属性类型枚举
    /// </summary>
    enum PropertyType { String, Number, Boolean, Enum, Color }

    /// <summary>
    /// 属性定义
    /// </summary>
    class PropertyDef
    {
        public string Name { get; set; } = "";
        public PropertyType Type { get; set; }
        public object? Default { get; set; }
        public string Label { get; set; } = "";
        public int Min { get; set; } = 0;
        public int Max { get; set; } = 0;
        public string[]? Options { get; set; }
        public bool ReadOnly { get; set; } = false;
    }

    /// <summary>
    /// 控件元数据
    /// </summary>
    class WidgetMeta
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string SchemaVersion { get; set; } = "1.0";
        public string Version { get; set; } = "1.0";
        public string Icon { get; set; } = "";
        public List<PropertyDef> Properties { get; set; } = new();
        public string LuaFilePath { get; set; } = "";
    }
}
