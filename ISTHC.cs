using System.Diagnostics;
using System.Net;

namespace InternetSpeedTesterHttpClient
{
    /// <summary>
    /// Главная форма приложения для тестирования скорости интернета
    /// Обеспечивает пользовательский интерфейс и координацию работы сервисов
    /// </summary>
    public partial class ISTHC : Form
    {
        // Сервисы для различных функций приложения
        private readonly YandexAuthService _authService;    // Сервис аутентификации
        private readonly YandexDiskService _diskService;    // Сервис работы с Яндекс.Диском
        private readonly SpeedTestService _speedTestService;// Сервис тестирования скорости
        private readonly Random _random;                    // Генератор случайных чисел
        private CancellationTokenSource _cancellationTokenSource; // Для отмены операций

        /// <summary>
        /// Конструктор главной формы
        /// Инициализирует сервисы и создает тестовый файл
        /// </summary>
        public ISTHC()
        {
            InitializeComponent();

            // Инициализация сервисов
            _authService = new YandexAuthService();
            _diskService = new YandexDiskService();
            _speedTestService = new SpeedTestService();
            _random = new Random();

            // Создание локального тестового файла
            CreateLocalTestFile();
        }

        /// <summary>
        /// Обработчик кнопки начала теста скорости скачивания
        /// </summary>
        private async void StartTestButton_Click(object sender, EventArgs e)
        {
            // Блокировка UI во время теста
            StartTestButton.Enabled = false;
            CancelTestButton.Enabled = true;
            ProgressBar.Value = 0;
            StatusLabel.Text = "Подготовка...";

            // Создание токена для возможности отмены операции
            _cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = _cancellationTokenSource.Token;

            try
            {
                // Прохождение OAuth аутентификации
                if (!await _authService.AuthenticateWithCodeAsync(cancellationToken))
                {
                    StatusLabel.Text = "Авторизация отменена";
                    return;
                }

                // Измерение сетевой задержки (ping)
                StatusLabel.Text = "Измерение задержки (Ping)...";
                long pingMs = await _speedTestService.MeasurePingAsync("https://yandex.ru", cancellationToken);
                PingResultLabel.Text = $"Ping: {pingMs} мс";

                // Измерение скорости скачивания
                StatusLabel.Text = "Тест скорости скачивания...";
                string downloadUrl = await GetRandomDownloadUrlAsync(cancellationToken);
                double downloadSpeed = await _speedTestService.MeasureDownloadSpeedAsync(
                    downloadUrl,
                    _authService.AccessToken,
                    UpdateProgress, // Callback для обновления прогресса
                    cancellationToken);
                DownloadResultLabel.Text = $"Скачивание: {downloadSpeed:F2} Мбит/с";

                StatusLabel.Text = "Тест завершен!";
            }
            catch (TaskCanceledException)
            {
                // Обработка отмены теста пользователем
                StatusLabel.Text = "Тест отменен.";
                ProgressBar.Value = 0;
                ProgressLabel.Text = "Прогресс: 0%";
            }
            catch (Exception ex)
            {
                // Обработка общих ошибок
                MessageBox.Show($"Ошибка во время теста: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                StatusLabel.Text = "Ошибка";
            }
            finally
            {
                // Восстановление состояния UI
                StartTestButton.Enabled = true;
                CancelTestButton.Enabled = false;
            }
        }

        /// <summary>
        /// Обработчик кнопки теста скорости загрузки
        /// </summary>
        private async void UploadTestButton_Click(object sender, EventArgs e)
        {
            try
            {
                StartTestButton.Enabled = false;
                UploadTestButton.Enabled = false;
                StatusLabel.Text = "Подготовка к тесту загрузки...";

                // Проверка валидности токена
                if (!await _authService.ValidateYandexTokenAsync(CancellationToken.None))
                {
                    MessageBox.Show("Токен невалиден. Требуется повторная авторизация.", "Ошибка",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Проверка прав доступа токена
                await _authService.CheckTokenScopes(CancellationToken.None);

                // Создание временного тестового файла (1MB)
                string testFilePath = CreateTestFile(1 * 1024 * 1024);

                try
                {
                    StatusLabel.Text = "Тест скорости загрузки...";
                    // Измерение скорости загрузки на Яндекс.Диск
                    double uploadSpeed = await _speedTestService.MeasureUploadSpeedAsync(
                        testFilePath,
                        _authService.AccessToken,
                        UpdateUploadProgress, // Callback для обновления прогресса загрузки
                        CancellationToken.None);
                    UploadResultLabel.Text = $"Загрузка: {uploadSpeed:F2} Мбит/с";

                    StatusLabel.Text = "Тест загрузки завершен!";
                }
                finally
                {
                    // Удаление временного файла и восстановление UI
                    if (File.Exists(testFilePath))
                        File.Delete(testFilePath);

                    StartTestButton.Enabled = true;
                    UploadTestButton.Enabled = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка теста загрузки: {ex.Message}", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                StatusLabel.Text = "Ошибка загрузки";
                StartTestButton.Enabled = true;
                UploadTestButton.Enabled = true;
            }
        }

        /// <summary>
        /// Обработчик кнопки отмены теста
        /// </summary>
        private void CancelTestButton_Click(object sender, EventArgs e)
        {
            _cancellationTokenSource?.Cancel(); // Отмена текущей операции
        }

        /// <summary>
        /// Обработчик кнопки повторной авторизации
        /// </summary>
        private async void ReAuthButton_Click(object sender, EventArgs e)
        {
            try
            {
                // Сброс текущих учетных данных
                _authService.ResetCredentials();

                // Запрос новой авторизации
                if (await _authService.AuthenticateWithCodeAsync(CancellationToken.None))
                {
                    MessageBox.Show("Повторная авторизация успешна!", "Успех",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка повторной авторизации: {ex.Message}",
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Обработчик закрытия формы - освобождение ресурсов
        /// </summary>
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            _speedTestService?.Dispose(); // Освобождение HttpClient
        }

        /// <summary>
        /// Выбор случайного URL для тестирования скачивания
        /// </summary>
        private async Task<string> GetRandomDownloadUrlAsync(CancellationToken cancellationToken)
        {
            string[] downloadUrls = {
                "https://disk.yandex.ru/d/vt-pDfQ1qOFpWQ",
                "https://disk.yandex.ru/d/vAawlND_0pDyCg"
            };

            // Выбор случайного URL из массива
            string publicUrl = downloadUrls[_random.Next(downloadUrls.Length)];

            // Проверка доступности файла на Яндекс.Диске
            if (publicUrl.Contains("disk.yandex.ru"))
            {
                bool isAvailable = await _diskService.CheckYandexDiskFileAvailable(publicUrl, cancellationToken);
                if (!isAvailable)
                {
                    Debug.WriteLine("Файл на Яндекс Диске недоступен, используем локальный файл");
                    return GetLocalTestFileUrl(); // Использование локального файла как fallback
                }
            }

            return _diskService.GetYandexDiskDownloadUrl(publicUrl, _authService.AccessToken);
        }

        /// <summary>
        /// Получение URL локального тестового файла
        /// </summary>
        private string GetLocalTestFileUrl()
        {
            string testFilePath = Path.Combine(Path.GetTempPath(), "test100mb.bin");
            return $"file:///{testFilePath.Replace("\\", "/")}"; // Форматирование file:// URL
        }

        /// <summary>
        /// Обновление прогресса скачивания в UI
        /// </summary>
        private void UpdateProgress(int progressPercentage, long bytesRead, TimeSpan timeElapsed)
        {
            // Проверка необходимости вызова через Invoke (потокобезопасность)
            if (InvokeRequired)
            {
                Invoke(new Action<int, long, TimeSpan>(UpdateProgress), progressPercentage, bytesRead, timeElapsed);
                return;
            }

            // Обновление прогресс-бара
            if (progressPercentage >= 0)
            {
                ProgressBar.Value = progressPercentage;
                ProgressLabel.Text = $"Прогресс: {progressPercentage}%";
            }

            // Расчет и отображение текущей скорости
            if (timeElapsed.TotalSeconds > 0)
            {
                double currentSpeedMbps = (bytesRead * 8) / (timeElapsed.TotalSeconds * 1024 * 1024);
                SpeedLabel.Text = currentSpeedMbps < 0.01 ?
                    "Текущая скорость: < 0.01 Мбит/с" :
                    $"Текущая скорость: {currentSpeedMbps:F2} Мбит/с";
            }
            else
            {
                SpeedLabel.Text = "Текущая скорость: измеряется...";
            }
        }

        /// <summary>
        /// Обновление прогресса загрузки в UI
        /// </summary>
        private void UpdateUploadProgress(long bytesRead, long totalBytesRead, TimeSpan timeElapsed, long totalFileSize)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<long, long, TimeSpan, long>(UpdateUploadProgress),
                       bytesRead, totalBytesRead, timeElapsed, totalFileSize);
                return;
            }

            // Расчет прогресса в процентах
            int progressPercentage = totalFileSize > 0 ?
                (int)((double)totalBytesRead / totalFileSize * 100) : 0;

            ProgressBar.Value = progressPercentage;
            ProgressLabel.Text = $"Прогресс: {progressPercentage}%";

            // Расчет и отображение текущей скорости загрузки
            if (timeElapsed.TotalSeconds > 0)
            {
                double currentSpeedMbps = (totalBytesRead * 8) / (timeElapsed.TotalSeconds * 1024 * 1024);
                SpeedLabel.Text = currentSpeedMbps < 0.01 ?
                    "Текущая скорость: < 0.01 Мбит/с" :
                    $"Текущая скорость: {currentSpeedMbps:F2} Мбит/с";
            }
        }

        /// <summary>
        /// Создание временного тестового файла
        /// </summary>
        private string CreateTestFile(long sizeInBytes)
        {
            return FileHelper.CreateTestFile(sizeInBytes);
        }

        /// <summary>
        /// Создание локального тестового файла (100MB)
        /// </summary>
        private void CreateLocalTestFile()
        {
            FileHelper.CreateLocalTestFile();
        }
    }
}