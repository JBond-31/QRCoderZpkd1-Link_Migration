using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;

namespace QRCoderZpkd1_Link.Core
{
  /// <summary>
  /// Класс бизнес-логики для экспорта полной карточки предпросмотра (размер 490x630).
  /// Изолирован от UI (WPF), использует только GDI+ (System.Drawing) для компоновки.
  /// </summary>
  public static class PreviewCardExporter
  {
    /// <summary>
    /// Собирает финальную карточку предпросмотра, объединяя текст, фон и QR-код.
    /// </summary>
    public static Bitmap ExportCard(
        string url,
        Stream logoStream,
        string typeText,
        string nameText,
        string versionText,
        string modelsText)
    {
      // Строго заданные размеры итогового файла
      int width = 490;
      int height = 630;
      Bitmap cardCanvas = new Bitmap(width, height);

      using (Graphics g = Graphics.FromImage(cardCanvas))
      {
        // Настраиваем максимальное качество отрисовки
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.TextRenderingHint = TextRenderingHint.AntiAlias;

        // Базовый холст оставляем прозрачным
        g.Clear(Color.Transparent);

        // 1. Отрисовка фона самой карточки (имитация стиля PreviewCardStyle)
        RectangleF cardRect = new RectangleF(1, 1, width - 2, height - 2);

        using (GraphicsPath cardPath = GetRoundedRect(cardRect, 16f))
        {
          // Задаем цвет фона карточки
          using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(255, 255, 218, 185)))
          {
            g.FillPath(bgBrush, cardPath);
          }
          // Внешняя черная рамка
          using (Pen borderPen = new Pen(Color.Black, 2))
          {
            g.DrawPath(borderPen, cardPath);
          }
        }

        float currentTypeSize = 13.5f;
        float currentNameSize = 14.5f;
        float currentVerSize = 13.5f;

        Font typeFont = new Font("Segoe UI", currentTypeSize, FontStyle.Regular);
        Font nameFont = new Font("Segoe UI", currentNameSize, FontStyle.Bold);
        Font verFont = new Font("Segoe UI", currentVerSize, FontStyle.Bold);

        StringFormat sf = StringFormat.GenericTypographic;
        sf.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;

        float maxAvailableWidth = width - 50; // Оставляем по 10px отступов с краев

        // Высота области шапки карточки (соответствует RowDefinition Height="35" в WPF, умноженному на 1.4 = 49 ~ 50 пикселей)
        float headerBoxHeight = 60f;

        // Измеряем ширину кусочков текста
        float w1 = string.IsNullOrEmpty(typeText) ? 0 : g.MeasureString(typeText, typeFont, 1000, sf).Width;
        float w2 = string.IsNullOrEmpty(nameText) ? 0 : g.MeasureString(nameText, nameFont, 1000, sf).Width;
        float w3 = string.IsNullOrEmpty(versionText) ? 0 : g.MeasureString(versionText, verFont, 1000, sf).Width;

        // ЗАЩИТА ОТ "УЛЕТА" ТЕКСТА: 
        // Если Тип + Имя слишком длинные и не влезают в ширину картинки, 
        // мы пропорционально уменьшаем их шрифт, пока они не поместятся.
        while (w1 + w2 > maxAvailableWidth && currentTypeSize > 6f)
        {
          currentTypeSize -= 0.5f;
          currentNameSize -= 0.5f;

          typeFont.Dispose();
          nameFont.Dispose();

          typeFont = new Font("Segoe UI", currentTypeSize, FontStyle.Regular);
          nameFont = new Font("Segoe UI", currentNameSize, FontStyle.Bold);

          w1 = string.IsNullOrEmpty(typeText) ? 0 : g.MeasureString(typeText, typeFont, 1000, sf).Width;
          w2 = string.IsNullOrEmpty(nameText) ? 0 : g.MeasureString(nameText, nameFont, 1000, sf).Width;
        }

        // 2. Отрисовка заголовка
        using (SolidBrush textBrush = new SolidBrush(Color.Black))
        {
          if (w1 + w2 + w3 <= maxAvailableWidth)
          {
            // ВАРИАНТ 1: Всё помещается в одну строку
            float totalWidth = w1 + w2 + w3;
            float startX = (width - totalWidth) / 2f; // Идеальное центрирование
            float currentX = startX;

            float lineHeight = nameFont.GetHeight(g);
            float ascent = GetFontAscent(nameFont);
            float baselineY = (headerBoxHeight - lineHeight) / 2f + ascent;

            if (!string.IsNullOrEmpty(typeText))
            {
              g.DrawString(typeText, typeFont, textBrush, currentX, baselineY - GetFontAscent(typeFont), sf);
              currentX += w1;
            }
            if (!string.IsNullOrEmpty(nameText))
            {
              g.DrawString(nameText, nameFont, textBrush, currentX, baselineY - GetFontAscent(nameFont), sf);
              currentX += w2;
            }
            if (!string.IsNullOrEmpty(versionText))
            {
              g.DrawString(versionText, verFont, textBrush, currentX, baselineY - GetFontAscent(verFont), sf);
            }
          }
          else
          {
            // ВАРИАНТ 2: Перенос версии на вторую строку
            float line1Width = w1 + w2;
            float line1StartX = (width - line1Width) / 2f;
            float line2StartX = (width - w3) / 2f;

            float h1 = nameFont.GetHeight(g);
            float h2 = verFont.GetHeight(g);
            float a1 = GetFontAscent(nameFont);
            float a2 = GetFontAscent(verFont);

            float spacing = 0.1f; // Минимальный отступ между первой и второй строкой
            float totalBlockHeight = h1 + spacing + h2;
            float blockTop = (headerBoxHeight - totalBlockHeight) / 2f;

            float baseline1Y = blockTop + a1;
            float baseline2Y = blockTop + h1 + spacing + a2;

            float currentX = line1StartX;
            if (!string.IsNullOrEmpty(typeText))
            {
              g.DrawString(typeText, typeFont, textBrush, currentX, baseline1Y - GetFontAscent(typeFont), sf);
              currentX += w1;
            }
            if (!string.IsNullOrEmpty(nameText))
            {
              g.DrawString(nameText, nameFont, textBrush, currentX, baseline1Y - GetFontAscent(nameFont), sf);
            }

            if (!string.IsNullOrEmpty(versionText))
            {
              g.DrawString(versionText, verFont, textBrush, line2StartX, baseline2Y - GetFontAscent(verFont), sf);
            }
          }
        }

        // 3. Внедрение QR-кода
        using (Bitmap qrCodeImage = QrGenerator.GenerateFinalImage(url, logoStream))
        {
          float qrSize = 455f;
          float qrX = (width - qrSize) / 2f;
          float qrY = 60f;
          RectangleF qrRect = new RectangleF(qrX, qrY, qrSize, qrSize);

          using (GraphicsPath qrPath = GetRoundedRect(qrRect, 20f)) // Радиус 14 * 1.4
          {
            var state = g.Save();
            g.SetClip(qrPath);
            g.DrawImage(qrCodeImage, qrRect.X, qrRect.Y, qrRect.Width, qrRect.Height);
            g.Restore(state);

            // Рамка 1.5 * 1.4 = 2.1
            using (Pen qrBorderPen = new Pen(Color.FromArgb(60, 60, 60), 2.1f))
            {
              g.DrawPath(qrBorderPen, qrPath);
            }
          }
        }

        // 4. Отрисовка списка моделей под QR-кодом в заданной области
        RectangleF modelsArea = new RectangleF(10, 515, width - 20, height - 515 - 10);
        float currentModelsFontSize = 14.5f; // Начальный (максимальный) размер
        Font modelsFont = new Font("Segoe UI", currentModelsFontSize, FontStyle.Bold);

        StringFormat sfModels = new StringFormat
        {
          Alignment = StringAlignment.Center,
          LineAlignment = StringAlignment.Near
        };

        // Цикл подбора шрифта, чтобы список моделей идеально влез в блок
        while (currentModelsFontSize > 6f)
        {
          SizeF textSize = g.MeasureString(modelsText, modelsFont, (int)modelsArea.Width, sfModels);
          if (textSize.Height <= modelsArea.Height) break;

          modelsFont.Dispose();
          currentModelsFontSize -= 1f;
          modelsFont = new Font("Segoe UI", currentModelsFontSize, FontStyle.Bold);
        }

        using (SolidBrush modelsTextBrush = new SolidBrush(Color.Black))
        {
          g.DrawString(modelsText, modelsFont, modelsTextBrush, modelsArea, sfModels);
        }

        // Очищаем оперативную память от сгенерированных шрифтов
        typeFont.Dispose();
        nameFont.Dispose();
        verFont.Dispose();
        modelsFont.Dispose();
      }

      return cardCanvas;
    }

    /// <summary>
    /// Вычисление высоты восходящей части (ascent) шрифта для идеального выравнивания по базовой линии.
    /// </summary>
    private static float GetFontAscent(Font font)
    {
      int emHeight = font.FontFamily.GetEmHeight(font.Style);
      int cellAscent = font.FontFamily.GetCellAscent(font.Style);
      return font.Size * cellAscent / emHeight;
    }

    /// <summary>
    /// Вспомогательный метод математического построения прямоугольника со скругленными углами.
    /// </summary>
    private static GraphicsPath GetRoundedRect(RectangleF rect, float radius)
    {
      GraphicsPath path = new GraphicsPath();
      if (radius <= 0)
      {
        path.AddRectangle(rect);
        return path;
      }

      float d = radius * 2;
      path.AddArc(rect.X, rect.Y, d, d, 180, 90);
      path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
      path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
      path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);

      path.CloseFigure();
      return path;
    }
  }
}