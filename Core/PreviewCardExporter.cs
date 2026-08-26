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

      // QR всегда занимает ту же область карточки: 455x455 начиная с Y=60.
      GeneratedImage qrImage =
        QrGenerator.GenerateFinalImage(url, logoStream);

      DrawQr(canvas, qrImage);

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

      float typeSize = 13.5f;
      float nameSize = 14.5f;
      float versionSize = 13.5f;

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
      while (typeWidth + nameWidth > maxWidth && typeSize > 6f)
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

      // Рисуем обе строки по центру через актуальную перегрузку SkiaSharp.
      canvas.DrawText(
        firstLine,
        CardWidth / 2f,
        firstBaseline,
        SKTextAlign.Center,
        nameFont,
        textPaint);

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
      const float textSpacing = 3f;

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
      const float areaY = 515;
      const float areaWidth = 470;
      const float areaHeight = 105;

      float fontSize = 14.5f;

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

        if (totalHeight <= areaHeight || fontSize <= 6f)
          break;

        fontSize -= 1f;
        font.Size = fontSize;
      }
      while (fontSize > 6f);

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