using System;
using System.IO;
using SkiaSharp;
using SkiaSharp.QrCode;
using SkiaSharp.QrCode.Image;

namespace QRCoderZpkd1_Link.Core
{

  /// Генерирует QR-код в платформонезависимый PNG.
  public static class QrGenerator
  {

    /// Генерирует стандартный QR с логотипом и ECC H.
    public static GeneratedImage GenerateFinalImage(
      string text,
      Stream logoStream = null)
    {
      if (string.IsNullOrWhiteSpace(text))
        throw new ArgumentException("QR text cannot be empty.", nameof(text));

      SKBitmap logo = null;

      try
      {
        // Загружаем логотип непосредственно из переданного потока без System.Drawing.
        if (logoStream != null)
        {
          using (var logoMemory = new MemoryStream())
          {
            logoStream.CopyTo(logoMemory);
            logo = SKBitmap.Decode(logoMemory.ToArray());
          }
        }

        var builder = new QRCodeImageBuilder(text)
          // Сохраняем чёткие квадратные модули QR.
          .WithModulePixelSize(10)
          // ECC H используется библиотекой для QR с логотипом.
          .WithErrorCorrection(ECCLevel.H)
          // Оставляем тихую зону 2 модуля вокруг QR.
          .WithQuietZone(2)
          // Используем классические чёрный QR и белый фон.
          .WithColors(
            codeColor: SKColors.Black,
            backgroundColor: SKColors.White,
            clearColor: SKColors.White);

        if (logo != null)
        {
          // Логотип располагается по центру с белой зоной вокруг.
          var icon = IconData.FromImage(
            logo,
            iconSizePercent: 20,
            iconBorderWidth: 5);

          builder = builder.WithIcon(icon);
        }

        byte[] pngData = builder.ToByteArray();

        // Получаем фактические размеры готового PNG.
        using (var resultBitmap = SKBitmap.Decode(pngData))
        {
          if (resultBitmap == null)
            throw new InvalidOperationException("Failed to decode generated QR PNG.");

          return new GeneratedImage(
            pngData,
            resultBitmap.Width,
            resultBitmap.Height);
        }
      }
      finally
      {
        // Освобождаем только созданный SkiaSharp-объект логотипа.
        logo?.Dispose();
      }
    }
  }
}