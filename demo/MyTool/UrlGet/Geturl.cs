using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using OfficeOpenXml;

namespace MyTool.UrlGet
{
    public class Geturl
    {
        // ================== 参数变量（手动修改） ==================
        /// <summary>输入 Excel 路径（第1列 Name，第2列 Service URL，第1行为表头）</summary>
        public static string InputExcelPath = @"C:\Achong\M\Project\demo\MyTool\UrlGet\data\new.xlsx";

        /// <summary>输出 Excel 路径</summary>
        public static string OutputExcelPath = @"C:\Achong\M\Project\demo\MyTool\UrlGet\res\services4_result.xlsx";

        /// <summary>工作表名，留空则使用第一个工作表</summary>
        public static string SheetName = "Sheet4";

        /// <summary>单次请求超时（秒）</summary>
        public static int TimeoutSeconds = 15;

        /// <summary>并发数</summary>
        public static int MaxParallel = 10;

        /// <summary>失败重试次数（0 表示不重试）</summary>
        public static int RetryCount = 1;

        /// <summary>重试间隔（毫秒）</summary>
        public static int RetryDelayMs = 1000;

        /// <summary>是否将 3xx 视为 OK（默认仅 2xx 为 OK）</summary>
        public static bool TreatRedirectAsOk = false;
        // =========================================================

        // 静态 HttpClient，避免重复创建导致 socket 耗尽
        private static readonly HttpClient Http;

        static Geturl()
        {
            var handler = new HttpClientHandler
            {
                // 忽略证书错误（内网自签证书场景）。生产环境如需校验证书，请删除此行
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
                AllowAutoRedirect = true
            };
            Http = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(TimeoutSeconds)
            };
        }

        /// <summary>
        /// 执行 URL 批量检测，返回统计结果。
        /// </summary>
        public async Task<CheckSummary> GetUrl()
        {
            ExcelPackage.License.SetNonCommercialPersonal("achong");

            if (!File.Exists(InputExcelPath))
            {
                Console.WriteLine($"[错误] 输入文件不存在: {InputExcelPath}");
                return new CheckSummary { OutputPath = OutputExcelPath };
            }

            var records = ReadExcel(InputExcelPath, SheetName);
            Console.WriteLine($"读取到 {records.Count} 条记录，开始检测...\n");

            var semaphore = new SemaphoreSlim(MaxParallel);
            var tasks = records.Select(async r =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var (status, code, message) = await CheckUrlWithRetryAsync(Http, r.Url);
                    r.Status = status;
                    r.Code = code;
                    r.Message = message;
                    r.CheckedAt = DateTime.Now; // 检测完成时间
                    Console.WriteLine($"[{status}] {r.Name,-30} {r.Url}  ({code}, {message})");
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            WriteExcel(OutputExcelPath, records);

            var summary = new CheckSummary
            {
                Total = records.Count,
                Ok = records.Count(x => x.Status == "OK"),
                Fail = records.Count(x => x.Status != "OK"),
                OutputPath = OutputExcelPath
            };

            Console.WriteLine($"\n完成！总数 {summary.Total}，成功 {summary.Ok}，失败 {summary.Fail}");
            Console.WriteLine($"结果已输出到: {summary.OutputPath}");
            return summary;
        }

        // ---------- 读取 Excel ----------
        private static List<ServiceRecord> ReadExcel(string path, string sheetName)
        {
            var list = new List<ServiceRecord>();
            using var package = new ExcelPackage(new FileInfo(path));
            var ws = string.IsNullOrWhiteSpace(sheetName)
                ? package.Workbook.Worksheets[0]
                : package.Workbook.Worksheets[sheetName] ?? package.Workbook.Worksheets[0];

            if (ws.Dimension == null) return list;

            int rows = ws.Dimension.End.Row;
            const int startRow = 2; // 第1行为表头

            for (int row = startRow; row <= rows; row++)
            {
                var name = ws.Cells[row, 1].Text?.Trim();
                var url = ws.Cells[row, 2].Text?.Trim();
                if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(url)) continue;

                list.Add(new ServiceRecord { Name = name, Url = url });
            }
            return list;
        }

        // ---------- 写入 Excel ----------
        private static void WriteExcel(string path, List<ServiceRecord> records)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("Result");

            ws.Cells[1, 1].Value = "Name";
            ws.Cells[1, 2].Value = "Service URL";
            ws.Cells[1, 3].Value = "Status";      // OK / FAIL
            ws.Cells[1, 4].Value = "HttpCode";
            ws.Cells[1, 5].Value = "Message";
            ws.Cells[1, 6].Value = "CheckedAt";

            using (var rng = ws.Cells[1, 1, 1, 6]) rng.Style.Font.Bold = true;

            int r = 2;
            foreach (var rec in records)
            {
                ws.Cells[r, 1].Value = rec.Name;
                ws.Cells[r, 2].Value = rec.Url;
                ws.Cells[r, 3].Value = rec.Status;
                ws.Cells[r, 4].Value = rec.Code;
                ws.Cells[r, 5].Value = rec.Message;
                ws.Cells[r, 6].Value = rec.CheckedAt.ToString("yyyy-MM-dd HH:mm:ss");
                r++;
            }

            if (ws.Dimension != null)
                ws.Cells[1, 1, ws.Dimension.End.Row, 6].AutoFitColumns();

            package.SaveAs(new FileInfo(path));
        }

        // ---------- 带重试的检测 ----------
        private static async Task<(string Status, string Code, string Message)> CheckUrlWithRetryAsync(
            HttpClient client, string url)
        {
            (string Status, string Code, string Message) result = default;
            int attempts = Math.Max(0, RetryCount) + 1;

            for (int i = 1; i <= attempts; i++)
            {
                result = await CheckUrlAsync(client, url);
                if (result.Status == "OK") break;

                // 格式/空 URL 类错误不重试
                if (result.Message == "URL 为空" || result.Message == "URL 格式无效") break;

                if (i < attempts)
                    await Task.Delay(RetryDelayMs);
            }
            return result;
        }

        // ---------- 单条检测 ----------
        private static async Task<(string Status, string Code, string Message)> CheckUrlAsync(
            HttpClient client, string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return ("FAIL", "-", "URL 为空");

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                return ("FAIL", "-", "URL 格式无效");

            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, uri);
                using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);

                int code = (int)resp.StatusCode;
                string msg = resp.ReasonPhrase ?? "";

                bool ok;
                if (TreatRedirectAsOk)
                    ok = code >= 200 && code < 400;
                else
                    ok = resp.IsSuccessStatusCode;

                return (ok ? "OK" : "FAIL", code.ToString(), msg);
            }
            catch (TaskCanceledException)
            {
                return ("FAIL", "-", $"超时 ({TimeoutSeconds}s)");
            }
            catch (HttpRequestException ex)
            {
                return ("FAIL", "-", $"请求失败: {ex.Message}");
            }
            catch (Exception ex)
            {
                return ("FAIL", "-", $"异常: {ex.Message}");
            }
        }

        // ---------- 数据模型 ----------
        private class ServiceRecord
        {
            public string Name { get; set; } = "";
            public string Url { get; set; } = "";
            public string Status { get; set; } = "";
            public string Code { get; set; } = "";
            public string Message { get; set; } = "";
            public DateTime CheckedAt { get; set; }
        }
    }

    /// <summary>检测结果统计</summary>
    public class CheckSummary
    {
        public int Total { get; set; }
        public int Ok { get; set; }
        public int Fail { get; set; }
        public string OutputPath { get; set; } = "";
    }
}