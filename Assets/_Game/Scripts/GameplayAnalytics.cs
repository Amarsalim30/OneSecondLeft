using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

public interface IGameplayAnalyticsSink
{
    void Emit(string eventName, IReadOnlyDictionary<string, string> fields);
}

public static class GameplayAnalytics
{
    private static readonly List<IGameplayAnalyticsSink> sinks = new List<IGameplayAnalyticsSink>(1)
    {
        new LocalDebugLogAnalyticsSink()
    };

    public static event Action<string, IReadOnlyDictionary<string, string>> EventTracked;

    public static void AddSink(IGameplayAnalyticsSink sink)
    {
        if (sink == null || sinks.Contains(sink))
        {
            return;
        }

        sinks.Add(sink);
    }

    public static void RemoveSink(IGameplayAnalyticsSink sink)
    {
        if (sink == null)
        {
            return;
        }

        sinks.Remove(sink);
    }

    public static void Track(string eventName, IDictionary<string, object> fields = null)
    {
        if (string.IsNullOrWhiteSpace(eventName))
        {
            return;
        }

        Dictionary<string, string> payload = BuildPayload(fields);
        for (int i = 0; i < sinks.Count; i++)
        {
            IGameplayAnalyticsSink sink = sinks[i];
            if (sink == null)
            {
                continue;
            }

            try
            {
                sink.Emit(eventName, payload);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"GameplayAnalytics sink failure on '{eventName}': {ex.Message}");
            }
        }

        EventTracked?.Invoke(eventName, payload);
    }

    private static Dictionary<string, string> BuildPayload(IDictionary<string, object> fields)
    {
        Dictionary<string, string> payload = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["utc_time"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
        };

        if (fields == null)
        {
            return payload;
        }

        foreach (KeyValuePair<string, object> pair in fields)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                continue;
            }

            payload[pair.Key] = ConvertToString(pair.Value);
        }

        return payload;
    }

    private static string ConvertToString(object value)
    {
        if (value == null)
        {
            return "null";
        }

        if (value is string text)
        {
            return text;
        }

        if (value is bool flag)
        {
            return flag ? "true" : "false";
        }

        if (value is IFormattable formattable)
        {
            return formattable.ToString(null, CultureInfo.InvariantCulture);
        }

        return value.ToString();
    }

    private sealed class LocalDebugLogAnalyticsSink : IGameplayAnalyticsSink
    {
        public void Emit(string eventName, IReadOnlyDictionary<string, string> fields)
        {
            StringBuilder builder = new StringBuilder(128);
            builder.Append("[analytics] ");
            builder.Append(eventName);

            if (fields != null)
            {
                foreach (KeyValuePair<string, string> pair in fields)
                {
                    builder.Append(' ');
                    builder.Append(pair.Key);
                    builder.Append('=');
                    builder.Append(pair.Value);
                }
            }

            Debug.Log(builder.ToString());
        }
    }
}
