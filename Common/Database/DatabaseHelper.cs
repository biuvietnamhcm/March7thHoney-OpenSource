using System.Collections.Concurrent;
using System.Globalization;
using March7thHoney.Database.Account;
using March7thHoney.Database.Quests;
using March7thHoney.Internationalization;
using March7thHoney.Util;
using SqlSugar;

namespace March7thHoney.Database;

public class DatabaseHelper
{
    public static Logger logger = new("Database");
    public static SqlSugarScope? sqlSugarScope;
    public static DatabaseHelper? Instance;
    public static readonly ConcurrentDictionary<int, List<BaseDatabaseDataHelper>> UidInstanceMap = [];
    public static readonly List<int> ToSaveUidList = [];
    public static long LastSaveTick = DateTime.UtcNow.Ticks;
    public static Thread? SaveThread;
    public static bool LoadAccount;
    public static bool LoadAllData;

    public DatabaseHelper()
    {
        Instance = this;
    }

    public static ConfigureExternalServices CreateExternalServices(DbType type) => new()
    {
        SerializeService = new CustomSerializeService(),
        EntityService = (_, column) =>
        {
            // JSON collections (including complete warp history) outgrow VARCHAR/TEXT.
            // Keep MySQL rows small and avoid truncating serialized player state.
            if (type == DbType.MySql && column.IsJson)
            {
                column.DataType = "longtext";
                column.Length = 0;
            }
        }
    };

    public void Initialize()
    {
        logger.Info(I18NManager.Translate("Server.ServerInfo.LoadingItem", I18NManager.Translate("Word.Database")));
        var config = ConfigManager.Config;
        DbType type;
        string connectionString;
        switch (config.Database.DatabaseType)
        {
            case "sqlite":
                type = DbType.Sqlite;
                var f = new FileInfo(config.Path.DatabasePath + "/" + config.Database.DatabaseName);
                if (!f.Exists && f.Directory != null) f.Directory.Create();
                connectionString = $"Data Source={f.FullName};";
                break;
            case "mysql":
                type = DbType.MySql;
                connectionString =
                    $"server={config.Database.MySqlHost};Port={config.Database.MySqlPort};Database={config.Database.MySqlDatabase};Uid={config.Database.MySqlUser};Pwd={config.Database.MySqlPassword};";
                break;
            default:
                return;
        }

        sqlSugarScope = new SqlSugarScope(new ConnectionConfig
        {
            ConnectionString = connectionString,
            DbType = type,
            IsAutoCloseConnection = true,
            ConfigureExternalServices = CreateExternalServices(type)
        });

        switch (config.Database.DatabaseType)
        {
            case "sqlite":
                InitializeSqlite(); 
                break;
            case "mysql":
                InitializeMysql();
                break;
            default:
                logger.Error("Unsupported database type");
                break;
        }

        var baseType = typeof(BaseDatabaseDataHelper);
        var assembly = typeof(BaseDatabaseDataHelper).Assembly;

        var types = assembly.GetTypes().Where(t => t.IsSubclassOf(baseType));

        var list = sqlSugarScope.Queryable<AccountData>()
            .Select(x => x)
            .ToList();

        foreach (var inst in list!.Select(instance => (instance as BaseDatabaseDataHelper)!))
        {
            if (!UidInstanceMap.TryGetValue(inst.Uid, out var value))
            {
                value = [];
                UidInstanceMap[inst.Uid] = value;
            }

            value.Add(inst); 
        }

        foreach (var account in list) account.SyncPermissionsWithRole();

        
        LoadAccount = true;

        var res = Parallel.ForEach(list, account =>
        {
            Parallel.ForEach(types, t =>
            {
                if (t == typeof(AccountData)) return; 

                typeof(DatabaseHelper).GetMethod(nameof(InitializeTable))?.MakeGenericMethod(t)
                    .Invoke(null, [account.Uid]);
            }); 
        });

        while (!res.IsCompleted)
        {
        }

        LastSaveTick = DateTime.UtcNow.Ticks;

        SaveThread = new Thread(() =>
        {
            while (true) CalcSaveDatabase();
        });
        SaveThread.Start();

        LoadAllData = true;
    }

    public static void InitializeTable<T>(int uid) where T : BaseDatabaseDataHelper, new()
    {
        var list = sqlSugarScope?.Queryable<T>()
            .Select(x => x)
            .Select<T>()
            .Where(x => x.Uid == uid)
            .ToList();

        foreach (var inst in list!.Select(instance => (instance as BaseDatabaseDataHelper)!))
        {
            if (!UidInstanceMap.TryGetValue(inst.Uid, out var value))
            {
                value = [];
                UidInstanceMap[inst.Uid] = value;
            }

            value.Add(inst); 
        }
    }

    public void UpgradeDatabase()
    {
        logger.Info("Upgrading database...");

        foreach (var instance in GetAllInstance<MissionData>()!) instance.MoveFromOld();
    }

    public void MoveFromSqlite()
    {
        logger.Info("Moving from sqlite...");

        var config = ConfigManager.Config;
        var f = new FileInfo(config.Path.DatabasePath + "/" + config.Database.DatabaseName);
        var sqliteScope = new SqlSugarScope(new ConnectionConfig
        {
            ConnectionString = $"Data Source={f.FullName};",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
            ConfigureExternalServices = new ConfigureExternalServices
            {
                SerializeService = new CustomSerializeService()
            }
        });

        var baseType = typeof(BaseDatabaseDataHelper);
        var assembly = typeof(BaseDatabaseDataHelper).Assembly;
        var types = assembly.GetTypes().Where(t => t.IsSubclassOf(baseType));
        foreach (var type in types)
            typeof(DatabaseHelper).GetMethod("MoveSqliteTable")?.MakeGenericMethod(type).Invoke(null, [sqliteScope]);

        
        Environment.Exit(0);
    }

    public static void MoveSqliteTable<T>(SqlSugarScope scope) where T : class, new()
    {
        try
        {
            var list = scope.Queryable<T>().ToList();
            foreach (var instance in list!) sqlSugarScope?.Insertable(instance).ExecuteCommand();
        }
        catch (Exception e)
        {
            Logger.GetByClassName().Error("An error occurred while moving the table", e);
        }
    }

    public static void InitializeSqlite()
    {
        var baseType = typeof(BaseDatabaseDataHelper);
        var assembly = typeof(BaseDatabaseDataHelper).Assembly;
        var types = assembly.GetTypes().Where(t => t.IsSubclassOf(baseType));
        foreach (var type in types)
            typeof(DatabaseHelper).GetMethod("InitializeSqliteTable")?.MakeGenericMethod(type).Invoke(null, null);
    }

    public static void InitializeMysql()
    {
        sqlSugarScope?.DbMaintenance.CreateDatabase();
        InitializeSqlite();
    }

    
    public static void InitializeSqliteTable<T>() where T : class, new()
    {
        try
        {
            sqlSugarScope?.CodeFirst.InitTables<T>();
        }
        catch (Exception e)
        {
            throw new InvalidOperationException($"Cannot initialize database table for {typeof(T).FullName}.", e);
        }
    }

    public T? GetInstance<T>(int uid) where T : class, new()
    {
        try
        {
            if (UidInstanceMap.TryGetValue(uid, out var value))
                return value.OfType<T>().Select(instance => instance).FirstOrDefault();
            value = [];
            UidInstanceMap[uid] = value;

            return value.OfType<T>().Select(instance => instance).FirstOrDefault();
        }
        catch (Exception e)
        {
            logger.Error("Unsupported type", e);
            return null;
        }
    }

    public T GetInstanceOrCreateNew<T>(int uid) where T : BaseDatabaseDataHelper, new()
    {
        var instance = GetInstance<T>(uid);
        if (instance != null) return instance;
        

        var t = sqlSugarScope?.Queryable<T>()
            .Where(x => x.Uid == uid)
            .ToList();

        if (t is { Count: > 0 }) 
        {
            instance = t[0];
            if (!UidInstanceMap.TryGetValue(uid, out var value))
            {
                value = [];
                UidInstanceMap[uid] = value;
            }

            value.Add(instance); 
            return instance;
        }

        
        instance = new T
        {
            Uid = uid
        };
        SaveInstance(instance);

        return instance;
    }

    public static List<T>? GetAllInstance<T>() where T : class, new()
    {
        try
        {
            return sqlSugarScope?.Queryable<T>()
                .Select(x => x)
                .ToList();
        }
        catch (Exception e)
        {
            logger.Error("Unsupported type", e);
            return null;
        }
    }

    public static List<T>? GetAllInstanceFromMap<T>() where T : class, new()
    {
        try
        {
            var list = UidInstanceMap.Values.SelectMany(x => x).ToList();
            return list.OfType<T>().Select(instance => instance).ToList();
        }
        catch (Exception e)
        {
            logger.Error("Unsupported type", e);
            return null;
        }
    }

    public static void SaveInstance<T>(T instance) where T : class, new()
    {
        sqlSugarScope?.Insertable(instance).ExecuteCommand();
        var baseInstance = (instance as BaseDatabaseDataHelper)!;
        if (!UidInstanceMap.TryGetValue(baseInstance.Uid, out var value))
        {
            value = [];
            UidInstanceMap[baseInstance.Uid] = value;
        }

        value.Add(baseInstance); 
    }

    public void CalcSaveDatabase() 
    {
        if (LastSaveTick + TimeSpan.TicksPerMinute * 5 > DateTime.UtcNow.Ticks) return;
        SaveDatabase();
    }

    public void SaveDatabase() 
    {
        try
        {
            var prev = DateTime.Now;
            var list = ToSaveUidList.ToList(); 
            foreach (var uid in list)
            {
                var value = UidInstanceMap[uid];
                var baseType = typeof(BaseDatabaseDataHelper);
                var assembly = typeof(BaseDatabaseDataHelper).Assembly;
                var types = assembly.GetTypes().Where(t => t.IsSubclassOf(baseType));
                foreach (var type in types)
                {
                    var instance = value.Find(x => x.GetType() == type);
                    if (instance != null)
                        typeof(DatabaseHelper).GetMethod("SaveDatabaseType")?.MakeGenericMethod(type)
                            .Invoke(null, [instance]);
                }
            }

            var t = (DateTime.Now - prev).TotalSeconds;
            logger.Info(I18NManager.Translate("Server.ServerInfo.SaveDatabase",
                Math.Round(t, 2).ToString(CultureInfo.InvariantCulture)));

            ToSaveUidList.Clear();
        }
        catch (Exception e)
        {
            logger.Error("An error occurred while saving the database", e);
        }

        LastSaveTick = DateTime.UtcNow.Ticks;
    }

    public void SaveUidData(int uid)
    {
        try
        {
            if (!UidInstanceMap.TryGetValue(uid, out var value)) return;

            var baseType = typeof(BaseDatabaseDataHelper);
            var assembly = typeof(BaseDatabaseDataHelper).Assembly;
            var types = assembly.GetTypes().Where(t => t.IsSubclassOf(baseType));
            foreach (var type in types)
            {
                var instance = value.Find(x => x.GetType() == type);
                if (instance != null)
                    typeof(DatabaseHelper).GetMethod(nameof(SaveDatabaseType))?.MakeGenericMethod(type)
                        .Invoke(null, [instance]);
            }

            ToSaveUidList.RemoveAll(savedUid => savedUid == uid);
        }
        catch (Exception e)
        {
            logger.Error($"An error occurred while saving gameplay data for uid {uid}", e);
        }
    }

    public static void SaveDatabaseType<T>(T instance) where T : class, new()
    {
        try
        {
            sqlSugarScope?.Updateable(instance).ExecuteCommand();
        }
        catch (Exception e)
        {
            logger.Error("An error occurred while saving the database", e);
        }
    }

    public void DeleteUidGameplayData(int uid)
    {
        var baseType = typeof(BaseDatabaseDataHelper);
        var assembly = typeof(BaseDatabaseDataHelper).Assembly;
        var types = assembly.GetTypes()
            .Where(t => t.IsSubclassOf(baseType) && t != typeof(AccountData));

        foreach (var type in types)
            typeof(DatabaseHelper).GetMethod(nameof(DeleteUidDatabaseType))?.MakeGenericMethod(type)
                .Invoke(null, [uid]);

        if (UidInstanceMap.TryGetValue(uid, out var value))
            value.RemoveAll(instance => instance is not AccountData);

        ToSaveUidList.RemoveAll(savedUid => savedUid == uid);
    }

    public static void DeleteUidDatabaseType<T>(int uid) where T : BaseDatabaseDataHelper, new()
    {
        try
        {
            sqlSugarScope?.Deleteable<T>().Where(data => data.Uid == uid).ExecuteCommand();
        }
        catch (Exception e)
        {
            logger.Error("An error occurred while deleting account gameplay data", e);
        }
    }

    public void DeleteInstance<T>(T instance) where T : class, new()
    {
        sqlSugarScope?.Deleteable(instance).ExecuteCommand();
        UidInstanceMap[(instance as BaseDatabaseDataHelper)!.Uid]
            .Remove((instance as BaseDatabaseDataHelper)!); 
        ToSaveUidList.Remove((instance as BaseDatabaseDataHelper)!.Uid); 
    }

    public void DeleteUidData(int uid)
    {
        if (!UidInstanceMap.TryGetValue(uid, out var value)) return;

        foreach (var instance in value.ToList())
            typeof(DatabaseHelper).GetMethod(nameof(DeleteInstance))?.MakeGenericMethod(instance.GetType())
                .Invoke(this, [instance]);

        UidInstanceMap.TryRemove(uid, out _);
        ToSaveUidList.Remove(uid);
    }
}
