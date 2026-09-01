using System;
using System.Collections.Generic;
using System.IO;
using SkiaSharp;

namespace QRCoderZpkd1_Link.Core
{

  /// Создаёт готовую PNG-карточку размером 490x630.
  public static class PreviewCardExporter
  {
    private const int CardWidth = 490;
    private const int CardHeight = 630;

    /// Собирает карточку из QR, текста, фона и списка моделей.
    public static GeneratedImage ExportCard(
      string url,
      Stream logoStream,
      string typeText,
      string nameText,
      string versionText,
      string modelsText)
    {
      var imageInfo = new SKImageInfo(
        CardWidth,
        CardHeight,
        SKColorType.Rgba8888,
        SKAlphaType.Premul);

      using var surface = SKSurface.Create(imageInfo);
      if (surface == null)
        throw new InvalidOperationException("Failed to create SkiaSharp surface.");

      SKCanvas canvas = surface.Canvas;
      canvas.Clear(SKColors.Transparent);

      // Сначала рисуем неизменяемую геометрию карточки.
      DrawCardBackground(canvas);

      // Затем заголовок с поддержкой переноса длинного названия.
      DrawHeader(canvas, typeText, nameText, versionText);

      // Если ссылка есть — создаём настоящий QR.
      // Если ссылки нет — создаём QR-зону только с логотипом-заглушкой.
      if (!string.IsNullOrWhiteSpace(url))
      {
        GeneratedImage qrImage =
          QrGenerator.GenerateFinalImage(url, logoStream);

        DrawQr(canvas, qrImage);
      }
      else
      {
        DrawEmptyQrZone(canvas, logoStream);
      }

      // Нижняя область карточки предназначена для списка моделей.
      DrawModels(canvas, modelsText);

      using var image = surface.Snapshot();
      using var pngData = image.Encode(SKEncodedImageFormat.Png, 100);
      using var output = new MemoryStream();

      pngData.SaveTo(output);

      return new GeneratedImage(
        output.ToArray(),
        CardWidth,
        CardHeight);
    }

    private static void DrawCardBackground(SKCanvas canvas)
    {
      var cardRect = new SKRect(1, 1, CardWidth - 1, CardHeight - 1);
      var cardRoundRect = new SKRoundRect(cardRect, 16, 16);

      using var fillPaint = new SKPaint
      {
        IsAntialias = true,
        Style = SKPaintStyle.Fill,
        Color = new SKColor(255, 218, 185)
      };

      using var borderPaint = new SKPaint
      {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 2,
        Color = SKColors.Black
      };

      canvas.DrawRoundRect(cardRoundRect, fillPaint);
      canvas.DrawRoundRect(cardRoundRect, borderPaint);
    }

    private static void DrawHeader(
      SKCanvas canvas,
      string typeText,
      string nameText,
      string versionText)
    {
      const float headerHeight = 60;
      const float maxWidth = 440;

      string type = typeText?.Trim() ?? string.Empty;
      string name = nameText?.Trim() ?? string.Empty;
      string version = versionText?.Trim() ?? string.Empty;

      using var typefaceRegular =
        SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Normal);

      using var typefaceBold =
        SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold);

      float typeSize = 18.2f;
      float nameSize = 19.6f;
      float versionSize = 18.2f;

      using var textPaint = new SKPaint
      {
        IsAntialias = true,
        Color = SKColors.Black
      };

      using var typeFont = new SKFont(typefaceRegular, typeSize);
      using var nameFont = new SKFont(typefaceBold, nameSize);
      using var versionFont = new SKFont(typefaceBold, versionSize);

      float typeWidth = Measure(typeFont, type);
      float nameWidth = Measure(nameFont, name);
      float versionWidth = Measure(versionFont, version);

      // Сначала уменьшаем Type и Name, как делал старый renderer.
      while (typeWidth + nameWidth > maxWidth && typeSize > 8f)
      {
        typeSize -= 0.5f;
        nameSize -= 0.5f;

        typeFont.Size = typeSize;
        nameFont.Size = nameSize;

        typeWidth = Measure(typeFont, type);
        nameWidth = Measure(nameFont, name);
      }

      float totalWidth = typeWidth + nameWidth + versionWidth;

      if (totalWidth <= maxWidth)
      {
        DrawCenteredHeaderLine(
          canvas,
          textPaint,
          type,
          typeFont,
          name,
          nameFont,
          version,
          versionFont,
          headerHeight);

        return;
      }

      // Если вся строка не помещается, часть длинного имени переносится вместе с версией на вторую строку.
      string firstNamePart = name;
      string secondNamePart = string.Empty;

      while (firstNamePart.Length > 1 &&
             Measure(nameFont, type + firstNamePart) > maxWidth)
      {
        int splitIndex = firstNamePart.LastIndexOf(' ');

        if (splitIndex <= 0)
          splitIndex = firstNamePart.Length - 1;

        secondNamePart =
          firstNamePart.Substring(splitIndex).TrimStart() +
          (string.IsNullOrEmpty(secondNamePart)
            ? string.Empty
            : " " + secondNamePart);

        firstNamePart =
          firstNamePart.Substring(0, splitIndex).TrimEnd();
      }

      string firstLine =
  string.IsNullOrEmpty(type)
    ? firstNamePart
    : string.IsNullOrEmpty(firstNamePart)
      ? type
      : type + " " + firstNamePart;
      string secondLine =
        string.IsNullOrEmpty(secondNamePart)
          ? version
          : secondNamePart +
            (string.IsNullOrEmpty(version) ? string.Empty : " " + version);

      float firstWidth = Measure(nameFont, firstLine);
      float secondWidth = Measure(versionFont, secondLine);

      SKFontMetrics firstMetrics;
      SKFontMetrics secondMetrics;

      float firstLineHeight = nameFont.GetFontMetrics(out firstMetrics);
      float secondLineHeight = versionFont.GetFontMetrics(out secondMetrics);

      float spacing = 0.1f;
      float blockHeight =
        firstLineHeight +
        spacing +
        secondLineHeight;

      float blockTop =
        (headerHeight - blockHeight) / 2f;

      float firstBaseline =
        blockTop - firstMetrics.Ascent;

      float secondBaseline =
        blockTop +
        firstLineHeight +
        spacing -
        secondMetrics.Ascent;

      // Определяем размеры первой строки отдельно для обычного Type и жирного Name.
      float typePartWidth = string.IsNullOrEmpty(type) ? 0f : Measure(typeFont, type);
      float namePartWidth = string.IsNullOrEmpty(firstNamePart) ? 0f : Measure(nameFont, firstNamePart);
      float firstSpacing =
        !string.IsNullOrEmpty(type) && !string.IsNullOrEmpty(firstNamePart)
          ? 10f
          : 0f;

      float firstLineWidth =
        typePartWidth +
        firstSpacing +
        namePartWidth;

      // Центрируем составную первую строку относительно карточки.
      float firstX =
        CardWidth / 2f -
        firstLineWidth / 2f;

      // Type остаётся обычным, Name остаётся жирным.
      if (!string.IsNullOrEmpty(type))
      {
        canvas.DrawText(
          type,
          firstX,
          firstBaseline,
          SKTextAlign.Left,
          typeFont,
          textPaint);

        firstX += typePartWidth + firstSpacing;
      }

      if (!string.IsNullOrEmpty(firstNamePart))
      {
        canvas.DrawText(
          firstNamePart,
          firstX,
          firstBaseline,
          SKTextAlign.Left,
          nameFont,
          textPaint);
      }

      // Вторая строка содержит продолжение Name и Version и остаётся жирной.
      canvas.DrawText(
        secondLine,
        CardWidth / 2f,
        secondBaseline,
        SKTextAlign.Center,
        versionFont,
        textPaint);
    }

    private static void DrawCenteredHeaderLine(
  SKCanvas canvas,
  SKPaint paint,
  string type,
  SKFont typeFont,
  string name,
  SKFont nameFont,
  string version,
  SKFont versionFont,
  float headerHeight)
    {
      // Фиксированный визуальный интервал между Type, Name и Version.
      const float textSpacing = 10f;

      float typeWidth = Measure(typeFont, type);
      float nameWidth = Measure(nameFont, name);
      float versionWidth = Measure(versionFont, version);

      // Учитываем отступ только между реально существующими элементами.
      int textParts = 0;
      if (!string.IsNullOrEmpty(type)) textParts++;
      if (!string.IsNullOrEmpty(name)) textParts++;
      if (!string.IsNullOrEmpty(version)) textParts++;

      float totalSpacing =
        textParts > 1 ? (textParts - 1) * textSpacing : 0f;

      float totalWidth =
        typeWidth +
        nameWidth +
        versionWidth +
        totalSpacing;

      SKFontMetrics metrics;
      float lineHeight = nameFont.GetFontMetrics(out metrics);

      float baseline =
        (headerHeight - lineHeight) / 2f -
        metrics.Ascent;

      float x = (CardWidth - totalWidth) / 2f;

      if (!string.IsNullOrEmpty(type))
      {
        // Рисуем каждый элемент в вычисленной позиции через новую перегрузку API.
        canvas.DrawText(type, x, baseline, SKTextAlign.Left, typeFont, paint);
        x += typeWidth + textSpacing;
      }

      if (!string.IsNullOrEmpty(name))
      {
        // Рисуем каждый элемент в вычисленной позиции через новую перегрузку API.
        canvas.DrawText(name, x, baseline, SKTextAlign.Left, nameFont, paint);
        x += nameWidth + textSpacing;
      }

      if (!string.IsNullOrEmpty(version))
      {
        // Рисуем каждый элемент в вычисленной позиции через новую перегрузку API.
        canvas.DrawText(version, x, baseline, SKTextAlign.Left, versionFont, paint);
      }
    }

    // Рисует пустую QR-зону при старте приложения. Вместо QR отображается логотип по центру.
    private static void DrawEmptyQrZone(
      SKCanvas canvas,
      Stream logoStream)
    {
      const float qrSize = 455f;
      const float qrX = 17.5f;
      const float qrY = 60f;

      var qrRect = new SKRect(
        qrX,
        qrY,
        qrX + qrSize,
        qrY + qrSize);

      // Белый фон QR-зоны.
      using var backgroundPaint = new SKPaint
      {
        IsAntialias = true,
        Color = SKColors.White
      };

      canvas.DrawRoundRect(
        new SKRoundRect(qrRect, 20, 20),
        backgroundPaint);


      // Загружаем логотип.
      if (logoStream != null)
      {
        using var logoBitmap =
          SKBitmap.Decode(logoStream);

        if (logoBitmap != null)
        {
          // Логотип занимает всю QR-зону с отступом 10 пикселей со всех сторон.
          const float logoMargin = 10f;
          float logoSize = qrSize - logoMargin * 2f;

          float x = qrX + logoMargin;
          float y = qrY + logoMargin;

          var logoRect = new SKRect(
            x,
            y,
            x + logoSize,
            y + logoSize);

          // Рисуем логотип с использованием актуальной перегрузки SkiaSharp.
          canvas.DrawBitmap(
            logoBitmap,
            logoRect,
            new SKSamplingOptions(SKFilterMode.Linear));
        }
      }


      // Рамка QR-зоны.
      using var borderPaint = new SKPaint
      {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 2.1f,
        Color = new SKColor(60, 60, 60)
      };

      canvas.DrawRoundRect(
        new SKRoundRect(qrRect, 20, 20),
        borderPaint);
    }

    private static void DrawQr(SKCanvas canvas, GeneratedImage qrImage)
    {
      using var qrBitmap = SKBitmap.Decode(qrImage.PngData);

      if (qrBitmap == null)
        throw new InvalidOperationException("Failed to decode generated QR PNG.");

      const float qrSize = 455f;
      const float qrX = 17.5f;
      const float qrY = 60f;

      var qrRect = new SKRect(
        qrX,
        qrY,
        qrX + qrSize,
        qrY + qrSize);

      var qrRoundRect = new SKRoundRect(qrRect, 20, 20);

      canvas.Save();

      // Сохраняем скруглённое clipping-поведение старой карточки.
      canvas.ClipRoundRect(qrRoundRect, SKClipOperation.Intersect, true);

      canvas.DrawBitmap(
  qrBitmap,
  qrRect,
  new SKSamplingOptions(SKFilterMode.Linear));

      canvas.Restore();

      using var borderPaint = new SKPaint
      {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 2.1f,
        Color = new SKColor(60, 60, 60)
      };

      canvas.DrawRoundRect(qrRoundRect, borderPaint);
    }

    private static void DrawModels(
      SKCanvas canvas,
      string modelsText)
    {
      if (string.IsNullOrWhiteSpace(modelsText))
        return;

      const float areaX = 10;
      const float areaY = 518;
      const float areaWidth = 470;
      const float areaHeight = 105;

      float fontSize = 18.2f;

      using var typeface =
        SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold);

      using var font = new SKFont(typeface, fontSize);

      using var paint = new SKPaint
      {
        IsAntialias = true,
        Color = SKColors.Black
      };

      List<string> lines;

      // Уменьшаем шрифт до тех пор, пока модели не помещаются в область.
      do
      {
        lines = WrapText(font, modelsText, areaWidth);

        float lineHeight = font.GetFontMetrics(out _);
        float totalHeight = lines.Count * lineHeight;

        if (totalHeight <= areaHeight || fontSize <= 8f)
          break;

        fontSize -= 1f;
        font.Size = fontSize;
      }
      while (fontSize > 8f);

      float finalLineHeight = font.GetFontMetrics(out _);

      for (int i = 0; i < lines.Count; i++)
      {
        string line = lines[i];
        float lineWidth = Measure(font, line);

        float x =
          areaX +
          (areaWidth - lineWidth) / 2f;

        float baseline =
          areaY +
          i * finalLineHeight -
          font.Metrics.Ascent;

        // Рисуем строку моделей с той же рассчитанной левой координатой.
        canvas.DrawText(
          line,
          x,
          baseline,
          SKTextAlign.Left,
          font,
          paint);
      }
    }

    private static List<string> WrapText(
      SKFont font,
      string text,
      float maxWidth)
    {
      var lines = new List<string>();
      string[] words =
        text.Split(
          new[] { ' ' },
          StringSplitOptions.RemoveEmptyEntries);

      string current = string.Empty;

      foreach (string word in words)
      {
        string candidate =
          string.IsNullOrEmpty(current)
            ? word
            : current + " " + word;

        if (Measure(font, candidate) <= maxWidth)
        {
          current = candidate;
        }
        else
        {
          if (!string.IsNullOrEmpty(current))
            lines.Add(current);

          current = word;
        }
      }

      if (!string.IsNullOrEmpty(current))
        lines.Add(current);

      return lines;
    }

    private static float Measure(
      SKFont font,
      string text)
    {
      return string.IsNullOrEmpty(text)
        ? 0f
        : font.MeasureText(text);
    }
  }
}
