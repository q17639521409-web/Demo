using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MyTool.FileSearchCode
{
    /// <summary>
    /// 表示一条文本匹配记录，包含匹配位置、行号、上下文等详细信息
    /// </summary>
    public class MatchRecord
    {
        /// <summary>
        /// 文件完整路径。若匹配来自压缩包内部，则此属性为压缩包的物理路径
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// 匹配内容所在的行号（从1开始计数）
        /// </summary>
        public int LineNumber { get; set; }

        /// <summary>
        /// 匹配所在行的完整文本内容
        /// </summary>
        public string LineContent { get; set; }

        /// <summary>
        /// 指示该匹配是否位于ZIP压缩包内部
        /// </summary>
        public bool IsInsideZip { get; set; }

        /// <summary>
        /// 若匹配在压缩包内，存储压缩包的完整物理路径；否则为null
        /// </summary>
        public string ZipPath { get; set; }

        /// <summary>
        /// 若匹配在压缩包内，存储压缩包内部条目的相对路径（如 subfolder/data.xml）；否则为null
        /// </summary>
        public string EntryName { get; set; }
    }

    /// <summary>
    /// 表示单个文件夹维度的统计数据
    /// </summary>
    public class FolderStat
    {
        /// <summary>
        /// 文件夹完整路径
        /// </summary>
        public string FolderPath { get; set; }

        /// <summary>
        /// 该文件夹（包含压缩包内文件）中匹配到的总次数
        /// </summary>
        public int MatchCount { get; set; }

        /// <summary>
        /// 该文件夹下被扫描的文件总数（含压缩包内的文本条目）
        /// </summary>
        public int FileCount { get; set; }
    }

    /// <summary>
    /// 一次完整搜索操作的结果汇总
    /// </summary>
    public class SearchResult
    {
        /// <summary>
        /// 所有匹配记录列表（按扫描顺序排列）
        /// </summary>
        public List<MatchRecord> Matches { get; set; } = new List<MatchRecord>();

        /// <summary>
        /// 按文件夹维度的统计列表（默认按匹配数降序排列）
        /// </summary>
        public List<FolderStat> FolderStats { get; set; } = new List<FolderStat>();

        /// <summary>
        /// 全部搜索范围内匹配到的总次数
        /// </summary>
        public int TotalMatchCount { get; set; }

        /// <summary>
        /// 扫描过的文本文件总数（含压缩包内的文本条目）
        /// </summary>
        public int TotalFileScanned { get; set; }

        /// <summary>
        /// 扫描过的ZIP压缩包总数
        /// </summary>
        public int TotalZipScanned { get; set; }

        /// <summary>
        /// 本次搜索执行总耗时
        /// </summary>
        public TimeSpan ElapsedTime { get; set; }
    }

    /// <summary>
    /// 搜索任务的配置参数
    /// </summary>
    public class SearchOptions
    {
        /// <summary>
        /// 【推荐】要扫描的路径列表，支持混合输入：
        /// 可以是文件夹路径（递归扫描所有子目录），也可以是具体文件路径（txt/xml/log/zip 等，按扩展名规则识别）
        /// 与 SearchFolders 互斥或叠加使用，二者内容会被合并
        /// </summary>
        public List<string> SearchPaths { get; set; } = new List<string>();

        /// <summary>
        /// 要扫描的根文件夹列表（旧字段，保留以向后兼容）。
        /// 同样支持混合输入：可以是文件夹也可以是具体文件。
        /// 推荐使用 SearchPaths 替代。
        /// </summary>
        public List<string> SearchFolders { get; set; } = new List<string>();

        /// <summary>
        /// 要搜索的文本内容（普通文本或正则表达式，取决于 UseRegex 配置）
        /// </summary>
        public string SearchText { get; set; }

        /// <summary>
        /// 是否忽略大小写（默认 true）
        /// </summary>
        public bool IgnoreCase { get; set; } = true;

        /// <summary>
        /// 是否将 SearchText 作为正则表达式处理（默认 false，即普通文本匹配）
        /// </summary>
        public bool UseRegex { get; set; } = false;

        /// <summary>
        /// 要扫描的文本文件扩展名列表（默认包含 .txt .xml .log .config .json .csv）
        /// 注意：压缩包内的文件同样使用此过滤规则
        /// 另见 IncludeNumericExtensions / ScanAllUnknownAsText 两个开关可扩展范围
        /// </summary>
        public List<string> FileExtensions { get; set; } = new List<string> { ".txt", ".xml", ".log", ".config", ".json", ".csv" };

        /// <summary>
        /// 是否扫描没有扩展名的文件（默认 true）
        /// </summary>
        public bool IncludeNoExtension { get; set; } = true;

        /// <summary>
        /// 是否扫描"纯数字扩展名"的文件（默认 true）
        /// 常见于日志轮转命名：如 xxx.1、xxx.12、xxx.log.1、xxx.xml.17 等会被识别为文本文件
        /// </summary>
        public bool IncludeNumericExtensions { get; set; } = true;

        /// <summary>
        /// 是否将"未知扩展名"（不在白名单、也不是已知二进制、也不是压缩包）的文件一律尝试按文本读取（默认 true）
        /// 开启后：.1 .2 .old .bak .temp .dat 等常见文本型变种扩展名都会被扫描
        /// 已知二进制扩展名（.exe/.dll/.pdb/.png/.jpg/.pdf/.doc/.xls/.bin/.so/.dylib 等）始终会被跳过
        /// </summary>
        public bool ScanAllUnknownAsText { get; set; } = true;

        /// <summary>
        /// 是否扫描ZIP压缩包内部的文件（默认 true）
        /// </summary>
        public bool ScanZipFiles { get; set; } = true;

        /// <summary>
        /// 识别为压缩包的扩展名列表（默认 .zip）
        /// </summary>
        public List<string> ZipExtensions { get; set; } = new List<string> { ".zip" };

        /// <summary>
        /// 指定文本文件读取时使用的编码；设为 null 则自动检测编码（BOM检测，默认 UTF-8）
        /// </summary>
        public Encoding TextEncoding { get; set; } = null;

        /// <summary>
        /// 是否在控制台输出进度日志（默认 true）
        /// 关闭后：扫描过程中的 [任务启动] / [进入文件夹] / [!] 发现匹配 / [处理压缩包] / [报告导出] 等带时间戳的日志不再打印
        /// 注意：【搜索报告】汇总表、匹配明细、按文件/文件夹统计等最终报告内容不受此开关影响
        /// </summary>
        public bool EnableConsoleLog { get; set; } = true;
    }

    /// <summary>
    /// 提供文件及压缩包内文本搜索、统计、报告输出功能的核心服务类
    /// </summary>
    public class SearchCode
    {
        /// <summary>
        /// 当前搜索任务使用的配置（用于读取 EnableConsoleLog 等开关，避免在每个方法里传参）
        /// </summary>
        private SearchOptions _currentOptions = null;
        /// <summary>
        /// 执行搜索任务：扫描指定路径（支持文件夹或具体文件混合输入），在普通文件及ZIP压缩包内匹配文本
        /// 文件夹会递归扫描所有子目录；具体文件（txt/xml/log/zip 等）按扩展名规则直接处理
        /// </summary>
        /// <param name="options">搜索配置参数，不能为空</param>
        /// <returns>包含所有匹配明细、文件夹统计和汇总数据的搜索结果</returns>
        /// <exception cref="ArgumentNullException">options 为 null 时抛出</exception>
        /// <exception cref="ArgumentException">SearchPaths/SearchFolders 都为空或 SearchText 为空时抛出</exception>
        public SearchResult Search(SearchOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            // 保存当前 options 引用，便于 Log 方法读取 EnableConsoleLog 等开关
            _currentOptions = options;

            // 合并 SearchPaths 和 SearchFolders（去重 + 去空），保持向后兼容
            var allPaths = new List<string>();
            if (options.SearchPaths != null) allPaths.AddRange(options.SearchPaths);
            if (options.SearchFolders != null) allPaths.AddRange(options.SearchFolders);
            allPaths = allPaths.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            if (allPaths.Count == 0)
                throw new ArgumentException("至少需要指定一个搜索路径（SearchPaths 或 SearchFolders）", nameof(options.SearchPaths));
            if (string.IsNullOrWhiteSpace(options.SearchText))
                throw new ArgumentException("搜索文本不能为空", nameof(options.SearchText));

            var result = new SearchResult();
            var watch = System.Diagnostics.Stopwatch.StartNew();

            // 文件夹维度的统计字典，键为文件夹完整路径
            var folderMatchMap = new Dictionary<string, int>();
            var folderFileMap = new Dictionary<string, int>();

            // 分类统计路径类型，便于日志展示
            int dirCount = allPaths.Count(p => Directory.Exists(p));
            int fileCount = allPaths.Count(p => File.Exists(p));

            Log($"[任务启动] 开始搜索，共 {allPaths.Count} 个输入路径（文件夹 {dirCount} 个，文件 {fileCount} 个，无效 {allPaths.Count - dirCount - fileCount} 个）");
            Log($"[搜索条件] 文本=\"{options.SearchText}\"，忽略大小写={options.IgnoreCase}，正则={options.UseRegex}，扫描压缩包={options.ScanZipFiles}");

            foreach (var path in allPaths)
            {
                // 路径是文件夹 → 递归扫描该目录及其所有子目录
                if (Directory.Exists(path))
                {
                    Log($"[扫描目录] {path}");
                    ScanFolder(path, path, options, result, folderMatchMap, folderFileMap);
                }
                // 路径是具体文件 → 直接按扩展名判断是文本还是压缩包，走对应处理流程
                else if (File.Exists(path))
                {
                    Log($"[扫描文件] {path}");
                    string rootFolder = Path.GetDirectoryName(path) ?? path;
                    ProcessFile(rootFolder, path, options, result, folderMatchMap, folderFileMap);
                }
                // 路径不存在
                else
                {
                    Log($"[警告] 路径不存在（既不是文件夹也不是文件），已跳过: {path}");
                }
            }

            // 将字典统计转换为有序列表
            foreach (var kvp in folderMatchMap)
            {
                result.FolderStats.Add(new FolderStat
                {
                    FolderPath = kvp.Key,
                    MatchCount = kvp.Value,
                    FileCount = folderFileMap.ContainsKey(kvp.Key) ? folderFileMap[kvp.Key] : 0
                });
            }

            // result.FolderStats = result.FolderStats.OrderByDescending(s => s.MatchCount).ToList();
            result.TotalMatchCount = result.Matches.Count;

            watch.Stop();
            result.ElapsedTime = watch.Elapsed;

            Log($"[任务完成] 匹配总数={result.TotalMatchCount}，扫描文件={result.TotalFileScanned}，压缩包={result.TotalZipScanned}，耗时={result.ElapsedTime.TotalSeconds:F2}s");

            return result;
        }

        /// <summary>
        /// 递归扫描单个文件夹及其所有子目录，处理其中的文件
        /// </summary>
        /// <param name="rootFolder">本次搜索的根目录（用于标识归属）</param>
        /// <param name="currentFolder">当前正在扫描的文件夹路径</param>
        /// <param name="options">搜索配置</param>
        /// <param name="result">搜索结果对象（持续追加写入）</param>
        /// <param name="folderMatchMap">文件夹匹配数统计字典</param>
        /// <param name="folderFileMap">文件夹文件数统计字典</param>
        private void ScanFolder(string rootFolder, string currentFolder, SearchOptions options,
            SearchResult result, Dictionary<string, int> folderMatchMap, Dictionary<string, int> folderFileMap)
        {
            try
            {
                // 先处理当前目录下的所有文件
                string[] files = Directory.GetFiles(currentFolder);
                Log($"    [进入文件夹] {currentFolder}，包含 {files.Length} 个文件");

                foreach (var file in files)
                {
                    ProcessFile(rootFolder, file, options, result, folderMatchMap, folderFileMap);
                }

                // 再递归处理所有子目录
                string[] subDirs = Directory.GetDirectories(currentFolder);
                foreach (var dir in subDirs)
                {
                    ScanFolder(rootFolder, dir, options, result, folderMatchMap, folderFileMap);
                }
            }
            catch (Exception ex)
            {
                LogError($"扫描文件夹出错 [{currentFolder}]: {ex.Message}");
            }
        }

        /// <summary>
        /// 根据文件扩展名判断文件类型，分发到压缩包处理或普通文本搜索流程
        /// 判断顺序：压缩包 → 文本文件（综合判定） → 跳过并输出详细原因
        /// </summary>
        /// <param name="rootFolder">搜索根目录</param>
        /// <param name="filePath">待处理文件的完整路径</param>
        /// <param name="options">搜索配置</param>
        /// <param name="result">搜索结果</param>
        /// <param name="folderMatchMap">文件夹匹配数统计字典</param>
        /// <param name="folderFileMap">文件夹文件数统计字典</param>
        private void ProcessFile(string rootFolder, string filePath, SearchOptions options,
            SearchResult result, Dictionary<string, int> folderMatchMap, Dictionary<string, int> folderFileMap)
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            string folder = Path.GetDirectoryName(filePath);
            string fileName = Path.GetFileName(filePath);

            // 判断是否为压缩包（先判压缩包，避免 zip 被"未知扩展名当作文本"逻辑吃掉）
            bool isZip = options.ScanZipFiles && options.ZipExtensions != null && options.ZipExtensions.Contains(ext);

            if (isZip)
            {
                result.TotalZipScanned++;
                Log($"      [处理压缩包] {fileName}");
                ProcessZipFile(rootFolder, filePath, options, result, folderMatchMap, folderFileMap);
                return;
            }

            // 判断是否为文本文件（走综合判定，输出跳过原因）
            bool isTextFile = IsTextFileByExtension(fileName, options, out string skipReason);
            if (isTextFile)
            {
                result.TotalFileScanned++;
                IncrementDict(folderFileMap, folder, 1);

                int matchCount = SearchInTextFile(filePath, options, result, isInsideZip: false, zipPath: null, entryName: null);
                if (matchCount > 0)
                {
                    Log($"        [!] 发现 {matchCount} 处匹配 -> {fileName}");
                    IncrementDict(folderMatchMap, folder, matchCount);
                }
            }
            else
            {
                // 非压缩包又没被判为文本 → 输出跳过原因，便于排查
                Log($"        [跳过] {fileName}  原因: {skipReason ?? "未识别的类型"}");
            }
        }

        /// <summary>
        /// 打开ZIP压缩包，遍历其中所有条目并对符合条件的文本条目执行搜索
        /// </summary>
        /// <param name="rootFolder">搜索根目录</param>
        /// <param name="zipPath">压缩包物理路径</param>
        /// <param name="options">搜索配置</param>
        /// <param name="result">搜索结果</param>
        /// <param name="folderMatchMap">文件夹匹配数统计字典</param>
        /// <param name="folderFileMap">文件夹文件数统计字典</param>
        private void ProcessZipFile(string rootFolder, string zipPath, SearchOptions options,
            SearchResult result, Dictionary<string, int> folderMatchMap, Dictionary<string, int> folderFileMap)
        {
            // 压缩包内的所有匹配统计到压缩包物理所在的文件夹
            string folder = Path.GetDirectoryName(zipPath);
            int skippedEntries = 0;

            try
            {
                using (var archive = ZipFile.OpenRead(zipPath))
                {
                    int totalEntries = archive.Entries.Count;
                    int processedEntries = 0;

                    foreach (var entry in archive.Entries)
                    {
                        // 跳过空条目（通常是目录条目）
                        if (entry.Length == 0)
                            continue;

                        // 压缩包内的扩展名过滤与普通文件一致，走综合判定
                        bool isTextEntry = IsTextFileByExtension(entry.Name, options, out string skipReason);
                        if (!isTextEntry)
                        {
                            skippedEntries++;
                            continue;
                        }

                        processedEntries++;
                        result.TotalFileScanned++;
                        IncrementDict(folderFileMap, folder, 1);

                        int matchCount = SearchInZipEntry(zipPath, entry, options, result);
                        if (matchCount > 0)
                        {
                            Log($"        [!] 压缩包内发现 {matchCount} 处匹配 -> {entry.FullName}");
                            IncrementDict(folderMatchMap, folder, matchCount);
                        }
                    }

                    string skippedInfo = skippedEntries > 0 ? $"，跳过 {skippedEntries} 个非文本条目" : string.Empty;
                    Log($"        [压缩包统计] 共 {totalEntries} 条目，已扫描 {processedEntries} 个文本文件{skippedInfo}");
                }
            }
            catch (Exception ex)
            {
                LogError($"解析压缩包出错 [{zipPath}]: {ex.Message}");
            }
        }

        /// <summary>
        /// 在普通磁盘文本文件中逐行搜索匹配内容
        /// </summary>
        /// <param name="filePath">文本文件完整路径</param>
        /// <param name="options">搜索配置</param>
        /// <param name="result">搜索结果对象（匹配记录会被追加）</param>
        /// <param name="isInsideZip">是否来自压缩包内部（普通文件传 false）</param>
        /// <param name="zipPath">压缩包路径（普通文件传 null）</param>
        /// <param name="entryName">压缩包条目名（普通文件传 null）</param>
        /// <returns>在该文件中匹配到的总次数</returns>
        private int SearchInTextFile(string filePath, SearchOptions options, SearchResult result,
            bool isInsideZip, string zipPath, string entryName)
        {
            int count = 0;
            try
            {
                IEnumerable<string> lines;
                if (options.TextEncoding != null)
                {
                    // 使用调用方指定的编码读取
                    lines = File.ReadLines(filePath, options.TextEncoding);
                }
                else
                {
                    // 自动检测文件编码（BOM优先，回退UTF-8）
                    lines = ReadLinesWithAutoEncoding(filePath);
                }

                count = ProcessLines(lines, filePath, options, result, isInsideZip, zipPath, entryName);
            }
            catch (Exception ex)
            {
                LogError($"读取文件出错 [{filePath}]: {ex.Message}");
            }
            return count;
        }

        /// <summary>
        /// 在ZIP压缩包的单个条目（文件）中逐行搜索匹配内容
        /// </summary>
        /// <param name="zipPath">压缩包物理路径（用于日志和记录标识）</param>
        /// <param name="entry">压缩包条目对象</param>
        /// <param name="options">搜索配置</param>
        /// <param name="result">搜索结果对象</param>
        /// <returns>在该压缩包条目中匹配到的总次数</returns>
        private int SearchInZipEntry(string zipPath, ZipArchiveEntry entry, SearchOptions options, SearchResult result)
        {
            int count = 0;
            try
            {
                // 从压缩包条目打开只读流，避免解压到磁盘造成IO开销
                using (var stream = entry.Open())
                using (var reader = new StreamReader(stream, GetEncoding(options.TextEncoding)))
                {
                    var lines = new List<string>();
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        lines.Add(line);
                    }
                    count = ProcessLines(lines, zipPath, options, result, isInsideZip: true, zipPath: zipPath, entryName: entry.FullName);
                }
            }
            catch (Exception ex)
            {
                LogError($"读取压缩包条目出错 [{zipPath}!{entry.FullName}]: {ex.Message}");
            }
            return count;
        }

        /// <summary>
        /// 核心搜索逻辑：对行序列执行匹配，命中时构造 MatchRecord 并追加到结果集
        /// </summary>
        /// <param name="lines">待扫描的行序列</param>
        /// <param name="displayPath">展示用的文件路径</param>
        /// <param name="options">搜索配置</param>
        /// <param name="result">搜索结果对象</param>
        /// <param name="isInsideZip">是否来自压缩包</param>
        /// <param name="zipPath">压缩包路径</param>
        /// <param name="entryName">压缩包条目名</param>
        /// <returns>匹配到的次数</returns>
        private int ProcessLines(IEnumerable<string> lines, string displayPath, SearchOptions options,
            SearchResult result, bool isInsideZip, string zipPath, string entryName)
        {
            int count = 0;
            int lineNumber = 0;

            // 根据配置构造匹配器（正则或普通文本），仅构造一次以提升性能
            Func<string, bool> matcher;
            if (options.UseRegex)
            {
                var regexOptions = options.IgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;
                var regex = new Regex(options.SearchText, regexOptions);
                matcher = (text) => regex.IsMatch(text);
            }
            else
            {
                var comparison = options.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                matcher = (text) => text.IndexOf(options.SearchText, comparison) >= 0;
            }

            foreach (var line in lines)
            {
                lineNumber++;
                if (line != null && matcher(line))
                {
                    count++;
                    result.Matches.Add(new MatchRecord
                    {
                        FilePath = isInsideZip ? zipPath : displayPath,
                        LineNumber = lineNumber,
                        LineContent = line,
                        IsInsideZip = isInsideZip,
                        ZipPath = zipPath,
                        EntryName = entryName
                    });
                }
            }

            return count;
        }

        /// <summary>
        /// 自动检测文件编码后返回行序列
        /// 检测顺序：UTF-8 BOM → UTF-16 LE → UTF-16 BE → 默认 UTF-8
        /// </summary>
        /// <param name="filePath">目标文件路径</param>
        /// <returns>按检测编码读取的行序列</returns>
        private IEnumerable<string> ReadLinesWithAutoEncoding(string filePath)
        {
            var encoding = DetectEncoding(filePath);
            return File.ReadLines(filePath, encoding);
        }

        /// <summary>
        /// 通过读取文件头部BOM字节自动检测编码方式；未识别时回退到UTF-8
        /// </summary>
        /// <param name="filePath">目标文件路径</param>
        /// <returns>检测到的 Encoding 对象</returns>
        private Encoding DetectEncoding(string filePath)
        {
            try
            {
                byte[] buffer = new byte[4];
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    int read = fs.Read(buffer, 0, 4);
                    if (read >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
                        return Encoding.UTF8;
                    if (read >= 2 && buffer[0] == 0xFF && buffer[1] == 0xFE)
                        return Encoding.Unicode;
                    if (read >= 2 && buffer[0] == 0xFE && buffer[1] == 0xFF)
                        return Encoding.BigEndianUnicode;
                }
            }
            catch
            {
                // 检测失败时静默回退到默认编码
            }
            return Encoding.UTF8;
        }

        /// <summary>
        /// 获取实际使用的编码：优先使用配置编码，否则回退到UTF-8
        /// </summary>
        /// <param name="preferred">调用方指定的编码（可能为null）</param>
        /// <returns>非空的 Encoding 对象</returns>
        private Encoding GetEncoding(Encoding preferred)
        {
            return preferred ?? Encoding.UTF8;
        }

        /// <summary>
        /// 线程安全的字典值累加辅助方法：若键已存在则累加，否则新增键
        /// </summary>
        /// <param name="dict">目标字典</param>
        /// <param name="key">键</param>
        /// <param name="amount">累加值</param>
        private void IncrementDict(Dictionary<string, int> dict, string key, int amount)
        {
            if (dict.ContainsKey(key))
                dict[key] += amount;
            else
                dict[key] = amount;
        }

        /// <summary>
        /// 已知二进制扩展名黑名单：这些类型无论如何都不会当作文本读取，避免产生垃圾数据或解码异常
        /// </summary>
        private static readonly HashSet<string> BinaryExtensionBlacklist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".dll", ".pdb", ".bin", ".obj", ".o", ".a", ".lib", ".so", ".dylib",
            ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tiff", ".ico", ".webp", ".svg",
            ".mp3", ".mp4", ".avi", ".mov", ".wav", ".flv", ".mkv", ".wmv", ".zip", ".7z", ".rar", ".tar", ".gz", ".bz2",
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".rtf",
            ".class", ".jar", ".war", ".ear", ".pyc", ".pyo",
            ".msi", ".cab", ".iso", ".img", ".dmg",
            ".sys", ".drv", ".sfx", ".suo", ".user", ".nupkg", ".snupkg"
        };

        /// <summary>
        /// 根据扩展名综合判断该文件是否应当作为文本文件扫描
        /// 判断顺序：
        /// 1) 扩展名若在二进制黑名单 → 否
        /// 2) 扩展名若在 FileExtensions 白名单 → 是
        /// 3) 无扩展名 + IncludeNoExtension → 是
        /// 4) 纯数字扩展名(.1/.12/.9 等) + IncludeNumericExtensions → 是
        /// 5) 其余未知扩展名 + ScanAllUnknownAsText → 是
        /// 6) 其他 → 否
        /// </summary>
        /// <param name="fileName">文件名（或压缩包条目名），用于提取扩展名</param>
        /// <param name="options">搜索配置</param>
        /// <param name="skipReason">当返回 false 时，输出人类可读的跳过原因（用于日志）</param>
        /// <returns>true=按文本文件处理，false=跳过</returns>
        private bool IsTextFileByExtension(string fileName, SearchOptions options, out string skipReason)
        {
            skipReason = null;
            string ext = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
            bool hasExt = !string.IsNullOrEmpty(ext);

            // (1) 明确的二进制扩展名 → 跳过
            if (hasExt && BinaryExtensionBlacklist.Contains(ext))
            {
                skipReason = $"二进制扩展名黑名单 [{ext}]";
                return false;
            }

            // (2) 白名单命中 → 是文本
            if (hasExt && options.FileExtensions != null && options.FileExtensions.Contains(ext))
                return true;

            // (3) 无扩展名
            if (!hasExt)
            {
                if (options.IncludeNoExtension)
                    return true;
                skipReason = "无扩展名，且 IncludeNoExtension=false";
                return false;
            }

            // (4) 纯数字扩展名（日志轮转 .1 .2 .12 .17 等）
            // 去掉前面的点号后判断是否全部为数字
            string extWithoutDot = ext.Substring(1);
            if (extWithoutDot.Length > 0 && extWithoutDot.All(c => char.IsDigit(c)))
            {
                if (options.IncludeNumericExtensions)
                    return true;
                skipReason = $"纯数字扩展名 [{ext}]，且 IncludeNumericExtensions=false";
                return false;
            }

            // (5) 其他未知扩展名 → 根据 ScanAllUnknownAsText 决定
            if (options.ScanAllUnknownAsText)
                return true;

            skipReason = $"未知扩展名 [{ext}]，且 ScanAllUnknownAsText=false";
            return false;
        }

        /// <summary>
        /// 将搜索结果格式化为可读报告并输出到控制台
        /// 包含：配置摘要 → 汇总统计 → 按文件夹统计 → 逐行匹配明细
        /// </summary>
        /// <param name="result">搜索结果</param>
        /// <param name="options">搜索配置（用于显示搜索条件）</param>
        public void PrintReport(SearchResult result, SearchOptions options)
        {
            // 绑定 options（即使单独调用 PrintReport，也能保证 Log 开关生效）
            if (options != null) _currentOptions = options;
            var sb = new StringBuilder();

            // 合并后展示全部输入路径（与 Search 方法一致）
            var allPaths = new List<string>();
            if (options.SearchPaths != null) allPaths.AddRange(options.SearchPaths);
            if (options.SearchFolders != null) allPaths.AddRange(options.SearchFolders);
            allPaths = allPaths.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            sb.AppendLine(new string('=', 80));
            sb.AppendLine($"搜索报告");
            sb.AppendLine(new string('=', 80));
            sb.AppendLine($"搜索文本: {options.SearchText}");
            sb.AppendLine($"忽略大小写: {options.IgnoreCase}");
            sb.AppendLine($"使用正则: {options.UseRegex}");
            sb.AppendLine($"扫描路径({allPaths.Count}项，文件夹/文件混合):");
            foreach (var p in allPaths)
            {
                string kind = Directory.Exists(p) ? "[目录]" : File.Exists(p) ? "[文件]" : "[无效]";
                sb.AppendLine($"  {kind} {p}");
            }
            sb.AppendLine(new string('-', 80));

            sb.AppendLine($"【汇总统计】");
            sb.AppendLine($"  匹配总次数: {result.TotalMatchCount}");
            sb.AppendLine($"  扫描文件数: {result.TotalFileScanned}");
            sb.AppendLine($"  扫描压缩包: {result.TotalZipScanned}");
            sb.AppendLine($"  匹配文件数: {result.Matches.Select(m => m.IsInsideZip ? m.ZipPath + "!" + m.EntryName : m.FilePath).Distinct().Count()}");
            sb.AppendLine($"  耗时: {result.ElapsedTime.TotalSeconds:F2} 秒");
            sb.AppendLine(new string('-', 80));

            // ====== 新增：按单个文件统计匹配数量 ======
            var fileStat = result.Matches
                .GroupBy(m => m.IsInsideZip
                    ? $"{Path.GetFileName(m.ZipPath)}!{m.EntryName}"
                    : m.FilePath)
                .Select(g => new
                {
                    DisplayName = g.Key,
                    FullKey = g.First().IsInsideZip ? $"{g.First().ZipPath}!{g.First().EntryName}" : g.First().FilePath,
                    Count = g.Count(),
                    IsZip = g.First().IsInsideZip,
                    InsideFile = g.First().IsInsideZip ? g.First().EntryName : null,
                    ZipFile = g.First().IsInsideZip ? g.First().ZipPath : null,
                    PlainFile = g.First().IsInsideZip ? null : g.First().FilePath
                })
                .OrderByDescending(x => x.Count)
                .ToList();

            sb.AppendLine($"【按单个文件统计】");
            sb.AppendLine($"  匹配总次数: {result.TotalMatchCount}   (共 {fileStat.Count} 个文件命中)");
            sb.AppendLine(new string('-', 80));
            sb.AppendLine($"{"序号",-5} {"匹配数",-8} {"位置",-8} {"文件名"}");
            sb.AppendLine(new string('-', 80));
            int fIdx = 0;
            foreach (var fs in fileStat)
            {
                fIdx++;
                string pos = fs.IsZip ? "[压缩包]" : "[文件]";
                sb.AppendLine($"{fIdx,-5} {fs.Count,-8} {pos,-8} {fs.DisplayName}");
            }
            sb.AppendLine(new string('-', 80));

            sb.AppendLine($"【按文件夹统计】");
            sb.AppendLine($"{"文件夹路径",-60} {"匹配数",8} {"文件数",8}");
            sb.AppendLine(new string('-', 80));
            foreach (var stat in result.FolderStats)
            {
                sb.AppendLine($"{stat.FolderPath,-60} {stat.MatchCount,8} {stat.FileCount,8}");
            }
            sb.AppendLine(new string('-', 80));

            sb.AppendLine($"【匹配明细】");
            sb.AppendLine($"{"#",-5} {"匹配行号",-10} {"位置",-10} {"真实文件名"}");
            sb.AppendLine(new string('-', 80));

            int idx = 0;
            foreach (var match in result.Matches)
            {
                idx++;
                string pos = match.IsInsideZip ? "[压缩包]" : "[文件]";
                // 真实文件名：压缩包内显示 "压缩包文件名!条目名"，普通文件显示完整路径（左对齐截断）
                string realName = match.IsInsideZip
                    ? $"{Path.GetFileName(match.ZipPath)}!{match.EntryName}"
                    : match.FilePath;

                sb.AppendLine($"{idx,-5} {match.LineNumber,-10} {pos,-10} {realName}");
                if (!string.IsNullOrEmpty(match.LineContent))
                {
                    sb.AppendLine($"      内容: {match.LineContent.Trim()}");
                }
            }
            sb.AppendLine(new string('=', 80));

            // 分块写入 Console（避免一次性写入超大字符串触发 sandbox 输出缓冲区崩溃）
            var fullReport = sb.ToString();
            using (var reader = new StringReader(fullReport))
            {
                string? line;
                int lineCnt = 0;
                while ((line = reader.ReadLine()) != null)
                {
                    Console.WriteLine(line);
                    lineCnt++;
                    // 每输出 200 行小歇一下（极端大报告时让控制台缓冲有时间刷）
                    if (lineCnt % 200 == 0)
                    {
                        Console.Out.Flush();
                        Thread.Sleep(1);
                    }
                }
                Console.Out.Flush();
            }
        }

        /// <summary>
        /// 将搜索结果导出为CSV格式文件（带UTF-8 BOM，可直接在Excel中打开）
        /// </summary>
        /// <param name="result">搜索结果</param>
        /// <param name="options">搜索配置</param>
        /// <param name="outputPath">CSV输出路径（包含文件名）</param>
        public void ExportReportToFile(SearchResult result, SearchOptions options, string outputPath)
        {
            // 绑定 options（即使单独调用 ExportReportToFile，也能保证 Log 开关生效）
            if (options != null) _currentOptions = options;
            var sb = new StringBuilder();

            // 合并后展示全部输入路径（与 Search 方法一致）
            var allPaths = new List<string>();
            if (options.SearchPaths != null) allPaths.AddRange(options.SearchPaths);
            if (options.SearchFolders != null) allPaths.AddRange(options.SearchFolders);
            allPaths = allPaths.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            sb.AppendLine("搜索报告");
            sb.AppendLine($"生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"搜索文本: {options.SearchText}");
            //sb.AppendLine($"忽略大小写: {options.IgnoreCase}");
            //sb.AppendLine($"使用正则: {options.UseRegex}");
            sb.AppendLine($"扫描路径(共{allPaths.Count}项，文件夹/文件混合):");
            foreach (var p in allPaths)
            {
                string kind = Directory.Exists(p) ? "[目录]" : File.Exists(p) ? "[文件]" : "[无效]";
                sb.AppendLine($"  {kind} {p}");
            }
            sb.AppendLine();

            sb.AppendLine("【汇总统计】");
            sb.AppendLine($"匹配总次数, {result.TotalMatchCount}");
            sb.AppendLine($"扫描文件数, {result.TotalFileScanned}");
            sb.AppendLine($"扫描压缩包, {result.TotalZipScanned}");
            sb.AppendLine($"匹配到的独立文件数, {result.Matches.Select(m => m.IsInsideZip ? m.ZipPath + "!" + m.EntryName : m.FilePath).Distinct().Count()}");
            sb.AppendLine($"耗时(秒), {result.ElapsedTime.TotalSeconds:F2}");
            sb.AppendLine();

            // ====== 新增：按单个文件统计匹配数量 ======
            var fileStatCsv = result.Matches
                .GroupBy(m => m.IsInsideZip
                    ? $"{Path.GetFileName(m.ZipPath)}!{m.EntryName}"
                    : m.FilePath)
                .Select(g => new
                {
                    DisplayName = g.Key,
                    Count = g.Count(),
                    IsZip = g.First().IsInsideZip,
                    EntryName = g.First().IsInsideZip ? g.First().EntryName : null,
                    ZipPath = g.First().IsInsideZip ? g.First().ZipPath : null,
                    PlainPath = g.First().IsInsideZip ? null : g.First().FilePath
                })
                .OrderByDescending(x => x.Count)
                .ToList();

            sb.AppendLine("【按单个文件统计】");
            sb.AppendLine($"匹配总次数,{result.TotalMatchCount},,,命中文件总数,{fileStatCsv.Count}");
            sb.AppendLine("序号,匹配数,位置,真实文件名,压缩包路径,压缩包内条目,普通文件路径");
            int fIdxCsv = 0;
            foreach (var fs in fileStatCsv)
            {
                fIdxCsv++;
                string pos = fs.IsZip ? "[压缩包]" : "[文件]";
                sb.AppendLine($"{fIdxCsv},{fs.Count},{pos},{EscapeCsv(fs.DisplayName)},{EscapeCsv(fs.ZipPath)},{EscapeCsv(fs.EntryName)},{EscapeCsv(fs.PlainPath)}");
            }
            sb.AppendLine();

            sb.AppendLine("【按文件夹统计】");
            sb.AppendLine("文件夹路径,匹配数,文件数");
            foreach (var stat in result.FolderStats)
            {
                sb.AppendLine($"{EscapeCsv(stat.FolderPath)},{stat.MatchCount},{stat.FileCount}");
            }
            sb.AppendLine();

            sb.AppendLine("【匹配明细】");
            sb.AppendLine("序号,匹配行号,位置,真实文件名,文件完整路径,压缩包路径,压缩包条目,行内容");
            int idx = 0;
            foreach (var match in result.Matches)
            {
                idx++;
                string pos = match.IsInsideZip ? "[压缩包]" : "[文件]";
                string realName = match.IsInsideZip
                    ? $"{Path.GetFileName(match.ZipPath)}!{match.EntryName}"
                    : match.FilePath;
                string fullPath = match.IsInsideZip ? $"{match.ZipPath}!{match.EntryName}" : match.FilePath;
                sb.AppendLine($"{idx},{match.LineNumber},{pos},{EscapeCsv(realName)},{EscapeCsv(fullPath)},{EscapeCsv(match.ZipPath)},{EscapeCsv(match.EntryName)},{EscapeCsv(match.LineContent)}");
            }

            // 写入 CSV：支持被占用（比如 Excel 打开中）时自动重试，仍失败则切到带时间戳的新文件名，
            // 并 try/catch，保证 CSV 导出失败不会影响后续控制台报告打印。
            string csvContent = sb.ToString();
            bool saved = false;
            string actualSavePath = outputPath;
            int retries = 3;
            for (int i = 0; i < retries && !saved; i++)
            {
                try
                {
                    File.WriteAllText(actualSavePath, csvContent, new UTF8Encoding(true));
                    saved = true;
                }
                catch (IOException) when (i < retries - 1)
                {
                    // 前 N-1 次：尝试在文件名上追加时间戳避免占用冲突
                    string dir = Path.GetDirectoryName(actualSavePath) ?? ".";
                    string name = Path.GetFileNameWithoutExtension(outputPath);
                    string ext = Path.GetExtension(outputPath);
                    actualSavePath = Path.Combine(dir, $"{name}_{DateTime.Now:yyyyMMdd_HHmmss}{ext}");
                    Thread.Sleep(50);
                }
                catch (Exception ex)
                {
                    LogError($"导出 CSV 失败（路径: {actualSavePath}）: {ex.Message}");
                }
            }
            if (saved)
            {
                if (string.Equals(actualSavePath, outputPath, StringComparison.OrdinalIgnoreCase))
                    Log($"[报告导出] CSV已保存: {actualSavePath}");
                else
                    Log($"[报告导出] 原路径被占用，已改用新文件名保存: {actualSavePath}  (原路径: {outputPath})");
            }
        }

        /// <summary>
        /// CSV字段转义：当值包含逗号、双引号或换行时，用双引号包裹并转义内部双引号
        /// </summary>
        /// <param name="value">原始字段值</param>
        /// <returns>符合CSV标准的安全字段值</returns>
        private string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }
            return value;
        }

        /// <summary>
        /// 输出普通信息日志（带时间戳前缀）
        /// 受 SearchOptions.EnableConsoleLog 控制：=false 时不再输出进度日志（只留错误 + 最终报告）
        /// </summary>
        /// <param name="message">日志内容</param>
        private void Log(string message)
        {
            // EnableConsoleLog 默认 true；当 options 未绑定或开关为 true 时才输出进度日志
            if (_currentOptions != null && !_currentOptions.EnableConsoleLog)
                return;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
        }

        /// <summary>
        /// 输出错误级别日志，使用标准错误流
        /// 注意：错误日志不受 EnableConsoleLog 影响，始终输出（便于发现扫描异常）
        /// </summary>
        /// <param name="message">错误信息</param>
        private void LogError(string message)
        {
            Console.Error.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [错误] {message}");
        }

        /// <summary>
        /// 使用示例方法：演示如何配置参数（支持文件夹 + 具体文件混合输入）、启动搜索、打印报告、导出CSV
        /// </summary>
        public static void RunExample()
        {
            var searcher = new SearchCode();

            // 示例：推荐使用 SearchPaths，支持混合输入文件夹路径和具体文件路径
            // - 文件夹路径：会递归扫描所有子目录
            // - 具体文件路径：直接按扩展名识别为文本文件(txt/xml等)或压缩包(zip)并处理
            var options = new SearchOptions
            {
                SearchPaths = new List<string>
                {
                    @"C:\Logs",
                    @"C:\AnotherFolder",
                    @"C:\单独的日志文件\app_20260828.log",
                    @"C:\打包的历史数据\history_archive.zip",
                    @"C:\没有扩展名的配置文件\named_config"
                },
                SearchText = "TCP error code 10061:",
                IgnoreCase = true,
                UseRegex = false,
                IncludeNoExtension = true,
                ScanZipFiles = true,
                FileExtensions = new List<string> { ".txt", ".xml", ".log", ".config", ".json", ".csv" },
                ZipExtensions = new List<string> { ".zip" }
            };

            Console.WriteLine("开始搜索...");
            var result = searcher.Search(options);

            searcher.PrintReport(result, options);

            string reportPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                $"SearchReport_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            searcher.ExportReportToFile(result, options, reportPath);
            Console.WriteLine($"报告已导出到: {reportPath}");
        }
    }
}
