using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace HVACLoadTerminals.Revit.Testing
{
    /// <summary>
    /// Result entry for a single executed test method.
    /// </summary>
    public sealed class TestCaseResult
    {
        public string Fixture { get; set; } = "";
        public string Method { get; set; } = "";
        public bool Passed { get; set; }
        public bool Skipped { get; set; }
        public long DurationMs { get; set; }
        public string? Error { get; set; }
    }

    /// <summary>
    /// In-process test discovery + execution. Finds every public method marked
    /// <see cref="RevitTestAttribute"/> on the given assembly/type, runs it,
    /// and returns structured results. Pure reflection — no external runner.
    /// </summary>
    public static class RevitTestRunner
    {
        /// <summary>
        /// Discovers and runs every [RevitTest] method in <paramref name="assemblies"/>.
        /// </summary>
        public static IReadOnlyList<TestCaseResult> RunAll(params Assembly[] assemblies)
        {
            var results = new List<TestCaseResult>();
            var fixtures = assemblies
                .SelectMany(a => SafeTypes(a))
                .Where(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                             .Any(m => m.GetCustomAttribute<RevitTestAttribute>() != null))
                .ToList();

            foreach (var fixture in fixtures)
            {
                foreach (var method in fixture.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                                              .Where(m => m.GetCustomAttribute<RevitTestAttribute>() != null))
                {
                    results.Add(ExecuteSingle(fixture, method));
                }
            }

            return results;
        }

        private static TestCaseResult ExecuteSingle(Type fixture, MethodInfo method)
        {
            var result = new TestCaseResult { Fixture = fixture.Name, Method = method.Name };
            object? instance = null;
            try
            {
                instance = Activator.CreateInstance(fixture, nonPublic: true);
                var sw = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    var ret = method.Invoke(instance, null);
                    sw.Stop();
                    result.DurationMs = sw.ElapsedMilliseconds;
                    if (ret is bool bret && !bret)
                    {
                        result.Passed = false;
                        result.Error = "Test method returned false.";
                    }
                    else
                    {
                        result.Passed = true;
                    }
                }
                catch (TargetInvocationException tie)
                {
                    if (tie.InnerException is TestSkippedException skip)
                    {
                        sw.Stop();
                        result.DurationMs = sw.ElapsedMilliseconds;
                        result.Passed = false;
                        result.Skipped = true;
                        result.Error = "Skipped: " + skip.Message;
                    }
                    else
                    {
                        sw.Stop();
                        result.DurationMs = sw.ElapsedMilliseconds;
                        result.Passed = false;
                        result.Error = tie.InnerException?.Message ?? tie.Message;
                    }
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    result.DurationMs = sw.ElapsedMilliseconds;
                    result.Passed = false;
                    result.Error = ex.Message;
                }
            }
            catch (Exception ex)
            {
                result.Passed = false;
                result.Error = "Fixture construction failed: " + ex.Message;
            }
            finally
            {
                if (instance is IDisposable d) d.Dispose();
            }
            return result;
        }

        private static IEnumerable<Type> SafeTypes(Assembly asm)
        {
            try { return asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null)!; }
        }

        /// <summary>
        /// Serializes test results to a JSON report. Manual StringBuilder output —
        /// zero runtime dependencies (matches the add-in's no-framework policy).
        /// </summary>
        public static string ToJson(IReadOnlyList<TestCaseResult> results, string hostName, string timestamp)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("{\"Timestamp\":\"").Append(Escape(timestamp))
              .Append("\",\"Host\":\"").Append(Escape(hostName))
              .Append("\",\"Total\":").Append(results.Count)
              .Append(",\"Passed\":").Append(results.Count(r => r.Passed))
              .Append(",\"Skipped\":").Append(results.Count(r => r.Skipped))
              .Append(",\"Failed\":").Append(results.Count(r => !r.Passed && !r.Skipped))
              .Append(",\"Results\":[");
            for (int i = 0; i < results.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var r = results[i];
                sb.Append("{\"Fixture\":\"").Append(Escape(r.Fixture))
                  .Append("\",\"Method\":\"").Append(Escape(r.Method))
                  .Append("\",\"Passed\":").Append(r.Passed ? "true" : "false")
                  .Append(",\"Skipped\":").Append(r.Skipped ? "true" : "false")
                  .Append(",\"DurationMs\":").Append(r.DurationMs)
                  .Append(",\"Error\":");
                if (r.Error == null) sb.Append("null");
                else sb.Append('"').Append(Escape(r.Error)).Append('"');
                sb.Append('}');
            }
            sb.Append("]}");
            return sb.ToString();
        }

        /// <summary>
        /// Writes the JSON report as UTF-8 (no BOM) to
        /// <c>revit-tests-&lt;timestamp&gt;.json</c> in <paramref name="directory"/>
        /// (default: %LocalAppData%\HVACLoadTerminals\TestResults). Returns the full path.
        /// </summary>
        public static string WriteReport(
            IReadOnlyList<TestCaseResult> results,
            string hostName,
            string? directory = null)
        {
            string dir = directory ?? System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HVACLoadTerminals",
                "TestResults");
            System.IO.Directory.CreateDirectory(dir);
            string timestamp = DateTime.Now.ToString("yyyy-MM-ddTHHmmss");
            string path = System.IO.Path.Combine(dir, "revit-tests-" + timestamp + ".json");
            System.IO.File.WriteAllText(
                path,
                ToJson(results, hostName, timestamp),
                new System.Text.UTF8Encoding(false));
            return path;
        }

        private static string Escape(string? v)
        {
            if (string.IsNullOrEmpty(v)) return "";
            return v!.Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\r", "\\r")
                    .Replace("\n", "\\n")
                    .Replace("\t", "\\t");
        }
    }
}
