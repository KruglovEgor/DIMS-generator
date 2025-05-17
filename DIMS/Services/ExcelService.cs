using DIMS.Models;
using Newtonsoft.Json;
using OfficeOpenXml;
using System.ComponentModel;
using System.IO;
using System.Net.Http;


namespace DIMS.Services
{
    public class ExcelService
    {
        private readonly RedmineApiService _redmineApiService;
        private readonly string _templatePath;

        // Словари для хранения уже созданных проектов и задач, чтобы не создавать их повторно
        private Dictionary<string, int> _createdProjects = new Dictionary<string, int>();
        private Dictionary<string, int> _createdParentIssues = new Dictionary<string, int>();

        static ExcelService()
        {
            // Устанавливаем некоммерческую лицензию для EPPlus
            
        }

        public ExcelService(RedmineApiService redmineApiService, string templatePath)
        {
            _redmineApiService = redmineApiService;
            _templatePath = templatePath;

            ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;
        }

        public async Task ProcessExcelFile(string filePath)
        {
            // Проверяем относительные пути в различных комбинациях
            string originalPath = filePath;
            List<string> pathsToTry = new List<string>
            {
                originalPath,
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, originalPath),
                Path.Combine(Directory.GetCurrentDirectory(), originalPath),
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", originalPath)),
                Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", originalPath))
            };

            Console.WriteLine($"Текущая директория: {Directory.GetCurrentDirectory()}");
            Console.WriteLine($"Директория сборки: {AppDomain.CurrentDomain.BaseDirectory}");

            string actualFilePath = null;
            foreach (var path in pathsToTry)
            {
                Console.WriteLine($"Проверка пути: {path}");
                if (File.Exists(path))
                {
                    actualFilePath = path;
                    Console.WriteLine($"Файл найден: {actualFilePath}");
                    break;
                }
            }

            if (actualFilePath == null)
            {
                throw new FileNotFoundException($"Файл не найден: {originalPath}. Проверены следующие пути: {string.Join(", ", pathsToTry)}");
            }

            Console.WriteLine($"Обработка файла: {actualFilePath}");

            using var package = new ExcelPackage(new FileInfo(actualFilePath));
            var worksheet = package.Workbook.Worksheets[0]; // Первый лист

            // Получаем заголовки колонок из первой строки
            var headers = new Dictionary<string, int>();
            int colCount = worksheet.Dimension.End.Column;

            for (int col = 1; col <= colCount; col++)
            {
                string header = worksheet.Cells[1, col].Text?.Trim();
                if (!string.IsNullOrEmpty(header))
                {
                    headers[header] = col;
                }
            }

            Console.WriteLine($"Найдено {headers.Count} колонок");

            // Начинаем с третьей строки (пропускаем подсказку и заголовок)
            int rowCount = worksheet.Dimension.End.Row;
            int processedRows = 0;

            for (int row = 3; row <= rowCount; row++)
            {
                var firstCell = worksheet.Cells[row, 1].Text;
                var secondCell = worksheet.Cells[row, 2].Text;

                // Проверяем, не пустая ли строка
                if (string.IsNullOrEmpty(firstCell) && string.IsNullOrEmpty(secondCell))
                    continue;

                try
                {
                    await ProcessRow(worksheet, row, headers);
                    processedRows++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при обработке строки {row}: {ex.Message}");
                }
            }

            Console.WriteLine($"Обработано {processedRows} задач");
        }

        private async Task ProcessRow(ExcelWorksheet worksheet, int rowIndex, Dictionary<string, int> headers)
        {
            // Создаем или получаем проект
            var projectId = await GetOrCreateProject(worksheet, rowIndex, headers);

            // Создаем или получаем родительскую задачу, если есть
            int? parentIssueId = await GetOrCreateParentIssue(worksheet, rowIndex, headers, projectId);

            // Создаем задачу
            await CreateIssue(worksheet, rowIndex, headers, projectId, parentIssueId);
        }

        private async Task<int> GetOrCreateProject(ExcelWorksheet worksheet, int rowIndex, Dictionary<string, int> headers)
        {
            // Получаем название проекта
            string projectName = GetCellValue(worksheet, rowIndex, headers, "name (projects)");
            string projectIdentifier = GetValidIdentifier(GetCellValue(worksheet, rowIndex, headers, "identifier (projects)"));
            if (string.IsNullOrEmpty(projectName))
            {
                throw new InvalidOperationException("Не указано название проекта");
            }

            // Проверяем, был ли уже создан такой проект
            if (_createdProjects.TryGetValue(projectIdentifier, out int existingProjectId))
            {
                return existingProjectId;
            }

            try
            {
                int existingProjectIdInRedmine = await _redmineApiService.GetProjectIdByIdentifier(projectIdentifier);
                if (existingProjectIdInRedmine > 0)
                {
                    // Проект существует в Redmine, сохраняем его ID в локальном кэше
                    Console.WriteLine($"Найден существующий проект: {projectIdentifier} (ID: {existingProjectIdInRedmine})");
                    _createdProjects[projectIdentifier] = existingProjectIdInRedmine;
                    return existingProjectIdInRedmine;
                }
            }
            catch (Exception ex)
            {
                // Проект не найден или произошла ошибка при поиске, продолжаем создание нового
                Console.WriteLine($"Проект {projectIdentifier} не найден в Redmine: {ex.Message}");
            }



            //TODO : спросить про эту логику
            string parentProjecId = GetCellValue(worksheet, rowIndex, headers, "parent_id (projects)");
            if (string.IsNullOrEmpty(parentProjecId))
            {
                parentProjecId = "3"; // Если родительский проект не указан, используем корневой проект
            }

            // Создаем новый проект
            var project = new RedmineProject
            {
                Name = projectName,
                Identifier = projectIdentifier,
                Parent = new RedmineProjectParent { Id = int.Parse(parentProjecId) }, // ID родительского проекта (если есть)
                CustomFields = new List<RedmineCustomField>()
            };

            string[] projectCustomFields = { "43", "44", "38", "45", "39", "35", "36", "40", "41", "42"};

            // Добавляем кастомные поля проекта, если они есть в заголовках
            foreach (var header in headers)
            {
                if (projectCustomFields.Contains(header.Key)) {
                    string value = GetCellValue(worksheet, rowIndex, headers, header.Key);
                    if (!string.IsNullOrEmpty(value))
                    {
                        project.CustomFields.Add(new RedmineCustomField
                        {
                            Id = int.Parse(header.Key),
                            Value = value
                        });
                    }
                }
                
            }

            Console.WriteLine($"Создание проекта: {projectIdentifier}");
            int newProjectId = await _redmineApiService.CreateProject(project);

            // Сохраняем созданный проект в словаре
            _createdProjects[projectIdentifier] = newProjectId;

            return newProjectId;
        }


       


        private async Task<int?> GetOrCreateParentIssue(ExcelWorksheet worksheet, int rowIndex, Dictionary<string, int> headers, int projectId)
        {
            //// Проверяем наличие родительской задачи
            //if (!headers.ContainsKey("ParentIssue") ||
            //    string.IsNullOrEmpty(GetCellValue(worksheet, rowIndex, headers, "ParentIssue")))
            //{
            //    return null;
            //}

            string parentIssueSubject = GetCellValue(worksheet, rowIndex, headers, "parent_subject (issues)");
            if (string.IsNullOrEmpty(parentIssueSubject))
            {
                return null;
            }
            string parentIssueKey = $"{projectId}_{parentIssueSubject}";

            // Проверяем, была ли уже создана такая задача
            if (_createdParentIssues.TryGetValue(parentIssueKey, out int existingIssueId))
            {
                return existingIssueId;
            }

            string parentIssueTrackerIdStr = GetCellValue(worksheet, rowIndex, headers, "parent_tracker_id (issues)");
            int trackerId = int.Parse(parentIssueTrackerIdStr.Split('_')[0]);

            try
            {
                // Проверяем существование задачи в Redmine
                int existingIssueIdInRedmine = await _redmineApiService.FindIssueBySubjectInProject(
                    projectId, parentIssueSubject, trackerId);

                if (existingIssueIdInRedmine > 0)
                {
                    // Задача существует в Redmine, сохраняем её ID в локальном кэше
                    Console.WriteLine($"Найдена существующая родительская задача: {parentIssueSubject} (ID: {existingIssueIdInRedmine})");
                    _createdParentIssues[parentIssueKey] = existingIssueIdInRedmine;
                    return existingIssueIdInRedmine;
                }
            }
            catch (Exception ex)
            {
                // Задача не найдена или произошла ошибка при поиске, продолжаем создание новой
                Console.WriteLine($"Родительская задача '{parentIssueSubject}' не найдена в Redmine: {ex.Message}");
            }

            // Создаем новую родительскую задачу
            var parentIssue = new RedmineIssue
            {
                ProjectId = projectId,
                Subject = parentIssueSubject,
                Tracker = new RedmineTracker { Id = trackerId },
                CustomFields = new List<RedmineCustomField>()
            };

            Console.WriteLine($"Создание родительской задачи: {parentIssue.Subject}");
            int newParentIssueId = await _redmineApiService.CreateIssue(parentIssue);

            // Сохраняем созданную задачу в словаре
            _createdParentIssues[parentIssueKey] = newParentIssueId;

            return newParentIssueId;
        }

        private async Task CreateIssue(ExcelWorksheet worksheet, int rowIndex, Dictionary<string, int> headers, int projectId, int? parentIssueId)
        {
            var issue = new RedmineIssue
            {
                ProjectId = projectId,
                Subject = GetCellValue(worksheet, rowIndex, headers, "subject (issues)") ?? "Без названия",
                Description = GetCellValue(worksheet, rowIndex, headers, "description (issues)") ?? "",
                //Tracker = new RedmineTracker { Id = int.Parse(GetCellValue(worksheet, rowIndex, headers, "tracker_id\r\n(issues)").Split('_')[0]) }, // ID трекера по умолчанию
                CustomFields = new List<RedmineCustomField>()
            };

            // Устанавливаем родительскую задачу, если есть
            if (parentIssueId.HasValue)
            {
                issue.Parent = new RedmineIssueParent { Id = parentIssueId.Value };
            }

            // Обрабатываем все поля из заголовков
            foreach (var header in headers)
            {
                string value = GetCellValue(worksheet, rowIndex, headers, header.Key);
                if (string.IsNullOrEmpty(value))
                    continue;

                switch (header.Key)
                {
                    case "tracker_id (issues)":
                        if (int.TryParse(value.Split('_')[0], out int trackerId))
                        {
                            issue.Tracker = new RedmineTracker { Id = trackerId };
                        }
                        break;

                    case "assigned_to_id (issues)":
                        if (int.TryParse(value.Split('_')[0], out int userId))
                        {
                            issue.AssignedTo = new RedmineUser { Id = userId };
                        }
                        break;

                    case "start_date (issues)":
                        if (DateTime.TryParse(value, out DateTime startDate))
                        {
                            issue.StartDate = startDate;
                        }
                        break;

                    case "due_date (issues)":
                        if (DateTime.TryParse(value, out DateTime dueDate))
                        {
                            issue.DueDate = dueDate;
                        }
                        break;

                    case "estimated_hours (issues)":
                        if (decimal.TryParse(value, out decimal hours))
                        {
                            issue.EstimatedHours = hours;
                        }
                        break;

                    default:
                        // Обрабатываем кастомные поля задачи
                        string[] issueCustomFields = { "20", "49", "37", "47", "48", "46"};

                        if (issueCustomFields.Contains(header.Key))
                        {
                            if (int.TryParse(header.Key, out int cfId))
                            {
                                issue.CustomFields.Add(new RedmineCustomField
                                {
                                    Id = cfId,
                                    Value = value
                                });
                            }
                        }
                        break;
                }
            }

            Console.WriteLine($"Создание задачи: {issue.Subject}");
            await _redmineApiService.CreateIssue(issue);
        }

        private string GetCellValue(ExcelWorksheet worksheet, int rowIndex, Dictionary<string, int> headers, string headerName)
        {
            if (!headers.ContainsKey(headerName))
                return null;

            return worksheet.Cells[rowIndex, headers[headerName]].Text?.Trim();
        }

        private string GetValidIdentifier(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            // Преобразование имени проекта в допустимый идентификатор
            // (только строчные латинские буквы, цифры, дефисы)
            var identifier = name.ToLower()
                .Replace(" ", "-")
                .Replace(".", "")
                .Replace(",", "")
                .Replace("_", "-");

            // Удаляем все символы, кроме a-z, 0-9 и дефиса
            identifier = new string(identifier.Where(c => (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-').ToArray());

            // Проверяем длину и добавляем уникальный суффикс, если нужно
            if (string.IsNullOrEmpty(identifier))
                identifier = "Проект";

            //if (identifier.Length > 30)
            //    identifier = identifier.Substring(0, 30);

            // Добавляем уникальный суффикс
            //string uniqueSuffix = Guid.NewGuid().ToString("N").Substring(0, 6);

            return identifier;
        }
    }
}
