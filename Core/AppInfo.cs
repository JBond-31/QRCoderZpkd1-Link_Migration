using System.Reflection;

namespace QRCoderZpkd1_Link.Core
{
  public static class AppInfo
  {
    public static string GetDisplayVersion()
    {
      // Берем текущую сборку (наш исполняемый файл)
      var assembly = Assembly.GetEntryAssembly()
      // Если главную сборку получить невозможно, используем сборку, содержащую AppInfo.
      ?? typeof(AppInfo).Assembly;

      // Получаем атрибут InformationalVersion, который мы задали в Directory.Build.props
      var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

      // Если атрибут по какой-либо причине отсутствует, возвращаем безопасное значение вместо null.
      return string.IsNullOrWhiteSpace(informationalVersion)
                ? "v.unknown"
                : informationalVersion;
    }
  }
}