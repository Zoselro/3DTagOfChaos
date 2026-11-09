using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

// 개발 도구: EditMode 시험을 클래스 이름으로 돌리고 결과를 Temp/test_results.txt에 쓴다(자동 검증용 — 메뉴 없이 코드로 부른다).
public static class TestRunHelper
{
    public const string ResultPath = "Temp/test_results.txt";

    public static void Run(params string[] classNames)
    {
        System.IO.File.WriteAllText(ResultPath, "RUNNING");
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks(new Callbacks(api));
        api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode, groupNames = classNames }));
    }

    private class Callbacks : ICallbacks
    {
        private readonly TestRunnerApi api;
        private readonly StringBuilder fails = new StringBuilder();
        public Callbacks(TestRunnerApi owner) => api = owner;
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result)
        {
            if (!result.HasChildren && result.TestStatus == TestStatus.Failed)
                fails.AppendLine(result.FullName + ": " + result.Message);
        }
        public void RunFinished(ITestResultAdaptor result)
        {
            System.IO.File.WriteAllText(ResultPath, $"DONE passed={result.PassCount} failed={result.FailCount} skipped={result.SkipCount}\n{fails}");
            api.UnregisterCallbacks(this);
        }
    }
}
