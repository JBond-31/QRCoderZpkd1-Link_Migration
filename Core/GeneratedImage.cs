namespace QRCoderZpkd1_Link.Core
{

  /// Платформонезависимый результат рендеринга изображения в PNG.
  public sealed class GeneratedImage
  {
    // Храним готовое PNG без Bitmap, Stream и других платформенных типов.
    public byte[] PngData { get; }

    // Фактический размер изображения в пикселях.
    public int Width { get; }

    public int Height { get; }

    public GeneratedImage(byte[] pngData, int width, int height)
    {
      // DTO не допускает пустые данные изображения.
      PngData = pngData ?? throw new System.ArgumentNullException(nameof(pngData));

      // Размеры нужны UI и renderer без необходимости декодировать PNG.
      Width = width;
      Height = height;
    }
  }
}