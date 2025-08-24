using System.Diagnostics;
using System.Net;

namespace InternetSpeedTesterHttpClient
{
    // КЛАСС ДЛЯ ОТСЛЕЖИВАНИЯ ПРОГРЕССА
    // Кастомная реализация StreamContent с callback'ом для отслеживания прогресса загрузки
    // Позволяет в реальном времени отслеживать процесс передачи данных
    public class ProgressStreamContent : StreamContent
    {
        private readonly Stream _stream;          // Исходный поток данных для отправки
        private readonly long _totalSize;         // Общий размер отправляемых данных
        private readonly Action<long, long, TimeSpan> _progressCallback; // Callback для обновления прогресса
        private readonly Stopwatch _stopwatch;    // Таймер для измерения скорости передачи

        // Конструктор - инициализация параметров отслеживания прогресса
        public ProgressStreamContent(Stream stream, long totalSize, Action<long, long, TimeSpan> progressCallback)
            : base(stream)  // Вызов конструктора базового класса StreamContent
        {
            _stream = stream;                    // Сохранение исходного потока
            _totalSize = totalSize;              // Сохранение общего размера данных
            _progressCallback = progressCallback; // Сохранение callback функции
            _stopwatch = Stopwatch.StartNew();   // Запуск таймера измерения времени
        }

        // Переопределение метода сериализации потока с отслеживанием прогресса
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            var buffer = new byte[8192];  // Буфер 8KB для чтения/записи данных
            long totalRead = 0;           // Счетчик общего количества прочитанных байт
            int bytesRead;                // Количество байт, прочитанных за текущую итерацию

            // Поточное чтение и отправка данных
            while ((bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                // Запись данных в выходной поток
                await stream.WriteAsync(buffer, 0, bytesRead);

                // Обновление счетчика общего количества отправленных данных
                totalRead += bytesRead;

                // Вызов callback функции с параметрами:
                // - bytesRead: количество байт, отправленных в текущем chunk
                // - totalRead: общее количество отправленных байт
                // - _stopwatch.Elapsed: общее время с начала передачи
                _progressCallback?.Invoke(bytesRead, totalRead, _stopwatch.Elapsed);
            }
        }
    }
}