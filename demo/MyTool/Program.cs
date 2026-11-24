using MyTool.FileSearchCode;
using MyTool.UrlGet;

// See https://aka.ms/new-console-template for more information
Console.WriteLine("Hello, World!");
//Log.LogSplit.RunLogSplit("./Log/user_log.txt");

#region 获取http是否可以访问
// var tool = new Geturl();
// var summary = await tool.GetUrl();

// Console.WriteLine();
// Console.WriteLine("===== 检测完成 =====");
// Console.WriteLine($"总数: {summary.Total}");
// Console.WriteLine($"成功: {summary.Ok}");
// Console.WriteLine($"失败: {summary.Fail}");
// Console.WriteLine($"输出: {summary.OutputPath}");
#endregion

#region 扫描指定文本信息 （zip/txt）
// ====== 自定义配置示例 ======
var searcher = new SearchCode();
var options = new SearchOptions
{
    // 推荐使用 SearchPaths：支持混合输入！
    // 可以放文件夹路径（递归扫描子目录），也可以直接放具体的 txt/xml/log/zip 等文件路径
    SearchPaths = new List<string>
    {
        // 示例2：直接指定一个普通文本文件（.log/.txt/.xml/无后缀 等都支持）
        //@"C:\Users\3000100552\Downloads\single_file\error_report.log",

        // 示例3：直接指定一个压缩包 .zip，会自动解析包内的 xml 及无后缀文件
        //@"C:\Users\3000100552\Downloads\archive_package\202608_logs.zip",

        // 示例1：文件夹 → 递归扫描其中所有子目录及文件
        @"C:\Users\3000100552\Downloads\Down\lot\116",
    },
    SearchText = "TCP error code 10061",//10060  10061   Exception is Could not connect to net   TCP error code 10061:        No Url Found    Error Code = 3014683, Source = Camstar.XMLClient.API.csiConnectionImpl.createSession()
    //SearchText = @"10060|10061",//正则匹配
    IgnoreCase = true,
    //UseRegex = true,//正则表达式
    IncludeNoExtension = true,
    ScanZipFiles = true,

    // 控制台进度日志开关（默认 true）
    // = true ：扫描过程中输出 [任务启动] / [扫描目录] / [!] 发现匹配 / [处理压缩包] 等带时间戳的进度日志
    // = false：不输出上述进度日志，只在最后打印 搜索报告正文（汇总/按文件统计/按文件夹统计/匹配明细 等），控制台更干净
    EnableConsoleLog = false
};
// 旧写法 SearchFolders 依然可用（也已兼容文件夹+文件混合），但推荐切到 SearchPaths
// options.SearchFolders = new List<string> { @"C:\Users\3000100552\Downloads\logs" };
var result = searcher.Search(options);
string reportDir = @"C:\Achong\M\Project\demo\MyTool\FileSearchCode\resnew";
if (!Directory.Exists(reportDir)) Directory.CreateDirectory(reportDir);
var path = @"ees116".Replace(@"\", "_");
path = path.Replace(@"-", "_");
string reportCsv = Path.Combine(reportDir, $"res_report_{path}.csv");
searcher.ExportReportToFile(result, options, reportCsv);

// 控制台打印（输出量大时可能被 sandbox 截断，但 CSV 已经保存）
//searcher.PrintReport(result, options);
#endregion
