using System.IO.Compression;
using System.IO.MemoryMappedFiles;
using System.Reflection;
using System.Text;
using System.Collections;
using March7thHoney.Data.Config.Scene;
using March7thHoney.Internationalization;
using March7thHoney.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace March7thHoney.Data;

public static class CompressionHelper
{
    public static byte[] Compress(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length == 0) return [];

        try
        {
            if (data.Length < 1024)
            {
                var result = new byte[data.Length + 1];
                result[0] = 0;
                Buffer.BlockCopy(data, 0, result, 1, data.Length);
                return result;
            }

            using var output = new MemoryStream();
            output.WriteByte(1);
            using (var compressor = new DeflateStream(output, CompressionMode.Compress, true))
            {
                compressor.Write(data, 0, data.Length);
            }

            return output.ToArray();
        }
        catch
        {
            var result = new byte[data.Length + 1];
            result[0] = 0;
            Buffer.BlockCopy(data, 0, result, 1, data.Length);
            return result;
        }
    }

    public static byte[] Decompress(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length == 0) return [];

        try
        {
            if (data[0] == 0)
            {
                var result = new byte[data.Length - 1];
                Buffer.BlockCopy(data, 1, result, 0, result.Length);
                return result;
            }

            using var input = new MemoryStream(data, 1, data.Length - 1);
            using var decompressor = new DeflateStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            decompressor.CopyTo(output);
            return output.ToArray();
        }
        catch
        {
            return data;
        }
    }
}

public class ResourceCacheData
{
    public Dictionary<string, byte[]> GameDataValues { get; set; } = [];
}

public class IgnoreJsonIgnoreContractResolver : DefaultContractResolver
{
    protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
    {
        var property = base.CreateProperty(member, memberSerialization);
        property.Ignored = false;
        return property;
    }
}

public class ResourceCache
{
    public static readonly JsonSerializerSettings Serializer = new()
    {
        ContractResolver = new IgnoreJsonIgnoreContractResolver(),
        TypeNameHandling = TypeNameHandling.Auto,
        Converters =
        {
            new ConcurrentBagConverter<PropInfo>(),
            new ConcurrentDictionaryConverter<string, FloorInfo>()
        }
    };

    public static Logger Logger { get; } = new("ResCache");
    public static string CachePath { get; } = ConfigManager.Config.Path.ConfigPath + "/Resource.cache";
    public static bool IsComplete { get; set; } = true; 

    public static Task SaveCache()
    {
        return Task.Run(() =>
        {
            var cacheData = new ResourceCacheData
            {
                GameDataValues = typeof(GameData)
                    .GetProperties(BindingFlags.Public | BindingFlags.Static)
                    .Where(p => p.GetValue(null) != null)
                    .ToDictionary(
                        p => p.Name,
                        p => CompressionHelper.Compress(
                            Encoding.UTF8.GetBytes(
                                JsonConvert.SerializeObject(p.GetValue(null), Serializer)
                            )
                        )
                    )
            };

            File.WriteAllText(CachePath, JsonConvert.SerializeObject(cacheData));
            Logger.Info(I18NManager.Translate("Server.ServerInfo.GeneratedItem",
                I18NManager.Translate("Word.Cache")));
        });
    }

    public static bool LoadCache()
    {
        var buffer = new byte[new FileInfo(CachePath).Length];
        var viewAccessor = MemoryMappedFile.CreateFromFile(CachePath, FileMode.Open).CreateViewAccessor();
        viewAccessor.ReadArray(0, buffer, 0, buffer.Length);

        var cacheData = JsonConvert.DeserializeObject<ResourceCacheData>(Encoding.UTF8.GetString(buffer));
        if (cacheData == null) return false;

        Parallel.ForEach(
            typeof(GameData).GetProperties(BindingFlags.Public | BindingFlags.Static),
            prop =>
            {
                try
                {
                    if (ConfigManager.Config.ServerOption.LogOption.LogResourceCacheLoading)
                    {
                        Logger.Info(I18NManager.Translate("Server.ServerInfo.LoadingItem",
                            $"{prop.DeclaringType?.Name}.{prop.Name}"));
                    }
                    if (!cacheData.GameDataValues.TryGetValue(prop.Name, out var valueBytes)) return;

                    var deserialized = JsonConvert.DeserializeObject(
                        Encoding.UTF8.GetString(CompressionHelper.Decompress(valueBytes)),
                        prop.PropertyType,
                        Serializer
                    );

                    ApplyCachedValue(prop, deserialized);
                }
                catch (Exception e)
                {
                    Logger.Error(I18NManager.Translate("Server.ServerInfo.FailedToLoadItem",
                        $"{prop.DeclaringType?.Name}.{prop.Name}"));
                    Logger.Error(e.Message);
                }
            }
        );

        Logger.Info(I18NManager.Translate("Server.ServerInfo.LoadedItem",
            I18NManager.Translate("Word.Cache")));

        return ValidateLoadedCache();
    }

    private static bool ValidateLoadedCache()
    {
        if (GameData.EquipmentConfigData.Count == 0)
        {
            Logger.Warn("Resource cache is stale: EquipmentConfigData is missing.");
            return false;
        }

        var staleEquipmentConfig = GameData.EquipmentConfigData.Values
            .Any(config => config.Release && (config.ExpProvide <= 0 || config.CoinCost <= 0));
        if (GameData.DailyActiveConfigData.Count == 0 || GameData.DailyQuestData.Count == 0 || GameData.DailyMissionData.Count == 0)
            return false; // Rebuild caches created before daily activity resource support.
        if (!staleEquipmentConfig) return true;

        Logger.Warn("Resource cache is stale: EquipmentConfigData is missing Light Cone EXP/cost fields.");
        return false;
    }

    private static void ApplyCachedValue(PropertyInfo prop, object? deserialized)
    {
        if (prop.CanWrite)
        {
            prop.SetValue(null, deserialized);
            return;
        }

        var current = prop.GetValue(null);
        if (current == null || deserialized == null) return;

        if (current is IDictionary currentDict && deserialized is IDictionary sourceDict)
        {
            currentDict.Clear();
            foreach (DictionaryEntry entry in sourceDict)
            {
                currentDict[entry.Key] = entry.Value;
            }

            return;
        }

        if (current is IList currentList && deserialized is IList sourceList)
        {
            currentList.Clear();
            foreach (var item in sourceList)
            {
                currentList.Add(item);
            }
        }
    }

    public static void ClearGameData()
    {
        var properties = typeof(GameData).GetProperties(BindingFlags.Public | BindingFlags.Static);

        foreach (var prop in properties)
        {
            var propType = prop.PropertyType;
            var emptyValue = propType.IsGenericType && propType.GetGenericTypeDefinition() == typeof(Dictionary<,>)
                ? Activator.CreateInstance(propType)
                : propType.IsGenericType && propType.GetGenericTypeDefinition() == typeof(List<>)
                    ? Activator.CreateInstance(propType)
                    : propType.IsClass
                        ? Activator.CreateInstance(propType)
                        : null;

            if (prop.CanWrite)
            {
                prop.SetValue(null, emptyValue);
                continue;
            }

            var current = prop.GetValue(null);
            switch (current)
            {
                case IDictionary currentDict:
                    currentDict.Clear();
                    break;
                case IList currentList:
                    currentList.Clear();
                    break;
            }
        }
    }
}
