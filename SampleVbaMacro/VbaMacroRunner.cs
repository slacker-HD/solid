using SolidWorks.Interop.sldworks;
using System;
using System.IO;
using System.Text;

namespace SampleVbaMacro
{
    /// <summary>
    /// VBA 宏运行器。
    ///
    /// SolidWorks 只能“运行宏文件”，没有直接“运行一段字符串代码”的接口，
    /// 因此这里的做法是：
    ///   1. 把用户输入的 VBA 代码写成临时宏文件（.swb，SolidWorks Basic 文本宏）；
    ///   2. 用 ISldWorks.RunMacro2 运行该文件里名为 Main 的过程。
    ///
    /// 注意：SolidWorks API 所有长度单位都是“米”，所以 10mm 要写成 0.01。
    /// </summary>
    public static class VbaMacroRunner
    {
        /// <summary>
        /// 运行一段 VBA 代码（代码中必须包含 Sub Main()）。
        /// </summary>
        /// <param name="swApp">当前 SolidWorks 应用实例（由插件传入）。</param>
        /// <param name="vbaCode">要执行的完整 VBA 代码。</param>
        /// <param name="errorCode">返回的 SolidWorks 错误码。</param>
        /// <returns>运行日志文本。</returns>
        public static string Run(ISldWorks swApp, string vbaCode, out int errorCode)
        {
            errorCode = 0;

            if (swApp == null)
                return "SolidWorks 实例未传入，无法运行宏。";
            if (string.IsNullOrWhiteSpace(vbaCode))
                return "VBA 代码为空。";

            // 生成临时宏文件：.swb 是文本格式的 SolidWorks Basic 宏
            string tempPath = Path.Combine(Path.GetTempPath(), $"vba_macro_{Guid.NewGuid():N}.swb");

            // 使用系统默认编码（ANSI）写入：VBA 编辑器对 UTF-8 支持不佳，中文注释会乱码
            try
            {
                File.WriteAllText(tempPath, vbaCode, Encoding.Default);
            }
            catch (Exception ex)
            {
                return "写入临时宏文件失败：" + ex.Message;
            }

            StringBuilder sb = new StringBuilder();
            try
            {
                // 策略1：用 GetMacroMethods 探出模块名（.swb 的模块名不一定是 Module1）
                string moduleName = "Module1";
                try
                {
                    object macroMethods = swApp.GetMacroMethods(tempPath, 0);
                    if (macroMethods is object[] methods && methods.Length > 0)
                        moduleName = methods[0].ToString();
                }
                catch
                {
                    // 探不到模块名时保持默认值即可
                }

                int runErr = 0;
                bool ok = swApp.RunMacro2(tempPath, moduleName, "Main", 0, out runErr);
                errorCode = runErr;

                // 策略2：模块名不对时，尝试用空模块名再运行一次
                if (!ok && runErr == 0)
                {
                    try
                    {
                        ok = swApp.RunMacro2(tempPath, "", "Main", 0, out runErr);
                        errorCode = runErr;
                        if (ok) moduleName = "(空)";
                    }
                    catch { }
                }

                // 策略3：仍失败时，回退到旧版 RunMacro
                if (!ok && runErr == 0)
                {
                    try
                    {
                        ok = swApp.RunMacro(tempPath, moduleName, "Main");
                        if (ok) errorCode = 0;
                    }
                    catch { }
                }

                sb.AppendLine("==== VBA 宏运行结果 ====");
                sb.AppendLine(ok ? "宏执行成功。" : $"宏执行失败，错误码：{runErr}，模块名：{moduleName}");
                sb.AppendLine($"临时宏文件：{tempPath}");
                sb.AppendLine($"代码长度：{vbaCode.Length} 字符");
            }
            catch (Exception ex)
            {
                sb.AppendLine("运行异常：" + ex.Message);
                sb.AppendLine("堆栈：" + ex.StackTrace);
            }

            return sb.ToString();
        }
    }
}
