我来帮你编写这个C#脚本。首先让我查看一下现有的代码文件，了解项目结构。
用户需要一个C#脚本来解析zip压缩文件，搜索指定内容。让我先分析需求：

1. 查询文件夹中（可能有多层嵌套）的压缩包或文件中指定内容出现的次数
2. 查看文件中出现的指定文本内容的当前行数
3. 记录文件名称 + 行数
4. 按照文件夹统计数量以及总数量
5. 压缩包里边是xml或者是没后缀的文件
6. 可以通过txt文本文件打开

首先我需要查看用户当前打开的文件，了解现有的代码结构。

好的，现在我了解了项目结构。用户需要一个C#脚本来：

1. 搜索文件夹（包括多层嵌套子文件夹）中的文件和zip压缩包
2. 在文件和压缩包内的文件中搜索指定文本内容（例如：TCP error code 10061:）
3. 记录：
   - 出现的次数
   - 文件名 + 出现的行号
   - 按文件夹统计数量
   - 总数量
4. 压缩包内可能是xml或没有后缀的文件，都可以用txt方式打开

让我来编写这个功能。这是一个比较复杂的多步骤任务，我应该使用TodoWrite来规划。

现在我来编写完整的C#脚本，实现所有功能。
好的，现在我来设计并实现这个C#脚本。我需要：

1. 设计数据结构来存储搜索结果
2. 遍历文件夹（递归多层）
3. 解析zip文件（包括嵌套的文件，xml和无后缀文件都按文本处理）
4. 在每个文件中搜索指定文本，记录文件名和行号
5. 按文件夹统计数量，以及总数量
6. 提供示例用法

我会使用.NET内置的System.IO.Compression来处理zip文件，不需要额外的依赖。

让我编写完整的代码。

现在让我一次性编写完整的代码，整合所有功能。

代码已经写好了。现在我需要更新TodoWrite来标记所有任务为完成，然后给用户一个完整的总结。

代码已完成！以下是实现的功能概览：

## 核心数据结构

| 类 | 作用 |
|---|---|
| [MatchRecord](file:///c:/Achong/M/Project/demo/MyTool/FileSearchCode/SearchCode.cs#L11-L19) | 单条匹配记录（文件路径、行号、内容、是否在压缩包内等） |
| [FolderStat](file:///c:/Achong/M/Project/demo/MyTool/FileSearchCode/SearchCode.cs#L21-L26) | 文件夹统计（路径、匹配数、扫描文件数） |
| [SearchResult](file:///c:/Achong/M/Project/demo/MyTool/FileSearchCode/SearchCode.cs#L28-L36) | 搜索结果汇总（所有匹配、文件夹统计、总数、耗时） |
| [SearchOptions](file:///c:/Achong/M/Project/demo/MyTool/FileSearchCode/SearchCode.cs#L38-L49) | 搜索配置项 |

## 主要功能

1. **递归多层文件夹遍历** — [ScanFolder](file:///c:/Achong/M/Project/demo/MyTool/FileSearchCode/SearchCode.cs#L95-L116) 方法自动扫描所有子目录
2. **ZIP压缩包解析** — [ProcessZipFile](file:///c:/Achong/M/Project/demo/MyTool/FileSearchCode/SearchCode.cs#L146-L182) 使用 `System.IO.Compression`，支持包内的 `.xml` 和无后缀文件
3. **文本搜索 + 行数定位** — [ProcessLines](file:///c:/Achong/M/Project/demo/MyTool/FileSearchCode/SearchCode.cs#L233-L271) 逐行扫描，支持普通文本匹配和正则匹配，忽略大小写可选
4. **按文件夹统计 + 总数量** — 在 [Search](file:///c:/Achong/M/Project/demo/MyTool/FileSearchCode/SearchCode.cs#L53-L93) 方法中用字典统计，最终排序输出
5. **报告输出** — [PrintReport](file:///c:/Achong/M/Project/demo/MyTool/FileSearchCode/SearchCode.cs#L312-L364) 打印控制台报告，[ExportReportToFile](file:///c:/Achong/M/Project/demo/MyTool/FileSearchCode/SearchCode.cs#L366-L404) 导出 CSV（带UTF-8 BOM，Excel可直接打开）
6. **编码自动检测** — [DetectEncoding](file:///c:/Achong/M/Project/demo/MyTool/FileSearchCode/SearchCode.cs#L279-L297) 自动识别 UTF-8 / UTF-16 LE / UTF-16 BE

## 快速使用

```csharp
// 调用示例入口
SearchCode.RunExample();

// 或者自定义配置：
var searcher = new SearchCode();
var options = new SearchOptions
{
    SearchFolders = new List<string> { @"C:\Logs" },
    SearchText = "TCP error code 10061:",
    IgnoreCase = true,
    IncludeNoExtension = true,
    ScanZipFiles = true
};
var result = searcher.Search(options);
searcher.PrintReport(result, options);
searcher.ExportReportToFile(result, options, @"C:\report.csv");
```

**注意**：项目需要引用 `System.IO.Compression` 和 `System.IO.Compression.FileSystem` 程序集（.NET Framework），或者在 .NET Core/.NET 5+ 中默认已包含。