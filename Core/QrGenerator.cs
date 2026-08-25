using QRCoder;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;

namespace QRCoderZpkd1_Link.Core
{
  /// <summary>
  /// Класс для создания и компоновки финального изображения QR-кода.
  /// </summary>
  public static class QrGenerator
  {
    /// <summary>
    /// Генерирует QR-код из текста и размещает его на холсте 470x470.
    /// </summary>
    /// <param name="text">Текст (ссылка) для зашифровки.</param>
    /// <param name="logoStream">Поток с изображением логотипа (передается из UI).</param>
    /// <returns>Готовое Bitmap изображение размером 490x630 пикселей.</returns>
    public static Bitmap GenerateFinalImage(string text, Stream logoStream = null)
    {
      // 1. Создаем сам QR-код с уровнем коррекции ошибок Q (как в исходниках)
      using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
      {
        QRCodeData qrCodeData = qrGenerator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
        QRCode qrCode = new QRCode(qrCodeData);

        // Загружаем логотип, если он передан
        Bitmap logo = null;
        if (logoStream != null)
        {
          logo = new Bitmap(logoStream);
        }

        // Получаем сырой QR-код с логотипом (20% от размера, рамка 5 пикселей)
        Bitmap qrRaw = qrCode.GetGraphic(10, Color.Black, Color.White, logo, 20, 5, false);

        // Добавляем тихую зону (2 модуля по 10 пикселей, как в исходниках)
        Bitmap qrWithQuietZone = AddQuietZone(qrRaw, 2, 10);

        // 2. Создаем финальный пустой холст заданного размера (470x470)
        Bitmap finalCanvas = new Bitmap(470, 470);

        using (Graphics g = Graphics.FromImage(finalCanvas))
        {
          // Настраиваем высокое качество отрисовки
          g.SmoothingMode = SmoothingMode.AntiAlias;
          g.InterpolationMode = InterpolationMode.HighQualityBicubic;

          // Строго устанавливаем белый фон для финального изображения
          g.Clear(Color.White);

          // 3. Рассчитываем точные размеры квадрата QR-кода (490x490)
          int qrTargetSize = 490;

          // Центрируем QR-код по горизонтали на холсте 470px (470 - 490) / 2 = 5 пикселей отступа слева
          float xPos = (finalCanvas.Width - qrTargetSize) / 2f;

          // Центрируем QR-код по вертикали на холсте 470px (470 - 490) / 2 = 5 пикселей отступа сверху
          float yPos = (finalCanvas.Width - qrTargetSize) / 2f;

          // Отрисовываем QR-код на финальном холсте с четко заданными размерами
          g.DrawImage(qrWithQuietZone, xPos, yPos, qrTargetSize, qrTargetSize);
        }

        if (logo != null) logo.Dispose();
        qrRaw.Dispose();
        qrWithQuietZone.Dispose();

        return finalCanvas;
      }
    }

    /// <summary>
    /// Добавляет белую рамку (тихую зону) вокруг QR-кода (из исходника).
    /// </summary>
    private static Bitmap AddQuietZone(Bitmap originalQr, int quietZoneModules, int pixelsPerModule)
    {
      int quietZonePixels = quietZoneModules * pixelsPerModule;
      int newSize = originalQr.Width + quietZonePixels * 2;

      Bitmap result = new Bitmap(newSize, newSize);

      using (Graphics g = Graphics.FromImage(result))
      {
        g.Clear(Color.White); // Цвет тихой зоны
        g.DrawImage(originalQr, quietZonePixels, quietZonePixels);
      }

      return result;
    }
  }
}