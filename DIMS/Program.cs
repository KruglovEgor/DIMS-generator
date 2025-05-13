using System;
using System.Collections.Generic;
using System.IO;
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

            
            // Путь к файлу Excel по умолчанию
            string excelPath = "Resources/Templates/DIMS.xlsm";

            // Если указан путь к файлу в аргументах, используем его
            if (args.Length > 1)
            {
                excelPath = args[1];
            }

            // Создаем сервис для работы с Excel и обрабатываем файл
            var excelService = new ExcelService(apiService, "Resources/Templates/DIMS.xlsm");
            await excelService.ProcessExcelFile(excelPath);

            Console.WriteLine("Обработка Excel-файла завершена");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Необработанная ошибка: {ex.Message}");
            Console.WriteLine($"StackTrace: {ex.StackTrace}");
            if (ex.InnerException != null)
                Console.WriteLine($"InnerException: {ex.InnerException.Message}");
        }

        Console.WriteLine("Нажмите Enter для завершения...");
        // Проверка, доступен ли ввод с консоли
        if (Console.IsInputRedirected)
        {
            // Если ввод перенаправлен (как в Docker), просто ждем
            await Task.Delay(5000); // Ждем 5 секунд и завершаем работу
        }
        else
        {
            // Если консоль доступна, ждем нажатия клавиши
            Console.ReadLine();
        }
    }
}
