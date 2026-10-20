using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Configuration.Memory;

namespace ShoppingCart.Api.Infrastructure;

/// <summary>
/// อ่านไฟล์ .env (KEY=VALUE) เข้า IConfiguration
/// ใช้ชื่อ key แบบเดียวกับ environment variable ของ ASP.NET เช่น ConnectionStrings__Default
///
/// ลำดับความสำคัญ (ตัวหลังชนะ): appsettings.json &lt; .env &lt; environment variable จริง &lt; command line
/// ค่าใน .env จึงใช้ตอน dev ส่วนตอน deploy ตั้ง env จริงทับได้เลย
/// </summary>
public static class DotEnvConfiguration
{
    public static void AddDotEnvFile(this ConfigurationManager config, string path)
    {
        if (!File.Exists(path)) return;

        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;

            var eq = line.IndexOf('=');
            if (eq <= 0) continue;

            var key = line[..eq].Trim().Replace("__", ConfigurationPath.KeyDelimiter);
            var value = line[(eq + 1)..].Trim();
            if (value.Length >= 2 && (value[0] == '"' || value[0] == '\'') && value[^1] == value[0])
                value = value[1..^1];

            values[key] = value;
        }

        // แทรกไว้ก่อน environment variables (ตัวที่ไม่มี prefix) เพื่อให้ env จริงทับค่าใน .env ได้
        var sources = (IList<IConfigurationSource>)config.Sources;
        var envIndex = sources
            .Select((s, i) => (s, i))
            .Where(x => x.s is EnvironmentVariablesConfigurationSource { Prefix: null or "" })
            .Select(x => x.i)
            .DefaultIfEmpty(sources.Count)
            .First();

        sources.Insert(envIndex, new MemoryConfigurationSource { InitialData = values });
    }
}
