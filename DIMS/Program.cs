using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DIMS.Models;
using DIMS.Services;
using Microsoft.Extensions.Configuration;

class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            // Чтение конфигурации из appsettings.json
            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            var configuration = configBuilder.Build();

            var redmineConfig = configuration.GetSection("RedmineApi").Get<RedmineApiConfig>();

            var apiService = new RedmineApiService(redmineConfig);

            // --- Создание подпроекта с использованием нашей модели ---
            var subProject = new RedmineProject
            {
                Name = "Мой подпроект " + DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss"),
                Identifier = "my-subproject-" + Guid.NewGuid().ToString("N").Substring(0, 8),
                Parent = new RedmineProjectParent { Id = redmineConfig.RootProjectId },
                CustomFields = new List<RedmineCustomField>
                {
                    new RedmineCustomField { Id = 43, Value = "Проект" },
                    new RedmineCustomField { Id = 44, Value = "Общий проект для всех проектов и процессов" },
                    new RedmineCustomField { Id = 38, Value = "ЛИНК" },
                    new RedmineCustomField { Id = 45, Value = "Ильич Д.В." },
                    new RedmineCustomField { Id = 39, Value = "Левченко Е.Н." },
                    new RedmineCustomField { Id = 35, Value = "Необходимо достичь прозрачности и прогнозируемости деятельности ЦЦМ" },
                    new RedmineCustomField { Id = 41, Value = "10" },
                }
            };

            Console.WriteLine("Создаем подпроект...");
            int subProjectId;

            try
            {
                subProjectId = await apiService.CreateProject(subProject);
                Console.WriteLine($"Создан подпроект с ID: {subProjectId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при создании подпроекта: {ex.Message}");
                throw;
            }

            // --- Создание подзадачи с использованием нашей модели ---
            var subIssue = new RedmineIssue
            {
                ProjectId = subProjectId, 
                Subject = "Моя подзадача " + DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss"),
                Description = "Описание подзадачи",
                StartDate = DateTime.Today,
                DueDate = DateTime.Today.AddDays(7),
                Tracker = new RedmineTracker { Id = 10 }, 
                CustomFields = new List<RedmineCustomField>
                {
                    new RedmineCustomField { Id = 20, Value = "Исследование" },
                    new RedmineCustomField { Id = 49, Value = "" },
                    new RedmineCustomField { Id = 47, Value = "1" },

                }
            };

            Console.WriteLine("Создаем задачу...");
            try
            {
                int subIssueId = await apiService.CreateIssue(subIssue);
                Console.WriteLine($"Создана подзадача с ID: {subIssueId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при создании подзадачи: {ex.Message}");
                throw;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Необработанная ошибка: {ex.Message}");
            Console.WriteLine($"StackTrace: {ex.StackTrace}");
            if (ex.InnerException != null)
                Console.WriteLine($"InnerException: {ex.InnerException.Message}");
        }

        Console.WriteLine("Нажмите любую клавишу для завершения...");
        Console.ReadKey();
    }
}
