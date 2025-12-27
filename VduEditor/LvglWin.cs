using LVGLSharp;
using LVGLSharp.Interop;
using LVGLSharp.Runtime.Windows;
using NLua;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace VduEditor
{

    unsafe partial class Program
    {
        static Size mianWindowsSize = new Size(1920, 1080);
        static Win32Window? _window;
        static lv_obj_t* root;
        // 默认字体样式（用于中文显示）
        static lv_style_t* defaultFontStyle = null;
        static void createWindow()
        {
            // Initialize window
            _window = new Win32Window("VduDemo", 
                (uint)mianWindowsSize.Width, (uint)mianWindowsSize.Height);
            _window.Init();
            root = Win32Window.root;
            
            // 禁用root的滚动功能，防止鼠标拖动时整个画面移动
            lv_obj_remove_flag(root, LV_OBJ_FLAG_SCROLLABLE);
            lv_obj_set_scrollbar_mode(root, LV_SCROLLBAR_MODE_OFF);
            // 移除flex布局，使用自定义布局
            lv_obj_set_flex_flow(root, LV_FLEX_FLOW_ROW);
            lv_obj_set_style_pad_all(root, 0, 0);
            
            // 初始化默认字体样式
            InitDefaultFontStyle();
            initeUI();
            // Start the main loop
            _window.StartLoop(() => { });
        }
        static void initeUI()
        {
            //创建菜单栏
            CreateMenuBar();
            //创建画布区域
            CreateCanvasArea();
            //创建工具箱
            CreateFloatingToolbox();
            //创建属性
            createPropertyBox();
        }
       
        static void createPropertyBox()
        { }
        /// <summary>
        /// 初始化默认字体样式（用于中文显示）
        /// </summary>
        static void InitDefaultFontStyle()
        {
            var font = lv_obj_get_style_text_font(root, LV_PART_MAIN);
            defaultFontStyle = (lv_style_t*)NativeMemory.Alloc((nuint)sizeof(lv_style_t));
            NativeMemory.Clear(defaultFontStyle, (nuint)sizeof(lv_style_t));
            lv_style_init(defaultFontStyle);
            lv_style_set_text_font(defaultFontStyle, font);
        }
        /// <summary>
        /// 应用默认字体样式到对象
        /// </summary>
        static void ApplyDefaultFontStyle(lv_obj_t* obj)
        {
            if (defaultFontStyle != null)
            {
                lv_obj_add_style(obj, defaultFontStyle, 0);
            }
        }
    }
}
