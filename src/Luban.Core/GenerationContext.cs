﻿using System.Collections.Concurrent;
using Luban.CodeFormat;
using Luban.DataLoader;
using Luban.Datas;
using Luban.Defs;
using Luban.RawDefs;
using Luban.Types;
using Luban.TypeVisitors;
using Luban.Utils;

namespace Luban;

public class GenerationContextBuilder
{
    public DefAssembly Assembly { get; set; }

    public List<string> IncludeTags { get; set; }
    
    public List<string> ExcludeTags { get; set; }
    
    public TimeZoneInfo TimeZone { get; set; }
}

public class GenerationContext
{
    private static readonly NLog.Logger s_logger = NLog.LogManager.GetCurrentClassLogger();

    public static GenerationContext Current { get; private set; }

    public DefAssembly Assembly { get; set; }

    public RawTarget Target => Assembly.Target;

    public List<string> IncludeTags { get; }
    
    public List<string> ExcludeTags { get; }
    
    private readonly ConcurrentDictionary<string, TableDataInfo> _recordsByTables = new();
    
    public string TopModule => Target.TopModule;

    public List<DefTable> Tables => Assembly.GetAllTables();
    
    private List<DefTypeBase> ExportTypes { get; }
    
    public List<DefTable> ExportTables { get; }
    
    public List<DefBean> ExportBeans { get; }
    
    public List<DefEnum> ExportEnums { get; }
    
    private readonly Dictionary<string, RawExternalType> _externalTypesByTypeName = new();
    
    public TimeZoneInfo TimeZone { get; }

    public void LoadDatas()
    {
        s_logger.Info("load datas begin");
        DataLoaderManager.Ins.LoadDatas(this);
        s_logger.Info("load datas end");
    }

    public GenerationContext(GenerationContextBuilder builder)
    {
        Current = this;
        Assembly = builder.Assembly;
        IncludeTags = builder.IncludeTags;
        ExcludeTags = builder.ExcludeTags;
        TimeZone = builder.TimeZone;
        
        ExportTables = Assembly.ExportTables;
        ExportTypes = CalculateExportTypes();
        ExportBeans = ExportTypes.OfType<DefBean>().ToList();
        ExportEnums = ExportTypes.OfType<DefEnum>().ToList();
    }
    
    private bool NeedExportNotDefault(List<string> groups)
    {
        return groups.Any(g => Target.Groups.Contains(g));
    }
    
    private List<DefTypeBase> CalculateExportTypes()
    {
        var refTypes = new Dictionary<string, DefTypeBase>();
        var types = Assembly.TypeList;
        foreach (var t in types)
        {
            if (!refTypes.ContainsKey(t.FullName))
            {
                if (t is DefBean bean && NeedExportNotDefault(t.Groups))
                {
                    TBean.Create(false, bean, null).Apply(RefTypeVisitor.Ins, refTypes);
                }
                else if (t is DefEnum)
                {
                    refTypes.Add(t.FullName, t);
                }
            }
        }

        foreach (var table in ExportTables)
        {
            refTypes[table.FullName] = table;
            table.ValueTType.Apply(RefTypeVisitor.Ins, refTypes);
        }

        return refTypes.Values.ToList();
    }
    
    public static string GetInputDataPath()
    {
        return EnvManager.Current.GetOption("", "inputDataDir", true);
    }
    
    public static string GetOutputCodePath(string family)
    {
        return EnvManager.Current.GetOption(family, "outputCodeDir", true);
    }
    
    public static string GetOutputDataPath(string family)
    {
        return EnvManager.Current.GetOption(family, "outputDataDir", true);
    }
    
    public void AddDataTable(DefTable table, List<Record> mainRecords, List<Record> patchRecords)
    {
        s_logger.Debug("AddDataTable name:{} record count:{}", table.FullName, mainRecords.Count);
        _recordsByTables[table.FullName] = new TableDataInfo(table, mainRecords, patchRecords);
    }

    public List<Record> GetTableAllDataList(DefTable table)
    {
        return _recordsByTables[table.FullName].FinalRecords;
    }

    public List<Record> GetTableExportDataList(DefTable table)
    {
        var tableDataInfo = _recordsByTables[table.FullName];
        if (ExcludeTags.Count == 0)
        {
            return tableDataInfo.FinalRecords;
        }
        else
        {
            var finalRecords = tableDataInfo.FinalRecords.Where(r => r.IsNotFiltered(ExcludeTags)).ToList();
            if (table.IsSingletonTable && finalRecords.Count != 1)
            {
                throw new Exception($"配置表 {table.FullName} 是单值表 mode=one,但数据个数:{finalRecords.Count} != 1");
            }
            return finalRecords;
        }
    }

    public static List<Record> ToSortByKeyDataList(DefTable table, List<Record> originRecords)
    {
        var sortedRecords = new List<Record>(originRecords);

        DefField keyField = table.IndexField;
        if (keyField != null && (keyField.CType is TInt || keyField.CType is TLong))
        {
            string keyFieldName = keyField.Name;
            sortedRecords.Sort((a, b) =>
            {
                DType keya = a.Data.GetField(keyFieldName);
                DType keyb = b.Data.GetField(keyFieldName);
                switch (keya)
                {
                    case DInt ai: return ai.Value.CompareTo((keyb as DInt).Value);
                    case DLong al: return al.Value.CompareTo((keyb as DLong).Value);
                    default: throw new NotSupportedException();
                }
            });
        }
        return sortedRecords;
    }

    public TableDataInfo GetTableDataInfo(DefTable table)
    {
        return _recordsByTables[table.FullName];
    }

    public ICodeStyle GetCodeStyle(string family)
    {
        if (EnvManager.Current.TryGetOption(family, "codeStyle", true, out var codeStyleName))
        {
            return CodeFormatManager.Ins.GetCodeStyle(codeStyleName);
        }
        return null;
    }
}