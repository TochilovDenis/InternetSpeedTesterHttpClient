using System.Diagnostics;

namespace InternetSpeedTesterHttpClient
{
    // HELPER ДЛЯ РАБОТЫ С ФАЙЛАМИ
    // Статический класс с утилитами для создания и управления тестовыми файлами
    // Предоставляет методы для генерации файлов со случайным содержимым
    public static class FileHelper
    {
        // Создает временный тестовый файл указанного размера со случайным содержимым
        // Parameters:
        //   sizeInBytes: размер файла в байтах
        // Returns: путь к созданному временному файлу
        public static string CreateTestFile(long sizeInBytes)
        {
            // Создание временного файла с уникальным именем в системной папке temp
            string tempFile = Path.GetTempFileName();

            using (var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write))
            {
                byte[] buffer = new byte[8192];  // Буфер 8KB для записи данных
                Random random = new Random();    // Генератор случайных чисел
                long bytesWritten = 0;           // Счетчик записанных байт

                // Заполнение файла случайными данными до достижения нужного размера
                while (bytesWritten < sizeInBytes)
                {
                    random.NextBytes(buffer);  // Заполнение буфера случайными байтами

                    // Расчет количества байт для записи в текущей итерации
                    int bytesToWrite = (int)Math.Min(buffer.Length, sizeInBytes - bytesWritten);

                    // Запись данных в файл
                    fileStream.Write(buffer, 0, bytesToWrite);

                    // Обновление счетчика
                    bytesWritten += bytesToWrite;
                }
            }

            return tempFile;  // Возврат пути к созданному файлу
        }

        // Создает локальный тестовый файл размером 100MB для использования в тестах скорости
        // Файл создается в системной временной папке и не перезаписывается если уже существует
        public static void CreateLocalTestFile()
        {
            try
            {
                // Формирование пути к файлу в системной временной папке
                string testFilePath = Path.Combine(Path.GetTempPath(), "test100mb.bin");

                // Проверка существования файла (не перезаписываем если уже существует)
                if (!File.Exists(testFilePath))
                {
                    using (var fileStream = new FileStream(testFilePath, FileMode.Create))
                    {
                        byte[] buffer = new byte[8192];      // Буфер 8KB для записи
                        Random random = new Random();        // Генератор случайных чисел
                        long bytesToWrite = 100 * 1024 * 1024; // 100MB в байтах
                        long bytesWritten = 0;               // Счетчик записанных байт

                        // Заполнение файла случайными данными
                        while (bytesWritten < bytesToWrite)
                        {
                            random.NextBytes(buffer);  // Заполнение буфера случайными данными

                            // Расчет размера chunk'а для текущей итерации
                            int chunkSize = (int)Math.Min(buffer.Length, bytesToWrite - bytesWritten);

                            // Запись данных в файл
                            fileStream.Write(buffer, 0, chunkSize);

                            // Обновление счетчика
                            bytesWritten += chunkSize;
                        }
                    }

                    Debug.WriteLine($"Создан тестовый файл: {testFilePath}");
                }
                else
                {
                    Debug.WriteLine("Тестовый файл уже существует, пропускаем создание");
                }
            }
            catch (Exception ex)
            {
                // Логирование ошибки без прерывания работы приложения
                Debug.WriteLine($"Ошибка создания тестового файла: {ex.Message}");
            }
        }
    }
}