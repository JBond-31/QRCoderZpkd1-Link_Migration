using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace QRCoderZpkd1_Link.Core
{
  /// Логический класс-помощник для парсинга ссылки на файл .zpk.
  /// Обрабатывает извлечение имени, версий (в т.ч. внутри имени перед суффиксами устройств) и очистку.
  public static class LinkParser
  {
    public class ParseResult
    {
      /// Название, извлечённое из имени файла.
      public string Name { get; set; } = string.Empty;
      /// Версия, извлечённая из имени файла.
      public string Version { get; set; } = string.Empty;
    }

    /// Разбирает ссылку или путь к ZPK-файлу и извлекает из имени файла название и версию.
    public static ParseResult Parse(string urlOrPath)
    {
      // Создаём результат сразу. Если входная строка пустая или не содержит пригодного имени файла, вернётся объект с пустыми Name и Version.
      var result = new ParseResult();

      if (string.IsNullOrWhiteSpace(urlOrPath))
      {
        return result;
      }

      try
      {
        // ШАГ 1. Получаем только имя файла.
        string fileName;

        // Сначала проверяем, является ли входная строка абсолютным URI.
        if (Uri.TryCreate(urlOrPath, UriKind.Absolute, out var uri))
        {
          // Получаем последнюю часть URL-пути.
          fileName = Path.GetFileName(uri.LocalPath);
        }
        else
        {
          // Если это не URI, считаем строку обычным локальным путём к файлу.
          // Path является кроссплатформенным API .NET.
          fileName = Path.GetFileName(urlOrPath);
        }

        // Если имя файла получить не удалось, возвращаем пустой результат.
        if (string.IsNullOrEmpty(fileName))
        {
          return result;
        }

        // ШАГ 2. Декодируем URL-символы.
        fileName = WebUtility.UrlDecode(fileName);

        // ШАГ 3. Убираем расширение .zpk.
        if (fileName.EndsWith(
                ".zpk",
                StringComparison.OrdinalIgnoreCase))
        {
          fileName = fileName.Substring(
              0,
              fileName.Length - ".zpk".Length);
        }

        // ШАГ 4. Ищем версию.
        var versionRegex = new Regex(
            @"[vV](\d+(?:[_\.]\d+)*)",
            RegexOptions.Compiled);

        var match = versionRegex.Match(fileName);

        if (match.Success)
        {
          // Получаем только числовую часть после V.
          string rawVersion = match.Groups[1].Value;

          // В текущем формате приложения подчёркивание используется как разделитель компонентов версии.
          string formattedVersion =
              rawVersion.Replace('_', '.');

          result.Version = formattedVersion;

          // ШАГ 5. Всё до версии считаем названием.
          fileName = fileName.Substring(
              0,
              match.Index);

          fileName = fileName.TrimEnd(
              '-',
              '—',
              '_',
              ' ');
        }

        // ШАГ 6. Финально очищаем название.

        result.Name = fileName.Trim();
      }
      catch
      {
        // Метод используется для автоматического заполнения полей интерфейса.
        return new ParseResult();
      }

      return result;
    }

    /// Декодирует URL-строку.
    public static string DecodeUrl(string input)
    {
      // Для null и пустой строки возвращаем пустую строку.
      if (string.IsNullOrEmpty(input))
      {
        return string.Empty;
      }

      // WebUtility.UrlDecode является стандартным кроссплатформенным API .NET.
      return WebUtility.UrlDecode(input);
    }

    /// Преобразует ссылку на файл в репозитории GitHub в соответствующую ссылку GitHub Pages.
    public static string ConvertGitHubBlobToPagesUrl(string url)
    {
      if (string.IsNullOrEmpty(url))
      {
        return url;
      }

      try
      {
        // Разбираем строку именно как URI.
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
          return url;
        }

        // Нас интересуют ссылки именно GitHub.
        if (!uri.Host.Equals(
                "github.com",
                StringComparison.OrdinalIgnoreCase))
        {
          return url;
        }

        // Убираем начальный и конечный "/".
        var trimmedPath = uri.AbsolutePath.Trim('/');

        // Разбиваем путь на отдельные сегменты.
        var segments = trimmedPath.Split(
    new[] { '/' },
    StringSplitOptions.RemoveEmptyEntries);

        // Ожидаем структуру: /UserName/RepoName/blob/main/file.zpk
        // Минимально необходимо 5 сегментов:
        // 0 = UserName
        // 1 = RepoName
        // 2 = blob
        // 3 = main
        // 4 = file.zpk
        if (segments.Length < 5 ||
            !segments[2].Equals(
                "blob",
                StringComparison.OrdinalIgnoreCase) ||
            !segments[3].Equals(
                "main",
                StringComparison.OrdinalIgnoreCase))
        {
          return url;
        }

        string user = segments[0];
        string repo = segments[1];

        // Получаем путь к файлу без User/Repo/blob/main.
        string filePath = string.Join(
            "/",
            segments,
            4,
            segments.Length - 4);

        // Если репозиторий имеет имя User.github.io, GitHub Pages работает непосредственно от корня сайта.
        if (repo.Equals(
                $"{user}.github.io",
                StringComparison.OrdinalIgnoreCase))
        {
          return $"https://{user}.github.io/{filePath}";
        }

        // Для обычного репозитория GitHub Pages добавляет имя репозитория после домена пользователя.
        return $"https://{user}.github.io/{repo}/{filePath}";
      }
      catch
      {
        // Если строка не является корректной ссылкой, возвращаем её без изменений.
        return url;
      }
    }

    /// <summary>
    /// Исправляет протокол ссылки. http://  -> zpkd1:// или https:// -> zpkd1://
    /// Если протокол отсутствует, добавляется zpkd1://.
    public static string CorrectUrl(string input)
    {
      if (string.IsNullOrEmpty(input))
      {
        return input;
      }

      // Сначала обрабатываем HTTPS. Используем StartsWith вместо Replace, чтобы изменить только протокол в начале строки.
      if (input.StartsWith(
              "https://",
              StringComparison.OrdinalIgnoreCase))
      {
        return "zpkd1://" + input.Substring("https://".Length);
      }

      // Затем HTTP.
      if (input.StartsWith(
              "http://",
              StringComparison.OrdinalIgnoreCase))
      {
        return "zpkd1://" + input.Substring("http://".Length);
      }

      // Если ссылка уже использует внутренний протокол, ничего не изменяем.
      if (input.StartsWith(
              "zpkd1://",
              StringComparison.OrdinalIgnoreCase))
      {
        return input;
      }

      // Если протокол вообще не указан, добавляем внутренний протокол приложения.
      return "zpkd1://" + input;
    }

    /// Асинхронно проверяет доступность файла по URL. Метод использует стандартный HttpClient .NET и не зависит от WPF или Windows.
    public static async Task<bool> UrlFileExistsAsync(string url)
    {
      if (string.IsNullOrWhiteSpace(url))
      {
        return false;
      }

      // Для сетевой проверки нам нужен настоящий HTTP/HTTPS. Внутренний протокол приложения zpkd1:// заменяем на HTTP.
      if (url.StartsWith(
              "zpkd1://",
              StringComparison.OrdinalIgnoreCase))
      {
        url = "http://" + url.Substring("zpkd1://".Length);
      }

      try
      {
        // HttpClient является кроссплатформенным API .NET.
        using (var client = new HttpClient())
        {
          // Ограничиваем ожидание ответа пятью секундами, чтобы интерфейс не ждал бесконечно при проблемах сети.
          client.Timeout = TimeSpan.FromSeconds(5);

          // HEAD-запрос получает только HTTP-заголовки, не скачивая сам ZPK-файл.
          using (var request =
                 new HttpRequestMessage(
                     HttpMethod.Head,
                     url))
          {
            using (var response =
                   await client.SendAsync(request))
            {
              // Успешным считаем любой HTTP-код из диапазона 200-299.
              return response.IsSuccessStatusCode;
            }
          }
        }
      }
      catch
      {
        // Отсутствие сети, таймаут, неправильный URL или другая сетевая ошибка означают, что файл проверить не удалось.
        return false;
      }
    }
  }
}