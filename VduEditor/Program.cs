using LVGLSharp.Interop;
using LVGLSharp.Runtime.Windows;
using NLua;
using System.Runtime.InteropServices;
using System.Text;

namespace VduEditor
{
    unsafe partial class Program
    {
        static void Main(string[] args)
        {
            // 设置控制台输出编码为UTF-8，解决中文乱码问题
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;
            //将LVGL注册到Lua环境中
            reglvglToLua();
            //读取控件元数据和Lua脚本
            readLuaControls();
            //创建主窗口
            createWindow();
        }
    }
}
