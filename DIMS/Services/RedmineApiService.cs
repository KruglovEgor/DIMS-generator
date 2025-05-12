using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using DIMS.Models;
using Redmine.Net.Api;
using Redmine.Net.Api.Types;
using Redmine.Net.Api.Async;
using System.Text.Json;
using Newtonsoft.Json;

namespace DIMS.Services
{
    public class RedmineApiService
    {
        private readonly RedmineManager _redmineManager;
        private readonly RedmineApiConfig _config;
        private readonly HttpClient _httpClient;

        public RedmineApiService(RedmineApiConfig config)
        {
            _config = config;
            _redmineManager = new RedmineManager(_config.BaseUrl, _config.ApiKey);

            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(_config.BaseUrl)
            };
            _httpClient.DefaultRequestHeaders.Add("X-Redmine-API-Key", _config.ApiKey);
        }

        /// <summary>
        /// Создает проект в Redmine
        /// </summary>
        /// <param name="projectData">Данные проекта</param>
        /// <returns>ID созданного проекта</returns>
        public async Task<int> CreateProject(RedmineProject projectData)
        {
            try
            {
                // Подготовка данных для запроса в формате JSON
                var customFieldsList = new List<object>();

                if (projectData.CustomFields != null && projectData.CustomFields.Any())
                {
                    foreach (var field in projectData.CustomFields)
                    {
                        customFieldsList.Add(new { id = field.Id, value = field.Value });
                    }
                }

                // Формируем объект для сериализации в JSON
                var projectObj = new
                {
                    project = new
                    {
                        name = projectData.Name,
                        identifier = projectData.Identifier,
                        description = "Создано с использованием DIMS",
                        parent_id = projectData.Parent?.Id ?? _config.RootProjectId,
                        custom_fields = customFieldsList
                    }
                };

                // Сериализуем в JSON
                var json = JsonConvert.SerializeObject(projectObj);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Отправляем запрос
                var response = await _httpClient.PostAsync("/projects.json", content);

                // Обрабатываем ответ
                if (response.IsSuccessStatusCode)
                {
                    var responseData = await response.Content.ReadAsStringAsync();
                    var createdProject = JsonConvert.DeserializeObject<dynamic>(responseData);
                    return (int)createdProject.project.id;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Ошибка создания проекта: {response.StatusCode}. {errorContent}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при создании проекта: {ex.Message}");
                if (ex.InnerException != null)
                    Console.WriteLine($"Внутренняя ошибка: {ex.InnerException.Message}");
                throw;
            }
        }

        /// <summary>
        /// Создает задачу в Redmine
        /// </summary>
        /// <param name="issueData">Данные задачи</param>
        /// <returns>ID созданной задачи</returns>
        public async Task<int> CreateIssue(RedmineIssue issueData)
        {
            try
            {
                // Подготовка данных для запроса в формате JSON
                var customFieldsList = new List<object>();

                if (issueData.CustomFields != null && issueData.CustomFields.Any())
                {
                    foreach (var field in issueData.CustomFields)
                    {
                        customFieldsList.Add(new { id = field.Id, value = field.Value });
                    }
                }

                // Формируем объект для сериализации
                var issueObj = new
                {
                    issue = new
                    {
                        project_id = issueData.ProjectId,
                        subject = issueData.Subject,
                        description = issueData.Description,
                        start_date = issueData.StartDate?.ToString("yyyy-MM-dd"),
                        due_date = issueData.DueDate?.ToString("yyyy-MM-dd"),
                        estimated_hours = issueData.EstimatedHours,
                        tracker_id = issueData.Tracker?.Id,
                        assigned_to_id = issueData.AssignedTo?.Id,
                        parent_issue_id = issueData.Parent?.Id,
                        custom_fields = customFieldsList
                    }
                };

                // Сериализуем в JSON
                var json = JsonConvert.SerializeObject(issueObj);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Отправляем запрос
                var response = await _httpClient.PostAsync("/issues.json", content);

                // Обрабатываем ответ
                if (response.IsSuccessStatusCode)
                {
                    var responseData = await response.Content.ReadAsStringAsync();
                    var createdIssue = JsonConvert.DeserializeObject<dynamic>(responseData);
                    return (int)createdIssue.issue.id;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Ошибка создания задачи: {response.StatusCode}. {errorContent}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при создании задачи: {ex.Message}");
                if (ex.InnerException != null)
                    Console.WriteLine($"Внутренняя ошибка: {ex.InnerException.Message}");
                throw;
            }
        }
    }
}
