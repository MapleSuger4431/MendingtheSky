using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace MendingTheSky.EditorTools
{
    /// <summary>
    /// 一键跑 EditMode 测试并把结果写到 Temp/editmode_test_results.txt。
    /// 目的：让自动化脚本（或命令行）不需要人肉点 Test Runner 也能确认测试状态。
    /// 用法：菜单 MendingTheSky/跑 EditMode 测试，或调用 EditModeTestRunner.Run()。
    /// </summary>
    public class EditModeTestRunner : ICallbacks
    {
        private const string ResultPath = "Temp/editmode_test_results.txt";

        private static StringBuilder report;

        [MenuItem("MendingtheSky/跑 EditMode 测试")]
        public static void Run()
        {
            report = new StringBuilder();
            if (File.Exists(ResultPath))
            {
                File.Delete(ResultPath);
            }

            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new EditModeTestRunner());
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode }));
            Debug.Log("[EditModeTestRunner] 已启动，结果将写入 " + ResultPath);
        }

        public void RunStarted(ITestAdaptor testsToRun)
        {
        }

        public void TestStarted(ITestAdaptor test)
        {
        }

        public void TestFinished(ITestResultAdaptor result)
        {
            if (result.TestStatus == TestStatus.Failed)
            {
                string message = (result.Message ?? string.Empty).Split('\n').FirstOrDefault();
                report.AppendLine($"FAIL {result.FullName} :: {message}");
            }
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            report.Insert(0,
                $"status={result.TestStatus} pass={result.PassCount} fail={result.FailCount} " +
                $"skip={result.SkipCount} inconclusive={result.InconclusiveCount} duration={result.Duration:0.0}s\n");
            File.WriteAllText(ResultPath, report.ToString());
            Debug.Log("[EditModeTestRunner] 完成\n" + report);
        }
    }
}
