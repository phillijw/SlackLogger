﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SlackLogger.Extensions;

namespace SlackLogger
{
    internal class SlackService
    {
        private readonly SlackLoggerOptions _options;

        public SlackService(SlackLoggerOptions options)
        {
            _options = options;
        }


        public void Log(LogLevel logLevel, string typeName, string message, string environmentName, Exception exception = null)
        {
            PostAsync(typeName, message, exception, environmentName, logLevel);
        }


        private async void PostAsync(string typeName, string message, Exception exception, string environment, LogLevel logLevel)
        {
            var icon = GetIcon(logLevel);
            var color = GetColor(logLevel);
            var environmentName = string.IsNullOrEmpty(environment) ? "" : $"({environment})";

            var stackTrace = exception?.ToString();

            if (_options.SanitizeOutputFunction != null && exception != null)
            {
                stackTrace = _options.SanitizeOutputFunction(stackTrace);
            }

            var formattedStacktrace =
                exception != null
                    ? $"```\n{stackTrace.Truncate(1800)}```"
                    : string.Empty;


            var notification = ShouldNotify(logLevel) ? "<!channel>: \n" : "";

            using (var client = new HttpClient())
            {
                var payload = new
                {
                    channel = GetChannel(logLevel),
                    username = "SlackLogger",
                    icon_emoji = icon,
                    text = $"{notification}*{_options.ApplicationName}* {environmentName}",
                    attachments = new[]
                    {
                        new
                        {
                            fallback = $"Error in {_options.ApplicationName}",
                            color = color,
                            mrkdwn_in = new[] {"fields"},
                            fields = new[]
                            {
                                new
                                {
                                    title = "",
                                    value = $"`{typeName}`",
                                },
                                new
                                {
                                    title = $"{icon} [{logLevel}]",
                                    value = $"{message.Sanitize(_options)}",
                                },
                                new
                                {
                                    title = "",
                                    value = formattedStacktrace,
                                }
                            }
                        }
                    }
                };

                var url = _options.WebhookUrl;
                var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8,
                    "application/json");

                await client.PostAsync(url, content);

            }
        }

        private string GetChannel(LogLevel logLevel)
        {
            switch (logLevel)
            {
                case LogLevel.Critical:
                    return _options.ChannelCritical ?? _options.Channel;
                case LogLevel.Error:
                    return _options.ChannelError ?? _options.Channel;
                case LogLevel.Warning:
                    return _options.ChannelWarning ?? _options.Channel;
                case LogLevel.Information:
                    return _options.ChannelInformation ?? _options.Channel;
                case LogLevel.Debug:
                    return _options.ChannelDebug ?? _options.Channel;
                case LogLevel.Trace:
                    return _options.ChannelTrace ?? _options.Channel;
                default: return "";
            }
        }

        private string GetIcon(LogLevel logLevel)
        {
            switch (logLevel)
            {
                case LogLevel.Critical:
                    return ":fire:";
                case LogLevel.Error:
                    return ":exclamation:";
                case LogLevel.Warning:
                    return ":warning:";
                case LogLevel.Information:
                    return ":information_source:";
                case LogLevel.Debug:
                    return ":bug:";
                case LogLevel.Trace:
                    return ":mag:";
                default: return "";
            }
        }

        private string GetColor(LogLevel logLevel)
        {
            switch (logLevel)
            {
                case LogLevel.Error:
                case LogLevel.Critical:
                    return "danger";
                case LogLevel.Warning:
                    return "warning";
                case LogLevel.Information:
                    return "#007AB8";
                default: return "";
            }
        }

        private bool ShouldNotify(LogLevel logLevel) => GetNotificationLogLevels().Contains(logLevel);

        private IEnumerable<LogLevel> GetNotificationLogLevels()
        {
            var result = new List<LogLevel>();
            for (int i = (int)_options.NotificationLevel; i < (int)LogLevel.None; i++)
            {
                result.Add((LogLevel)i);
            }
            return result;
        }




    }

    internal static class StringExtensions
    {
        public static string Sanitize(this string input, SlackLoggerOptions options)
            =>
                options.SanitizeOutputFunction != null ?
                    options.SanitizeOutputFunction(input) :
                    input;

    }

}
