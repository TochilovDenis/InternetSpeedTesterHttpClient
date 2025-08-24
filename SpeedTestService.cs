using System.Diagnostics;
using System.Net;

namespace InternetSpeedTesterHttpClient
{
    /// <summary>
    /// Сервис для измерения сетевых характеристик
    /// Выполняет тестирование скорости скачивания, загрузки и ping
    /// </summary>
    public class SpeedTestService : IDisposable
    {
        private readonly HttpClient _httpClient;          // HTTP клиент для выполнения запросов
        private readonly YandexDiskService _diskService;  // Сервис для работы с Яндекс.Диском

        /// <summary>
        /// Конструктор сервиса тестирования скорости
        /// </summary>
        public SpeedTestService()
        {

            _httpClient = new HttpClient(new HttpClientHandler
            {
                AllowAutoRedirect = true,        // Разрешить автоматические перенаправления (301, 302)
                MaxAutomaticRedirections = 3,    // Максимальное количество перенаправлений
                UseProxy = true,                 // Использовать системные настройки прокси
                Proxy = HttpClient.DefaultProxy  // Системный прокси по умолчанию
            });

            // Добавление User-Agent для обхода ограничений (имитация браузера)
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            _httpClient.Timeout = TimeSpan.FromSeconds(60); // Таймаут запросов 60 секунд
            _diskService = new YandexDiskService();         // Инициализация сервиса диска
        }

        // Измерение сетевой задержки (ping) до указанного URL
        public async Task<long> MeasurePingAsync(string url, CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew(); // Запуск таймера
            try
            {
                // Используем HEAD запрос для минимизации передаваемых данных
                using (var request = new HttpRequestMessage(HttpMethod.Head, url))
                using (var response = await _httpClient.SendAsync(request, cancellationToken))
                {
                    response.EnsureSuccessStatusCode(); // Проверка успешности запроса
                    stopwatch.Stop();
                    return stopwatch.ElapsedMilliseconds; // Возвращаем время отклика в миллисекундах
                }
            }
            catch (HttpRequestException)
            {
                // Fallback: если HEAD не поддерживается, используем GET запрос
                stopwatch.Stop();
                stopwatch = Stopwatch.StartNew();
                using (var response = await _httpClient.GetAsync(url, cancellationToken))
                {
                    response.EnsureSuccessStatusCode();
                    stopwatch.Stop();
                    return stopwatch.ElapsedMilliseconds;
                }
            }
        }

        // Измерение скорости скачивания файла
        public async Task<double> MeasureDownloadSpeedAsync(string url, string accessToken,
            Action<int, long, TimeSpan> progressCallback, CancellationToken cancellationToken)
        {
            try
            {
                // Обработка локальных файлов (file:// protocol)
                if (url.StartsWith("file:///"))
                {
                    return await MeasureLocalFileSpeed(url, progressCallback, cancellationToken);
                }

                // Получение прямой ссылки для скачивания (обход страниц с предпросмотром)
                string downloadUrl = await _diskService.GetDirectDownloadUrlAsync(url, accessToken, cancellationToken);

                // Проверка существования файла
                bool fileExists = await _diskService.CheckFileExistsAsync(downloadUrl, accessToken, cancellationToken);

                // Получение размера файла через HEAD запрос
                long? fileSize = await _diskService.GetFileSizeAsync(downloadUrl, accessToken, cancellationToken);

                // Если размер не удалось определить, используем значение по умолчанию (100MB)
                if (!fileSize.HasValue || fileSize.Value == 0) fileSize = 100 * 1024 * 1024;

                // Добавление случайного параметра для избежания кэширования
                string uniqueUrl = $"{downloadUrl}?cache={new Random().Next()}";

                Stopwatch stopwatch = new Stopwatch(); // Таймер для измерения скорости
                long totalBytesRead = 0L;              // Счетчик скачанных байт

                using (var response = await _httpClient.GetAsync(uniqueUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                {
                    // Обработка HTTP ошибок
                    if (response.StatusCode == HttpStatusCode.NotFound)
                        throw new Exception("Файл не найден (404)");
                    if (!response.IsSuccessStatusCode)
                        throw new Exception($"Ошибка HTTP: {response.StatusCode}");

                    // Используем реальный размер из заголовков ответа
                    long? contentLength = response.Content.Headers.ContentLength;
                    if (contentLength.HasValue && contentLength.Value > 0)
                        fileSize = contentLength.Value;

                    // Начало измерения скорости
                    stopwatch.Start();
                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    {
                        byte[] buffer = new byte[8192]; // Буфер 8KB для чтения
                        int bytesRead;
                        int updateCounter = 0;

                        // Поточное чтение данных с отслеживанием прогресса
                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                        {
                            totalBytesRead += bytesRead;
                            cancellationToken.ThrowIfCancellationRequested(); // Проверка отмены

                            // Обновление прогресса каждые ~80KB (10 итераций по 8KB) или при завершении
                            updateCounter++;
                            if (updateCounter % 10 == 0 || totalBytesRead == fileSize)
                            {
                                int progressPercentage = fileSize > 0 ?
                                    (int)((double)totalBytesRead / fileSize.Value * 100) : 0;

                                // Вызов callback для обновления UI
                                progressCallback?.Invoke(progressPercentage, totalBytesRead, stopwatch.Elapsed);
                            }
                        }
                    }
                    stopwatch.Stop();
                }

                // Расчет скорости в Мбит/с (мегабитах в секунду)
                // Формула: (байты * 8) / (время * 1024 * 1024)
                if (stopwatch.Elapsed.TotalSeconds <= 0) return 0;
                return (totalBytesRead * 8) / (stopwatch.Elapsed.TotalSeconds * 1024 * 1024);
            }
            catch (HttpRequestException ex)
            {
                // Специфичная обработка HTTP ошибок
                throw HandleHttpException(ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка скачивания: {ex.Message}", ex);
            }
        }

        // Измерение скорости чтения локального файла (для тестирования без сети)
        public async Task<double> MeasureLocalFileSpeed(string fileUrl, Action<int, long, TimeSpan> progressCallback,
            CancellationToken cancellationToken)
        {
            try
            {
                // Преобразование file:// URL в путь к файлу
                string filePath = fileUrl.Replace("file:///", "").Replace("/", "\\");
                if (!File.Exists(filePath)) throw new Exception("Локальный файл не найден");

                FileInfo fileInfo = new FileInfo(filePath);
                long fileSize = fileInfo.Length; // Получение размера файла
                Stopwatch stopwatch = new Stopwatch();
                long totalBytesRead = 0L;

                using (var fileStream = File.OpenRead(filePath))
                {
                    byte[] buffer = new byte[8192];
                    int bytesRead;
                    int updateCounter = 0;

                    stopwatch.Start();
                    // Чтение файла с имитацией сетевой задержки
                    while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                    {
                        totalBytesRead += bytesRead;
                        cancellationToken.ThrowIfCancellationRequested();

                        // Обновление прогресса
                        updateCounter++;
                        if (updateCounter % 10 == 0 || totalBytesRead == fileSize)
                        {
                            int progressPercentage = fileSize > 0 ?
                                (int)((double)totalBytesRead / fileSize * 100) : 0;

                            progressCallback?.Invoke(progressPercentage, totalBytesRead, stopwatch.Elapsed);
                        }

                        // Имитация сетевой задержки (1ms) для реалистичности
                        await Task.Delay(1, cancellationToken);
                    }
                    stopwatch.Stop();
                }

                // Расчет скорости чтения файла
                if (stopwatch.Elapsed.TotalSeconds <= 0) return 0;
                return (totalBytesRead * 8) / (stopwatch.Elapsed.TotalSeconds * 1024 * 1024);
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка измерения скорости локального файла: {ex.Message}", ex);
            }
        }

        // Измерение скорости загрузки файла на Яндекс.Диск
        public async Task<double> MeasureUploadSpeedAsync(string filePath, string accessToken,
            Action<long, long, TimeSpan, long> progressCallback, CancellationToken cancellationToken)
        {
            try
            {
                // Генерация уникального имени файла с timestamp
                string fileName = $"speedtest_{DateTime.Now:yyyyMMdd_HHmmss}.dat";
                YandexDiskService diskService = new YandexDiskService();

                // Получение URL для загрузки от Яндекс.Диска
                string uploadUrl = await diskService.GetUploadUrlAsync(fileName, accessToken, cancellationToken);

                if (string.IsNullOrEmpty(uploadUrl))
                    throw new Exception("Не удалось получить URL для загрузки");

                Stopwatch stopwatch = new Stopwatch();
                long fileSize = new FileInfo(filePath).Length; // Размер загружаемого файла

                // Использование кастомного контента с отслеживанием прогресса
                using (var fileStream = File.OpenRead(filePath))
                using (var content = new ProgressStreamContent(fileStream, fileSize, (bytesRead, totalRead, elapsed) =>
                    progressCallback?.Invoke(bytesRead, totalRead, elapsed, fileSize)))
                {
                    stopwatch.Start();
                    // PUT запрос для загрузки файла
                    using (var response = await _httpClient.PutAsync(uploadUrl, content, cancellationToken))
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            // Чтение деталей ошибки из ответа
                            string errorContent = await response.Content.ReadAsStringAsync();
                            throw new Exception($"Ошибка загрузки: {response.StatusCode} - {errorContent}");
                        }
                        stopwatch.Stop();
                    }
                }

                // Расчет скорости загрузки в Мбит/с
                if (stopwatch.Elapsed.TotalSeconds <= 0) return 0;
                return (fileSize * 8) / (stopwatch.Elapsed.TotalSeconds * 1024 * 1024);
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка измерения скорости загрузки: {ex.Message}", ex);
            }
        }

        // Обработка HTTP исключений с понятными сообщениями
        private Exception HandleHttpException(HttpRequestException ex)
        {
            switch (ex.StatusCode)
            {
                case HttpStatusCode.Unauthorized:
                    return new Exception("Ошибка авторизации. Токен невалиден.");
                case HttpStatusCode.Forbidden:
                    return new Exception("Доступ запрещен.");
                case HttpStatusCode.NotFound:
                    return new Exception("Файл не найден.");
                case HttpStatusCode.TooManyRequests:
                    return new Exception("Слишком много запросов. Попробуйте позже.");
                default:
                    return new Exception($"HTTP ошибка: {ex.StatusCode}");
            }
        }

        // Освобождение ресурсов HttpClient
        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}